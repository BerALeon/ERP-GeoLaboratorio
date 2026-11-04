using Geolaboratorio.Application;
using Geolaboratorio.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Geolaboratorio.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Inicia sesión y devuelve un token JWT.</summary>
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest r, CancellationToken ct) => await auth.LoginAsync(r, ct);
}

[ApiController, Route("api/usuarios"), Authorize(Policy = Policies.AdminOTecnico)]
public class UsuariosController(IUsuarioService svc) : ControllerBase
{
    [HttpGet] public async Task<IReadOnlyList<UsuarioDto>> List(CancellationToken ct) => await svc.ListAsync(ct);
    [HttpPost, Authorize(Policy = Policies.Admin)] public async Task<UsuarioDto> Create(UsuarioCreateDto d, CancellationToken ct) => await svc.CreateAsync(d, ct);
    [HttpPut("{id:int}"), Authorize(Policy = Policies.Admin)] public async Task<UsuarioDto> Update(int id, UsuarioUpdateDto d, CancellationToken ct) => await svc.UpdateAsync(id, d, ct);
}

[ApiController, Route("api/clientes"), Authorize(Policy = Policies.AdminOTecnico)]
public class ClientesController(IClienteService svc) : ControllerBase
{
    [HttpGet] public async Task<PagedResult<ClienteDto>> List(string? q, int page = 1, int size = 20, CancellationToken ct = default) => await svc.ListAsync(q, page, size, ct);
    [HttpGet("{id:int}")] public async Task<ClienteDto> Get(int id, CancellationToken ct) => await svc.GetAsync(id, ct);
    [HttpPost] public async Task<ClienteDto> Create(ClienteSaveDto d, CancellationToken ct) => await svc.CreateAsync(d, ct);
    [HttpPut("{id:int}")] public async Task<ClienteDto> Update(int id, ClienteSaveDto d, CancellationToken ct) => await svc.UpdateAsync(id, d, ct);
}

[ApiController, Route("api/equipos"), Authorize]
public class EquiposController(IEquipoService svc) : ControllerBase
{
    [HttpGet] public async Task<PagedResult<EquipoDto>> List(string? q, int? clienteId, int page = 1, int size = 20, CancellationToken ct = default) => await svc.ListAsync(q, clienteId, page, size, ct);
    [HttpGet("{id:int}")] public async Task<EquipoDto> Get(int id, CancellationToken ct) => await svc.GetAsync(id, ct);
    [HttpPost, Authorize(Policy = Policies.AdminOTecnico)] public async Task<EquipoDto> Create(EquipoSaveDto d, CancellationToken ct) => await svc.CreateAsync(d, ct);
    [HttpPut("{id:int}"), Authorize(Policy = Policies.AdminOTecnico)] public async Task<EquipoDto> Update(int id, EquipoSaveDto d, CancellationToken ct) => await svc.UpdateAsync(id, d, ct);

    /// <summary>Búsqueda rápida e inteligente por QR, Código de Barras, No. Serie (principal o secundaria/adicional) o No. Interno.</summary>
    [HttpGet("buscar")]
    public async Task<EquipoBusquedaResultDto> Buscar(string codigo, CancellationToken ct) => await svc.BuscarIdentificacionAsync(codigo, ct);

    /// <summary>Vista 360° / Expediente Digital completo del equipo.</summary>
    [HttpGet("{id:int}/expediente")]
    public async Task<ExpedienteEquipoDto> Expediente(int id, CancellationToken ct) => await svc.GetExpedienteAsync(id, ct);

    /// <summary>Registra la baja definitiva del equipo conservando su historial técnico.</summary>
    [HttpPost("{id:int}/baja"), Authorize(Policy = Policies.Admin)]
    public async Task<EquipoDto> DarDeBaja(int id, EquipoBajaDto d, CancellationToken ct) => await svc.DarDeBajaAsync(id, d, ct);

    /// <summary>Agrega un archivo o fotografía directamente al expediente del equipo.</summary>
    [HttpPost("{id:int}/archivos"), Authorize(Policy = Policies.AdminOTecnico)]
    public async Task<EquipoArchivoDto> AgregarArchivo(int id, EquipoArchivoCreateDto d, CancellationToken ct) => await svc.AgregarArchivoAsync(id, d, ct);

