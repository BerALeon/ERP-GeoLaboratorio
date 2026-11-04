using System.Text.Json;
using Geolaboratorio.Application;
using Geolaboratorio.Domain;

namespace Geolaboratorio.Infrastructure.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "Geolaboratorio";
    public string Audience { get; set; } = "GeolaboratorioApp";
    public string Key { get; set; } = "";
    public int ExpireMinutes { get; set; } = 480;
}

public class AppOptions
{
    /// <summary>Base pública del portal de verificación; el QR apunta a {Base}/{folio}.</summary>
    public string VerificationBaseUrl { get; set; } = "https://certificados.geolaboratorio.com.mx/verificar";
    public string StoragePath { get; set; } = "storage";
    public string? LogoPath { get; set; }
    public string ResponsableNombre { get; set; } = "Responsable Técnico";
    public string ResponsablePuesto { get; set; } = "Responsable del Laboratorio";
    public string? ResponsableFirmaPath { get; set; }
    public int DiasAlertaPorVencer { get; set; } = 30;
}

public static class Mapper
{
    public static string EstadoVigencia(Certificado c, DateTime hoy)
    {
        if (c.Estatus == EstatusCertificado.Cancelado) return "Cancelado";
        if (c.FechaVencimiento.Date < hoy) return "Vencido";
        return (c.FechaVencimiento.Date - hoy).Days <= 30 ? "PorVencer" : "Vigente";
    }

    public static string TipoNombre(TipoEquipo t) => t switch
    {
        TipoEquipo.EstacionTotal => "Estación Total",
        TipoEquipo.NivelAutomatico => "Nivel Automático",
        TipoEquipo.NivelDigital => "Nivel Digital",
        TipoEquipo.DroneRTK => "Drone RTK",
        TipoEquipo.EscanerLaser => "Escáner Láser",
        _ => t.ToString()
    };

    public static IReadOnlyList<EquipoSerieDto> BuildSeriesList(this Equipo e)
    {
        var list = new List<EquipoSerieDto>();
        if (e.Series is { Count: > 0 })
        {
            list.AddRange(e.Series.Select(s => new EquipoSerieDto(s.EquipoSerieId, s.TipoSerie, s.Valor, s.Observaciones)));
        }
        // Garantizar que SeriePrincipal y series clásicas siempre estén representadas si no se duplican
        if (!string.IsNullOrWhiteSpace(e.SeriePrincipal) && !list.Any(x => x.TipoSerie.Equals("Serie Principal", StringComparison.OrdinalIgnoreCase) && x.Valor == e.SeriePrincipal))
            list.Insert(0, new EquipoSerieDto(null, "Serie Principal", e.SeriePrincipal));
        if (!string.IsNullOrWhiteSpace(e.SerieSecundaria) && !list.Any(x => x.Valor == e.SerieSecundaria))
            list.Add(new EquipoSerieDto(null, "Serie Secundaria", e.SerieSecundaria));
        if (!string.IsNullOrWhiteSpace(e.IMEI1) && !list.Any(x => x.Valor == e.IMEI1))
            list.Add(new EquipoSerieDto(null, "IMEI 1", e.IMEI1));
        if (!string.IsNullOrWhiteSpace(e.IMEI2) && !list.Any(x => x.Valor == e.IMEI2))
            list.Add(new EquipoSerieDto(null, "IMEI 2", e.IMEI2));
        return list;
    }

    public static ClienteDto ToDto(this Cliente c, int equipos) => new(
        c.ClienteId, c.RazonSocial, c.NombreComercial, c.RFC,
        c.Direccion, c.Ciudad, c.Estado, c.Pais, c.Telefono, c.Correo, c.Contacto, equipos);

    public static EquipoDto ToDto(this Equipo e) => new(
        e.EquipoId,
        e.ClienteId,
        e.Cliente?.RazonSocial ?? "",
        TipoNombre(e.TipoEquipo),
        (int)e.TipoEquipo,
        e.Marca,
        e.Modelo,
        e.SeriePrincipal,
        e.SerieSecundaria,
        e.IMEI1,
        e.IMEI2,
        e.NumeroInterno,
        e.FechaCompra,
        e.Observaciones,
        e.CodigoQR ?? $"EQ-{e.EquipoId:D6}",
        e.CodigoBarras ?? e.SeriePrincipal,
        e.Estatus.ToString(),
        e.FechaBaja,
        e.MotivoBaja,
        e.BuildSeriesList(),
        e.OrdenesServicio?.Count ?? 0,
        e.Certificados?.Count ?? 0);

