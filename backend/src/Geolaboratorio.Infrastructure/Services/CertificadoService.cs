using System.Security.Cryptography;
using Geolaboratorio.Application;
using Geolaboratorio.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Geolaboratorio.Infrastructure.Services;

public class CertificadoService(IUnitOfWork uow, IPdfService pdf, IQrService qr, ICurrentUser user, IOptions<AppOptions> opt) : ICertificadoService
{
    private readonly AppOptions _o = opt.Value;

    private IQueryable<Certificado> Full() => uow.Certificados.Query()
        .Include(c => c.Equipo!).ThenInclude(e => e.Cliente)
        .Include(c => c.Equipo!).ThenInclude(e => e.Series)
        .Include(c => c.Tecnico)
        .Include(c => c.OrdenServicio);

    private async Task<Certificado> Load(int id, CancellationToken ct)
        => await Full().FirstOrDefaultAsync(c => c.CertificadoId == id, ct) ?? throw new NotFoundException("Certificado no encontrado.");

    public async Task<PagedResult<CertificadoDto>> ListAsync(string? q, string? estado, int? equipoId, int page, int size, CancellationToken ct = default)
    {
        (page, size) = (Math.Max(page, 1), Math.Clamp(size, 1, 200));
        var hoy = DateTime.UtcNow.Date;
        var query = Full().AsNoTracking();
        if (equipoId.HasValue) query = query.Where(c => c.EquipoId == equipoId);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Folio.Contains(q) || c.Equipo!.SeriePrincipal.Contains(q) || c.Equipo.Cliente!.RazonSocial.Contains(q));
        query = estado switch
        {
            "Cancelado" => query.Where(c => c.Estatus == EstatusCertificado.Cancelado),
            "Vencido" => query.Where(c => c.Estatus == EstatusCertificado.Vigente && c.FechaVencimiento < hoy),
            "PorVencer" => query.Where(c => c.Estatus == EstatusCertificado.Vigente && c.FechaVencimiento >= hoy && c.FechaVencimiento <= hoy.AddDays(30)),
            "Vigente" => query.Where(c => c.Estatus == EstatusCertificado.Vigente && c.FechaVencimiento > hoy.AddDays(30)),
            _ => query
        };
        if (user.Rol == nameof(RolUsuario.Consulta)) query = query.Where(c => c.Estatus == EstatusCertificado.Vigente);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(c => c.CertificadoId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(c => c.ToDto()).ToList(), total, page, size);
    }

    public async Task<CertificadoDto> GetAsync(int id, CancellationToken ct = default) => (await Load(id, ct)).ToDto();

    public async Task<CertificadoDto> CreateAsync(CertificadoCreateDto d, CancellationToken ct = default)
    {
        var equipo = await uow.Equipos.GetByIdAsync(d.EquipoId, ct) ?? throw new BusinessException("El equipo no existe.");
        var tecnicoId = user.UserId ?? throw new UnauthorizedAccessException();
        var meses = d.VigenciaMeses ?? await uow.VigenciaPorTipoAsync(equipo.TipoEquipo, ct);
        var fechaCal = d.FechaCalibracion.Date;
        var emision = (d.FechaEmision ?? DateTime.UtcNow).Date;
        var tipoReg = string.IsNullOrWhiteSpace(d.TipoRegistro) ? "Calibración" : d.TipoRegistro.Trim();

        await uow.BeginAsync(ct);
        try
        {
            var anio = emision.Year;
            var consecutivo = await uow.NextConsecutivoAsync(anio, ct);
            var cert = new Certificado
            {
                Anio = anio, Consecutivo = consecutivo, Folio = $"GEO-{anio}-{consecutivo:D6}",
                EquipoId = equipo.EquipoId, OrdenServicioId = d.OrdenServicioId, TipoRegistro = tipoReg,
                TecnicoId = tecnicoId, FechaCalibracion = fechaCal, FechaEmision = emision,
                VigenciaMeses = meses, FechaVencimiento = fechaCal.AddMonths(meses), Resultado = d.Resultado,
                Resultados = d.Resultados, Observaciones = d.Observaciones, Conclusiones = d.Conclusiones,
                QRUrl = qr.BuildUrl($"GEO-{anio}-{consecutivo:D6}")
            };
            await uow.Certificados.AddAsync(cert, ct);
            await uow.SaveChangesAsync(ct);

            await uow.EquipoHistoriales.AddAsync(new EquipoHistorial
            {
                EquipoId = equipo.EquipoId,
                TipoEvento = tipoReg == "Certificación" ? "Certificacion" : "Calibracion",
                Titulo = $"{tipoReg} emitida · Folio {cert.Folio}",
                Detalle = $"Dictamen: {cert.Resultado}. Vigencia: {cert.VigenciaMeses} meses (vence {cert.FechaVencimiento:dd/MM/yyyy}).",
                FolioReferencia = cert.Folio,
                ReferenciaId = cert.CertificadoId,
                UsuarioId = user.UserId,
                UsuarioNombre = user.Nombre
            }, ct);

            await Log(cert.CertificadoId, "Creado", $"Folio {cert.Folio}", null, ct);
            await GenerateAndStore(cert.CertificadoId, "PdfGenerado", ct);
            await uow.CommitAsync(ct);
            return (await Load(cert.CertificadoId, ct)).ToDto();
        }
        catch { await uow.RollbackAsync(ct); throw; }
    }

    public async Task<CertificadoDto> UpdateAsync(int id, CertificadoUpdateDto d, CancellationToken ct = default)
    {
        var c = await Load(id, ct);
        if (c.Estatus == EstatusCertificado.Cancelado) throw new BusinessException("Un certificado cancelado no puede modificarse.");
        c.FechaCalibracion = d.FechaCalibracion.Date; c.VigenciaMeses = d.VigenciaMeses;
        c.FechaVencimiento = c.FechaCalibracion.AddMonths(d.VigenciaMeses);
        c.Resultado = d.Resultado; c.Resultados = d.Resultados; c.Observaciones = d.Observaciones; c.Conclusiones = d.Conclusiones;
        if (!string.IsNullOrWhiteSpace(d.TipoRegistro)) c.TipoRegistro = d.TipoRegistro.Trim();
        c.Version++;
        await uow.SaveChangesAsync(ct);
        await Log(id, "Modificado", $"v{c.Version}: {d.MotivoCambio}", null, ct);
        await GenerateAndStore(id, "PdfGenerado", ct);
        return (await Load(id, ct)).ToDto();
    }

    public async Task<CertificadoDto> CancelAsync(int id, string motivo, CancellationToken ct = default)
    {
        var c = await Load(id, ct);
        if (c.Estatus == EstatusCertificado.Cancelado) throw new BusinessException("El certificado ya está cancelado.");
        c.Estatus = EstatusCertificado.Cancelado; c.MotivoCancelacion = motivo; c.CanceladoEn = DateTime.UtcNow;
        await uow.EquipoHistoriales.AddAsync(new EquipoHistorial
        {
            EquipoId = c.EquipoId,
            TipoEvento = "CertificadoCancelado",
            Titulo = $"Certificado {c.Folio} cancelado",
            Detalle = $"Motivo: {motivo}",
            FolioReferencia = c.Folio,
            ReferenciaId = c.CertificadoId,
            UsuarioId = user.UserId,
            UsuarioNombre = user.Nombre
        }, ct);
        await uow.SaveChangesAsync(ct);
        await Log(id, "Cancelado", motivo, null, ct);
        await GenerateAndStore(id, "PdfGenerado", ct);
        return (await Load(id, ct)).ToDto();
    }

    public async Task<PdfFile> GetPdfAsync(int id, bool regenerate, CancellationToken ct = default)
    {
        var c = await Load(id, ct);
        if (user.Rol == nameof(RolUsuario.Consulta) && c.Estatus != EstatusCertificado.Vigente)
            throw new ForbiddenException("No tiene permiso para descargar este certificado.");

        byte[] bytes;
        var path = c.PDFUrl is null ? null : Path.Combine(_o.StoragePath, c.PDFUrl);
        if (!regenerate && path != null && File.Exists(path))
        {
            bytes = await File.ReadAllBytesAsync(path, ct);
            if (Hash(bytes) != c.PdfHash) throw new BusinessException("Integridad del PDF comprometida: el hash no coincide.");
        }
        else bytes = await GenerateAndStore(id, "PdfGenerado", ct);

        await Log(id, "PdfDescargado", null, Hash(bytes), ct);
        return new PdfFile(bytes, $"{c.Folio}.pdf");
    }

    public async Task<IReadOnlyList<HistorialDto>> HistorialAsync(int id, CancellationToken ct = default)
    {
        if (!await uow.Certificados.Query().AnyAsync(c => c.CertificadoId == id, ct)) throw new NotFoundException("Certificado no encontrado.");
        var rows = await uow.Historial.Where(h => h.CertificadoId == id).OrderByDescending(h => h.HistorialId).ToListAsync(ct);
        return rows.Select(h => new HistorialDto(h.HistorialId, h.Accion, h.Detalle, h.PdfHash, h.UsuarioNombre, h.Fecha)).ToList();
    }

    public async Task<VerificacionDto> VerificarAsync(string folio, CancellationToken ct = default)
    {
        folio = folio.Trim().ToUpperInvariant();
        var c = await Full().AsNoTracking().FirstOrDefaultAsync(x => x.Folio == folio, ct);
        if (c is null)
            return new("NoEncontrado", "CERTIFICADO NO EXISTENTE", null, null, null, null, null, null, null, null);
        var hoy = DateTime.UtcNow.Date;
        var equipo = $"{Mapper.TipoNombre(c.Equipo!.TipoEquipo)} {c.Equipo.Marca} {c.Equipo.Modelo}";
        if (c.Estatus == EstatusCertificado.Cancelado)
            return new("Cancelado", "CERTIFICADO EXPIRADO", c.Folio, null, null, null, null, null, null, "Expirado");
        if (c.FechaVencimiento.Date < hoy)
            return new("Vencido", "CERTIFICADO EXPIRADO", c.Folio, c.Equipo.Cliente!.RazonSocial, equipo, c.Equipo.SeriePrincipal,
                c.FechaCalibracion, c.FechaVencimiento, (hoy - c.FechaVencimiento.Date).Days, "Expirado");
        return new("Vigente", "CERTIFICADO VIGENTE", c.Folio, c.Equipo.Cliente!.RazonSocial, equipo, c.Equipo.SeriePrincipal,
            c.FechaCalibracion, c.FechaVencimiento, null, "Vigente");
    }

    private static string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    private async Task Log(int certId, string accion, string? detalle, string? hash, CancellationToken ct)
    {
        await uow.AddHistorialAsync(new CertificadoHistorial
        {
            CertificadoId = certId, Accion = accion, Detalle = detalle, PdfHash = hash,
            UsuarioId = user.UserId, UsuarioNombre = user.Nombre
        }, ct);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<byte[]> GenerateAndStore(int id, string accion, CancellationToken ct)
    {
        var c = await Load(id, ct);
        var bytes = pdf.Generate(c, qr.GeneratePng(c.Folio));
        var rel = Path.Combine(c.Anio.ToString(), $"{c.Folio}_v{c.Version}{(c.Estatus == EstatusCertificado.Cancelado ? "_CANCELADO" : "")}.pdf");
        var full = Path.Combine(_o.StoragePath, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, bytes, ct);
        c.PDFUrl = rel; c.PdfHash = Hash(bytes);
        await uow.SaveChangesAsync(ct);
        await Log(id, accion, $"Archivo {rel}", c.PdfHash, ct);
        return bytes;
    }
}
