using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Geolaboratorio.Application;
using Geolaboratorio.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Geolaboratorio.Infrastructure.Services;

public class AuthService(IUnitOfWork uow, IOptions<JwtOptions> opt) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest r, CancellationToken ct = default)
    {
        var correo = r.Correo.Trim().ToLowerInvariant();
        var u = await uow.Usuarios.Query().FirstOrDefaultAsync(x => x.Correo == correo, ct);
        if (u is null || !u.Activo || !BCrypt.Net.BCrypt.Verify(r.Password, u.PasswordHash))
            throw new UnauthorizedAccessException("Credenciales inválidas.");

        var o = opt.Value;
        var expira = DateTime.UtcNow.AddMinutes(o.ExpireMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, u.UsuarioId.ToString()),
            new Claim(ClaimTypes.Name, u.Nombre),
            new Claim(ClaimTypes.Email, u.Correo),
            new Claim(ClaimTypes.Role, u.Rol.ToString())
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, expires: expira, signingCredentials: creds);
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expira, u.ToDto());
    }
}

public static class UsuarioMap
{
    public static UsuarioDto ToDto(this Usuario u) => new(u.UsuarioId, u.Nombre, u.Correo, u.Rol.ToString(), u.Activo);
}

public class UsuarioService(IUnitOfWork uow) : IUsuarioService
{
    public async Task<IReadOnlyList<UsuarioDto>> ListAsync(CancellationToken ct = default)
        => (await uow.Usuarios.Query().AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(ct)).Select(x => x.ToDto()).ToList();

    public async Task<UsuarioDto> CreateAsync(UsuarioCreateDto d, CancellationToken ct = default)
    {
        var correo = d.Correo.Trim().ToLowerInvariant();
        if (await uow.Usuarios.Query().AnyAsync(x => x.Correo == correo, ct)) throw new BusinessException("El correo ya está registrado.");
        var u = new Usuario { Nombre = d.Nombre.Trim(), Correo = correo, Rol = d.Rol, PasswordHash = BCrypt.Net.BCrypt.HashPassword(d.Password) };
        await uow.Usuarios.AddAsync(u, ct);
        await uow.SaveChangesAsync(ct);
        return u.ToDto();
    }

    public async Task<UsuarioDto> UpdateAsync(int id, UsuarioUpdateDto d, CancellationToken ct = default)
    {
        var u = await uow.Usuarios.GetByIdAsync(id, ct) ?? throw new NotFoundException("Usuario no encontrado.");
        u.Nombre = d.Nombre.Trim(); u.Rol = d.Rol; u.Activo = d.Activo;
        if (!string.IsNullOrWhiteSpace(d.Password))
        {
            if (d.Password.Length < 8) throw new BusinessException("La contraseña debe tener al menos 8 caracteres.");
            u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(d.Password);
        }
        await uow.SaveChangesAsync(ct);
        return u.ToDto();
    }
}