    /// <summary>Catálogo de tipos de equipo.</summary>
    [HttpGet("tipos")]
    public IEnumerable<object> Tipos() => Enum.GetValues<TipoEquipo>().Select(t => new { id = (int)t, nombre = Infrastructure.Services.Mapper.TipoNombre(t), value = t.ToString() });

    /// <summary>Catálogo de tipos de serie configurables para equipos topográficos.</summary>
    [HttpGet("tipos-serie")]
    public IEnumerable<string> TiposSerie() =>
    [
        "Serie Principal",
        "Serie Radio Interna",
        "Serie Radio Externa",
        "Serie Controladora",
        "Serie Antena",
        "IMEI 1",
        "IMEI 2",
        "Serie Batería",
        "Serie Prisma / Base",
        "Otro"
    ];
}

[ApiController, Route("api/certificados"), Authorize]
public class CertificadosController(ICertificadoService svc) : ControllerBase
{
    [HttpGet] public async Task<PagedResult<CertificadoDto>> List(string? q, string? estado, int? equipoId, int page = 1, int size = 20, CancellationToken ct = default) => await svc.ListAsync(q, estado, equipoId, page, size, ct);
    [HttpGet("{id:int}")] public async Task<CertificadoDto> Get(int id, CancellationToken ct) => await svc.GetAsync(id, ct);

    [HttpPost, Authorize(Policy = Policies.AdminOTecnico)]
    public async Task<CertificadoDto> Create(CertificadoCreateDto d, CancellationToken ct) => await svc.CreateAsync(d, ct);

    [HttpPut("{id:int}"), Authorize(Policy = Policies.Admin)]
    public async Task<CertificadoDto> Update(int id, CertificadoUpdateDto d, CancellationToken ct) => await svc.UpdateAsync(id, d, ct);

    [HttpPost("{id:int}/cancelar"), Authorize(Policy = Policies.Admin)]
    public async Task<CertificadoDto> Cancel(int id, CancelarDto d, CancellationToken ct) => await svc.CancelAsync(id, d.Motivo, ct);

    /// <summary>Descarga el PDF. Con regenerar=true (solo Administrador) se genera nuevamente.</summary>
    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, bool regenerar = false, CancellationToken ct = default)
    {
        if (regenerar && !User.IsInRole("Administrador")) return Forbid();
        var f = await svc.GetPdfAsync(id, regenerar, ct);
        return File(f.Content, "application/pdf", f.FileName);
    }

    [HttpGet("{id:int}/historial"), Authorize(Policy = Policies.AdminOTecnico)]
    public async Task<IReadOnlyList<HistorialDto>> Historial(int id, CancellationToken ct) => await svc.HistorialAsync(id, ct);
}

[ApiController, Route("api/ordenes-servicio"), Authorize]
public class OrdenesServicioController(IOrdenServicioService svc) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<OrdenServicioDto>> List(
        string? q, string? estatus, string? tipoServicio, int? equipoId, int? clienteId, int page = 1, int size = 20, CancellationToken ct = default)
        => await svc.ListAsync(q, estatus, tipoServicio, equipoId, clienteId, page, size, ct);

    [HttpGet("{id:int}")]
    public async Task<OrdenServicioDto> Get(int id, CancellationToken ct) => await svc.GetAsync(id, ct);

    [HttpPost, Authorize(Policy = Policies.AdminOTecnico)]
    public async Task<OrdenServicioDto> Create(OrdenServicioCreateDto d, CancellationToken ct) => await svc.CreateAsync(d, ct);

    [HttpPut("{id:int}/tecnico"), Authorize(Policy = Policies.AdminOTecnico)]
    public async Task<OrdenServicioDto> UpdateTecnico(int id, OrdenServicioUpdateTecnicoDto d, CancellationToken ct) => await svc.UpdateTecnicoAsync(id, d, ct);

    [HttpPost("{id:int}/estatus"), Authorize(Policy = Policies.AdminOTecnico)]
    public async Task<OrdenServicioDto> CambiarEstatus(int id, OrdenServicioCambiarEstatusDto d, CancellationToken ct) => await svc.CambiarEstatusAsync(id, d, ct);

    [HttpPost("{id:int}/fotos"), Authorize(Policy = Policies.AdminOTecnico)]
    public async Task<OrdenServicioFotoDto> AgregarFoto(int id, OrdenServicioFotoCreateDto d, CancellationToken ct) => await svc.AgregarFotoAsync(id, d, ct);

    [HttpDelete("{id:int}/fotos/{fotoId:int}"), Authorize(Policy = Policies.AdminOTecnico)]
    public async Task<IActionResult> EliminarFoto(int id, int fotoId, CancellationToken ct)
    {
        await svc.EliminarFotoAsync(id, fotoId, ct);
        return NoContent();
    }

    /// <summary>Descarga el Reporte Técnico en PDF de la Orden de Servicio.</summary>
    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        var f = await svc.GetReportePdfAsync(id, ct);
        return File(f.Content, "application/pdf", f.FileName);
    }

    [HttpGet("catalogos")]
    public object Catalogos() => new
    {
        accesorios = new[]
        {
            "Batería", "Batería adicional", "Cargador", "Estuche", "Maleta rígida",
            "Cable USB", "Cable corriente", "Cable red", "Prisma", "Trípode",
            "Bastón", "Adaptador", "Tarjeta SD", "Radio", "Controladora", "Antena GNSS"
        },
        condiciones = new[]
        {
            "Sin daños visibles", "Golpes", "Rayones", "Equipo mojado",
            "Pantalla dañada", "Teclado dañado", "Equipo incompleto", "Con piezas faltantes"
        },
        tiposServicio = new[]
        {
            "Diagnóstico", "Revisión", "Mantenimiento Preventivo", "Mantenimiento Correctivo",
            "Calibración", "Certificación", "Reparación", "Garantía", "Actualización Firmware", "Cotización"
        },
        estatus = new[]
        {
            "Recibido", "En Diagnóstico", "Pendiente Cotización", "Esperando Autorización",
            "Autorizado", "En Reparación", "En Mantenimiento", "En Calibración",
            "En Certificación", "Control de Calidad", "Terminado", "Listo para Entrega",
            "Entregado", "Cancelado"
        },
        categoriasFoto = new[]
        {
            "Equipo completo", "Número de serie", "Golpes", "Daños", "Accesorios recibidos"
        }
    };
}

