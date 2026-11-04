using System.Text.Json.Serialization;
using FluentValidation;
using Geolaboratorio.Domain;

namespace Geolaboratorio.Application;

// ───────────── DTOs: Auth, Usuarios, Clientes ─────────────
public record LoginRequest(string Correo, string Password);
public record LoginResponse(string Token, DateTime Expira, UsuarioDto Usuario);

public record UsuarioDto(int UsuarioId, string Nombre, string Correo, string Rol, bool Activo);
public record UsuarioCreateDto(string Nombre, string Correo, string Password, RolUsuario Rol);
public record UsuarioUpdateDto(string Nombre, RolUsuario Rol, bool Activo, string? Password);

public record ClienteDto(int ClienteId, string RazonSocial, string? NombreComercial, string RFC, string? Direccion,
    string? Ciudad, string? Estado, string? Pais, string? Telefono, string? Correo, string? Contacto, int TotalEquipos);
public record ClienteSaveDto(string RazonSocial, string? NombreComercial, string RFC, string? Direccion,
    string? Ciudad, string? Estado, string? Pais, string? Telefono, string? Correo, string? Contacto);

// ───────────── DTOs: Equipos, Series y Expediente 360° ─────────────
public record EquipoSerieDto(int? EquipoSerieId, string TipoSerie, string Valor, string? Observaciones = null);

public record EquipoDto(
    int EquipoId,
    int ClienteId,
    string Cliente,
    string TipoEquipo,
    int TipoEquipoId,
    string Marca,
    string Modelo,
    string SeriePrincipal,
    string? SerieSecundaria,
    [property: JsonPropertyName("imei1")] string? IMEI1,
    [property: JsonPropertyName("imei2")] string? IMEI2,
    string? NumeroInterno,
    DateTime? FechaCompra,
    string? Observaciones,
    string? CodigoQR = null,
    string? CodigoBarras = null,
    string Estatus = "Activo",
    DateTime? FechaBaja = null,
    string? MotivoBaja = null,
    IReadOnlyList<EquipoSerieDto>? Series = null,
    int TotalOrdenesServicio = 0,
    int TotalCertificados = 0);

public record EquipoSaveDto(
    int ClienteId,
    TipoEquipo TipoEquipo,
    string Marca,
    string Modelo,
    string SeriePrincipal,
    string? SerieSecundaria,
    [property: JsonPropertyName("imei1")] string? IMEI1,
    [property: JsonPropertyName("imei2")] string? IMEI2,
    string? NumeroInterno,
    DateTime? FechaCompra,
    string? Observaciones,
    string? CodigoQR = null,
    string? CodigoBarras = null,
    EstatusEquipo? Estatus = null,
    IReadOnlyList<EquipoSerieDto>? Series = null);

public record EquipoBajaDto(string MotivoBaja, DateTime? FechaBaja = null);

public record EquipoHistorialDto(
    long EquipoHistorialId,
    int EquipoId,
    string TipoEvento,
    string Titulo,
    string? Detalle,
    string? FolioReferencia,
    int? ReferenciaId,
    string? UsuarioNombre,
    DateTime Fecha);

public record EquipoArchivoDto(
    int EquipoArchivoId,
    int EquipoId,
    int? OrdenServicioId,
    string TipoArchivo,
    string Categoria,
    string NombreArchivo,
    string ArchivoUrl,
    string? Descripcion,
    string? SubidoPor,
    DateTime Fecha);

public record EquipoArchivoCreateDto(
    int? OrdenServicioId,
    string TipoArchivo,
    string Categoria,
    string NombreArchivo,
    string ArchivoUrl,
    string? Descripcion);

public record RefaccionEquipoResumenDto(
    int OrdenServicioRefaccionId,
    int OrdenServicioId,
    string FolioOrden,
    int ProductoId,
    string SKU,
    string NombreProducto,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Importe,
    DateTime Fecha);

public record EquipoBusquedaResultDto(
    bool Encontrado,
    string TerminoBusqueda,
    EquipoDto? Equipo,
    ClienteDto? Cliente,
    IReadOnlyList<EquipoSerieDto> Series,
    IReadOnlyList<OrdenServicioResumenDto> HistorialOrdenes,
    IReadOnlyList<CertificadoDto> CertificadosRecientes,
    IReadOnlyList<GarantiaDto> GarantiasVigentes);