public class ClienteService(IUnitOfWork uow) : IClienteService
{
    public async Task<PagedResult<ClienteDto>> ListAsync(string? q, int page, int size, CancellationToken ct = default)
    {
        (page, size) = (Math.Max(page, 1), Math.Clamp(size, 1, 200));
        var query = uow.Clientes.Query().AsNoTracking().Where(c => c.Activo);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.RazonSocial.Contains(q) || (c.NombreComercial != null && c.NombreComercial.Contains(q)) || c.RFC.Contains(q));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(c => c.RazonSocial).Skip((page - 1) * size).Take(size)
            .Select(c => new { c, n = c.Equipos.Count }).ToListAsync(ct);
        return new(rows.Select(x => x.c.ToDto(x.n)).ToList(), total, page, size);
    }

    public async Task<ClienteDto> GetAsync(int id, CancellationToken ct = default)
    {
        var r = await uow.Clientes.Query().AsNoTracking().Where(c => c.ClienteId == id).Select(c => new { c, n = c.Equipos.Count }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Cliente no encontrado.");
        return r.c.ToDto(r.n);
    }

    private static void Apply(Cliente c, ClienteSaveDto d)
    {
        c.RazonSocial = d.RazonSocial.Trim(); c.NombreComercial = d.NombreComercial?.Trim(); c.RFC = d.RFC.Trim().ToUpperInvariant();
        c.Direccion = d.Direccion; c.Ciudad = d.Ciudad; c.Estado = d.Estado; c.Pais = d.Pais; c.Telefono = d.Telefono;
        c.Correo = d.Correo; c.Contacto = d.Contacto;
    }

    public async Task<ClienteDto> CreateAsync(ClienteSaveDto d, CancellationToken ct = default)
    {
        var rfc = d.RFC.Trim().ToUpperInvariant();
        if (await uow.Clientes.Query().AnyAsync(c => c.RFC == rfc, ct)) throw new BusinessException("Ya existe un cliente con ese RFC.");
        var c = new Cliente(); Apply(c, d);
        await uow.Clientes.AddAsync(c, ct); await uow.SaveChangesAsync(ct);
        return c.ToDto(0);
    }

    public async Task<ClienteDto> UpdateAsync(int id, ClienteSaveDto d, CancellationToken ct = default)
    {
        var c = await uow.Clientes.GetByIdAsync(id, ct) ?? throw new NotFoundException("Cliente no encontrado.");
        var rfc = d.RFC.Trim().ToUpperInvariant();
        if (await uow.Clientes.Query().AnyAsync(x => x.RFC == rfc && x.ClienteId != id, ct)) throw new BusinessException("Ya existe un cliente con ese RFC.");
        Apply(c, d); await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }
}

public class EquipoService(IUnitOfWork uow, ICurrentUser user) : IEquipoService
{
    private IQueryable<Equipo> BaseQuery() => uow.Equipos.Query()
        .Include(e => e.Cliente)
        .Include(e => e.Series)
        .Include(e => e.Certificados)
        .Include(e => e.OrdenesServicio);

