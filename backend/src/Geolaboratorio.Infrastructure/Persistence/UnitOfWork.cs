using Geolaboratorio.Application;
using Geolaboratorio.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Geolaboratorio.Infrastructure.Persistence;

public class Repository<T>(AppDbContext db) : IRepository<T> where T : class
{
    public IQueryable<T> Query() => db.Set<T>().AsQueryable();
    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => await db.Set<T>().FindAsync([id], ct);
    public async Task AddAsync(T entity, CancellationToken ct = default) => await db.Set<T>().AddAsync(entity, ct);
    public void Update(T entity) => db.Set<T>().Update(entity);
    public void Remove(T entity) => db.Set<T>().Remove(entity);
}

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _tx;
    public IRepository<Usuario> Usuarios { get; } = new Repository<Usuario>(db);
    public IRepository<Cliente> Clientes { get; } = new Repository<Cliente>(db);
    public IRepository<Equipo> Equipos { get; } = new Repository<Equipo>(db);
    public IRepository<EquipoSerie> EquipoSeries { get; } = new Repository<EquipoSerie>(db);
    public IRepository<EquipoHistorial> EquipoHistoriales { get; } = new Repository<EquipoHistorial>(db);
    public IRepository<EquipoArchivo> EquipoArchivos { get; } = new Repository<EquipoArchivo>(db);
    public IRepository<Certificado> Certificados { get; } = new Repository<Certificado>(db);
    public IRepository<OrdenServicio> OrdenesServicio { get; } = new Repository<OrdenServicio>(db);
    public IRepository<OrdenServicioEstatusHistorial> OrdenesServicioHistorial { get; } = new Repository<OrdenServicioEstatusHistorial>(db);
    public IRepository<OrdenServicioRefaccion> OrdenesServicioRefacciones { get; } = new Repository<OrdenServicioRefaccion>(db);
    public IRepository<OrdenServicioFoto> OrdenesServicioFotos { get; } = new Repository<OrdenServicioFoto>(db);
    public IRepository<Mantenimiento> Mantenimientos { get; } = new Repository<Mantenimiento>(db);
    public IRepository<Garantia> Garantias { get; } = new Repository<Garantia>(db);
    public IRepository<Producto> Productos { get; } = new Repository<Producto>(db);
    public IRepository<MovimientoInventario> MovimientosInventario { get; } = new Repository<MovimientoInventario>(db);
    public IRepository<VentaDocumento> Ventas { get; } = new Repository<VentaDocumento>(db);
    public IRepository<VentaPartida> VentaPartidas { get; } = new Repository<VentaPartida>(db);
    public IRepository<Compra> Compras { get; } = new Repository<Compra>(db);
    public IRepository<CompraPartida> CompraPartidas { get; } = new Repository<CompraPartida>(db);

    public IQueryable<AuditLog> AuditLogs => db.AuditLogs.AsNoTracking();
    public IQueryable<CertificadoHistorial> Historial => db.CertificadoHistorial.AsNoTracking();

    public async Task AddHistorialAsync(CertificadoHistorial h, CancellationToken ct = default)
        => await db.CertificadoHistorial.AddAsync(h, ct);

    public async Task<int> NextConsecutivoAsync(int anio, CancellationToken ct = default)
    {
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            var seq = await db.FolioSecuencias.FirstOrDefaultAsync(f => f.Anio == anio, ct);
            if (seq is null)
            {
                seq = new FolioSecuencia { Anio = anio, Ultimo = 1 };
                db.FolioSecuencias.Add(seq);
            }
            else seq.Ultimo++;
            await db.SaveChangesAsync(ct);
            return seq.Ultimo;
        }

        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT IGNORE INTO FolioSecuencias (Anio, Ultimo) VALUES ({anio}, 0)", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE FolioSecuencias SET Ultimo = Ultimo + 1 WHERE Anio = {anio}", ct);
        return await db.Database.SqlQuery<int>($"SELECT Ultimo AS `Value` FROM FolioSecuencias WHERE Anio = {anio}").SingleAsync(ct);
    }

    public async Task<string> NextFolioPrefijoAsync(string prefijo, int anio, CancellationToken ct = default)
    {
        prefijo = prefijo.Trim().ToUpperInvariant();
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            var seq = await db.FolioSecuenciasPrefijo.FirstOrDefaultAsync(f => f.Prefijo == prefijo && f.Anio == anio, ct);
            if (seq is null)
            {
                seq = new FolioSecuenciaPrefijo { Prefijo = prefijo, Anio = anio, Ultimo = 1 };
                db.FolioSecuenciasPrefijo.Add(seq);
            }
            else seq.Ultimo++;
            await db.SaveChangesAsync(ct);
            return $"{prefijo}-{anio}-{seq.Ultimo:D6}";
        }

        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT IGNORE INTO FolioSecuenciasPrefijo (Prefijo, Anio, Ultimo) VALUES ({prefijo}, {anio}, 0)", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE FolioSecuenciasPrefijo SET Ultimo = Ultimo + 1 WHERE Prefijo = {prefijo} AND Anio = {anio}", ct);
        var n = await db.Database.SqlQuery<int>($"SELECT Ultimo AS `Value` FROM FolioSecuenciasPrefijo WHERE Prefijo = {prefijo} AND Anio = {anio}").SingleAsync(ct);
        return $"{prefijo}-{anio}-{n:D6}";
    }

    public async Task<int> VigenciaPorTipoAsync(TipoEquipo tipo, CancellationToken ct = default)
        => await db.ConfiguracionVigencias.Where(v => v.TipoEquipo == tipo).Select(v => (int?)v.Meses).FirstOrDefaultAsync(ct) ?? 12;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
    public async Task BeginAsync(CancellationToken ct = default) => _tx = await db.Database.BeginTransactionAsync(ct);
    public async Task CommitAsync(CancellationToken ct = default) { if (_tx != null) { await _tx.CommitAsync(ct); await _tx.DisposeAsync(); _tx = null; } }
    public async Task RollbackAsync(CancellationToken ct = default) { if (_tx != null) { await _tx.RollbackAsync(ct); await _tx.DisposeAsync(); _tx = null; } }
}