public record OrdenServicioResumenDto(
    int OrdenServicioId,
    string Folio,
    string TipoServicio,
    string Estatus,
    DateTime FechaRecepcion,
    string? Diagnostico);

public record ExpedienteEquipoDto(
    EquipoDto Equipo,
    ClienteDto Cliente,
    IReadOnlyList<EquipoSerieDto> Series,
    IReadOnlyList<CertificadoDto> Certificados,
    IReadOnlyList<CertificadoDto> Calibraciones,
    IReadOnlyList<CertificadoDto> Certificaciones,
    IReadOnlyList<MantenimientoDto> Mantenimientos,
    IReadOnlyList<MantenimientoDto> Reparaciones,
    IReadOnlyList<RefaccionEquipoResumenDto> RefaccionesUtilizadas,
    IReadOnlyList<OrdenServicioDto> OrdenesServicio,
    IReadOnlyList<GarantiaDto> Garantias,
    IReadOnlyList<VentaDocumentoDto> Ventas,
    IReadOnlyList<EquipoArchivoDto> Archivos,
    IReadOnlyList<OrdenServicioFotoDto> Fotografias,
    IReadOnlyList<EquipoHistorialDto> HistorialCompleto);

// ───────────── DTOs: Certificados y Calibraciones ─────────────
public record CertificadoDto(
    int CertificadoId,
    string Folio,
    int EquipoId,
    string Cliente,
    string TipoEquipo,
    string Equipo,
    string SeriePrincipal,
    string Tecnico,
    DateTime FechaCalibracion,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    int VigenciaMeses,
    string Resultado,
    string? Resultados,
    string? Observaciones,
    string? Conclusiones,
    string Estatus,
    string EstadoVigencia,
    int DiasParaVencer,
    string? MotivoCancelacion,
    int Version,
    int? OrdenServicioId = null,
    string? FolioOrdenServicio = null,
    string TipoRegistro = "Calibración");

public record CertificadoCreateDto(
    int EquipoId,
    DateTime FechaCalibracion,
    DateTime? FechaEmision,
    int? VigenciaMeses,
    ResultadoCalibracion Resultado,
    string? Resultados,
    string? Observaciones,
    string? Conclusiones,
    int? OrdenServicioId = null,
    string? TipoRegistro = "Calibración");

public record CertificadoUpdateDto(
    DateTime FechaCalibracion,
    int VigenciaMeses,
    ResultadoCalibracion Resultado,
    string? Resultados,
    string? Observaciones,
    string? Conclusiones,
    string MotivoCambio,
    string? TipoRegistro = null);

public record CancelarDto(string Motivo);
public record HistorialDto(long HistorialId, string Accion, string? Detalle, string? PdfHash, string? Usuario, DateTime Fecha);

public record VerificacionDto(string Estado, string Mensaje, string? Folio, string? Cliente, string? Equipo,
    string? SerieEquipo, DateTime? FechaCalibracion, DateTime? FechaVencimiento, int? DiasVencido, string? Estatus);

// ───────────── DTOs: Órdenes de Servicio ─────────────
public record OrdenServicioEstatusHistorialDto(
    long HistorialEstatusId,
    string? EstatusAnterior,
    string EstatusNuevo,
    DateTime Fecha,
    string? UsuarioNombre,
    string? Observaciones);

public record OrdenServicioRefaccionDto(
    int OrdenServicioRefaccionId,
    int ProductoId,
    string SKU,
    string NombreProducto,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Importe,
    bool Descontado);

public record OrdenServicioRefaccionInputDto(
    int ProductoId,
    decimal Cantidad,
    decimal? PrecioUnitario = null);

public record OrdenServicioFotoDto(
    int OrdenServicioFotoId,
    int OrdenServicioId,
    string Categoria,
    string? Descripcion,
    string ImagenUrl,
    string? UsuarioNombre,
    DateTime Fecha);

public record OrdenServicioFotoCreateDto(
    string Categoria,
    string? Descripcion,
    string ImagenUrl);

