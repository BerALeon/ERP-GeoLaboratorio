namespace Geolaboratorio.Domain;

public enum RolUsuario { Administrador = 1, Tecnico = 2, Consulta = 3 }

public enum TipoEquipo
{
    EstacionTotal = 1, GPS = 2, GNSS = 3, NivelAutomatico = 4, NivelDigital = 5,
    DroneRTK = 6, EscanerLaser = 7, LiDAR = 8, Otro = 9
}

public enum ResultadoCalibracion { Aceptado = 1, Condicionado = 2, Rechazado = 3 }

public enum EstatusCertificado { Vigente = 1, Cancelado = 2 }

public enum EstatusEquipo
{
    Activo = 1,
    EnTaller = 2,
    EnGarantia = 3,
    Vendido = 4,
    Baja = 5
}

public abstract class AuditableEntity
{
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? ModificadoEn { get; set; }
    /// <summary>Preparado para multi-sucursal.</summary>
    public int SucursalId { get; set; } = 1;
}

public class Usuario : AuditableEntity
{
    public int UsuarioId { get; set; }
    public string Nombre { get; set; } = "";
    public string Correo { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public RolUsuario Rol { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary>Ruta de la imagen de firma del técnico (base para firma electrónica avanzada).</summary>
    public string? FirmaUrl { get; set; }
}

public class Cliente : AuditableEntity
{
    public int ClienteId { get; set; }
    public string RazonSocial { get; set; } = "";
    public string? NombreComercial { get; set; }
    public string RFC { get; set; } = "";
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Estado { get; set; }
    public string? Pais { get; set; } = "México";
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Contacto { get; set; }
    public bool Activo { get; set; } = true;
    public ICollection<Equipo> Equipos { get; set; } = new List<Equipo>();
    public ICollection<OrdenServicio> OrdenesServicio { get; set; } = new List<OrdenServicio>();
    public ICollection<VentaDocumento> Ventas { get; set; } = new List<VentaDocumento>();
    public ICollection<Garantia> Garantias { get; set; } = new List<Garantia>();
}

/// <summary>
/// Entidad principal del ERP GEOLABORATORIO.
/// Mantiene trazabilidad completa desde el registro del equipo hasta su baja.
/// </summary>
public class Equipo : AuditableEntity
{
    public int EquipoId { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public TipoEquipo TipoEquipo { get; set; }
    public string Marca { get; set; } = "";
    public string Modelo { get; set; } = "";
    public string SeriePrincipal { get; set; } = "";
    public string? SerieSecundaria { get; set; }
    public string? IMEI1 { get; set; }
    public string? IMEI2 { get; set; }
    public string? NumeroInterno { get; set; }
    public string? CodigoQR { get; set; }
    public string? CodigoBarras { get; set; }
    public DateTime? FechaCompra { get; set; }
    public string? Observaciones { get; set; }
    public EstatusEquipo Estatus { get; set; } = EstatusEquipo.Activo;
    public DateTime? FechaBaja { get; set; }
    public string? MotivoBaja { get; set; }

    public ICollection<EquipoSerie> Series { get; set; } = new List<EquipoSerie>();
    public ICollection<Certificado> Certificados { get; set; } = new List<Certificado>();
    public ICollection<OrdenServicio> OrdenesServicio { get; set; } = new List<OrdenServicio>();
    public ICollection<Mantenimiento> Mantenimientos { get; set; } = new List<Mantenimiento>();
    public ICollection<Garantia> Garantias { get; set; } = new List<Garantia>();
    public ICollection<EquipoHistorial> HistorialTecnico { get; set; } = new List<EquipoHistorial>();
    public ICollection<EquipoArchivo> Archivos { get; set; } = new List<EquipoArchivo>();
}

/// <summary>Múltiples números de serie configurables por equipo (Radio Interna, Radio Externa, Controladora, Antena, IMEI, etc.).</summary>
public class EquipoSerie
{
    public int EquipoSerieId { get; set; }
    public int EquipoId { get; set; }
    public Equipo? Equipo { get; set; }
    public string TipoSerie { get; set; } = "";
    public string Valor { get; set; } = "";
    public string? Observaciones { get; set; }
}

/// <summary>Trazabilidad cronológica completa del ciclo de vida del equipo desde su registro hasta su baja.</summary>
public class EquipoHistorial
{
    public long EquipoHistorialId { get; set; }
    public int EquipoId { get; set; }
    public Equipo? Equipo { get; set; }
    public string TipoEvento { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string? Detalle { get; set; }
    public string? FolioReferencia { get; set; }
    public int? ReferenciaId { get; set; }
    public int? UsuarioId { get; set; }
    public string? UsuarioNombre { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

/// <summary>Archivos, fotografías y PDFs vinculados al expediente digital 360° del equipo.</summary>
public class EquipoArchivo
{
    public int EquipoArchivoId { get; set; }
    public int EquipoId { get; set; }
    public Equipo? Equipo { get; set; }
    public int? OrdenServicioId { get; set; }
    public string TipoArchivo { get; set; } = "Fotografia"; // Fotografia | PDF | Documento | Firmware
    public string Categoria { get; set; } = "General";
    public string NombreArchivo { get; set; } = "";
    public string ArchivoUrl { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? SubidoPor { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

public class Certificado : AuditableEntity
{
    public int CertificadoId { get; set; }
    /// <summary>Folio consecutivo inalterable, p. ej. GEO-2026-000123.</summary>
    public string Folio { get; set; } = "";
    public int Anio { get; set; }
    public int Consecutivo { get; set; }
    public int EquipoId { get; set; }
    public Equipo? Equipo { get; set; }
    public int? OrdenServicioId { get; set; }
    public OrdenServicio? OrdenServicio { get; set; }
    public string TipoRegistro { get; set; } = "Calibración"; // Calibración | Certificación
    public int TecnicoId { get; set; }
    public Usuario? Tecnico { get; set; }
    public DateTime FechaCalibracion { get; set; }
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public int VigenciaMeses { get; set; }
    public ResultadoCalibracion Resultado { get; set; }
    public string? Resultados { get; set; }
    public string? Observaciones { get; set; }
    public string? Conclusiones { get; set; }
    public string? PDFUrl { get; set; }
    public string? QRUrl { get; set; }
    public EstatusCertificado Estatus { get; set; } = EstatusCertificado.Vigente;
    public string? MotivoCancelacion { get; set; }
    public DateTime? CanceladoEn { get; set; }
    /// <summary>Hash SHA-256 del PDF generado: trazabilidad e integridad.</summary>
    public string? PdfHash { get; set; }
    public int Version { get; set; } = 1;
    public ICollection<CertificadoHistorial> Historial { get; set; } = new List<CertificadoHistorial>();
}

/// <summary>Cada PDF generado/descargado/reenviado queda registrado (trazabilidad).</summary>
public class CertificadoHistorial
{
    public long HistorialId { get; set; }
    public int CertificadoId { get; set; }
    public string Accion { get; set; } = ""; // Creado, Modificado, Cancelado, PdfGenerado, PdfDescargado, Reenviado
    public string? Detalle { get; set; }
    public string? PdfHash { get; set; }
    public int? UsuarioId { get; set; }
    public string? UsuarioNombre { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

/// <summary>Órdenes de Servicio (OS-AAAA-000001) para recepción, taller, diagnóstico, reparación, mantenimiento, calibración y certificación.</summary>
public class OrdenServicio : AuditableEntity
{
    public int OrdenServicioId { get; set; }
    public string Folio { get; set; } = ""; // OS-2026-000001
    public int Anio { get; set; }
    public int Consecutivo { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int EquipoId { get; set; }
    public Equipo? Equipo { get; set; }
    public string TipoServicio { get; set; } = "Diagnóstico";
    public string? TiposServicioAdicionales { get; set; }
    public string Estatus { get; set; } = "Recibido";
    public int? TecnicoId { get; set; }
    public Usuario? Tecnico { get; set; }
    public int? SupervisorId { get; set; }
    public Usuario? Supervisor { get; set; }
    public DateTime FechaRecepcion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCompromiso { get; set; }
    public DateTime? FechaTermino { get; set; }
    public DateTime? FechaEntrega { get; set; }
    public string? MetodoIdentificacion { get; set; } = "Manual";
    /// <summary>JSON con la lista de accesorios recibidos en el checklist.</summary>
    public string? AccesoriosChecklistJson { get; set; }
    public string? AccesoriosPersonalizados { get; set; }
    /// <summary>JSON con la lista de condiciones físicas detectadas en recepción.</summary>
    public string? CondicionChecklistJson { get; set; }
    public string? FallaReportada { get; set; }
    public string? ObservacionesRecepcion { get; set; }
    public string? Diagnostico { get; set; }
    public string? ActividadesRealizadas { get; set; }
    public string? ObservacionesTecnicas { get; set; }
    public decimal CostoManoObra { get; set; }
    public decimal SubtotalRefacciones { get; set; }
    public decimal Total { get; set; }
    public bool RefaccionesDescontadas { get; set; }
    public string? FirmaTecnico { get; set; }
    public string? FirmaSupervisor { get; set; }
    public string? FirmaCliente { get; set; }
    public string? NombreClienteFirma { get; set; }
    public string? ReportePdfUrl { get; set; }

    public ICollection<OrdenServicioEstatusHistorial> HistorialEstatus { get; set; } = new List<OrdenServicioEstatusHistorial>();
    public ICollection<OrdenServicioRefaccion> Refacciones { get; set; } = new List<OrdenServicioRefaccion>();
    public ICollection<OrdenServicioFoto> Fotografias { get; set; } = new List<OrdenServicioFoto>();
}

public class OrdenServicioEstatusHistorial
{
    public long HistorialEstatusId { get; set; }
    public int OrdenServicioId { get; set; }
    public OrdenServicio? OrdenServicio { get; set; }
    public string? EstatusAnterior { get; set; }
    public string EstatusNuevo { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public int? UsuarioId { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? Observaciones { get; set; }
}

public class OrdenServicioRefaccion
{
    public int OrdenServicioRefaccionId { get; set; }
    public int OrdenServicioId { get; set; }
    public OrdenServicio? OrdenServicio { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public string NombreProducto { get; set; } = "";
    public string? SKU { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Importe { get; set; }
    public bool Descontado { get; set; }
}

public class OrdenServicioFoto
{
    public int OrdenServicioFotoId { get; set; }
    public int OrdenServicioId { get; set; }
    public OrdenServicio? OrdenServicio { get; set; }
    /// <summary>Equipo completo | Número de serie | Golpes | Daños | Accesorios recibidos | Reparación</summary>
    public string Categoria { get; set; } = "Equipo completo";
    public string? Descripcion { get; set; }
    public string ImagenUrl { get; set; } = "";
    public string? UsuarioNombre { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

/// <summary>Mantenimientos preventivos, correctivos, reparaciones y revisiones técnicas.</summary>
public class Mantenimiento : AuditableEntity
{
    public int MantenimientoId { get; set; }
    public string Folio { get; set; } = ""; // MTO-2026-000001
    public int EquipoId { get; set; }
    public Equipo? Equipo { get; set; }
    public int? OrdenServicioId { get; set; }
    public OrdenServicio? OrdenServicio { get; set; }
    /// <summary>Mantenimiento Preventivo | Mantenimiento Correctivo | Reparación | Revisión Técnica</summary>
    public string Tipo { get; set; } = "Mantenimiento Preventivo";
    /// <summary>Programado | En Proceso | Completado | Cancelado</summary>
    public string Estatus { get; set; } = "Programado";
    public DateTime FechaProgramada { get; set; } = DateTime.UtcNow;
    public DateTime? FechaRealizada { get; set; }
    public DateTime? ProximoMantenimiento { get; set; }
    public int? TecnicoId { get; set; }
    public Usuario? Tecnico { get; set; }
    public string? Descripcion { get; set; }
    public string? Actividades { get; set; }
    public string? Hallazgos { get; set; }
    public string? Recomendaciones { get; set; }
    public decimal Costo { get; set; }
}

/// <summary>Control de garantías de fábrica, venta o reparación/servicio.</summary>
public class Garantia : AuditableEntity
{
    public int GarantiaId { get; set; }
    public string Folio { get; set; } = ""; // GAR-2026-000001
    public int EquipoId { get; set; }
    public Equipo? Equipo { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int? OrdenServicioId { get; set; }
    public int? VentaId { get; set; }
    public string TipoGarantia { get; set; } = "Servicio"; // Fábrica | Venta | Servicio | Refacción
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime FechaFin { get; set; } = DateTime.UtcNow.AddMonths(6);
    public string Cobertura { get; set; } = "";
    public string? Condiciones { get; set; }
    public string Estatus { get; set; } = "Vigente"; // Vigente | Vencida | En Reclamación | Aplicada | Cancelada
    public string? MotivoReclamacion { get; set; }
    public string? Resolucion { get; set; }
}

/// <summary>Catálogo de productos, equipos nuevos, refacciones, accesorios, consumibles y herramientas con ubicación física.</summary>
public class Producto : AuditableEntity
{
    public int ProductoId { get; set; }
    public string SKU { get; set; } = "";
    public string? CodigoBarras { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    /// <summary>Equipo Nuevo | Refacciones | Accesorios | Consumibles | Herramientas</summary>
    public string GrupoCategoria { get; set; } = "Refacciones";
    /// <summary>Estaciones Totales, GNSS, GPS, Drones, Niveles, Escáneres, Pantallas, Tarjetas, Antenas, Teclados, Radios, Baterías, Trípodes, Prismas, Bastones, Adaptadores, Cables, Pintura, Estacas, Etiquetas, Herramientas</summary>
    public string Categoria { get; set; } = "Pantallas";
    public string? Marca { get; set; }
    public string? ModeloCompatible { get; set; }
    public string Unidad { get; set; } = "PZA";
    public decimal CostoUnitario { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal StockActual { get; set; }
    public decimal StockMinimo { get; set; }
    public decimal StockMaximo { get; set; } = 100;

    // Ubicación física
    public string Almacen { get; set; } = "ALM01";
    public string Pasillo { get; set; } = "PAS01";
    public string Rack { get; set; } = "R01";
    public string Anaquel { get; set; } = "ANA";
    public string Nivel { get; set; } = "N1";
    public string Posicion { get; set; } = "P1";
    public string CodigoUbicacion { get; set; } = "ALM01-PAS01-R01-ANA-N1";

    public bool Activo { get; set; } = true;
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}

/// <summary>Kardex de Movimientos de Inventario (Entradas y Salidas con trazabilidad completa).</summary>
public class MovimientoInventario
{
    public long MovimientoId { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    /// <summary>Entrada | Salida</summary>
    public string TipoMovimiento { get; set; } = "";
    /// <summary>
    /// Entradas: Compra | Ajuste | Devolución | Transferencia
    /// Salidas: Venta | Reparación | Garantía | Consumo interno | Transferencia
    /// </summary>
    public string Subtipo { get; set; } = "";
    public string Concepto { get; set; } = "";
    public string? ReferenciaFolio { get; set; }
    public int? OrdenServicioId { get; set; }
    public int? VentaId { get; set; }
    public int? CompraId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal StockAnterior { get; set; }
    public decimal StockPosterior { get; set; }
    public decimal CostoUnitario { get; set; }
    public string? UbicacionOrigen { get; set; }
    public string? UbicacionDestino { get; set; }
    public string? Observaciones { get; set; }
    public int? UsuarioId { get; set; }
    public string? UsuarioNombre { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

/// <summary>Módulo Comercial: Cotizaciones, Pedidos, Ventas, Remisiones y Facturación.</summary>
public class VentaDocumento : AuditableEntity
{
    public int VentaId { get; set; }
    public string Folio { get; set; } = "";
    /// <summary>Cotización | Pedido | Venta | Remisión | Factura</summary>
    public string TipoDocumento { get; set; } = "Cotización";
    /// <summary>Borrador | Enviada | Autorizada | Pendiente Entrega | Entregada | Facturada | Cancelada</summary>
    public string Estatus { get; set; } = "Borrador";
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int? OrdenServicioId { get; set; }
    public int? DocumentoOrigenId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public DateTime? FechaVigencia { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal IVA { get; set; }
    public decimal Total { get; set; }
    public string? FolioFacturaCFDI { get; set; }
    public string? UsoCFDI { get; set; }
    public string? MetodoPago { get; set; }
    public string? FormaPago { get; set; }
    public bool DescuentoAutomaticoInventario { get; set; } = true;
    public bool SalidaInventarioRegistrada { get; set; }
    public string? Observaciones { get; set; }
    public int? VendedorId { get; set; }
    public string? VendedorNombre { get; set; }
    public ICollection<VentaPartida> Partidas { get; set; } = new List<VentaPartida>();
}

public class VentaPartida
{
    public int VentaPartidaId { get; set; }
    public int VentaId { get; set; }
    public VentaDocumento? Venta { get; set; }
    public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public int? EquipoId { get; set; }
    public Equipo? Equipo { get; set; }
    public string Descripcion { get; set; } = "";
    public string? NumeroSerie { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public decimal Importe { get; set; }
}

/// <summary>Módulo de Compras y Abastecimiento de Inventario.</summary>
public class Compra : AuditableEntity
{
    public int CompraId { get; set; }
    public string Folio { get; set; } = ""; // COM-2026-000001
    public string Proveedor { get; set; } = "";
    public string? RFCProveedor { get; set; }
    public string? FacturaProveedor { get; set; }
    public DateTime FechaCompra { get; set; } = DateTime.UtcNow;
    public DateTime? FechaRecepcion { get; set; }
    public string Estatus { get; set; } = "Recibida"; // Ordenada | Recibida | Cancelada
    public decimal Subtotal { get; set; }
    public decimal IVA { get; set; }
    public decimal Total { get; set; }
    public bool EntradaInventarioRegistrada { get; set; }
    public string? Observaciones { get; set; }
    public string? UsuarioNombre { get; set; }
    public ICollection<CompraPartida> Partidas { get; set; } = new List<CompraPartida>();
}

public class CompraPartida
{
    public int CompraPartidaId { get; set; }
    public int CompraId { get; set; }
    public Compra? Compra { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public string Descripcion { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Importe { get; set; }
}

/// <summary>Bitácora de auditoría general (append-only).</summary>
public class AuditLog
{
    public long AuditLogId { get; set; }
    public string Entidad { get; set; } = "";
    public string EntidadId { get; set; } = "";
    public string Accion { get; set; } = ""; // Insert, Update, Delete
    public string? ValoresAnteriores { get; set; }
    public string? ValoresNuevos { get; set; }
    public int? UsuarioId { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? Ip { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

/// <summary>Contador por año para Certificados GEO; se incrementa con bloqueo de fila para garantizar consecutivos sin huecos.</summary>
public class FolioSecuencia
{
    public int Anio { get; set; }
    public int Ultimo { get; set; }
}

/// <summary>Contador consecutivo por prefijo y año (OS, MTO, GAR, COT, PED, VTA, REM, FAC, COM).</summary>
public class FolioSecuenciaPrefijo
{
    public string Prefijo { get; set; } = "";
    public int Anio { get; set; }
    public int Ultimo { get; set; }
}

public class ConfiguracionVigencia
{
    public TipoEquipo TipoEquipo { get; set; }
    public int Meses { get; set; } = 12; // 6 o 12
}
