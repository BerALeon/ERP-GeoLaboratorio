using System.Text.Json;
using Geolaboratorio.Application;
using Geolaboratorio.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Geolaboratorio.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<EquipoSerie> EquipoSeries => Set<EquipoSerie>();
    public DbSet<EquipoHistorial> EquipoHistoriales => Set<EquipoHistorial>();
    public DbSet<EquipoArchivo> EquipoArchivos => Set<EquipoArchivo>();
    public DbSet<Certificado> Certificados => Set<Certificado>();
    public DbSet<CertificadoHistorial> CertificadoHistorial => Set<CertificadoHistorial>();
    public DbSet<OrdenServicio> OrdenesServicio => Set<OrdenServicio>();
    public DbSet<OrdenServicioEstatusHistorial> OrdenesServicioHistorial => Set<OrdenServicioEstatusHistorial>();
    public DbSet<OrdenServicioRefaccion> OrdenesServicioRefacciones => Set<OrdenServicioRefaccion>();
    public DbSet<OrdenServicioFoto> OrdenesServicioFotos => Set<OrdenServicioFoto>();
    public DbSet<Mantenimiento> Mantenimientos => Set<Mantenimiento>();
    public DbSet<Garantia> Garantias => Set<Garantia>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<VentaDocumento> Ventas => Set<VentaDocumento>();
    public DbSet<VentaPartida> VentaPartidas => Set<VentaPartida>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<CompraPartida> CompraPartidas => Set<CompraPartida>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<FolioSecuencia> FolioSecuencias => Set<FolioSecuencia>();
    public DbSet<FolioSecuenciaPrefijo> FolioSecuenciasPrefijo => Set<FolioSecuenciaPrefijo>();
    public DbSet<ConfiguracionVigencia> ConfiguracionVigencias => Set<ConfiguracionVigencia>();

    private static readonly HashSet<string> Sensitive = ["PasswordHash"];

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios"); e.HasKey(x => x.UsuarioId);
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Correo).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Correo).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FirmaUrl).HasMaxLength(500);
        });

        b.Entity<Cliente>(e =>
        {
            e.ToTable("Clientes"); e.HasKey(x => x.ClienteId);
            e.Property(x => x.RazonSocial).HasMaxLength(250).IsRequired();
            e.Property(x => x.NombreComercial).HasMaxLength(250);
            e.Property(x => x.RFC).HasMaxLength(13).IsRequired();
            e.HasIndex(x => x.RFC).IsUnique();
            e.Property(x => x.Direccion).HasMaxLength(500);
            e.Property(x => x.Ciudad).HasMaxLength(100);
            e.Property(x => x.Estado).HasMaxLength(100);
            e.Property(x => x.Pais).HasMaxLength(100);
            e.Property(x => x.Telefono).HasMaxLength(30);
            e.Property(x => x.Correo).HasMaxLength(150);
            e.Property(x => x.Contacto).HasMaxLength(150);
        });

        b.Entity<Equipo>(e =>
        {
            e.ToTable("Equipos"); e.HasKey(x => x.EquipoId);
            e.Property(x => x.TipoEquipo).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Marca).HasMaxLength(100).IsRequired();
            e.Property(x => x.Modelo).HasMaxLength(100).IsRequired();
            e.Property(x => x.SeriePrincipal).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.SeriePrincipal).IsUnique();
            e.Property(x => x.SerieSecundaria).HasMaxLength(100);
            e.Property(x => x.IMEI1).HasMaxLength(20);
            e.Property(x => x.IMEI2).HasMaxLength(20);
            e.Property(x => x.NumeroInterno).HasMaxLength(50);
            e.Property(x => x.CodigoQR).HasMaxLength(120);
            e.Property(x => x.CodigoBarras).HasMaxLength(120);
            e.Property(x => x.Observaciones).HasMaxLength(1000);
            e.Property(x => x.Estatus).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.MotivoBaja).HasMaxLength(500);
            e.HasOne(x => x.Cliente).WithMany(c => c.Equipos).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EquipoSerie>(e =>
        {
            e.ToTable("EquipoSeries"); e.HasKey(x => x.EquipoSerieId);
            e.Property(x => x.TipoSerie).HasMaxLength(80).IsRequired();
            e.Property(x => x.Valor).HasMaxLength(120).IsRequired();
            e.Property(x => x.Observaciones).HasMaxLength(300);
            e.HasIndex(x => x.Valor);
            e.HasOne(x => x.Equipo).WithMany(q => q.Series).HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EquipoHistorial>(e =>
        {
            e.ToTable("EquipoHistorial"); e.HasKey(x => x.EquipoHistorialId);
            e.Property(x => x.TipoEvento).HasMaxLength(50).IsRequired();
            e.Property(x => x.Titulo).HasMaxLength(200).IsRequired();
            e.Property(x => x.Detalle).HasMaxLength(2000);
            e.Property(x => x.FolioReferencia).HasMaxLength(40);
            e.Property(x => x.UsuarioNombre).HasMaxLength(150);
            e.HasIndex(x => x.EquipoId);
            e.HasOne(x => x.Equipo).WithMany(q => q.HistorialTecnico).HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EquipoArchivo>(e =>
        {
            e.ToTable("EquipoArchivos"); e.HasKey(x => x.EquipoArchivoId);
            e.Property(x => x.TipoArchivo).HasMaxLength(40).IsRequired();
            e.Property(x => x.Categoria).HasMaxLength(80).IsRequired();
            e.Property(x => x.NombreArchivo).HasMaxLength(250).IsRequired();
            e.Property(x => x.ArchivoUrl).HasColumnType("longtext");
            e.Property(x => x.Descripcion).HasMaxLength(500);
            e.Property(x => x.SubidoPor).HasMaxLength(150);
            e.HasOne(x => x.Equipo).WithMany(q => q.Archivos).HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Certificado>(e =>
        {
            e.ToTable("Certificados"); e.HasKey(x => x.CertificadoId);
            e.Property(x => x.Folio).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Folio).IsUnique();
            e.HasIndex(x => new { x.Anio, x.Consecutivo }).IsUnique();
            e.HasIndex(x => x.FechaVencimiento);
            e.Property(x => x.TipoRegistro).HasMaxLength(30);
            e.Property(x => x.Resultado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Estatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Resultados).HasColumnType("text");
            e.Property(x => x.Observaciones).HasColumnType("text");
            e.Property(x => x.Conclusiones).HasColumnType("text");
            e.Property(x => x.PDFUrl).HasMaxLength(500);
            e.Property(x => x.QRUrl).HasMaxLength(500);
            e.Property(x => x.PdfHash).HasMaxLength(64);
            e.Property(x => x.MotivoCancelacion).HasMaxLength(500);
            e.HasOne(x => x.Equipo).WithMany(q => q.Certificados).HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tecnico).WithMany().HasForeignKey(x => x.TecnicoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.OrdenServicio).WithMany().HasForeignKey(x => x.OrdenServicioId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<CertificadoHistorial>(e =>
        {
            e.ToTable("CertificadoHistorial"); e.HasKey(x => x.HistorialId);
            e.Property(x => x.Accion).HasMaxLength(30);
            e.Property(x => x.Detalle).HasMaxLength(1000);
            e.Property(x => x.PdfHash).HasMaxLength(64);
            e.Property(x => x.UsuarioNombre).HasMaxLength(150);
            e.HasOne<Certificado>().WithMany(c => c.Historial).HasForeignKey(x => x.CertificadoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrdenServicio>(e =>
        {
            e.ToTable("OrdenesServicio"); e.HasKey(x => x.OrdenServicioId);
            e.Property(x => x.Folio).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Folio).IsUnique();
            e.HasIndex(x => new { x.Anio, x.Consecutivo }).IsUnique();
            e.Property(x => x.TipoServicio).HasMaxLength(80).IsRequired();
            e.Property(x => x.TiposServicioAdicionales).HasMaxLength(500);
            e.Property(x => x.Estatus).HasMaxLength(50).IsRequired();
            e.Property(x => x.MetodoIdentificacion).HasMaxLength(40);
            e.Property(x => x.AccesoriosChecklistJson).HasColumnType("text");
            e.Property(x => x.AccesoriosPersonalizados).HasMaxLength(1000);
            e.Property(x => x.CondicionChecklistJson).HasColumnType("text");
            e.Property(x => x.FallaReportada).HasColumnType("text");
            e.Property(x => x.ObservacionesRecepcion).HasColumnType("text");
            e.Property(x => x.Diagnostico).HasColumnType("text");
            e.Property(x => x.ActividadesRealizadas).HasColumnType("text");
            e.Property(x => x.ObservacionesTecnicas).HasColumnType("text");
            e.Property(x => x.CostoManoObra).HasPrecision(14, 2);
            e.Property(x => x.SubtotalRefacciones).HasPrecision(14, 2);
            e.Property(x => x.Total).HasPrecision(14, 2);
            e.Property(x => x.FirmaTecnico).HasColumnType("longtext");
            e.Property(x => x.FirmaSupervisor).HasColumnType("longtext");
            e.Property(x => x.FirmaCliente).HasColumnType("longtext");
            e.Property(x => x.NombreClienteFirma).HasMaxLength(150);
            e.Property(x => x.ReportePdfUrl).HasMaxLength(500);
            e.HasOne(x => x.Cliente).WithMany(c => c.OrdenesServicio).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Equipo).WithMany(q => q.OrdenesServicio).HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tecnico).WithMany().HasForeignKey(x => x.TecnicoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<OrdenServicioEstatusHistorial>(e =>
        {
            e.ToTable("OrdenServicioEstatusHistorial"); e.HasKey(x => x.HistorialEstatusId);
            e.Property(x => x.EstatusAnterior).HasMaxLength(50);
            e.Property(x => x.EstatusNuevo).HasMaxLength(50).IsRequired();
            e.Property(x => x.UsuarioNombre).HasMaxLength(150);
            e.Property(x => x.Observaciones).HasMaxLength(1000);
            e.HasOne(x => x.OrdenServicio).WithMany(o => o.HistorialEstatus).HasForeignKey(x => x.OrdenServicioId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OrdenServicioRefaccion>(e =>
        {
            e.ToTable("OrdenServicioRefacciones"); e.HasKey(x => x.OrdenServicioRefaccionId);
            e.Property(x => x.NombreProducto).HasMaxLength(200).IsRequired();
            e.Property(x => x.SKU).HasMaxLength(60);
            e.Property(x => x.Cantidad).HasPrecision(12, 2);
            e.Property(x => x.PrecioUnitario).HasPrecision(14, 2);
            e.Property(x => x.Importe).HasPrecision(14, 2);
            e.HasOne(x => x.OrdenServicio).WithMany(o => o.Refacciones).HasForeignKey(x => x.OrdenServicioId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrdenServicioFoto>(e =>
        {
            e.ToTable("OrdenServicioFotos"); e.HasKey(x => x.OrdenServicioFotoId);
            e.Property(x => x.Categoria).HasMaxLength(80).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(500);
            e.Property(x => x.ImagenUrl).HasColumnType("longtext");
            e.Property(x => x.UsuarioNombre).HasMaxLength(150);
            e.HasOne(x => x.OrdenServicio).WithMany(o => o.Fotografias).HasForeignKey(x => x.OrdenServicioId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Mantenimiento>(e =>
        {
            e.ToTable("Mantenimientos"); e.HasKey(x => x.MantenimientoId);
            e.Property(x => x.Folio).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Folio).IsUnique();
            e.Property(x => x.Tipo).HasMaxLength(60).IsRequired();
            e.Property(x => x.Estatus).HasMaxLength(40).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(1000);
            e.Property(x => x.Actividades).HasColumnType("text");
            e.Property(x => x.Hallazgos).HasColumnType("text");
            e.Property(x => x.Recomendaciones).HasColumnType("text");
            e.Property(x => x.Costo).HasPrecision(14, 2);
            e.HasOne(x => x.Equipo).WithMany(q => q.Mantenimientos).HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.OrdenServicio).WithMany().HasForeignKey(x => x.OrdenServicioId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Tecnico).WithMany().HasForeignKey(x => x.TecnicoId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Garantia>(e =>
        {
            e.ToTable("Garantias"); e.HasKey(x => x.GarantiaId);
            e.Property(x => x.Folio).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Folio).IsUnique();
            e.Property(x => x.TipoGarantia).HasMaxLength(60).IsRequired();
            e.Property(x => x.Cobertura).HasMaxLength(1000);
            e.Property(x => x.Condiciones).HasMaxLength(1000);
            e.Property(x => x.Estatus).HasMaxLength(40).IsRequired();
            e.Property(x => x.MotivoReclamacion).HasMaxLength(1000);
            e.Property(x => x.Resolucion).HasMaxLength(1000);
            e.HasOne(x => x.Equipo).WithMany(q => q.Garantias).HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Cliente).WithMany(c => c.Garantias).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Producto>(e =>
        {
            e.ToTable("Productos"); e.HasKey(x => x.ProductoId);
            e.Property(x => x.SKU).HasMaxLength(60).IsRequired();
            e.HasIndex(x => x.SKU).IsUnique();
            e.Property(x => x.CodigoBarras).HasMaxLength(100);
            e.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(1000);
            e.Property(x => x.GrupoCategoria).HasMaxLength(60).IsRequired();
            e.Property(x => x.Categoria).HasMaxLength(80).IsRequired();
            e.Property(x => x.Marca).HasMaxLength(100);
            e.Property(x => x.ModeloCompatible).HasMaxLength(150);
            e.Property(x => x.Unidad).HasMaxLength(20);
            e.Property(x => x.CostoUnitario).HasPrecision(14, 2);
            e.Property(x => x.PrecioVenta).HasPrecision(14, 2);
            e.Property(x => x.StockActual).HasPrecision(12, 2);
            e.Property(x => x.StockMinimo).HasPrecision(12, 2);
            e.Property(x => x.StockMaximo).HasPrecision(12, 2);
            e.Property(x => x.Almacen).HasMaxLength(30);
            e.Property(x => x.Pasillo).HasMaxLength(30);
            e.Property(x => x.Rack).HasMaxLength(30);
            e.Property(x => x.Anaquel).HasMaxLength(30);
            e.Property(x => x.Nivel).HasMaxLength(30);
            e.Property(x => x.Posicion).HasMaxLength(30);
            e.Property(x => x.CodigoUbicacion).HasMaxLength(120);
        });

        b.Entity<MovimientoInventario>(e =>
        {
            e.ToTable("MovimientosInventario"); e.HasKey(x => x.MovimientoId);
            e.Property(x => x.TipoMovimiento).HasMaxLength(20).IsRequired();
            e.Property(x => x.Subtipo).HasMaxLength(50).IsRequired();
            e.Property(x => x.Concepto).HasMaxLength(250).IsRequired();
            e.Property(x => x.ReferenciaFolio).HasMaxLength(50);
            e.Property(x => x.Cantidad).HasPrecision(12, 2);
            e.Property(x => x.StockAnterior).HasPrecision(12, 2);
            e.Property(x => x.StockPosterior).HasPrecision(12, 2);
            e.Property(x => x.CostoUnitario).HasPrecision(14, 2);
            e.Property(x => x.UbicacionOrigen).HasMaxLength(120);
            e.Property(x => x.UbicacionDestino).HasMaxLength(120);
            e.Property(x => x.Observaciones).HasMaxLength(1000);
            e.Property(x => x.UsuarioNombre).HasMaxLength(150);
            e.HasIndex(x => x.ProductoId);
            e.HasIndex(x => x.Fecha);
            e.HasOne(x => x.Producto).WithMany(p => p.Movimientos).HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<VentaDocumento>(e =>
        {
            e.ToTable("Ventas"); e.HasKey(x => x.VentaId);
            e.Property(x => x.Folio).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Folio).IsUnique();
            e.Property(x => x.TipoDocumento).HasMaxLength(40).IsRequired();
            e.Property(x => x.Estatus).HasMaxLength(40).IsRequired();
            e.Property(x => x.Subtotal).HasPrecision(14, 2);
            e.Property(x => x.Descuento).HasPrecision(14, 2);
            e.Property(x => x.IVA).HasPrecision(14, 2);
            e.Property(x => x.Total).HasPrecision(14, 2);
            e.Property(x => x.FolioFacturaCFDI).HasMaxLength(80);
            e.Property(x => x.UsoCFDI).HasMaxLength(40);
            e.Property(x => x.MetodoPago).HasMaxLength(40);
            e.Property(x => x.FormaPago).HasMaxLength(40);
            e.Property(x => x.Observaciones).HasMaxLength(1000);
            e.Property(x => x.VendedorNombre).HasMaxLength(150);
            e.HasOne(x => x.Cliente).WithMany(c => c.Ventas).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<VentaPartida>(e =>
        {
            e.ToTable("VentaPartidas"); e.HasKey(x => x.VentaPartidaId);
            e.Property(x => x.Descripcion).HasMaxLength(300).IsRequired();
            e.Property(x => x.NumeroSerie).HasMaxLength(100);
            e.Property(x => x.Cantidad).HasPrecision(12, 2);
            e.Property(x => x.PrecioUnitario).HasPrecision(14, 2);
            e.Property(x => x.Descuento).HasPrecision(14, 2);
            e.Property(x => x.Importe).HasPrecision(14, 2);
            e.HasOne(x => x.Venta).WithMany(v => v.Partidas).HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Equipo).WithMany().HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Compra>(e =>
        {
            e.ToTable("Compras"); e.HasKey(x => x.CompraId);
            e.Property(x => x.Folio).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Folio).IsUnique();
            e.Property(x => x.Proveedor).HasMaxLength(200).IsRequired();
            e.Property(x => x.RFCProveedor).HasMaxLength(20);
            e.Property(x => x.FacturaProveedor).HasMaxLength(80);
            e.Property(x => x.Estatus).HasMaxLength(40).IsRequired();
            e.Property(x => x.Subtotal).HasPrecision(14, 2);
            e.Property(x => x.IVA).HasPrecision(14, 2);
            e.Property(x => x.Total).HasPrecision(14, 2);
            e.Property(x => x.Observaciones).HasMaxLength(1000);
            e.Property(x => x.UsuarioNombre).HasMaxLength(150);
        });

        b.Entity<CompraPartida>(e =>
        {
            e.ToTable("CompraPartidas"); e.HasKey(x => x.CompraPartidaId);
            e.Property(x => x.Descripcion).HasMaxLength(300).IsRequired();
            e.Property(x => x.Cantidad).HasPrecision(12, 2);
            e.Property(x => x.CostoUnitario).HasPrecision(14, 2);
            e.Property(x => x.Importe).HasPrecision(14, 2);
            e.HasOne(x => x.Compra).WithMany(c => c.Partidas).HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLog"); e.HasKey(x => x.AuditLogId);
            e.Property(x => x.Entidad).HasMaxLength(50);
            e.Property(x => x.EntidadId).HasMaxLength(50);
            e.Property(x => x.Accion).HasMaxLength(10);
            e.Property(x => x.ValoresAnteriores).HasColumnType("json");
            e.Property(x => x.ValoresNuevos).HasColumnType("json");
            e.Property(x => x.UsuarioNombre).HasMaxLength(150);
            e.Property(x => x.Ip).HasMaxLength(45);
            e.HasIndex(x => new { x.Entidad, x.EntidadId });
        });

        b.Entity<FolioSecuencia>(e => { e.ToTable("FolioSecuencias"); e.HasKey(x => x.Anio); e.Property(x => x.Anio).ValueGeneratedNever(); });
        b.Entity<FolioSecuenciaPrefijo>(e =>
        {
            e.ToTable("FolioSecuenciasPrefijo");
            e.HasKey(x => new { x.Prefijo, x.Anio });
            e.Property(x => x.Prefijo).HasMaxLength(10);
        });
        b.Entity<ConfiguracionVigencia>(e =>
        {
            e.ToTable("ConfiguracionVigencias"); e.HasKey(x => x.TipoEquipo);
            e.Property(x => x.TipoEquipo).HasConversion<string>().HasMaxLength(30).ValueGeneratedNever();
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var pending = new List<(EntityEntry Entry, string Action, string? Old, string? New)>();

        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is Domain.AuditLog or Domain.CertificadoHistorial or FolioSecuencia or FolioSecuenciaPrefijo or ConfiguracionVigencia or EquipoHistorial or OrdenServicioEstatusHistorial)
            {
                if (entry.Entity is Domain.AuditLog && (entry.State == EntityState.Modified || entry.State == EntityState.Deleted))
                    throw new InvalidOperationException("La bitácora de auditoría es inalterable.");
                continue;
            }
            if (entry.State is EntityState.Detached or EntityState.Unchanged) continue;

            if (entry.Entity is Certificado)
            {
                if (entry.State == EntityState.Deleted)
                    throw new InvalidOperationException("Los certificados no pueden eliminarse; cancélelos.");
                if (entry.State == EntityState.Modified)
                    foreach (var p in new[] { nameof(Certificado.Folio), nameof(Certificado.Anio), nameof(Certificado.Consecutivo) })
                        if (entry.Property(p).IsModified)
                            throw new InvalidOperationException("El folio es inalterable.");
            }

            if (entry.Entity is AuditableEntity a)
            {
                if (entry.State == EntityState.Added) a.CreadoEn = now;
                else if (entry.State == EntityState.Modified) { a.ModificadoEn = now; entry.Property(nameof(AuditableEntity.CreadoEn)).IsModified = false; }
            }

            string action = entry.State switch { EntityState.Added => "Insert", EntityState.Modified => "Update", _ => "Delete" };
            string? oldV = null, newV = null;
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                oldV = Serialize(entry.Properties.Where(p => entry.State == EntityState.Deleted || p.IsModified)
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
            if (entry.State is EntityState.Modified)
                newV = Serialize(entry.Properties.Where(p => p.IsModified).ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
            pending.Add((entry, action, oldV, newV));
        }

        var result = await base.SaveChangesAsync(ct);

        foreach (var (entry, action, oldV, newV) in pending)
        {
            var nv = action == "Insert"
                ? Serialize(entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))
                : newV;
            AuditLogs.Add(new AuditLog
            {
                Entidad = entry.Metadata.ClrType.Name,
                EntidadId = entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()).FirstOrDefault() ?? "",
                Accion = action, ValoresAnteriores = oldV, ValoresNuevos = nv,
                UsuarioId = currentUser.UserId, UsuarioNombre = currentUser.Nombre, Ip = currentUser.Ip, Fecha = now
            });
        }
        if (pending.Count > 0) await base.SaveChangesAsync(ct);
        return result;
    }

    private static string Serialize(Dictionary<string, object?> d)
    {
        foreach (var k in d.Keys.ToList())
        {
            if (Sensitive.Contains(k)) d[k] = "***";
            else if (d[k] is string s && s.Length > 500) d[k] = s[..120] + "...[truncado]";
        }
        return JsonSerializer.Serialize(d);
    }
}