[ApiController, Route("api/inventario"), Authorize(Policy = Policies.AdminOTecnico)]
public class InventarioController(IInventarioService svc) : ControllerBase
{
    [HttpGet("productos")]
    public async Task<PagedResult<ProductoDto>> ListProductos(
        string? q, string? grupo, string? categoria, bool? bajoStock, int page = 1, int size = 20, CancellationToken ct = default)
        => await svc.ListProductosAsync(q, grupo, categoria, bajoStock, page, size, ct);

    [HttpGet("productos/{id:int}")]
    public async Task<ProductoDto> GetProducto(int id, CancellationToken ct) => await svc.GetProductoAsync(id, ct);

    [HttpPost("productos")]
    public async Task<ProductoDto> CreateProducto(ProductoSaveDto d, CancellationToken ct) => await svc.CreateProductoAsync(d, ct);

    [HttpPut("productos/{id:int}")]
    public async Task<ProductoDto> UpdateProducto(int id, ProductoSaveDto d, CancellationToken ct) => await svc.UpdateProductoAsync(id, d, ct);

    [HttpPost("movimientos")]
    public async Task<MovimientoInventarioDto> RegistrarMovimiento(MovimientoCreateDto d, CancellationToken ct) => await svc.RegistrarMovimientoAsync(d, ct);

    [HttpGet("kardex")]
    public async Task<PagedResult<MovimientoInventarioDto>> ListKardex(
        int? productoId, string? tipo, string? subtipo, string? q, int page = 1, int size = 30, CancellationToken ct = default)
        => await svc.ListKardexAsync(productoId, tipo, subtipo, q, page, size, ct);

    [HttpGet("catalogos")]
    public object Catalogos() => new
    {
        grupos = new Dictionary<string, string[]>
        {
            ["Equipo Nuevo"] = ["Estaciones Totales", "GNSS", "GPS", "Drones", "Niveles", "Escáneres"],
            ["Refacciones"] = ["Pantallas", "Tarjetas", "Antenas", "Teclados", "Radios", "Baterías"],
            ["Accesorios"] = ["Trípodes", "Prismas", "Bastones", "Adaptadores", "Cables"],
            ["Consumibles"] = ["Pintura", "Estacas", "Etiquetas"],
            ["Herramientas"] = ["Herramientas"]
        },
        entradas = new[] { "Compra", "Ajuste", "Devolución", "Transferencia" },
        salidas = new[] { "Venta", "Reparación", "Garantía", "Consumo interno", "Transferencia" }
    };
}