public record OrdenServicioDto(
    int OrdenServicioId,
    string Folio,
    int ClienteId,
    string Cliente,
    string ClienteRFC,
    string? ClienteTelefono,
    int EquipoId,
    string TipoEquipo,
    string Marca,
    string Modelo,
    string SeriePrincipal,
    string? NumeroInterno,
    IReadOnlyList<EquipoSerieDto> SeriesEquipo,
    string TipoServicio,
    IReadOnlyList<string> TiposServicioAdicionales,
    string Estatus,
    int? TecnicoId,
    string? TecnicoNombre,
    int? SupervisorId,
    string? SupervisorNombre,
    DateTime FechaRecepcion,
    DateTime? FechaCompromiso,
    DateTime? FechaTermino,
    DateTime? FechaEntrega,
    string MetodoIdentificacion,
    IReadOnlyList<string> AccesoriosRecibidos,
    string? AccesoriosPersonalizados,
    IReadOnlyList<string> CondicionEquipo,
    string? FallaReportada,
    string? ObservacionesRecepcion,
    string? Diagnostico,
    string? ActividadesRealizadas,
    string? ObservacionesTecnicas,
    decimal CostoManoObra,
    decimal SubtotalRefacciones,
    decimal Total,
    bool RefaccionesDescontadas,
    string? FirmaTecnico,
    string? FirmaSupervisor,
    string? FirmaCliente,
    string? NombreClienteFirma,
    IReadOnlyList<OrdenServicioEstatusHistorialDto> HistorialEstatus,
    IReadOnlyList<OrdenServicioRefaccionDto> Refacciones,
    IReadOnlyList<OrdenServicioFotoDto> Fotografias);

public record OrdenServicioCreateDto(
    int EquipoId,
    string TipoServicio,
    IReadOnlyList<string>? TiposServicioAdicionales,
    int? TecnicoId,
    int? SupervisorId,
    DateTime? FechaCompromiso,
    string? MetodoIdentificacion,
    IReadOnlyList<string>? AccesoriosRecibidos,
    string? AccesoriosPersonalizados,
    IReadOnlyList<string>? CondicionEquipo,
    string? FallaReportada,
    string? ObservacionesRecepcion,
    IReadOnlyList<OrdenServicioFotoCreateDto>? FotografiasIniciales = null);

public record OrdenServicioUpdateTecnicoDto(
    string? TipoServicio,
    int? TecnicoId,
    int? SupervisorId,
    DateTime? FechaCompromiso,
    string? Diagnostico,
    string? ActividadesRealizadas,
    string? ObservacionesTecnicas,
    decimal CostoManoObra,
    string? FirmaTecnico,
    string? FirmaSupervisor,
    string? FirmaCliente,
    string? NombreClienteFirma,
    IReadOnlyList<OrdenServicioRefaccionInputDto>? Refacciones);

public record OrdenServicioCambiarEstatusDto(
    string NuevoEstatus,
    string? Observaciones,
    string? FirmaTecnico = null,
    string? FirmaSupervisor = null,
    string? FirmaCliente = null,
    string? NombreClienteFirma = null);

// ───────────── DTOs: Mantenimientos, Reparaciones, Revisiones y Garantías ─────────────
public record MantenimientoDto(
    int MantenimientoId,
    string Folio,
    int EquipoId,
    string Equipo,
    string SeriePrincipal,
    string Cliente,
    int? OrdenServicioId,
    string? FolioOrdenServicio,
    string Tipo,
    string Estatus,
    DateTime FechaProgramada,
    DateTime? FechaRealizada,
    DateTime? ProximoMantenimiento,
    int? TecnicoId,
    string? TecnicoNombre,
    string? Descripcion,
    string? Actividades,
    string? Hallazgos,
    string? Recomendaciones,
    decimal Costo);

public record MantenimientoSaveDto(
    int EquipoId,
    int? OrdenServicioId,
    string Tipo,
    string Estatus,
    DateTime FechaProgramada,
    DateTime? FechaRealizada,
    DateTime? ProximoMantenimiento,
    int? TecnicoId,
    string? Descripcion,
    string? Actividades,
    string? Hallazgos,
    string? Recomendaciones,
    decimal Costo);

public record GarantiaDto(
    int GarantiaId,
    string Folio,
    int EquipoId,
    string Equipo,
    string SeriePrincipal,
    int ClienteId,
    string Cliente,
    int? OrdenServicioId,
    int? VentaId,
    string TipoGarantia,
    DateTime FechaInicio,
    DateTime FechaFin,
    int DiasRestantes,
    string Cobertura,
    string? Condiciones,
    string Estatus,
    string? MotivoReclamacion,
    string? Resolucion);

public record GarantiaSaveDto(
    int EquipoId,
    int ClienteId,
    int? OrdenServicioId,
    int? VentaId,
    string TipoGarantia,
    DateTime FechaInicio,
    DateTime FechaFin,
    string Cobertura,
    string? Condiciones,
    string Estatus,
    string? MotivoReclamacion,
    string? Resolucion);