    public async Task<PagedResult<EquipoDto>> ListAsync(string? q, int? clienteId, int page, int size, CancellationToken ct = default)
    {
        (page, size) = (Math.Max(page, 1), Math.Clamp(size, 1, 200));
        var query = BaseQuery().AsNoTracking().AsQueryable();
        if (clienteId.HasValue) query = query.Where(e => e.ClienteId == clienteId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(e =>
                e.SeriePrincipal.Contains(q)
                || (e.SerieSecundaria != null && e.SerieSecundaria.Contains(q))
                || (e.IMEI1 != null && e.IMEI1.Contains(q))
                || (e.IMEI2 != null && e.IMEI2.Contains(q))
                || (e.NumeroInterno != null && e.NumeroInterno.Contains(q))
                || (e.CodigoQR != null && e.CodigoQR.Contains(q))
                || (e.CodigoBarras != null && e.CodigoBarras.Contains(q))
                || e.Marca.Contains(q)
                || e.Modelo.Contains(q)
                || e.Cliente!.RazonSocial.Contains(q)
                || e.Series.Any(s => s.Valor.Contains(q)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(e => e.EquipoId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(e => e.ToDto()).ToList(), total, page, size);
    }

    public async Task<EquipoDto> GetAsync(int id, CancellationToken ct = default)
        => (await BaseQuery().AsNoTracking().FirstOrDefaultAsync(e => e.EquipoId == id, ct)
            ?? throw new NotFoundException("Equipo no encontrado.")).ToDto();

    private static void Apply(Equipo e, EquipoSaveDto d)
    {
        e.ClienteId = d.ClienteId;
        e.TipoEquipo = d.TipoEquipo;
        e.Marca = d.Marca.Trim();
        e.Modelo = d.Modelo.Trim();
        e.SeriePrincipal = d.SeriePrincipal.Trim();
        e.SerieSecundaria = d.SerieSecundaria;
        e.IMEI1 = string.IsNullOrWhiteSpace(d.IMEI1) ? null : d.IMEI1.Trim();
        e.IMEI2 = string.IsNullOrWhiteSpace(d.IMEI2) ? null : d.IMEI2.Trim();
        e.NumeroInterno = d.NumeroInterno;
        e.CodigoQR = string.IsNullOrWhiteSpace(d.CodigoQR) ? e.CodigoQR : d.CodigoQR.Trim();
        e.CodigoBarras = string.IsNullOrWhiteSpace(d.CodigoBarras) ? d.SeriePrincipal.Trim() : d.CodigoBarras.Trim();
        e.FechaCompra = d.FechaCompra;
        e.Observaciones = d.Observaciones;
        if (d.Estatus.HasValue) e.Estatus = d.Estatus.Value;
    }

    private async Task SyncSeriesAsync(Equipo e, IReadOnlyList<EquipoSerieDto>? seriesDto, CancellationToken ct)
    {
        var existing = await uow.EquipoSeries.Query().Where(s => s.EquipoId == e.EquipoId).ToListAsync(ct);
        foreach (var ex in existing) uow.EquipoSeries.Remove(ex);

        var items = new List<EquipoSerieDto>();
        if (seriesDto is { Count: > 0 })
            items.AddRange(seriesDto.Where(s => !string.IsNullOrWhiteSpace(s.TipoSerie) && !string.IsNullOrWhiteSpace(s.Valor)));

        void EnsureSerie(string tipo, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            if (!items.Any(x => x.TipoSerie.Equals(tipo, StringComparison.OrdinalIgnoreCase) && x.Valor.Equals(val, StringComparison.OrdinalIgnoreCase)))
                items.Add(new EquipoSerieDto(null, tipo, val.Trim()));
        }

        EnsureSerie("Serie Principal", e.SeriePrincipal);
        EnsureSerie("Serie Secundaria", e.SerieSecundaria);
        EnsureSerie("IMEI 1", e.IMEI1);
        EnsureSerie("IMEI 2", e.IMEI2);

        foreach (var item in items)
        {
            await uow.EquipoSeries.AddAsync(new EquipoSerie
            {
                EquipoId = e.EquipoId,
                TipoSerie = item.TipoSerie.Trim(),
                Valor = item.Valor.Trim(),
                Observaciones = item.Observaciones
            }, ct);
        }
    }

    private async Task Validate(EquipoSaveDto d, int? id, CancellationToken ct)
    {
        if (!await uow.Clientes.Query().AnyAsync(c => c.ClienteId == d.ClienteId, ct))
            throw new BusinessException("El cliente no existe.");
        var s = d.SeriePrincipal.Trim();
        if (await uow.Equipos.Query().AnyAsync(e => e.SeriePrincipal == s && e.EquipoId != id, ct))
            throw new BusinessException("El número de serie principal ya está registrado.");
    }

    public async Task<EquipoDto> CreateAsync(EquipoSaveDto d, CancellationToken ct = default)
    {
        await Validate(d, null, ct);
        var e = new Equipo();
        Apply(e, d);
        await uow.Equipos.AddAsync(e, ct);
        await uow.SaveChangesAsync(ct);

        if (string.IsNullOrWhiteSpace(e.CodigoQR)) e.CodigoQR = $"EQ-{e.EquipoId:D6}";
        await SyncSeriesAsync(e, d.Series, ct);

        await uow.EquipoHistoriales.AddAsync(new EquipoHistorial
        {
            EquipoId = e.EquipoId,
            TipoEvento = "Registro",
            Titulo = $"Alta de equipo en plataforma · {Mapper.TipoNombre(e.TipoEquipo)} {e.Marca} {e.Modelo}",
            Detalle = $"Serie Principal: {e.SeriePrincipal} · No. Interno: {e.NumeroInterno ?? "N/A"}",
            FolioReferencia = e.CodigoQR,
            ReferenciaId = e.EquipoId,
            UsuarioId = user.UserId,
            UsuarioNombre = user.Nombre
        }, ct);

        await uow.SaveChangesAsync(ct);
        return await GetAsync(e.EquipoId, ct);
    }

    public async Task<EquipoDto> UpdateAsync(int id, EquipoSaveDto d, CancellationToken ct = default)
    {
        var e = await uow.Equipos.GetByIdAsync(id, ct) ?? throw new NotFoundException("Equipo no encontrado.");
        await Validate(d, id, ct);
        var prevCliente = e.ClienteId;
        Apply(e, d);
        await SyncSeriesAsync(e, d.Series, ct);

        await uow.EquipoHistoriales.AddAsync(new EquipoHistorial
        {
            EquipoId = e.EquipoId,
            TipoEvento = prevCliente != d.ClienteId ? "CambioCliente" : "Actualizacion",
            Titulo = prevCliente != d.ClienteId ? "Transferencia / cambio de cliente propietario" : "Actualización de datos y series del equipo",
            Detalle = $"Serie Principal: {e.SeriePrincipal} · Estatus: {e.Estatus}",
            ReferenciaId = e.EquipoId,
            UsuarioId = user.UserId,
            UsuarioNombre = user.Nombre
        }, ct);

        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<EquipoBusquedaResultDto> BuscarIdentificacionAsync(string codigo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return new(false, "", null, null, [], [], [], []);

        var q = codigo.Trim();
        var equipo = await BaseQuery().AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.SeriePrincipal == q
                || e.NumeroInterno == q
                || e.CodigoQR == q
                || e.CodigoBarras == q
                || e.SerieSecundaria == q
                || e.IMEI1 == q
                || e.IMEI2 == q
                || e.Series.Any(s => s.Valor == q), ct);

        if (equipo is null)
        {
            // Búsqueda parcial si no hubo coincidencia exacta
            equipo = await BaseQuery().AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.SeriePrincipal.Contains(q)
                    || (e.NumeroInterno != null && e.NumeroInterno.Contains(q))
                    || (e.CodigoQR != null && e.CodigoQR.Contains(q))
                    || e.Series.Any(s => s.Valor.Contains(q)), ct);
        }

        if (equipo is null)
            return new(false, q, null, null, [], [], [], []);

        var clienteCount = await uow.Equipos.Query().CountAsync(x => x.ClienteId == equipo.ClienteId, ct);
        var ordenes = await uow.OrdenesServicio.Query().AsNoTracking()
            .Where(o => o.EquipoId == equipo.EquipoId)
            .OrderByDescending(o => o.FechaRecepcion)
            .Take(10)
            .Select(o => new OrdenServicioResumenDto(o.OrdenServicioId, o.Folio, o.TipoServicio, o.Estatus, o.FechaRecepcion, o.Diagnostico))
            .ToListAsync(ct);

        var certs = await uow.Certificados.Query().AsNoTracking()
            .Include(c => c.Equipo!).ThenInclude(x => x.Cliente)
            .Include(c => c.Tecnico)
            .Where(c => c.EquipoId == equipo.EquipoId)
            .OrderByDescending(c => c.FechaCalibracion)
            .Take(5)
            .ToListAsync(ct);

        var garantias = await uow.Garantias.Query().AsNoTracking()
            .Include(g => g.Equipo).Include(g => g.Cliente)
            .Where(g => g.EquipoId == equipo.EquipoId && g.FechaFin >= DateTime.UtcNow.Date)
            .ToListAsync(ct);

        return new(
            true,
            q,
            equipo.ToDto(),
            equipo.Cliente?.ToDto(clienteCount),
            equipo.BuildSeriesList(),
            ordenes,
            certs.Select(c => c.ToDto()).ToList(),
            garantias.Select(g => g.ToDto()).ToList());
    }

    public async Task<ExpedienteEquipoDto> GetExpedienteAsync(int id, CancellationToken ct = default)
    {
        var equipo = await BaseQuery().AsNoTracking()
            .FirstOrDefaultAsync(e => e.EquipoId == id, ct)
            ?? throw new NotFoundException("Equipo no encontrado.");

        var totalEqCliente = await uow.Equipos.Query().CountAsync(x => x.ClienteId == equipo.ClienteId, ct);
        var clienteDto = equipo.Cliente!.ToDto(totalEqCliente);
        var series = equipo.BuildSeriesList();

        var certsRaw = await uow.Certificados.Query().AsNoTracking()
            .Include(c => c.Equipo!).ThenInclude(x => x.Cliente)
            .Include(c => c.Tecnico)
            .Include(c => c.OrdenServicio)
            .Where(c => c.EquipoId == id)
            .OrderByDescending(c => c.FechaCalibracion)
            .ToListAsync(ct);
        var certs = certsRaw.Select(c => c.ToDto()).ToList();
        var calibraciones = certs.Where(c => c.TipoRegistro != "Certificación").ToList();
        var certificaciones = certs.Where(c => c.TipoRegistro == "Certificación").ToList();

        var ordenesRaw = await uow.OrdenesServicio.Query().AsNoTracking()
            .Include(o => o.Cliente)
            .Include(o => o.Equipo!).ThenInclude(x => x.Series)
            .Include(o => o.Tecnico)
            .Include(o => o.Supervisor)
            .Include(o => o.HistorialEstatus)
            .Include(o => o.Refacciones).ThenInclude(r => r.Producto)
            .Include(o => o.Fotografias)
            .Where(o => o.EquipoId == id)
            .OrderByDescending(o => o.FechaRecepcion)
            .ToListAsync(ct);
        var ordenes = ordenesRaw.Select(o => o.ToDto()).ToList();

        var mantRaw = await uow.Mantenimientos.Query().AsNoTracking()
            .Include(m => m.Equipo!).ThenInclude(x => x.Cliente)
            .Include(m => m.OrdenServicio)
            .Include(m => m.Tecnico)
            .Where(m => m.EquipoId == id)
            .OrderByDescending(m => m.FechaProgramada)
            .ToListAsync(ct);
        var todosMants = mantRaw.Select(m => m.ToDto()).ToList();
        var mantenimientos = todosMants.Where(m => m.Tipo != "Reparación").ToList();
        var reparaciones = todosMants.Where(m => m.Tipo == "Reparación").ToList();

        var refacciones = ordenesRaw
            .SelectMany(o => o.Refacciones.Select(r => new RefaccionEquipoResumenDto(
                r.OrdenServicioRefaccionId,
                o.OrdenServicioId,
                o.Folio,
                r.ProductoId,
                r.SKU ?? r.Producto?.SKU ?? "",
                r.NombreProducto,
                r.Cantidad,
                r.PrecioUnitario,
                r.Importe,
                o.FechaRecepcion)))
            .OrderByDescending(r => r.Fecha)
            .ToList();

        var garantiasRaw = await uow.Garantias.Query().AsNoTracking()
            .Include(g => g.Equipo).Include(g => g.Cliente)
            .Where(g => g.EquipoId == id)
            .OrderByDescending(g => g.FechaFin)
            .ToListAsync(ct);
        var garantias = garantiasRaw.Select(g => g.ToDto()).ToList();

        var ventasRaw = await uow.Ventas.Query().AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Partidas).ThenInclude(p => p.Producto)
            .Where(v => v.Partidas.Any(p => p.EquipoId == id || p.NumeroSerie == equipo.SeriePrincipal) || v.ClienteId == equipo.ClienteId && v.Partidas.Any(p => p.Descripcion.Contains(equipo.SeriePrincipal)))
            .OrderByDescending(v => v.Fecha)
            .ToListAsync(ct);
        var ventas = ventasRaw.Select(v => v.ToDto()).ToList();

        var archivosRaw = await uow.EquipoArchivos.Query().AsNoTracking()
            .Where(a => a.EquipoId == id)
            .OrderByDescending(a => a.Fecha)
            .ToListAsync(ct);
        var archivos = archivosRaw.Select(a => new EquipoArchivoDto(
            a.EquipoArchivoId, a.EquipoId, a.OrdenServicioId, a.TipoArchivo, a.Categoria, a.NombreArchivo, a.ArchivoUrl, a.Descripcion, a.SubidoPor, a.Fecha)).ToList();

        var fotografias = ordenesRaw.SelectMany(o => o.Fotografias)
            .OrderByDescending(f => f.Fecha)
            .Select(f => new OrdenServicioFotoDto(f.OrdenServicioFotoId, f.OrdenServicioId, f.Categoria, f.Descripcion, f.ImagenUrl, f.UsuarioNombre, f.Fecha))
            .ToList();

        var historialRaw = await uow.EquipoHistoriales.Query().AsNoTracking()
            .Where(h => h.EquipoId == id)
            .OrderByDescending(h => h.Fecha)
            .ToListAsync(ct);
        var historial = historialRaw.Select(h => new EquipoHistorialDto(
            h.EquipoHistorialId, h.EquipoId, h.TipoEvento, h.Titulo, h.Detalle, h.FolioReferencia, h.ReferenciaId, h.UsuarioNombre, h.Fecha)).ToList();

        if (!historial.Any(h => h.TipoEvento == "Registro"))
        {
            historial.Add(new EquipoHistorialDto(
                0, equipo.EquipoId, "Registro",
                $"Registro inicial del equipo {Mapper.TipoNombre(equipo.TipoEquipo)} {equipo.Marca} {equipo.Modelo}",
                $"Serie Principal: {equipo.SeriePrincipal}",
                equipo.CodigoQR ?? $"EQ-{equipo.EquipoId:D6}",
                equipo.EquipoId, "Sistema", equipo.CreadoEn));
        }

        return new ExpedienteEquipoDto(
            equipo.ToDto(),
            clienteDto,
            series,
            certs,
            calibraciones,
            certificaciones,
            mantenimientos,
            reparaciones,
            refacciones,
            ordenes,
            garantias,
            ventas,
            archivos,
            fotografias,
            historial.OrderByDescending(h => h.Fecha).ToList());
    }

    public async Task<EquipoDto> DarDeBajaAsync(int id, EquipoBajaDto d, CancellationToken ct = default)
    {
        var e = await uow.Equipos.GetByIdAsync(id, ct) ?? throw new NotFoundException("Equipo no encontrado.");
        if (string.IsNullOrWhiteSpace(d.MotivoBaja)) throw new BusinessException("Indique el motivo de la baja del equipo.");
        e.Estatus = EstatusEquipo.Baja;
        e.FechaBaja = d.FechaBaja ?? DateTime.UtcNow;
        e.MotivoBaja = d.MotivoBaja.Trim();

        await uow.EquipoHistoriales.AddAsync(new EquipoHistorial
        {
            EquipoId = e.EquipoId,
            TipoEvento = "Baja",
            Titulo = "Baja definitiva del equipo",
            Detalle = $"Motivo de baja: {e.MotivoBaja}",
            ReferenciaId = e.EquipoId,
            UsuarioId = user.UserId,
            UsuarioNombre = user.Nombre,
            Fecha = e.FechaBaja.Value
        }, ct);

        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<EquipoArchivoDto> AgregarArchivoAsync(int id, EquipoArchivoCreateDto d, CancellationToken ct = default)
    {
        if (!await uow.Equipos.Query().AnyAsync(e => e.EquipoId == id, ct))
            throw new NotFoundException("Equipo no encontrado.");

        var a = new EquipoArchivo
        {
            EquipoId = id,
            OrdenServicioId = d.OrdenServicioId,
            TipoArchivo = d.TipoArchivo,
            Categoria = d.Categoria,
            NombreArchivo = d.NombreArchivo,
            ArchivoUrl = d.ArchivoUrl,
            Descripcion = d.Descripcion,
            SubidoPor = user.Nombre
        };
        await uow.EquipoArchivos.AddAsync(a, ct);
        await uow.SaveChangesAsync(ct);
        return new EquipoArchivoDto(a.EquipoArchivoId, a.EquipoId, a.OrdenServicioId, a.TipoArchivo, a.Categoria, a.NombreArchivo, a.ArchivoUrl, a.Descripcion, a.SubidoPor, a.Fecha);
    }
}

public class AuditService(IUnitOfWork uow) : IAuditService
{
    public async Task<PagedResult<AuditLogDto>> ListAsync(string? entidad, int page, int size, CancellationToken ct = default)
    {
        (page, size) = (Math.Max(page, 1), Math.Clamp(size, 1, 200));
        var q = uow.AuditLogs;
        if (!string.IsNullOrWhiteSpace(entidad)) q = q.Where(a => a.Entidad == entidad);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.AuditLogId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(a => new AuditLogDto(a.AuditLogId, a.Entidad, a.EntidadId, a.Accion, a.ValoresAnteriores,
            a.ValoresNuevos, a.UsuarioNombre, a.Ip, a.Fecha)).ToList(), total, page, size);
    }
}

public class DashboardService(IUnitOfWork uow) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var hoy = DateTime.UtcNow.Date;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var certs = uow.Certificados.Query().AsNoTracking();
        var vigentes = certs.Where(c => c.Estatus == EstatusCertificado.Vigente);