[ApiController, Route("api/ventas"), Authorize(Policy = Policies.AdminOTecnico)]
public class VentasController(IVentaService svc) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<VentaDocumentoDto>> List(
        string? q, string? tipoDocumento, string? estatus, int? clienteId, bool? sinSalidaInventario, int page = 1, int size = 20, CancellationToken ct = default)
        => await svc.ListAsync(q, tipoDocumento, estatus, clienteId, sinSalidaInventario, page, size, ct);

    [HttpGet("{id:int}")]
    public async Task<VentaDocumentoDto> Get(int id, CancellationToken ct) => await svc.GetAsync(id, ct);

    [HttpPost]
    public async Task<VentaDocumentoDto> Create(VentaCreateDto d, CancellationToken ct) => await svc.CreateAsync(d, ct);

    [HttpPost("{id:int}/estatus")]
    public async Task<VentaDocumentoDto> CambiarEstatus(int id, VentaCambiarEstatusDto d, CancellationToken ct) => await svc.CambiarEstatusAsync(id, d, ct);

    [HttpPost("{id:int}/salida-inventario")]
    public async Task<VentaDocumentoDto> RegistrarSalidaInventario(int id, CancellationToken ct) => await svc.RegistrarSalidaInventarioAsync(id, ct);
}

[ApiController, Route("api/compras"), Authorize(Policy = Policies.AdminOTecnico)]
public class ComprasController(ICompraService svc) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<CompraDto>> List(string? q, string? estatus, int page = 1, int size = 20, CancellationToken ct = default)
        => await svc.ListAsync(q, estatus, page, size, ct);

    [HttpPost]
    public async Task<CompraDto> Create(CompraCreateDto d, CancellationToken ct) => await svc.CreateAsync(d, ct);

    [HttpPost("{id:int}/recibir")]
    public async Task<CompraDto> Recibir(int id, CancellationToken ct) => await svc.RecibirCompraAsync(id, ct);
}

[ApiController, Route("api/mantenimientos"), Authorize(Policy = Policies.AdminOTecnico)]
public class MantenimientosController(IMantenimientoService svc) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<MantenimientoDto>> List(
        string? q, string? tipo, string? estatus, int? equipoId, int page = 1, int size = 20, CancellationToken ct = default)
        => await svc.ListAsync(q, tipo, estatus, equipoId, page, size, ct);

    [HttpPost]
    public async Task<MantenimientoDto> Create(MantenimientoSaveDto d, CancellationToken ct) => await svc.CreateAsync(d, ct);

    [HttpPut("{id:int}")]
    public async Task<MantenimientoDto> Update(int id, MantenimientoSaveDto d, CancellationToken ct) => await svc.UpdateAsync(id, d, ct);
}

[ApiController, Route("api/garantias"), Authorize(Policy = Policies.AdminOTecnico)]
public class GarantiasController(IGarantiaService svc) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<GarantiaDto>> List(string? q, string? estatus, int? equipoId, int page = 1, int size = 20, CancellationToken ct = default)
        => await svc.ListAsync(q, estatus, equipoId, page, size, ct);

    [HttpPost]
    public async Task<GarantiaDto> Create(GarantiaSaveDto d, CancellationToken ct) => await svc.CreateAsync(d, ct);

    [HttpPut("{id:int}")]
    public async Task<GarantiaDto> Update(int id, GarantiaSaveDto d, CancellationToken ct) => await svc.UpdateAsync(id, d, ct);
}

[ApiController, Route("api/dashboard"), Authorize]
public class DashboardController(IDashboardService svc) : ControllerBase
{
    [HttpGet] public async Task<DashboardDto> Get(CancellationToken ct) => await svc.GetAsync(ct);
}

[ApiController, Route("api/auditoria"), Authorize(Policy = Policies.Admin)]
public class AuditoriaController(IAuditService svc) : ControllerBase
{
    [HttpGet] public async Task<PagedResult<AuditLogDto>> List(string? entidad, int page = 1, int size = 50, CancellationToken ct = default) => await svc.ListAsync(entidad, page, size, ct);
}

/// <summary>Portal público: sin autenticación, solo datos mínimos de verificación.</summary>
[ApiController, Route("api/public"), AllowAnonymous, EnableRateLimiting("public")]
public class PublicController(ICertificadoService svc) : ControllerBase
{
    [HttpGet("verificar/{folio}")]
    public async Task<VerificacionDto> Verificar(string folio, CancellationToken ct) => await svc.VerificarAsync(folio, ct);
}