// ───────────── DTOs: Inventario y Kardex ─────────────
public record ProductoDto(
    int ProductoId,
    string SKU,
    string? CodigoBarras,
    string Nombre,
    string? Descripcion,
    string GrupoCategoria,
    string Categoria,
    string? Marca,
    string? ModeloCompatible,
    string Unidad,
    decimal CostoUnitario,
    decimal PrecioVenta,
    decimal StockActual,
    decimal StockMinimo,
    decimal StockMaximo,
    string Almacen,
    string Pasillo,
    string Rack,
    string Anaquel,
    string Nivel,
    string Posicion,
    string CodigoUbicacion,
    bool Activo,
    string EstadoStock); // Normal | Bajo | SinStock

public record ProductoSaveDto(
    string SKU,
    string? CodigoBarras,
    string Nombre,
    string? Descripcion,
    string GrupoCategoria,
    string Categoria,
    string? Marca,
    string? ModeloCompatible,
    string Unidad,
    decimal CostoUnitario,
    decimal PrecioVenta,
    decimal StockActual,
    decimal StockMinimo,
    decimal StockMaximo,
    string Almacen,
    string Pasillo,
    string Rack,
    string Anaquel,
    string Nivel,
    string Posicion,
    bool Activo = true);

public record MovimientoInventarioDto(
    long MovimientoId,
    int ProductoId,
    string SKU,
    string NombreProducto,
    string GrupoCategoria,
    string Categoria,
    string CodigoUbicacion,
    string TipoMovimiento,
    string Subtipo,
    string Concepto,
    string? ReferenciaFolio,
    int? OrdenServicioId,
    int? VentaId,
    int? CompraId,
    decimal Cantidad,
    decimal StockAnterior,
    decimal StockPosterior,
    decimal CostoUnitario,
    string? UbicacionOrigen,
    string? UbicacionDestino,
    string? Observaciones,
    string? UsuarioNombre,
    DateTime Fecha);

public record MovimientoCreateDto(
    int ProductoId,
    string TipoMovimiento, // Entrada | Salida
    string Subtipo,        // Compra | Ajuste | Devolución | Transferencia | Venta | Reparación | Garantía | Consumo interno
    decimal Cantidad,
    decimal? CostoUnitario,
    string? Concepto,
    string? ReferenciaFolio,
    string? UbicacionOrigen,
    string? UbicacionDestino,
    string? Observaciones);

// ───────────── DTOs: Ventas, Cotizaciones, Remisiones, Facturación y Compras ─────────────
public record VentaPartidaDto(
    int VentaPartidaId,
    int? ProductoId,
    string? SKU,
    int? EquipoId,
    string Descripcion,
    string? NumeroSerie,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Descuento,
    decimal Importe);

public record VentaPartidaInputDto(
    int? ProductoId,
    int? EquipoId,
    string Descripcion,
    string? NumeroSerie,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Descuento = 0);

public record VentaDocumentoDto(
    int VentaId,
    string Folio,
    string TipoDocumento,
    string Estatus,
    int ClienteId,
    string Cliente,
    string ClienteRFC,
    int? OrdenServicioId,
    int? DocumentoOrigenId,
    DateTime Fecha,
    DateTime? FechaVigencia,
    decimal Subtotal,
    decimal Descuento,
    decimal IVA,
    decimal Total,
    string? FolioFacturaCFDI,
    string? UsoCFDI,
    string? MetodoPago,
    string? FormaPago,
    bool DescuentoAutomaticoInventario,
    bool SalidaInventarioRegistrada,
    bool RequiereAlertaSinSalida,
    string? Observaciones,
    string? VendedorNombre,
    IReadOnlyList<VentaPartidaDto> Partidas);

public record VentaCreateDto(
    string TipoDocumento,
    string Estatus,
    int ClienteId,
    int? OrdenServicioId,
    int? DocumentoOrigenId,
    DateTime? FechaVigencia,
    string? FolioFacturaCFDI,
    string? UsoCFDI,
    string? MetodoPago,
    string? FormaPago,
    bool DescuentoAutomaticoInventario,
    string? Observaciones,
    IReadOnlyList<VentaPartidaInputDto> Partidas);

