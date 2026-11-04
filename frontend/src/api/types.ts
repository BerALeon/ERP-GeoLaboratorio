export type Rol = 'Administrador' | 'Tecnico' | 'Consulta'

export interface Usuario { usuarioId: number; nombre: string; correo: string; rol: Rol; activo: boolean }
export interface LoginResponse { token: string; expira: string; usuario: Usuario }
export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number }

export interface Cliente {
  clienteId: number; razonSocial: string; nombreComercial?: string; rfc: string; direccion?: string; ciudad?: string
  estado?: string; pais?: string; telefono?: string; correo?: string; contacto?: string; totalEquipos: number
}

export interface EquipoSerie {
  equipoSerieId?: number
  tipoSerie: string
  valor: string
  observaciones?: string
}

export interface Equipo {
  equipoId: number; clienteId: number; cliente: string; tipoEquipo: string; tipoEquipoId: number; marca: string; modelo: string
  seriePrincipal: string; serieSecundaria?: string; imei1?: string; imei2?: string; numeroInterno?: string
  fechaCompra?: string; observaciones?: string
  codigoQR?: string; codigoBarras?: string; estatus?: string; fechaBaja?: string; motivoBaja?: string
  series?: EquipoSerie[]; totalOrdenesServicio?: number; totalCertificados?: number
}

export interface Certificado {
  certificadoId: number; folio: string; equipoId: number; cliente: string; tipoEquipo: string; equipo: string
  seriePrincipal: string; tecnico: string; fechaCalibracion: string; fechaEmision: string; fechaVencimiento: string
  vigenciaMeses: number; resultado: string; resultados?: string; observaciones?: string; conclusiones?: string
  estatus: string; estadoVigencia: 'Vigente' | 'PorVencer' | 'Vencido' | 'Cancelado'; diasParaVencer: number
  motivoCancelacion?: string; version: number
  ordenServicioId?: number; folioOrdenServicio?: string; tipoRegistro?: string
}

export interface Historial { historialId: number; accion: string; detalle?: string; pdfHash?: string; usuario?: string; fecha: string }

export interface Verificacion {
  estado: 'Vigente' | 'Vencido' | 'Cancelado' | 'NoEncontrado'; mensaje: string; folio?: string; cliente?: string
  equipo?: string; serieEquipo?: string; fechaCalibracion?: string; fechaVencimiento?: string; diasVencido?: number
}

export interface OrdenServicioEstatusHistorial {
  historialEstatusId: number
  estatusAnterior?: string
  estatusNuevo: string
  fecha: string
  usuarioNombre?: string
  observaciones?: string
}

export interface OrdenServicioRefaccion {
  ordenServicioRefaccionId: number
  productoId: number
  sku: string
  nombreProducto: string
  cantidad: number
  precioUnitario: number
  importe: number
  descontado: boolean
}

export interface OrdenServicioFoto {
  ordenServicioFotoId: number
  ordenServicioId: number
  categoria: string
  descripcion?: string
  imagenUrl: string
  usuarioNombre?: string
  fecha: string
}

export interface OrdenServicio {
  ordenServicioId: number
  folio: string
  clienteId: number
  cliente: string
  clienteRFC: string
  clienteTelefono?: string
  equipoId: number
  tipoEquipo: string
  marca: string
  modelo: string
  seriePrincipal: string
  numeroInterno?: string
  seriesEquipo: EquipoSerie[]
  tipoServicio: string
  tiposServicioAdicionales: string[]
  estatus: string
  tecnicoId?: number
  tecnicoNombre?: string
  supervisorId?: number
  supervisorNombre?: string
  fechaRecepcion: string
  fechaCompromiso?: string
  fechaTermino?: string
  fechaEntrega?: string
  metodoIdentificacion: string
  accesoriosRecibidos: string[]
  accesoriosPersonalizados?: string
  condicionEquipo: string[]
  fallaReportada?: string
  observacionesRecepcion?: string
  diagnostico?: string
  actividadesRealizadas?: string
  observacionesTecnicas?: string
  costoManoObra: number
  subtotalRefacciones: number
  total: number
  refaccionesDescontadas: boolean
  firmaTecnico?: string
  firmaSupervisor?: string
  firmaCliente?: string
  nombreClienteFirma?: string
  historialEstatus: OrdenServicioEstatusHistorial[]
  refacciones: OrdenServicioRefaccion[]
  fotografias: OrdenServicioFoto[]
}

