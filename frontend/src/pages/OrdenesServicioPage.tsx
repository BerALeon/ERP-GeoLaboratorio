import {
  CameraOutlined,
  CheckCircleOutlined,
  ClearOutlined,
  DeleteOutlined,
  DownloadOutlined,
  EyeOutlined,
  FileProtectOutlined,
  PlusOutlined,
  QrcodeOutlined,
  SearchOutlined,
  SwapOutlined,
  ToolOutlined,
  UserAddOutlined,
} from '@ant-design/icons'
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Col,
  DatePicker,
  Descriptions,
  Divider,
  Drawer,
  Form,
  Image,
  Input,
  InputNumber,
  Modal,
  Row,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Timeline,
  Typography,
  Upload,
} from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { api, downloadOrdenPdf, errorMessage } from '../api/client'
import type {
  Cliente,
  Equipo,
  EquipoBusquedaResult,
  OrdenServicio,
  OrdenServicioFoto,
  Paged,
  Producto,
  Usuario,
} from '../api/types'
import { useTipos } from './EquiposPage'

const ACCESORIOS_BASE = [
  'Batería',
  'Batería adicional',
  'Cargador',
  'Estuche',
  'Maleta rígida',
  'Cable USB',
  'Cable corriente',
  'Cable red',
  'Prisma',
  'Trípode',
  'Bastón',
  'Adaptador',
  'Tarjeta SD',
  'Radio',
  'Controladora',
  'Antena GNSS',
]

const CONDICIONES_BASE = [
  'Sin daños visibles',
  'Golpes',
  'Rayones',
  'Equipo mojado',
  'Pantalla dañada',
  'Teclado dañado',
  'Equipo incompleto',
  'Con piezas faltantes',
]

const TIPOS_SERVICIO = [
  'Diagnóstico',
  'Revisión',
  'Mantenimiento Preventivo',
  'Mantenimiento Correctivo',
  'Calibración',
  'Certificación',
  'Reparación',
  'Garantía',
  'Actualización Firmware',
  'Cotización',
]

const ESTATUS_FLUJO = [
  'Recibido',
  'En Diagnóstico',
  'Pendiente Cotización',
  'Esperando Autorización',
  'Autorizado',
  'En Reparación',
  'En Mantenimiento',
  'En Calibración',
  'En Certificación',
  'Control de Calidad',
  'Terminado',
  'Listo para Entrega',
  'Entregado',
  'Cancelado',
]

const CATEGORIAS_FOTO = [
  'Equipo completo',
  'Número de serie',
  'Golpes',
  'Daños',
  'Accesorios recibidos',
]

const ESTATUS_COLOR: Record<string, string> = {
  Recibido: 'default',
  'En Diagnóstico': 'processing',
  'Pendiente Cotización': 'warning',
  'Esperando Autorización': 'gold',
  Autorizado: 'cyan',
  'En Reparación': 'orange',
  'En Mantenimiento': 'orange',
  'En Calibración': 'blue',
  'En Certificación': 'purple',
  'Control de Calidad': 'geekblue',
  Terminado: 'lime',
  'Listo para Entrega': 'green',
  Entregado: 'success',
  Cancelado: 'error',
}

/** Lienzo ligero para capturar firma en base64 PNG */
function SignaturePad({
  label,
  value,
  onChange,
}: {
  label: string
  value?: string
  onChange: (dataUrl: string) => void
}) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const drawing = useRef(false)

  const start = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const c = canvasRef.current
    if (!c) return
    const ctx = c.getContext('2d')
    if (!ctx) return
    const rect = c.getBoundingClientRect()
    drawing.current = true
    ctx.beginPath()
    ctx.moveTo(e.clientX - rect.left, e.clientY - rect.top)
  }

  const move = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!drawing.current) return
    const c = canvasRef.current
    if (!c) return
    const ctx = c.getContext('2d')
    if (!ctx) return
    const rect = c.getBoundingClientRect()
    ctx.lineWidth = 2
    ctx.lineCap = 'round'
    ctx.strokeStyle = '#0A2A8F'
    ctx.lineTo(e.clientX - rect.left, e.clientY - rect.top)
    ctx.stroke()
  }

  const stop = () => {
    if (!drawing.current) return
    drawing.current = false
    const c = canvasRef.current
    if (c) onChange(c.toDataURL('image/png'))
  }

  const clear = () => {
    const c = canvasRef.current
    if (!c) return
    const ctx = c.getContext('2d')
    ctx?.clearRect(0, 0, c.width, c.height)
    onChange('')
  }

  return (
    <Card size="small" title={label} extra={<Button size="small" icon={<ClearOutlined />} onClick={clear}>Limpiar</Button>}>
      {value ? (
        <div style={{ textAlign: 'center' }}>
          <img src={value} alt={label} style={{ maxHeight: 75, border: '1px dashed #d1d5db', background: '#fff' }} />
        </div>
      ) : null}
      <canvas
        ref={canvasRef}
        width={250}
        height={80}
        onMouseDown={start}
        onMouseMove={move}
        onMouseUp={stop}
        onMouseLeave={stop}
        style={{
          border: '1px solid #d1d5db',
          borderRadius: 4,
          background: '#fafafa',
          cursor: 'crosshair',
          width: '100%',
          marginTop: 6,
        }}
      />
      <Typography.Text type="secondary" style={{ fontSize: 11 }}> Dibuje la firma con el cursor en el recuadro</Typography.Text>
    </Card>
  )
}