    public static CertificadoDto ToDto(this Certificado c)
    {
        var hoy = DateTime.UtcNow.Date;
        return new(
            c.CertificadoId,
            c.Folio,
            c.EquipoId,
            c.Equipo?.Cliente?.RazonSocial ?? "",
            c.Equipo is null ? "" : TipoNombre(c.Equipo.TipoEquipo),
            c.Equipo is null ? "" : $"{c.Equipo.Marca} {c.Equipo.Modelo}",
            c.Equipo?.SeriePrincipal ?? "",
            c.Tecnico?.Nombre ?? "",
            c.FechaCalibracion,
            c.FechaEmision,
            c.FechaVencimiento,
            c.VigenciaMeses,
            c.Resultado.ToString(),
            c.Resultados,
            c.Observaciones,
            c.Conclusiones,
            c.Estatus.ToString(),
            EstadoVigencia(c, hoy),
            (c.FechaVencimiento.Date - hoy).Days,
            c.MotivoCancelacion,
            c.Version,
            c.OrdenServicioId,
            c.OrdenServicio?.Folio,
            string.IsNullOrWhiteSpace(c.TipoRegistro) ? "Calibración" : c.TipoRegistro);
    }

    public static IReadOnlyList<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return json.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); }
    }

    public static OrdenServicioDto ToDto(this OrdenServicio o) => new(
        o.OrdenServicioId,
        o.Folio,
        o.ClienteId,
        o.Cliente?.RazonSocial ?? "",
        o.Cliente?.RFC ?? "",
        o.Cliente?.Telefono,
        o.EquipoId,
        o.Equipo is null ? "" : TipoNombre(o.Equipo.TipoEquipo),
        o.Equipo?.Marca ?? "",
        o.Equipo?.Modelo ?? "",
        o.Equipo?.SeriePrincipal ?? "",
        o.Equipo?.NumeroInterno,
        o.Equipo?.BuildSeriesList() ?? [],
        o.TipoServicio,
        ParseJsonList(o.TiposServicioAdicionales),
        o.Estatus,
        o.TecnicoId,
        o.Tecnico?.Nombre,
        o.SupervisorId,
        o.Supervisor?.Nombre,
        o.FechaRecepcion,
        o.FechaCompromiso,
        o.FechaTermino,
        o.FechaEntrega,
        o.MetodoIdentificacion ?? "Manual",
        ParseJsonList(o.AccesoriosChecklistJson),
        o.AccesoriosPersonalizados,
        ParseJsonList(o.CondicionChecklistJson),
        o.FallaReportada,
        o.ObservacionesRecepcion,
        o.Diagnostico,
        o.ActividadesRealizadas,
        o.ObservacionesTecnicas,
        o.CostoManoObra,
        o.SubtotalRefacciones,
        o.Total,
        o.RefaccionesDescontadas,
        o.FirmaTecnico,
        o.FirmaSupervisor,
        o.FirmaCliente,
        o.NombreClienteFirma,
        o.HistorialEstatus.OrderByDescending(h => h.Fecha).Select(h => new OrdenServicioEstatusHistorialDto(
            h.HistorialEstatusId, h.EstatusAnterior, h.EstatusNuevo, h.Fecha, h.UsuarioNombre, h.Observaciones)).ToList(),
        o.Refacciones.Select(r => new OrdenServicioRefaccionDto(
            r.OrdenServicioRefaccionId, r.ProductoId, r.SKU ?? r.Producto?.SKU ?? "", r.NombreProducto, r.Cantidad, r.PrecioUnitario, r.Importe, r.Descontado)).ToList(),
        o.Fotografias.OrderByDescending(f => f.Fecha).Select(f => new OrdenServicioFotoDto(
            f.OrdenServicioFotoId, f.OrdenServicioId, f.Categoria, f.Descripcion, f.ImagenUrl, f.UsuarioNombre, f.Fecha)).ToList());

    public static ProductoDto ToDto(this Producto p)
    {
        var estado = p.StockActual <= 0 ? "SinStock" : p.StockActual <= p.StockMinimo ? "Bajo" : "Normal";
        return new(
            p.ProductoId, p.SKU, p.CodigoBarras, p.Nombre, p.Descripcion,
            p.GrupoCategoria, p.Categoria, p.Marca, p.ModeloCompatible, p.Unidad,
            p.CostoUnitario, p.PrecioVenta, p.StockActual, p.StockMinimo, p.StockMaximo,
            p.Almacen, p.Pasillo, p.Rack, p.Anaquel, p.Nivel, p.Posicion, p.CodigoUbicacion,
            p.Activo, estado);
    }

    public static MovimientoInventarioDto ToDto(this MovimientoInventario m) => new(
        m.MovimientoId,
        m.ProductoId,
        m.Producto?.SKU ?? "",
        m.Producto?.Nombre ?? "",
        m.Producto?.GrupoCategoria ?? "",
        m.Producto?.Categoria ?? "",
        m.Producto?.CodigoUbicacion ?? "",
        m.TipoMovimiento,
        m.Subtipo,
        m.Concepto,
        m.ReferenciaFolio,
        m.OrdenServicioId,
        m.VentaId,
        m.CompraId,
        m.Cantidad,
        m.StockAnterior,
        m.StockPosterior,
        m.CostoUnitario,
        m.UbicacionOrigen,
        m.UbicacionDestino,
        m.Observaciones,
        m.UsuarioNombre,
        m.Fecha);

    public static VentaDocumentoDto ToDto(this VentaDocumento v)
    {
        var requiereAlerta = (v.Estatus is "Entregada" or "Facturada") && !v.SalidaInventarioRegistrada && v.Partidas.Any(p => p.ProductoId.HasValue);
        return new(
            v.VentaId, v.Folio, v.TipoDocumento, v.Estatus, v.ClienteId,
            v.Cliente?.RazonSocial ?? "", v.Cliente?.RFC ?? "",
            v.OrdenServicioId, v.DocumentoOrigenId, v.Fecha, v.FechaVigencia,
            v.Subtotal, v.Descuento, v.IVA, v.Total,
            v.FolioFacturaCFDI, v.UsoCFDI, v.MetodoPago, v.FormaPago,
            v.DescuentoAutomaticoInventario, v.SalidaInventarioRegistrada, requiereAlerta,
            v.Observaciones, v.VendedorNombre,
            v.Partidas.Select(p => new VentaPartidaDto(
                p.VentaPartidaId, p.ProductoId, p.Producto?.SKU, p.EquipoId, p.Descripcion, p.NumeroSerie,
                p.Cantidad, p.PrecioUnitario, p.Descuento, p.Importe)).ToList());
    }

    public static CompraDto ToDto(this Compra c) => new(
        c.CompraId, c.Folio, c.Proveedor, c.RFCProveedor, c.FacturaProveedor,
        c.FechaCompra, c.FechaRecepcion, c.Estatus, c.Subtotal, c.IVA, c.Total,
        c.EntradaInventarioRegistrada, c.Observaciones, c.UsuarioNombre,
        c.Partidas.Select(p => new CompraPartidaDto(
            p.CompraPartidaId, p.ProductoId, p.Producto?.SKU ?? "", p.Descripcion, p.Cantidad, p.CostoUnitario, p.Importe)).ToList());

    public static MantenimientoDto ToDto(this Mantenimiento m) => new(
        m.MantenimientoId, m.Folio, m.EquipoId,
        m.Equipo is null ? "" : $"{TipoNombre(m.Equipo.TipoEquipo)} {m.Equipo.Marca} {m.Equipo.Modelo}",
        m.Equipo?.SeriePrincipal ?? "",
        m.Equipo?.Cliente?.RazonSocial ?? "",
        m.OrdenServicioId, m.OrdenServicio?.Folio,
        m.Tipo, m.Estatus, m.FechaProgramada, m.FechaRealizada, m.ProximoMantenimiento,
        m.TecnicoId, m.Tecnico?.Nombre, m.Descripcion, m.Actividades, m.Hallazgos, m.Recomendaciones, m.Costo);

    public static GarantiaDto ToDto(this Garantia g)
    {
        var hoy = DateTime.UtcNow.Date;
        var dias = (g.FechaFin.Date - hoy).Days;
        var estatus = g.Estatus == "Vigente" && dias < 0 ? "Vencida" : g.Estatus;
        return new(
            g.GarantiaId, g.Folio, g.EquipoId,
            g.Equipo is null ? "" : $"{TipoNombre(g.Equipo.TipoEquipo)} {g.Equipo.Marca} {g.Equipo.Modelo}",
            g.Equipo?.SeriePrincipal ?? "",
            g.ClienteId, g.Cliente?.RazonSocial ?? "",
            g.OrdenServicioId, g.VentaId, g.TipoGarantia,
            g.FechaInicio, g.FechaFin, dias, g.Cobertura, g.Condiciones, estatus, g.MotivoReclamacion, g.Resolucion);
    }
}