        var activos = await vigentes.CountAsync(c => c.FechaVencimiento >= hoy, ct);
        var vencidos = await vigentes.CountAsync(c => c.FechaVencimiento < hoy, ct);
        var porVencer = await vigentes.CountAsync(c => c.FechaVencimiento >= hoy && c.FechaVencimiento <= hoy.AddDays(30), ct);
        var cancelados = await certs.CountAsync(c => c.Estatus == EstatusCertificado.Cancelado, ct);
        var totalEquipos = await uow.Equipos.Query().CountAsync(ct);
        var totalClientes = await uow.Clientes.Query().CountAsync(c => c.Activo, ct);
        var emitidosMes = await certs.CountAsync(c => c.FechaEmision >= inicioMes, ct);

        var kpis = new KpiDto(activos, vencidos, porVencer, totalEquipos, totalClientes, emitidosMes);

        async Task<IReadOnlyList<AlertaDto>> Alertas(int dias)
        {
            var rows = await vigentes.Where(c => c.FechaVencimiento >= hoy && c.FechaVencimiento <= hoy.AddDays(dias))
                .OrderBy(c => c.FechaVencimiento).Take(50).Include(c => c.Equipo!).ThenInclude(e => e.Cliente).ToListAsync(ct);
            return rows.Select(c => new AlertaDto(c.CertificadoId, c.Folio, c.Equipo!.Cliente!.RazonSocial,
                $"{c.Equipo.Marca} {c.Equipo.Modelo}", c.FechaVencimiento, (c.FechaVencimiento.Date - hoy).Days)).ToList();
        }