public record VentaCambiarEstatusDto(
    string NuevoEstatus,
    string? NuevoTipoDocumento = null,
    string? FolioFacturaCFDI = null,
    string? UsoCFDI = null,
    string? MetodoPago = null,
    string? FormaPago = null,
    bool? DescontarInventarioAhora = null);

public record CompraPartidaDto(
    int CompraPartidaId,
    int ProductoId,
    string SKU,
    string Descripcion,
    decimal Cantidad,
    decimal CostoUnitario,
    decimal Importe);

public record CompraPartidaInputDto(
    int ProductoId,
    decimal Cantidad,
    decimal CostoUnitario);

public record CompraDto(
    int CompraId,
    string Folio,
    string Proveedor,
    string? RFCProveedor,
    string? FacturaProveedor,
    DateTime FechaCompra,
    DateTime? FechaRecepcion,
    string Estatus,
    decimal Subtotal,
    decimal IVA,
    decimal Total,
    bool EntradaInventarioRegistrada,
    string? Observaciones,
    string? UsuarioNombre,
    IReadOnlyList<CompraPartidaDto> Partidas);

public record CompraCreateDto(
    string Proveedor,
    string? RFCProveedor,
    string? FacturaProveedor,
    string Estatus,
    bool RegistrarEntradaInmediata,
    string? Observaciones,
    IReadOnlyList<CompraPartidaInputDto> Partidas);

// ───────────── DTOs: Paginación, Auditoría y Dashboard Ejecutivo ERP ─────────────
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public record AuditLogDto(long AuditLogId, string Entidad, string EntidadId, string Accion, string? ValoresAnteriores,
    string? ValoresNuevos, string? Usuario, string? Ip, DateTime Fecha);

public record KpiDto(int Activos, int Vencidos, int PorVencer, int Equipos, int Clientes, int EmitidosMes);
public record AlertaDto(int CertificadoId, string Folio, string Cliente, string Equipo, DateTime FechaVencimiento, int Dias);
public record SerieItem(string Etiqueta, int Valor);
public record SerieDecimalItem(string Etiqueta, decimal Valor, string? Extra = null);

public record IndicadoresGeneralesDto(
    int ClientesRegistrados,
    int EquiposRegistrados,
    int OrdenesServicioAbiertas,
    int EquiposEnTaller,
    int CertificadosVigentes,
    int CertificadosPorVencer,
    decimal VentasDelMes,
    int CantidadVentasDelMes,
    decimal ValorTotalInventario,
    int ProductosSinStock,
    int RefaccionesStockCritico,
    int VentasSinSalidaInventario);

public record IndicadoresTecnicosDto(
    int EquiposRecibidosHoy,
    int EquiposEnDiagnostico,
    int EquiposEnReparacion,
    int EquiposEnCalibracion,
    int EquiposTerminados,
    int EquiposEntregados);

public record IndicadoresInventarioDto(
    int ProductosRegistrados,
    int StockBajo,
    IReadOnlyList<SerieDecimalItem> RefaccionesMasUtilizadas,
    IReadOnlyList<SerieDecimalItem> ProductosMasVendidos,
    int EntradasDelMes,
    int SalidasDelMes,
    decimal UnidadesEntradasMes,
    decimal UnidadesSalidasMes);

public record DashboardDto(
    KpiDto Kpis,
    IReadOnlyList<AlertaDto> Vencen30,
    IReadOnlyList<AlertaDto> Vencen15,
    IReadOnlyList<AlertaDto> Vencen7,
    IReadOnlyList<SerieItem> PorMes,
    IReadOnlyList<SerieItem> PorTipo,
    IReadOnlyList<SerieItem> PorEstado,
    IndicadoresGeneralesDto? Generales = null,
    IndicadoresTecnicosDto? Tecnicos = null,
    IndicadoresInventarioDto? Inventario = null,
    IReadOnlyList<SerieItem>? OrdenesPorEstatus = null);

public record PdfFile(byte[] Content, string FileName);

// ───────────── Excepciones ─────────────
public class NotFoundException(string msg) : Exception(msg);
public class BusinessException(string msg) : Exception(msg);
public class ForbiddenException(string msg) : Exception(msg);

// ───────────── Puertos ─────────────
public interface ICurrentUser
{
    int? UserId { get; }
    string? Nombre { get; }
    string? Rol { get; }
    string? Ip { get; }
}