export interface OrdenServicioResumen {
  ordenServicioId: number
  folio: string
  tipoServicio: string
  estatus: string
  fechaRecepcion: string
  diagnostico?: string
}

export interface EquipoBusquedaResult {
  encontrado: boolean
  terminoBusqueda: string
  equipo?: Equipo
  cliente?: Cliente
  series: EquipoSerie[]
  historialOrdenes: OrdenServicioResumen[]
  certificadosRecientes: Certificado[]
  garantiasVigentes: Garantia[]
}

export interface Mantenimiento {
  mantenimientoId: number
  folio: string
  equipoId: number
  equipo: string
  seriePrincipal: string
  cliente: string
  ordenServicioId?: number
  folioOrdenServicio?: string
  tipo: string
  estatus: string
  fechaProgramada: string
  fechaRealizada?: string
  proximoMantenimiento?: string
  tecnicoId?: number
  tecnicoNombre?: string
  descripcion?: string
  actividades?: string
  hallazgos?: string
  recomendaciones?: string
  costo: number
}

export interface Garantia {
  garantiaId: number
  folio: string
  equipoId: number
  equipo: string
  seriePrincipal: string
  clienteId: number
  cliente: string
  ordenServicioId?: number
  ventaId?: number
  tipoGarantia: string
  fechaInicio: string
  fechaFin: string
  diasRestantes: number
  cobertura: string
  condiciones?: string
  estatus: string
  motivoReclamacion?: string
  resolucion?: string
}

export interface Producto {
  productoId: number
  sku: string
  codigoBarras?: string
  nombre: string
  descripcion?: string
  grupoCategoria: string
  categoria: string
  marca?: string
  modeloCompatible?: string
  unidad: string
  costoUnitario: number
  precioVenta: number
  stockActual: number
  stockMinimo: number
  stockMaximo: number
  almacen: string
  pasillo: string
  rack: string
  anaquel: string
  nivel: string
  posicion: string
  codigoUbicacion: string
  activo: boolean
  estadoStock: 'Normal' | 'Bajo' | 'SinStock'
}

export interface MovimientoInventario {
  movimientoId: number
  productoId: number
  sku: string
  nombreProducto: string
  grupoCategoria: string
  categoria: string
  codigoUbicacion: string
  tipoMovimiento: 'Entrada' | 'Salida'
  subtipo: string
  concepto: string
  referenciaFolio?: string
  ordenServicioId?: number
  ventaId?: number
  compraId?: number
  cantidad: number
  stockAnterior: number
  stockPosterior: number
  costoUnitario: number
  ubicacionOrigen?: string
  ubicacionDestino?: string
  observaciones?: string
  usuarioNombre?: string
  fecha: string
}

export interface VentaPartida {
  ventaPartidaId: number
  productoId?: number
  sku?: string
  equipoId?: number
  descripcion: string
  numeroSerie?: string
  cantidad: number
  precioUnitario: number
  descuento: number
  importe: number
}

export interface VentaDocumento {
  ventaId: number
  folio: string
  tipoDocumento: string
  estatus: string
  clienteId: number
  cliente: string
  clienteRFC: string
  ordenServicioId?: number
  documentoOrigenId?: number
  fecha: string
  fechaVigencia?: string
  subtotal: number
  descuento: number
  iva: number
  total: number
  folioFacturaCFDI?: string
  usoCFDI?: string
  metodoPago?: string
  formaPago?: string
  descuentoAutomaticoInventario: boolean
  salidaInventarioRegistrada: boolean
  requiereAlertaSinSalida: boolean
  observaciones?: string
  vendedorNombre?: string
  partidas: VentaPartida[]
}