        var desde = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-11);
        var porMesRaw = await certs.Where(c => c.FechaEmision >= desde)
            .GroupBy(c => new { c.FechaEmision.Year, c.FechaEmision.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, N = g.Count() }).ToListAsync(ct);
        var porMes = Enumerable.Range(0, 12).Select(i => desde.AddMonths(i)).Select(m =>
            new SerieItem(m.ToString("MMM yy", new System.Globalization.CultureInfo("es-MX")),
                porMesRaw.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.N ?? 0)).ToList();

        var porTipoRaw = await certs.Select(c => c.Equipo!.TipoEquipo).ToListAsync(ct);
        var porTipo = porTipoRaw.GroupBy(t => t).Select(g => new SerieItem(Mapper.TipoNombre(g.Key), g.Count())).OrderByDescending(s => s.Valor).ToList();

        var estado = new List<SerieItem>
        {
            new("Vigentes", Math.Max(0, activos - porVencer)), new("Por vencer", porVencer),
            new("Vencidos", vencidos), new("Cancelados", cancelados)
        };

        // ───────────── Indicadores Ejecutivos ERP ─────────────
        var ordenes = uow.OrdenesServicio.Query().AsNoTracking();
        var ordenesAbiertas = await ordenes.CountAsync(o => o.Estatus != "Entregado" && o.Estatus != "Cancelado", ct);
        var equiposEnTaller = await ordenes.Where(o => o.Estatus != "Entregado" && o.Estatus != "Cancelado")
            .Select(o => o.EquipoId).Distinct().CountAsync(ct);

        var recibidosHoy = await ordenes.CountAsync(o => o.FechaRecepcion >= hoy, ct);
        var enDiagnostico = await ordenes.CountAsync(o => o.Estatus == "Recibido" || o.Estatus == "En Diagnóstico" || o.Estatus == "Pendiente Cotización" || o.Estatus == "Esperando Autorización", ct);
        var enReparacion = await ordenes.CountAsync(o => o.Estatus == "Autorizado" || o.Estatus == "En Reparación" || o.Estatus == "En Mantenimiento", ct);
        var enCalibracion = await ordenes.CountAsync(o => o.Estatus == "En Calibración" || o.Estatus == "En Certificación", ct);
        var terminados = await ordenes.CountAsync(o => o.Estatus == "Control de Calidad" || o.Estatus == "Terminado" || o.Estatus == "Listo para Entrega", ct);
        var entregados = await ordenes.CountAsync(o => o.Estatus == "Entregado", ct);

        var ordenesPorEstatusRaw = await ordenes.GroupBy(o => o.Estatus).Select(g => new { Estatus = g.Key, N = g.Count() }).ToListAsync(ct);
        var ordenesPorEstatus = ordenesPorEstatusRaw.Select(x => new SerieItem(x.Estatus, x.N)).OrderByDescending(x => x.Valor).ToList();

        var productos = await uow.Productos.Query().AsNoTracking().Where(p => p.Activo).ToListAsync(ct);
        var productosRegistrados = productos.Count;
        var valorTotalInventario = productos.Sum(p => Math.Max(0, p.StockActual) * p.CostoUnitario);
        var productosSinStock = productos.Count(p => p.StockActual <= 0);
        var stockBajo = productos.Count(p => p.StockActual <= p.StockMinimo);
        var refaccionesCriticas = productos.Count(p => p.GrupoCategoria == "Refacciones" && p.StockActual <= p.StockMinimo);

        var ventas = uow.Ventas.Query().AsNoTracking();
        var ventasMesList = await ventas
            .Where(v => v.Fecha >= inicioMes && v.Estatus != "Cancelada" && v.TipoDocumento != "Cotización")
            .Select(v => v.Total)
            .ToListAsync(ct);
        var ventasDelMes = ventasMesList.Sum();
        var cantidadVentasMes = ventasMesList.Count;
        var ventasSinSalida = await ventas.CountAsync(v => (v.Estatus == "Entregada" || v.Estatus == "Facturada") && !v.SalidaInventarioRegistrada, ct);

        var movsMes = await uow.MovimientosInventario.Query().AsNoTracking()
            .Where(m => m.Fecha >= inicioMes)
            .ToListAsync(ct);
        var entradasMes = movsMes.Count(m => m.TipoMovimiento == "Entrada");
        var salidasMes = movsMes.Count(m => m.TipoMovimiento == "Salida");
        var unidadesEntradasMes = movsMes.Where(m => m.TipoMovimiento == "Entrada").Sum(m => m.Cantidad);
        var unidadesSalidasMes = movsMes.Where(m => m.TipoMovimiento == "Salida").Sum(m => m.Cantidad);

        var refaccionesTopRaw = await uow.OrdenesServicioRefacciones.Query().AsNoTracking()
            .Select(r => new { r.NombreProducto, r.Cantidad })
            .ToListAsync(ct);
        var refaccionesMasUsadas = refaccionesTopRaw
            .GroupBy(r => r.NombreProducto)
            .Select(g => new SerieDecimalItem(g.Key, g.Sum(x => x.Cantidad)))
            .OrderByDescending(x => x.Valor)
            .Take(6)
            .ToList();

        var productosVendidosRaw = await uow.VentaPartidas.Query().AsNoTracking()
            .Include(p => p.Venta)
            .Where(p => p.Venta != null && p.Venta.Estatus != "Cancelada" && p.Venta.TipoDocumento != "Cotización")
            .Select(p => new { p.Descripcion, p.Cantidad })
            .ToListAsync(ct);
        var productosMasVendidos = productosVendidosRaw
            .GroupBy(p => p.Descripcion)
            .Select(g => new SerieDecimalItem(g.Key, g.Sum(x => x.Cantidad)))
            .OrderByDescending(x => x.Valor)
            .Take(6)
            .ToList();

        var generales = new IndicadoresGeneralesDto(
            totalClientes,
            totalEquipos,
            ordenesAbiertas,
            equiposEnTaller,
            activos,
            porVencer,
            ventasDelMes,
            cantidadVentasMes,
            valorTotalInventario,
            productosSinStock,
            refaccionesCriticas,
            ventasSinSalida);

        var tecnicos = new IndicadoresTecnicosDto(
            recibidosHoy,
            enDiagnostico,
            enReparacion,
            enCalibracion,
            terminados,
            entregados);

        var inventario = new IndicadoresInventarioDto(
            productosRegistrados,
            stockBajo,
            refaccionesMasUsadas,
            productosMasVendidos,
            entradasMes,
            salidasMes,
            unidadesEntradasMes,
            unidadesSalidasMes);

        return new(kpis, await Alertas(30), await Alertas(15), await Alertas(7), porMes, porTipo, estado, generales, tecnicos, inventario, ordenesPorEstatus);
    }
}