public interface IRepository<T> where T : class
{
    IQueryable<T> Query();
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork
{
    IRepository<Usuario> Usuarios { get; }
    IRepository<Cliente> Clientes { get; }
    IRepository<Equipo> Equipos { get; }
    IRepository<EquipoSerie> EquipoSeries { get; }
    IRepository<EquipoHistorial> EquipoHistoriales { get; }
    IRepository<EquipoArchivo> EquipoArchivos { get; }
    IRepository<Certificado> Certificados { get; }
    IRepository<OrdenServicio> OrdenesServicio { get; }
    IRepository<OrdenServicioEstatusHistorial> OrdenesServicioHistorial { get; }
    IRepository<OrdenServicioRefaccion> OrdenesServicioRefacciones { get; }
    IRepository<OrdenServicioFoto> OrdenesServicioFotos { get; }
    IRepository<Mantenimiento> Mantenimientos { get; }
    IRepository<Garantia> Garantias { get; }
    IRepository<Producto> Productos { get; }
    IRepository<MovimientoInventario> MovimientosInventario { get; }
    IRepository<VentaDocumento> Ventas { get; }
    IRepository<VentaPartida> VentaPartidas { get; }
    IRepository<Compra> Compras { get; }
    IRepository<CompraPartida> CompraPartidas { get; }
    IQueryable<AuditLog> AuditLogs { get; }
    IQueryable<CertificadoHistorial> Historial { get; }
    Task AddHistorialAsync(CertificadoHistorial h, CancellationToken ct = default);
    Task<int> NextConsecutivoAsync(int anio, CancellationToken ct = default);
    Task<string> NextFolioPrefijoAsync(string prefijo, int anio, CancellationToken ct = default);
    Task<int> VigenciaPorTipoAsync(TipoEquipo tipo, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public interface IAuthService { Task<LoginResponse> LoginAsync(LoginRequest r, CancellationToken ct = default); }

public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioDto>> ListAsync(CancellationToken ct = default);
    Task<UsuarioDto> CreateAsync(UsuarioCreateDto d, CancellationToken ct = default);
    Task<UsuarioDto> UpdateAsync(int id, UsuarioUpdateDto d, CancellationToken ct = default);
}

public interface IClienteService
{
    Task<PagedResult<ClienteDto>> ListAsync(string? q, int page, int size, CancellationToken ct = default);
    Task<ClienteDto> GetAsync(int id, CancellationToken ct = default);
    Task<ClienteDto> CreateAsync(ClienteSaveDto d, CancellationToken ct = default);
    Task<ClienteDto> UpdateAsync(int id, ClienteSaveDto d, CancellationToken ct = default);
}

public interface IEquipoService
{
    Task<PagedResult<EquipoDto>> ListAsync(string? q, int? clienteId, int page, int size, CancellationToken ct = default);
    Task<EquipoDto> GetAsync(int id, CancellationToken ct = default);
    Task<EquipoDto> CreateAsync(EquipoSaveDto d, CancellationToken ct = default);
    Task<EquipoDto> UpdateAsync(int id, EquipoSaveDto d, CancellationToken ct = default);
    Task<EquipoBusquedaResultDto> BuscarIdentificacionAsync(string codigo, CancellationToken ct = default);
    Task<ExpedienteEquipoDto> GetExpedienteAsync(int id, CancellationToken ct = default);
    Task<EquipoDto> DarDeBajaAsync(int id, EquipoBajaDto d, CancellationToken ct = default);
    Task<EquipoArchivoDto> AgregarArchivoAsync(int id, EquipoArchivoCreateDto d, CancellationToken ct = default);
}

public interface ICertificadoService
{
    Task<PagedResult<CertificadoDto>> ListAsync(string? q, string? estado, int? equipoId, int page, int size, CancellationToken ct = default);
    Task<CertificadoDto> GetAsync(int id, CancellationToken ct = default);
    Task<CertificadoDto> CreateAsync(CertificadoCreateDto d, CancellationToken ct = default);
    Task<CertificadoDto> UpdateAsync(int id, CertificadoUpdateDto d, CancellationToken ct = default);
    Task<CertificadoDto> CancelAsync(int id, string motivo, CancellationToken ct = default);
    Task<PdfFile> GetPdfAsync(int id, bool regenerate, CancellationToken ct = default);
    Task<IReadOnlyList<HistorialDto>> HistorialAsync(int id, CancellationToken ct = default);
    Task<VerificacionDto> VerificarAsync(string folio, CancellationToken ct = default);
}

public interface IOrdenServicioService
{
    Task<PagedResult<OrdenServicioDto>> ListAsync(string? q, string? estatus, string? tipoServicio, int? equipoId, int? clienteId, int page, int size, CancellationToken ct = default);
    Task<OrdenServicioDto> GetAsync(int id, CancellationToken ct = default);
    Task<OrdenServicioDto> CreateAsync(OrdenServicioCreateDto d, CancellationToken ct = default);
    Task<OrdenServicioDto> UpdateTecnicoAsync(int id, OrdenServicioUpdateTecnicoDto d, CancellationToken ct = default);
    Task<OrdenServicioDto> CambiarEstatusAsync(int id, OrdenServicioCambiarEstatusDto d, CancellationToken ct = default);
    Task<OrdenServicioFotoDto> AgregarFotoAsync(int id, OrdenServicioFotoCreateDto d, CancellationToken ct = default);
    Task EliminarFotoAsync(int id, int fotoId, CancellationToken ct = default);
    Task<PdfFile> GetReportePdfAsync(int id, CancellationToken ct = default);
}

public interface IInventarioService
{
    Task<PagedResult<ProductoDto>> ListProductosAsync(string? q, string? grupo, string? categoria, bool? bajoStock, int page, int size, CancellationToken ct = default);
    Task<ProductoDto> GetProductoAsync(int id, CancellationToken ct = default);
    Task<ProductoDto> CreateProductoAsync(ProductoSaveDto d, CancellationToken ct = default);
    Task<ProductoDto> UpdateProductoAsync(int id, ProductoSaveDto d, CancellationToken ct = default);
    Task<MovimientoInventarioDto> RegistrarMovimientoAsync(MovimientoCreateDto d, CancellationToken ct = default);
    Task<PagedResult<MovimientoInventarioDto>> ListKardexAsync(int? productoId, string? tipo, string? subtipo, string? q, int page, int size, CancellationToken ct = default);
}

public interface IVentaService
{
    Task<PagedResult<VentaDocumentoDto>> ListAsync(string? q, string? tipoDocumento, string? estatus, int? clienteId, bool? sinSalidaInventario, int page, int size, CancellationToken ct = default);
    Task<VentaDocumentoDto> GetAsync(int id, CancellationToken ct = default);
    Task<VentaDocumentoDto> CreateAsync(VentaCreateDto d, CancellationToken ct = default);
    Task<VentaDocumentoDto> CambiarEstatusAsync(int id, VentaCambiarEstatusDto d, CancellationToken ct = default);
    Task<VentaDocumentoDto> RegistrarSalidaInventarioAsync(int id, CancellationToken ct = default);
}

public interface ICompraService
{
    Task<PagedResult<CompraDto>> ListAsync(string? q, string? estatus, int page, int size, CancellationToken ct = default);
    Task<CompraDto> CreateAsync(CompraCreateDto d, CancellationToken ct = default);
    Task<CompraDto> RecibirCompraAsync(int id, CancellationToken ct = default);
}

public interface IMantenimientoService
{
    Task<PagedResult<MantenimientoDto>> ListAsync(string? q, string? tipo, string? estatus, int? equipoId, int page, int size, CancellationToken ct = default);
    Task<MantenimientoDto> CreateAsync(MantenimientoSaveDto d, CancellationToken ct = default);
    Task<MantenimientoDto> UpdateAsync(int id, MantenimientoSaveDto d, CancellationToken ct = default);
}

public interface IGarantiaService
{
    Task<PagedResult<GarantiaDto>> ListAsync(string? q, string? estatus, int? equipoId, int page, int size, CancellationToken ct = default);
    Task<GarantiaDto> CreateAsync(GarantiaSaveDto d, CancellationToken ct = default);
    Task<GarantiaDto> UpdateAsync(int id, GarantiaSaveDto d, CancellationToken ct = default);
}

public interface IDashboardService { Task<DashboardDto> GetAsync(CancellationToken ct = default); }
public interface IAuditService { Task<PagedResult<AuditLogDto>> ListAsync(string? entidad, int page, int size, CancellationToken ct = default); }

public interface IQrService
{
    string BuildUrl(string folio);
    byte[] GeneratePng(string folio);
}

public interface IPdfService
{
    byte[] Generate(Certificado cert, byte[] qrPng);
    byte[] GenerateReporteTecnico(OrdenServicio os, byte[] qrPng);
}

/// <summary>Puertos futuros: se implementan sin tocar el dominio.</summary>
public interface INotificationSender { Task SendCertificadoAsync(Certificado c, byte[] pdf, CancellationToken ct = default); }
public interface IFirmaElectronicaService { Task<string> FirmarAsync(byte[] pdf, int usuarioId, CancellationToken ct = default); }

// ───────────── Validadores ─────────────
public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Correo).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class UsuarioCreateValidator : AbstractValidator<UsuarioCreateDto>
{
    public UsuarioCreateValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Rol).IsInEnum();
    }
}