export interface CompraPartida {
  compraPartidaId: number
  productoId: number
  sku: string
  descripcion: string
  cantidad: number
  costoUnitario: number
  importe: number
}

export interface Compra {
  compraId: number
  folio: string
  proveedor: string
  rfcProveedor?: string
  facturaProveedor?: string
  fechaCompra: string
  fechaRecepcion?: string
  estatus: string
  subtotal: number
  iva: number
  total: number
  entradaInventarioRegistrada: boolean
  observaciones?: string
  usuarioNombre?: string
  partidas: CompraPartida[]
}

export interface EquipoHistorial {
  equipoHistorialId: number
  equipoId: number
  tipoEvento: string
  titulo: string
  detalle?: string
  folioReferencia?: string
  referenciaId?: number
  usuarioNombre?: string
  fecha: string
}

export interface EquipoArchivo {
  equipoArchivoId: number
  equipoId: number
  ordenServicioId?: number
  tipoArchivo: string
  categoria: string
  nombreArchivo: string
  archivoUrl: string
  descripcion?: string
  subidoPor?: string
  fecha: string
}

export interface RefaccionEquipoResumen {
  ordenServicioRefaccionId: number
  ordenServicioId: number
  folioOrden: string
  productoId: number
  sku: string
  nombreProducto: string
  cantidad: number
  precioUnitario: number
  importe: number
  fecha: string
}

export interface ExpedienteEquipo {
  equipo: Equipo
  cliente: Cliente
  series: EquipoSerie[]
  certificados: Certificado[]
  calibraciones: Certificado[]
  certificaciones: Certificado[]
  mantenimientos: Mantenimiento[]
  reparaciones: Mantenimiento[]
  refaccionesUtilizadas: RefaccionEquipoResumen[]
  ordenesServicio: OrdenServicio[]
  garantias: Garantia[]
  ventas: VentaDocumento[]
  archivos: EquipoArchivo[]
  fotografias: OrdenServicioFoto[]
  historialCompleto: EquipoHistorial[]
}

export interface Alerta { certificadoId: number; folio: string; cliente: string; equipo: string; fechaVencimiento: string; dias: number }
export interface SerieItem { etiqueta: string; valor: number }
export interface SerieDecimalItem { etiqueta: string; valor: number; extra?: string }

export interface IndicadoresGenerales {
  clientesRegistrados: number
  equiposRegistrados: number
  ordenesServicioAbiertas: number
  equiposEnTaller: number
  certificadosVigentes: number
  certificadosPorVencer: number
  ventasDelMes: number
  cantidadVentasDelMes: number
  valorTotalInventario: number
  productosSinStock: number
  refaccionesStockCritico: number
  ventasSinSalidaInventario: number
}

export interface IndicadoresTecnicos {
  equiposRecibidosHoy: number
  equiposEnDiagnostico: number
  equiposEnReparacion: number
  equiposEnCalibracion: number
  equiposTerminados: number
  equiposEntregados: number
}

export interface IndicadoresInventario {
  productosRegistrados: number
  stockBajo: number
  refaccionesMasUtilizadas: SerieDecimalItem[]
  productosMasVendidos: SerieDecimalItem[]
  entradasDelMes: number
  salidasDelMes: number
  unidadesEntradasMes: number
  unidadesSalidasMes: number
}

export interface Dashboard {
  kpis: { activos: number; vencidos: number; porVencer: number; equipos: number; clientes: number; emitidosMes: number }
  vencen30: Alerta[]; vencen15: Alerta[]; vencen7: Alerta[]; porMes: SerieItem[]; porTipo: SerieItem[]; porEstado: SerieItem[]
  generales?: IndicadoresGenerales
  tecnicos?: IndicadoresTecnicos
  inventario?: IndicadoresInventario
  ordenesPorEstatus?: SerieItem[]
}

export interface AuditLog {
  auditLogId: number; entidad: string; entidadId: string; accion: string; valoresAnteriores?: string
  valoresNuevos?: string; usuario?: string; ip?: string; fecha: string
}