export default function OrdenesServicioPage() {
  const { message } = App.useApp()
  const nav = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const qc = useQueryClient()
  const { data: tiposEquipo } = useTipos()

  const [q, setQ] = useState('')
  const [estatusFiltro, setEstatusFiltro] = useState<string>()
  const [tipoFiltro, setTipoFiltro] = useState<string>()
  const [page, setPage] = useState(1)

  // Modal de Nueva Recepción de Equipo
  const [recepcionOpen, setRecepcionOpen] = useState(false)
  const [metodoId, setMetodoId] = useState<string>('NumeroSerie')
  const [codigoBusqueda, setCodigoBusqueda] = useState('')
  const [busquedaResult, setBusquedaResult] = useState<EquipoBusquedaResult | null>(null)
  const [noEncontrado, setNoEncontrado] = useState(false)
  const [fotosRecepcion, setFotosRecepcion] = useState<{ categoria: string; descripcion: string; imagenUrl: string }[]>([])
  const [catFotoNueva, setCatFotoNueva] = useState('Equipo completo')
  const [descFotoNueva, setDescFotoNueva] = useState('')

  // Sub-modales sin abandonar la captura
  const [nuevoClienteOpen, setNuevoClienteOpen] = useState(false)
  const [nuevoEquipoOpen, setNuevoEquipoOpen] = useState(false)

  // Drawer de Gestión Técnica / Estatus / Refacciones / Reporte PDF
  const [detalleId, setDetalleId] = useState<number | null>(null)
  const [estatusModal, setEstatusModal] = useState<OrdenServicio | null>(null)

  const [formRecepcion] = Form.useForm()
  const [formCliente] = Form.useForm()
  const [formEquipo] = Form.useForm()
  const [formTecnico] = Form.useForm()
  const [formEstatus] = Form.useForm()

  const [firmaTec, setFirmaTec] = useState<string>('')
  const [firmaSup, setFirmaSup] = useState<string>('')
  const [firmaCli, setFirmaCli] = useState<string>('')

  const { data, isLoading } = useQuery({
    queryKey: ['ordenes-servicio', q, estatusFiltro, tipoFiltro, page],
    queryFn: async () =>
      (
        await api.get<Paged<OrdenServicio>>('/ordenes-servicio', {
          params: { q, estatus: estatusFiltro, tipoServicio: tipoFiltro, page, size: 15 },
        })
      ).data,
  })

  const { data: detalleOrden } = useQuery({
    queryKey: ['orden-servicio-detalle', detalleId],
    enabled: !!detalleId,
    queryFn: async () => (await api.get<OrdenServicio>(`/ordenes-servicio/${detalleId}`)).data,
  })

  const { data: equiposList } = useQuery({
    queryKey: ['equipos-select-os'],
    enabled: recepcionOpen || nuevoEquipoOpen,
    queryFn: async () => (await api.get<Paged<Equipo>>('/equipos', { params: { size: 200 } })).data.items,
  })

  const { data: clientesList } = useQuery({
    queryKey: ['clientes-select-os'],
    enabled: nuevoEquipoOpen || nuevoClienteOpen,
    queryFn: async () => (await api.get<Paged<Cliente>>('/clientes', { params: { size: 200 } })).data.items,
  })

  const { data: usuariosList } = useQuery({
    queryKey: ['usuarios-select-os'],
    enabled: recepcionOpen || !!detalleId,
    queryFn: async () => (await api.get<Usuario[]>('/usuarios')).data,
  })

  const { data: productosRefacciones } = useQuery({
    queryKey: ['productos-refacciones'],
    enabled: !!detalleId,
    queryFn: async () => (await api.get<Paged<Producto>>('/inventario/productos', { params: { size: 200 } })).data.items,
  })

  // Si viene desde un equipo con ?equipoId=X&nueva=1
  useEffect(() => {
    const eqId = searchParams.get('equipoId')
    const nueva = searchParams.get('nueva')
    if (eqId && nueva === '1') {
      setRecepcionOpen(true)
      api.get<Equipo>(`/equipos/${eqId}`).then((r) => {
        api.get<EquipoBusquedaResult>('/equipos/buscar', { params: { codigo: r.data.seriePrincipal } }).then((res) => {
          setBusquedaResult(res.data)
          setNoEncontrado(false)
          formRecepcion.setFieldsValue({ equipoId: r.data.equipoId, tipoServicio: 'Diagnóstico', metodoIdentificacion: 'Manual' })
        })
      })
      setSearchParams({})
    }
  }, [searchParams, setSearchParams, formRecepcion])

  useEffect(() => {
    if (detalleOrden) {
      formTecnico.setFieldsValue({
        tipoServicio: detalleOrden.tipoServicio,
        tecnicoId: detalleOrden.tecnicoId,
        supervisorId: detalleOrden.supervisorId,
        fechaCompromiso: detalleOrden.fechaCompromiso ? dayjs(detalleOrden.fechaCompromiso) : undefined,
        diagnostico: detalleOrden.diagnostico,
        actividadesRealizadas: detalleOrden.actividadesRealizadas,
        observacionesTecnicas: detalleOrden.observacionesTecnicas,
        costoManoObra: detalleOrden.costoManoObra,
        nombreClienteFirma: detalleOrden.nombreClienteFirma,
        refacciones: detalleOrden.refacciones.map((r) => ({
          productoId: r.productoId,
          cantidad: r.cantidad,
          precioUnitario: r.precioUnitario,
        })),
      })
      setFirmaTec(detalleOrden.firmaTecnico ?? '')
      setFirmaSup(detalleOrden.firmaSupervisor ?? '')
      setFirmaCli(detalleOrden.firmaCliente ?? '')
    }
  }, [detalleOrden, formTecnico])

  const buscarEquipo = async (codigo: string) => {
    if (!codigo.trim()) return
    try {
      const res = (await api.get<EquipoBusquedaResult>('/equipos/buscar', { params: { codigo: codigo.trim() } })).data
      if (res.encontrado && res.equipo) {
        setBusquedaResult(res)
        setNoEncontrado(false)
        formRecepcion.setFieldsValue({ equipoId: res.equipo.equipoId })
        message.success(`Equipo localizado: ${res.equipo.marca} ${res.equipo.modelo} (${res.equipo.seriePrincipal})`)
      } else {
        setBusquedaResult(null)
        setNoEncontrado(true)
        message.warning('No se encontró un equipo con ese identificador. Puede registrar cliente y equipo sin salir de esta ventana.')
      }
    } catch (e) {
      message.error(errorMessage(e))
    }
  }

  const crearOrden = useMutation({
    mutationFn: (v: Record<string, unknown>) =>
      api.post<OrdenServicio>('/ordenes-servicio', {
        ...v,
        metodoIdentificacion: metodoId,
        fechaCompromiso: v.fechaCompromiso ? (v.fechaCompromiso as dayjs.Dayjs).format('YYYY-MM-DD') : null,
        fotografiasIniciales: fotosRecepcion,
      }),
    onSuccess: (res) => {
      message.success(`Orden de Servicio generada: ${res.data.folio}`)
      setRecepcionOpen(false)
      setBusquedaResult(null)
      setFotosRecepcion([])
      formRecepcion.resetFields()
      qc.invalidateQueries({ queryKey: ['ordenes-servicio'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const crearClienteRapido = useMutation({
    mutationFn: (v: Record<string, unknown>) => api.post<Cliente>('/clientes', v),
    onSuccess: (res) => {
      message.success(`Cliente registrado: ${res.data.razonSocial}`)
      setNuevoClienteOpen(false)
      formCliente.resetFields()
      qc.invalidateQueries({ queryKey: ['clientes-select-os'] })
      setNuevoEquipoOpen(true)
      formEquipo.setFieldsValue({ clienteId: res.data.clienteId, seriePrincipal: codigoBusqueda })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const crearEquipoRapido = useMutation({
    mutationFn: (v: Record<string, unknown>) => api.post<Equipo>('/equipos', v),
    onSuccess: async (res) => {
      message.success(`Equipo registrado: ${res.data.marca} ${res.data.modelo}`)
      setNuevoEquipoOpen(false)
      formEquipo.resetFields()
      qc.invalidateQueries({ queryKey: ['equipos-select-os'] })
      await buscarEquipo(res.data.seriePrincipal)
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const guardarTecnico = useMutation({
    mutationFn: (v: Record<string, unknown>) =>
      api.put(`/ordenes-servicio/${detalleId}/tecnico`, {
        ...v,
        fechaCompromiso: v.fechaCompromiso ? (v.fechaCompromiso as dayjs.Dayjs).format('YYYY-MM-DD') : null,
        firmaTecnico: firmaTec || undefined,
        firmaSupervisor: firmaSup || undefined,
        firmaCliente: firmaCli || undefined,
      }),
    onSuccess: () => {
      message.success('Expediente técnico, refacciones y firmas actualizados')
      qc.invalidateQueries({ queryKey: ['orden-servicio-detalle', detalleId] })
      qc.invalidateQueries({ queryKey: ['ordenes-servicio'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const cambiarEstatus = useMutation({
    mutationFn: (v: { nuevoEstatus: string; observaciones?: string }) =>
      api.post(`/ordenes-servicio/${estatusModal!.ordenServicioId}/estatus`, v),
    onSuccess: () => {
      message.success('Estatus de la orden actualizado y registrado en bitácora')
      setEstatusModal(null)
      formEstatus.resetFields()
      qc.invalidateQueries({ queryKey: ['ordenes-servicio'] })
      if (detalleId) qc.invalidateQueries({ queryKey: ['orden-servicio-detalle', detalleId] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const subirFotoDetalle = useMutation({
    mutationFn: (f: { categoria: string; descripcion: string; imagenUrl: string }) =>
      api.post<OrdenServicioFoto>(`/ordenes-servicio/${detalleId}/fotos`, f),
    onSuccess: () => {
      message.success('Evidencia fotográfica agregada a la orden')
      qc.invalidateQueries({ queryKey: ['orden-servicio-detalle', detalleId] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12, flexWrap: 'wrap', gap: 8 }}>
        <div>
          <h1 className="page-title" style={{ marginBottom: 2 }}>Módulo de Órdenes de Servicio y Taller</h1>
          <Typography.Text type="secondary">Recepción de equipos (OS-2026-XXXXXX), diagnóstico, reparaciones, calibración y reporte técnico PDF</Typography.Text>
        </div>
        <Button
          type="primary"
          size="large"
          icon={<PlusOutlined />}
          onClick={() => {
            setRecepcionOpen(true)
            setBusquedaResult(null)
            setNoEncontrado(false)
            setFotosRecepcion([])
            formRecepcion.resetFields()
            formRecepcion.setFieldsValue({
              tipoServicio: 'Diagnóstico',
              accesoriosRecibidos: ['Batería', 'Cargador', 'Estuche'],
              condicionEquipo: ['Sin daños visibles'],
            })
          }}
        >
          Recepción de Equipo (Nueva Orden)
        </Button>
      </div>

      <div className="toolbar">
        <Space wrap>
          <Input.Search
            placeholder="Folio OS, serie, marca, modelo o cliente"
            allowClear
            style={{ width: 320 }}
            onSearch={(v) => {
              setQ(v)
              setPage(1)
            }}
          />
          <Select
            placeholder="Filtrar por Estatus"
            allowClear
            style={{ width: 200 }}
            onChange={(v) => {
              setEstatusFiltro(v)
              setPage(1)
            }}
            options={ESTATUS_FLUJO.map((e) => ({ value: e, label: e }))}
          />
          <Select
            placeholder="Tipo de Servicio"
            allowClear
            style={{ width: 200 }}
            onChange={(v) => {
              setTipoFiltro(v)
              setPage(1)
            }}
            options={TIPOS_SERVICIO.map((t) => ({ value: t, label: t }))}
          />
        </Space>
      </div>

      <Table
        rowKey="ordenServicioId"
        loading={isLoading}
        dataSource={data?.items}
        size="middle"
        bordered
        scroll={{ x: 'max-content' }}
        pagination={{ current: page, pageSize: 15, total: data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: 'Folio OS', dataIndex: 'folio', width: 150, render: (v: string) => <strong>{v}</strong> },
          { title: 'Recepción', dataIndex: 'fechaRecepcion', width: 140, render: (v: string) => dayjs(v).format('DD/MM/YYYY HH:mm') },
          { title: 'Cliente', dataIndex: 'cliente' },
          { title: 'Equipo', render: (_, r) => `${r.tipoEquipo} · ${r.marca} ${r.modelo}` },
          { title: 'Serie Principal', dataIndex: 'seriePrincipal', render: (v: string) => <strong>{v}</strong> },
          { title: 'Servicio', dataIndex: 'tipoServicio', render: (v: string) => <Tag color="geekblue">{v}</Tag> },
          {
            title: 'Estatus Actual',
            dataIndex: 'estatus',
            render: (v: string) => <Tag color={ESTATUS_COLOR[v] ?? 'blue'}>{v}</Tag>,
          },
          { title: 'Técnico', dataIndex: 'tecnicoNombre', render: (v?: string) => v ?? '—' },
          {
            title: 'Total',
            dataIndex: 'total',
            align: 'right',
            render: (v: number) => `$${(v || 0).toLocaleString('es-MX', { minimumFractionDigits: 2 })}`,
          },
          {
            title: 'Acciones',
            fixed: 'right',
            width: 270,
            render: (_, r) => (
              <Space size={4}>
                <Button size="small" type="primary" icon={<ToolOutlined />} onClick={() => setDetalleId(r.ordenServicioId)}>
                  Taller / Detalle
                </Button>
                <Button
                  size="small"
                  icon={<SwapOutlined />}
                  onClick={() => {
                    setEstatusModal(r)
                    formEstatus.setFieldsValue({ nuevoEstatus: r.estatus, observaciones: '' })
                  }}
                >
                  Estatus
                </Button>
                <Button size="small" icon={<DownloadOutlined />} onClick={() => downloadOrdenPdf(r.ordenServicioId, r.folio)}>
                  PDF
                </Button>
              </Space>
            ),
          },
        ]}
      />

      {/* ───────────── MODAL DE RECEPCIÓN DE EQUIPOS ───────────── */}
      <Modal
        open={recepcionOpen}
        onCancel={() => setRecepcionOpen(false)}
        title="Recepción de Equipo en Taller · Generar Orden de Servicio (OS-AAAA-XXXXXX)"
        width={980}
        onOk={() => formRecepcion.submit()}
        confirmLoading={crearOrden.isPending}
        okText="Generar Orden de Servicio"
        cancelText="Cancelar"
        destroyOnClose
      >
        <Card size="small" title="1. Identificación y Localización del Equipo" style={{ marginBottom: 14, background: '#f8fafc' }}>
          <Row gutter={[12, 12]} align="middle">
            <Col xs={24} md={7}>
              <Select
                value={metodoId}
                onChange={setMetodoId}
                style={{ width: '100%' }}
                options={[
                  { value: 'CodigoQR', label: 'Código QR (Ej. EQ-000001)' },
                  { value: 'CodigoBarras', label: 'Código de Barras' },
                  { value: 'NumeroSerie', label: 'Número de Serie / IMEI' },
                  { value: 'NumeroInterno', label: 'Número Interno (Ej. INT-001)' },
                  { value: 'Manual', label: 'Búsqueda Manual en Catálogo' },
                ]}
              />
            </Col>
            <Col xs={24} md={11}>
              <Input.Search
                placeholder="Escanee o escriba QR, código de barras, serie o no. interno..."
                enterButton={<><SearchOutlined /> Localizar y Autocompletar</>}
                value={codigoBusqueda}
                onChange={(e) => setCodigoBusqueda(e.target.value)}
                onSearch={buscarEquipo}
              />
            </Col>
            <Col xs={24} md={6}>
              <Space>
                <Button size="small" icon={<UserAddOutlined />} onClick={() => setNuevoClienteOpen(true)}>
                  + Cliente
                </Button>
                <Button
                  size="small"
                  type="dashed"
                  icon={<PlusOutlined />}
                  onClick={() => {
                    formEquipo.setFieldsValue({ seriePrincipal: codigoBusqueda })
                    setNuevoEquipoOpen(true)
                  }}
                >
                  + Equipo
                </Button>
              </Space>
            </Col>
          </Row>

          {noEncontrado && (
            <Alert
              style={{ marginTop: 12 }}
              type="warning"
              showIcon
              message={`No se encontró ningún equipo con "${codigoBusqueda}".`}
              description="Puede registrarlo inmediatamente sin abandonar esta captura:"
              action={
                <Space>
                  <Button size="small" type="primary" onClick={() => setNuevoClienteOpen(true)}>
                    Registrar nuevo cliente
                  </Button>
                  <Button
                    size="small"
                    onClick={() => {
                      formEquipo.setFieldsValue({ seriePrincipal: codigoBusqueda })
                      setNuevoEquipoOpen(true)
                    }}
                  >
                    Registrar nuevo equipo
                  </Button>
                </Space>
              }
            />
          )}

          {busquedaResult?.encontrado && busquedaResult.equipo && (
            <Card size="small" style={{ marginTop: 12, borderColor: '#0A2A8F' }}>
              <Descriptions size="small" column={{ xs: 1, sm: 3 }} title={<><CheckCircleOutlined style={{ color: '#2E7D32' }} /> Equipo Identificado · Autocompletado Exitoso</>}>
                <Descriptions.Item label="Cliente Propietario"><strong>{busquedaResult.cliente?.razonSocial}</strong> ({busquedaResult.cliente?.rfc})</Descriptions.Item>
                <Descriptions.Item label="Tipo de Equipo">{busquedaResult.equipo.tipoEquipo}</Descriptions.Item>
                <Descriptions.Item label="Marca / Modelo"><strong>{busquedaResult.equipo.marca} {busquedaResult.equipo.modelo}</strong></Descriptions.Item>
                <Descriptions.Item label="Series Registradas" span={3}>
                  <Space wrap>
                    {busquedaResult.series.map((s, idx) => (
                      <Tag key={idx} color="blue">{s.tipoSerie}: <strong>{s.valor}</strong></Tag>
                    ))}
                  </Space>
                </Descriptions.Item>
              </Descriptions>
              {busquedaResult.historialOrdenes.length > 0 && (
                <div style={{ marginTop: 8, fontSize: 12 }}>
                  <strong>Historial Previo: </strong>
                  {busquedaResult.historialOrdenes.slice(0, 3).map((h) => (
                    <Tag key={h.ordenServicioId}>{h.folio} · {h.tipoServicio} ({dayjs(h.fechaRecepcion).format('DD/MM/YYYY')})</Tag>
                  ))}
                </div>
              )}
            </Card>
          )}
        </Card>

        <Form form={formRecepcion} layout="vertical" onFinish={(v) => crearOrden.mutate(v)}>
          <Row gutter={12}>
            <Col xs={24} md={10}>
              <Form.Item name="equipoId" label="Equipo seleccionado (o búsqueda manual)" rules={[{ required: true, message: 'Seleccione o identifique un equipo' }]}>
                <Select
                  showSearch
                  optionFilterProp="label"
                  placeholder="Seleccione equipo manualmente..."
                  onChange={(val) => {
                    const eq = equiposList?.find((e) => e.equipoId === val)
                    if (eq) buscarEquipo(eq.seriePrincipal)
                  }}
                  options={equiposList?.map((e) => ({
                    value: e.equipoId,
                    label: `${e.seriePrincipal} · ${e.tipoEquipo} ${e.marca} ${e.modelo} · ${e.cliente}`,
                  }))}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={7}>
              <Form.Item name="tipoServicio" label="Tipo de Servicio Principal" rules={[{ required: true }]}>
                <Select options={TIPOS_SERVICIO.map((t) => ({ value: t, label: t }))} />
              </Form.Item>
            </Col>
            <Col xs={24} md={7}>
              <Form.Item name="tiposServicioAdicionales" label="Servicios Adicionales">
                <Select mode="multiple" placeholder="Opcional" options={TIPOS_SERVICIO.map((t) => ({ value: t, label: t }))} />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={12}>
            <Col xs={24} md={8}>
              <Form.Item name="tecnicoId" label="Técnico Responsable Asignado">
                <Select
                  allowClear
                  placeholder="Asignar técnico"
                  options={usuariosList?.map((u) => ({ value: u.usuarioId, label: `${u.nombre} (${u.rol})` }))}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={8}>
              <Form.Item name="supervisorId" label="Supervisor de Laboratorio">
                <Select
                  allowClear
                  placeholder="Asignar supervisor"
                  options={usuariosList?.map((u) => ({ value: u.usuarioId, label: `${u.nombre} (${u.rol})` }))}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={8}>
              <Form.Item name="fechaCompromiso" label="Fecha Compromiso de Entrega">
                <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col xs={24} md={14}>
              <Card size="small" title="Recepción de Accesorios (Checklist Configurable)" style={{ marginBottom: 12 }}>
                <Form.Item name="accesoriosRecibidos" style={{ marginBottom: 8 }}>
                  <Checkbox.Group style={{ width: '100%' }}>
                    <Row gutter={[8, 6]}>
                      {ACCESORIOS_BASE.map((item) => (
                        <Col span={8} key={item}>
                          <Checkbox value={item}>{item}</Checkbox>
                        </Col>
                      ))}
                    </Row>
                  </Checkbox.Group>
                </Form.Item>
                <Form.Item name="accesoriosPersonalizados" label="Accesorios personalizados adicionales" style={{ marginBottom: 0 }}>
                  <Input placeholder="Ej. Arnés, Antena látigo extra, Tribrach adicional..." />
                </Form.Item>
              </Card>
            </Col>

            <Col xs={24} md={10}>
              <Card size="small" title="Condición Física del Equipo" style={{ marginBottom: 12 }}>
                <Form.Item name="condicionEquipo" style={{ marginBottom: 0 }}>
                  <Checkbox.Group style={{ width: '100%' }}>
                    <Row gutter={[8, 6]}>
                      {CONDICIONES_BASE.map((item) => (
                        <Col span={12} key={item}>
                          <Checkbox value={item}>{item}</Checkbox>
                        </Col>
                      ))}
                    </Row>
                  </Checkbox.Group>
                </Form.Item>
              </Card>
            </Col>
          </Row>

          <Row gutter={12}>
            <Col xs={24} md={12}>
              <Form.Item name="fallaReportada" label="Falla Reportada por el Cliente / Solicitud">
                <Input.TextArea rows={2} placeholder="Describa el motivo de ingreso o síntomas reportados..." />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item name="observacionesRecepcion" label="Observaciones de Recepción">
                <Input.TextArea rows={2} placeholder="Notas sobre estado físico, sellos o accesorios..." />
              </Form.Item>
            </Col>
          </Row>

          <Card size="small" title={<><CameraOutlined /> Evidencia Fotográfica de Recepción</>}>
            <Row gutter={8} align="middle">
              <Col xs={24} md={6}>
                <Select
                  value={catFotoNueva}
                  onChange={setCatFotoNueva}
                  style={{ width: '100%' }}
                  options={CATEGORIAS_FOTO.map((c) => ({ value: c, label: c }))}
                />
              </Col>
              <Col xs={24} md={10}>
                <Input
                  placeholder="Descripción de la fotografía (opcional)"
                  value={descFotoNueva}
                  onChange={(e) => setDescFotoNueva(e.target.value)}
                />
              </Col>
              <Col xs={24} md={8}>
                <Upload
                  accept="image/*"
                  showUploadList={false}
                  beforeUpload={(file) => {
                    const reader = new FileReader()
                    reader.onload = () => {
                      setFotosRecepcion((prev) => [
                        ...prev,
                        { categoria: catFotoNueva, descripcion: descFotoNueva, imagenUrl: String(reader.result) },
                      ])
                      setDescFotoNueva('')
                    }
                    reader.readAsDataURL(file)
                    return false
                  }}
                >
                  <Button icon={<CameraOutlined />}>Capturar / Adjuntar Fotografía</Button>
                </Upload>
              </Col>
            </Row>
            {fotosRecepcion.length > 0 && (
              <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginTop: 10 }}>
                {fotosRecepcion.map((f, idx) => (
                  <Card key={idx} size="small" style={{ width: 150 }}>
                    <img src={f.imagenUrl} alt={f.categoria} style={{ width: '100%', height: 80, objectFit: 'cover' }} />
                    <Tag color="blue" style={{ marginTop: 4 }}>{f.categoria}</Tag>
                    <div style={{ fontSize: 11 }}>{f.descripcion}</div>
                    <Button
                      size="small"
                      danger
                      type="link"
                      onClick={() => setFotosRecepcion((prev) => prev.filter((_, i) => i !== idx))}
                    >
                      Quitar
                    </Button>
                  </Card>
                ))}
              </div>
            )}
          </Card>
        </Form>
      </Modal>

      {/* ───────────── SUB-MODAL: REGISTRAR NUEVO CLIENTE SIN ABANDONAR CAPTURA ───────────── */}
      <Modal
        open={nuevoClienteOpen}
        title="Registrar Nuevo Cliente (Sin abandonar la recepción)"
        onCancel={() => setNuevoClienteOpen(false)}
        onOk={() => formCliente.submit()}
        confirmLoading={crearClienteRapido.isPending}
        okText="Guardar Cliente y Continuar"
        destroyOnClose
      >
        <Form form={formCliente} layout="vertical" initialValues={{ pais: 'México' }} onFinish={(v) => crearClienteRapido.mutate(v)}>
          <Form.Item name="razonSocial" label="Razón social" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="RFC" label="RFC" rules={[{ required: true, pattern: /^[A-ZÑ&a-zñ]{3,4}\d{6}[A-Za-z0-9]{3}$/, message: 'RFC inválido' }]}>
            <Input style={{ textTransform: 'uppercase' }} maxLength={13} />
          </Form.Item>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Form.Item name="contacto" label="Contacto"><Input /></Form.Item>
            <Form.Item name="telefono" label="Teléfono"><Input /></Form.Item>
            <Form.Item name="correo" label="Correo"><Input /></Form.Item>
            <Form.Item name="ciudad" label="Ciudad"><Input /></Form.Item>
          </div>
        </Form>
      </Modal>

      {/* ───────────── SUB-MODAL: REGISTRAR NUEVO EQUIPO SIN ABANDONAR CAPTURA ───────────── */}
      <Modal
        open={nuevoEquipoOpen}
        title="Registrar Nuevo Equipo Topográfico (Sin abandonar la recepción)"
        onCancel={() => setNuevoEquipoOpen(false)}
        onOk={() => formEquipo.submit()}
        confirmLoading={crearEquipoRapido.isPending}
        okText="Guardar Equipo y Seleccionar"
        width={680}
        destroyOnClose
      >
        <Form form={formEquipo} layout="vertical" onFinish={(v) => crearEquipoRapido.mutate(v)}>
          <Form.Item name="clienteId" label="Cliente Propietario" rules={[{ required: true }]}>
            <Select
              showSearch
              optionFilterProp="label"
              options={clientesList?.map((c) => ({ value: c.clienteId, label: `${c.razonSocial} (${c.rfc})` }))}
            />
          </Form.Item>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Form.Item name="tipoEquipo" label="Tipo de Equipo" rules={[{ required: true }]}>
              <Select options={tiposEquipo?.map((t) => ({ value: t.value, label: t.nombre }))} />
            </Form.Item>
            <Form.Item name="marca" label="Marca" rules={[{ required: true }]}><Input /></Form.Item>
            <Form.Item name="modelo" label="Modelo" rules={[{ required: true }]}><Input /></Form.Item>
            <Form.Item name="seriePrincipal" label="Serie Principal" rules={[{ required: true }]}><Input /></Form.Item>
            <Form.Item name="numeroInterno" label="Número Interno"><Input /></Form.Item>
            <Form.Item name="serieSecundaria" label="Serie Radio / Controladora"><Input /></Form.Item>
          </div>
        </Form>
      </Modal>

      {/* ───────────── MODAL DE CAMBIO DE ESTATUS ───────────── */}
      <Modal
        open={!!estatusModal}
        title={`Cambiar Estatus · ${estatusModal?.folio ?? ''}`}
        onCancel={() => setEstatusModal(null)}
        onOk={() => formEstatus.submit()}
        confirmLoading={cambiarEstatus.isPending}
        okText="Registrar Cambio de Estatus"
        destroyOnClose
      >
        <Form form={formEstatus} layout="vertical" onFinish={(v) => cambiarEstatus.mutate(v)}>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message="Cada cambio de estatus registra Fecha, Hora, Usuario y Observaciones. Al pasar a Terminado, Listo para Entrega o Entregado se descuentan automáticamente las refacciones del inventario."
          />
          <Form.Item name="nuevoEstatus" label="Nuevo Estatus" rules={[{ required: true }]}>
            <Select options={ESTATUS_FLUJO.map((e) => ({ value: e, label: e }))} />
          </Form.Item>
          <Form.Item name="observaciones" label="Observaciones / Bitácora del cambio" rules={[{ required: true, message: 'Ingrese observaciones del cambio de estatus' }]}>
            <Input.TextArea rows={3} placeholder="Indique el avance, motivo o resultado de la etapa..." />
          </Form.Item>
        </Form>
      </Modal>

      {/* ───────────── DRAWER: GESTIÓN TÉCNICA, REFACCIONES, EVIDENCIA Y REPORTE PDF ───────────── */}
      <Drawer
        open={!!detalleId}
        onClose={() => setDetalleId(null)}
        width={960}
        title={
          detalleOrden ? (
            <Space>
              <span>Orden de Servicio <strong>{detalleOrden.folio}</strong></span>
              <Tag color={ESTATUS_COLOR[detalleOrden.estatus] ?? 'blue'}>{detalleOrden.estatus}</Tag>
              <Tag color="geekblue">{detalleOrden.tipoServicio}</Tag>
            </Space>
          ) : 'Cargando orden...'
        }
        extra={
          detalleOrden && (
            <Space>
              <Button icon={<EyeOutlined />} onClick={() => nav(`/equipos/${detalleOrden.equipoId}`)}>
                Expediente 360°
              </Button>
              <Button icon={<FileProtectOutlined />} onClick={() => nav(`/certificados?equipoId=${detalleOrden.equipoId}&ordenId=${detalleOrden.ordenServicioId}`)}>
                Emitir Certificado
              </Button>
              <Button type="primary" icon={<DownloadOutlined />} onClick={() => downloadOrdenPdf(detalleOrden.ordenServicioId, detalleOrden.folio)}>
                Reporte Técnico PDF
              </Button>
            </Space>
          )
        }
      >
        {detalleOrden && (
          <Tabs
            items={[
              {
                key: 'tecnico',
                label: 'Diagnóstico, Refacciones y Firmas',
                children: (
                  <Form form={formTecnico} layout="vertical" onFinish={(v) => guardarTecnico.mutate(v)}>
                    <Card size="small" style={{ marginBottom: 12, background: '#f8fafc' }}>
                      <Descriptions size="small" column={3}>
                        <Descriptions.Item label="Cliente"><strong>{detalleOrden.cliente}</strong></Descriptions.Item>
                        <Descriptions.Item label="Equipo"><strong>{detalleOrden.tipoEquipo} {detalleOrden.marca} {detalleOrden.modelo}</strong></Descriptions.Item>
                        <Descriptions.Item label="Serie Principal"><QrcodeOutlined /> <strong>{detalleOrden.seriePrincipal}</strong></Descriptions.Item>
                        <Descriptions.Item label="Condición Recepción">{detalleOrden.condicionEquipo.join(', ') || 'Normal'}</Descriptions.Item>
                        <Descriptions.Item label="Accesorios">{detalleOrden.accesoriosRecibidos.join(', ') || 'Ninguno'}</Descriptions.Item>
                        <Descriptions.Item label="Falla Reportada">{detalleOrden.fallaReportada ?? '—'}</Descriptions.Item>
                      </Descriptions>
                    </Card>

                    <Row gutter={12}>
                      <Col xs={24} md={8}>
                        <Form.Item name="tipoServicio" label="Tipo de Servicio">
                          <Select options={TIPOS_SERVICIO.map((t) => ({ value: t, label: t }))} />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={8}>
                        <Form.Item name="tecnicoId" label="Técnico Responsable">
                          <Select allowClear options={usuariosList?.map((u) => ({ value: u.usuarioId, label: u.nombre }))} />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={8}>
                        <Form.Item name="supervisorId" label="Supervisor">
                          <Select allowClear options={usuariosList?.map((u) => ({ value: u.usuarioId, label: u.nombre }))} />
                        </Form.Item>
                      </Col>
                    </Row>

                    <Form.Item name="diagnostico" label="Diagnóstico Técnico">
                      <Input.TextArea rows={3} placeholder="Detalle técnico del diagnóstico realizado al equipo..." />
                    </Form.Item>

                    <Form.Item name="actividadesRealizadas" label="Actividades Realizadas en Laboratorio / Taller">
                      <Input.TextArea rows={3} placeholder="Limpieza óptica, ajuste colimación, reemplazo de componentes, actualización de firmware..." />
                    </Form.Item>

                    <Divider>Integración Inventario - Taller (Refacciones Utilizadas)</Divider>
                    <Typography.Paragraph type="secondary" style={{ marginTop: -8, fontSize: 12 }}>
                      Al cerrar/terminar la orden se descuentan automáticamente las existencias del almacén y se genera el movimiento en Kardex «Consumo por Orden de Servicio · {detalleOrden.folio}».
                    </Typography.Paragraph>

                    <Form.List name="refacciones">
                      {(fields, { add, remove }) => (
                        <>
                          {fields.map(({ key, name, ...restField }) => (
                            <div key={key} style={{ display: 'grid', gridTemplateColumns: '1fr 120px 150px 40px', gap: 8, marginBottom: 8 }}>
                              <Form.Item {...restField} name={[name, 'productoId']} style={{ margin: 0 }} rules={[{ required: true }]}>
                                <Select
                                  showSearch
                                  optionFilterProp="label"
                                  placeholder="Seleccionar refacción de inventario..."
                                  onChange={(prodId) => {
                                    const p = productosRefacciones?.find((x) => x.productoId === prodId)
                                    if (p) {
                                      const curr = formTecnico.getFieldValue('refacciones')
                                      curr[name].precioUnitario = p.precioVenta
                                      formTecnico.setFieldsValue({ refacciones: [...curr] })
                                    }
                                  }}
                                  options={productosRefacciones?.map((p) => ({
                                    value: p.productoId,
                                    label: `${p.sku} · ${p.nombre} (Stock: ${p.stockActual} · $${p.precioVenta})`,
                                  }))}
                                />
                              </Form.Item>
                              <Form.Item {...restField} name={[name, 'cantidad']} style={{ margin: 0 }} rules={[{ required: true }]}>
                                <InputNumber min={1} style={{ width: '100%' }} placeholder="Cant." />
                              </Form.Item>
                              <Form.Item {...restField} name={[name, 'precioUnitario']} style={{ margin: 0 }}>
                                <InputNumber min={0} style={{ width: '100%' }} prefix="$" placeholder="Precio Unit." />
                              </Form.Item>
                              <Button danger icon={<DeleteOutlined />} onClick={() => remove(name)} />
                            </div>
                          ))}
                          <Button type="dashed" onClick={() => add({ cantidad: 1 })} block icon={<PlusOutlined />} style={{ marginBottom: 14 }}>
                            Agregar Refacción o Componente de Inventario
                          </Button>
                        </>
                      )}
                    </Form.List>

                    <Row gutter={12}>
                      <Col xs={24} md={8}>
                        <Form.Item name="costoManoObra" label="Costo Mano de Obra / Servicio ($)">
                          <InputNumber min={0} style={{ width: '100%' }} prefix="$" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={8}>
                        <Form.Item name="nombreClienteFirma" label="Nombre de quien recibe (Cliente)">
                          <Input placeholder="Nombre completo" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={8}>
                        <Card size="small">
                          <div>Subtotal Refacciones: <strong>${detalleOrden.subtotalRefacciones.toLocaleString('es-MX', { minimumFractionDigits: 2 })}</strong></div>
                          <div>Total Orden: <strong style={{ color: '#0A2A8F', fontSize: 16 }}>${detalleOrden.total.toLocaleString('es-MX', { minimumFractionDigits: 2 })}</strong></div>
                        </Card>
                      </Col>
                    </Row>

                    <Form.Item name="observacionesTecnicas" label="Observaciones y Recomendaciones Finales">
                      <Input.TextArea rows={2} />
                    </Form.Item>

                    <Divider>Firmas del Reporte Técnico</Divider>
                    <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
                      <Col xs={24} md={8}>
                        <SignaturePad label="Firma Técnico" value={firmaTec} onChange={setFirmaTec} />
                      </Col>
                      <Col xs={24} md={8}>
                        <SignaturePad label="Firma Supervisor" value={firmaSup} onChange={setFirmaSup} />
                      </Col>
                      <Col xs={24} md={8}>
                        <SignaturePad label="Firma Cliente" value={firmaCli} onChange={setFirmaCli} />
                      </Col>
                    </Row>

                    <Button type="primary" htmlType="submit" loading={guardarTecnico.isPending} size="large" block>
                      Guardar Diagnóstico, Refacciones y Firmas
                    </Button>
                  </Form>
                ),
              },
              {
                key: 'fotos',
                label: `Evidencia Fotográfica (${detalleOrden.fotografias.length})`,
                children: (
                  <div>
                    <Card size="small" title="Agregar Nueva Evidencia Fotográfica" style={{ marginBottom: 14 }}>
                      <Space wrap>
                        <Select
                          value={catFotoNueva}
                          onChange={setCatFotoNueva}
                          style={{ width: 200 }}
                          options={CATEGORIAS_FOTO.map((c) => ({ value: c, label: c }))}
                        />
                        <Input
                          placeholder="Descripción de la foto"
                          style={{ width: 280 }}
                          value={descFotoNueva}
                          onChange={(e) => setDescFotoNueva(e.target.value)}
                        />
                        <Upload
                          accept="image/*"
                          showUploadList={false}
                          beforeUpload={(file) => {
                            const reader = new FileReader()
                            reader.onload = () => {
                              subirFotoDetalle.mutate({
                                categoria: catFotoNueva,
                                descripcion: descFotoNueva,
                                imagenUrl: String(reader.result),
                              })
                              setDescFotoNueva('')
                            }
                            reader.readAsDataURL(file)
                            return false
                          }}
                        >
                          <Button type="primary" icon={<CameraOutlined />} loading={subirFotoDetalle.isPending}>
                            Subir Fotografía
                          </Button>
                        </Upload>
                      </Space>
                    </Card>

                    <Image.PreviewGroup>
                      <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
                        {detalleOrden.fotografias.map((f) => (
                          <Card key={f.ordenServicioFotoId} size="small" style={{ width: 210 }}>
                            <Image src={f.imagenUrl} height={130} style={{ objectFit: 'cover', width: '100%' }} />
                            <div style={{ marginTop: 6 }}>
                              <Tag color="blue">{f.categoria}</Tag>
                              <div style={{ fontSize: 12 }}>{f.descripcion}</div>
                              <div style={{ fontSize: 11, color: '#6b7280' }}>
                                {dayjs(f.fecha).format('DD/MM/YYYY HH:mm')} · {f.usuarioNombre}
                              </div>
                            </div>
                          </Card>
                        ))}
                      </div>
                    </Image.PreviewGroup>
                  </div>
                ),
              },
              {
                key: 'bitacora',
                label: `Flujo y Bitácora de Estatus (${detalleOrden.historialEstatus.length})`,
                children: (
                  <div>
                    <div style={{ marginBottom: 16 }}>
                      <Button
                        type="primary"
                        icon={<SwapOutlined />}
                        onClick={() => {
                          setEstatusModal(detalleOrden)
                          formEstatus.setFieldsValue({ nuevoEstatus: detalleOrden.estatus, observaciones: '' })
                        }}
                      >
                        Cambiar Estatus de la Orden
                      </Button>
                    </div>
                    <Timeline
                      items={detalleOrden.historialEstatus.map((h) => ({
                        color: h.estatusNuevo === 'Entregado' ? 'green' : h.estatusNuevo === 'Cancelado' ? 'red' : 'blue',
                        children: (
                          <div>
                            <Space>
                              {h.estatusAnterior && <Tag>{h.estatusAnterior}</Tag>}
                              <span>→</span>
                              <Tag color={ESTATUS_COLOR[h.estatusNuevo] ?? 'blue'}><strong>{h.estatusNuevo}</strong></Tag>
                            </Space>
                            <div style={{ marginTop: 4 }}>{h.observaciones}</div>
                            <div style={{ fontSize: 11, color: '#6b7280' }}>
                              Fecha: {dayjs(h.fecha).format('DD/MM/YYYY')} · Hora: {dayjs(h.fecha).format('HH:mm:ss')} · Usuario: <strong>{h.usuarioNombre ?? 'Sistema'}</strong>
                            </div>
                          </div>
                        ),
                      }))}
                    />
                  </div>
                ),
              },
            ]}
          />
        )}
      </Drawer>
    </>
  )
}