public class ClienteValidator : AbstractValidator<ClienteSaveDto>
{
    public ClienteValidator()
    {
        RuleFor(x => x.RazonSocial).NotEmpty().MaximumLength(250);
        RuleFor(x => x.RFC).NotEmpty().Matches("^[A-ZÑ&]{3,4}[0-9]{6}[A-Z0-9]{3}$")
            .WithMessage("RFC con formato inválido.");
        RuleFor(x => x.Correo).EmailAddress().When(x => !string.IsNullOrEmpty(x.Correo));
        RuleFor(x => x.Telefono).MaximumLength(30);
    }
}

public class EquipoValidator : AbstractValidator<EquipoSaveDto>
{
    public EquipoValidator()
    {
        RuleFor(x => x.ClienteId).GreaterThan(0);
        RuleFor(x => x.TipoEquipo).IsInEnum();
        RuleFor(x => x.Marca).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Modelo).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SeriePrincipal).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IMEI1).Matches("^[0-9]{15}$").When(x => !string.IsNullOrEmpty(x.IMEI1)).WithMessage("IMEI debe tener 15 dígitos.");
        RuleFor(x => x.IMEI2).Matches("^[0-9]{15}$").When(x => !string.IsNullOrEmpty(x.IMEI2)).WithMessage("IMEI debe tener 15 dígitos.");
    }
}

public class CertificadoCreateValidator : AbstractValidator<CertificadoCreateDto>
{
    public CertificadoCreateValidator()
    {
        RuleFor(x => x.EquipoId).GreaterThan(0);
        RuleFor(x => x.FechaCalibracion).NotEmpty().LessThanOrEqualTo(DateTime.UtcNow.Date.AddDays(1));
        RuleFor(x => x.Resultado).IsInEnum();
        RuleFor(x => x.VigenciaMeses).Must(v => v is 6 or 12).When(x => x.VigenciaMeses.HasValue)
            .WithMessage("La vigencia debe ser de 6 o 12 meses.");
    }
}

public class CertificadoUpdateValidator : AbstractValidator<CertificadoUpdateDto>
{
    public CertificadoUpdateValidator()
    {
        RuleFor(x => x.VigenciaMeses).Must(v => v is 6 or 12);
        RuleFor(x => x.Resultado).IsInEnum();
        RuleFor(x => x.MotivoCambio).NotEmpty().MinimumLength(5).WithMessage("Indique el motivo del cambio (auditoría).");
    }
}

public class CancelarValidator : AbstractValidator<CancelarDto>
{
    public CancelarValidator() => RuleFor(x => x.Motivo).NotEmpty().MinimumLength(5);
}

public class OrdenServicioCreateValidator : AbstractValidator<OrdenServicioCreateDto>
{
    public OrdenServicioCreateValidator()
    {
        RuleFor(x => x.EquipoId).GreaterThan(0);
        RuleFor(x => x.TipoServicio).NotEmpty().MaximumLength(80);
    }
}

public class ProductoSaveValidator : AbstractValidator<ProductoSaveDto>
{
    public ProductoSaveValidator()
    {
        RuleFor(x => x.SKU).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GrupoCategoria).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Categoria).NotEmpty().MaximumLength(80);
        RuleFor(x => x.CostoUnitario).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PrecioVenta).GreaterThanOrEqualTo(0);
    }
}

public class MovimientoCreateValidator : AbstractValidator<MovimientoCreateDto>
{
    public MovimientoCreateValidator()
    {
        RuleFor(x => x.ProductoId).GreaterThan(0);
        RuleFor(x => x.TipoMovimiento).Must(t => t is "Entrada" or "Salida").WithMessage("Tipo de movimiento debe ser Entrada o Salida.");
        RuleFor(x => x.Subtipo).NotEmpty();
        RuleFor(x => x.Cantidad).GreaterThan(0);
    }
}
