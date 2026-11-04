import {
  ArrowLeftOutlined,
  DownloadOutlined,
  FilePdfOutlined,
  FileProtectOutlined,
  PictureOutlined,
  QrcodeOutlined,
  StopOutlined,
  ToolOutlined,
  UploadOutlined,
} from '@ant-design/icons'
import {
  Alert,
  App,
  Badge,
  Button,
  Card,
  Col,
  Descriptions,
  Empty,
  Form,
  Image,
  Input,
  Modal,
  Row,
  Select,
  Space,
  Spin,
  Table,
  Tabs,
  Tag,
  Timeline,
  Typography,
  Upload,
} from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, downloadOrdenPdf, downloadPdf, errorMessage } from '../api/client'
import type { ExpedienteEquipo } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { colors } from '../theme'

const fecha = (v?: string) => (v ? dayjs(v).format('DD/MM/YYYY') : '—')
const fechaHora = (v?: string) => (v ? dayjs(v).format('DD/MM/YYYY HH:mm') : '—')
const money = (v: number) =>
  new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' }).format(v || 0)

export default function ExpedienteEquipoPage() {
  const { id } = useParams<{ id: string }>()
  const nav = useNavigate()
  const { hasRole } = useAuth()
  const { message, modal } = App.useApp()
  const qc = useQueryClient()
  const [archivoModal, setArchivoModal] = useState(false)
  const [formArchivo] = Form.useForm()
  const [b64File, setB64File] = useState<{ name: string; dataUrl: string } | null>(null)

  const canEdit = hasRole('Administrador', 'Tecnico')
  const isAdmin = hasRole('Administrador')

  const { data, isLoading, error } = useQuery({
    queryKey: ['expediente-equipo', id],
    queryFn: async () => (await api.get<ExpedienteEquipo>(`/equipos/${id}/expediente`)).data,
    enabled: !!id,
  })

  const subirArchivo = useMutation({
    mutationFn: (v: Record<string, unknown>) =>
      api.post(`/equipos/${id}/archivos`, {
        ...v,
        nombreArchivo: b64File?.name ?? v.nombreArchivo ?? 'archivo',
        archivoUrl: b64File?.dataUrl ?? v.archivoUrl ?? '',
      }),
    onSuccess: () => {
      message.success('Archivo agregado al expediente')
      setArchivoModal(false)
      setB64File(null)
      formArchivo.resetFields()
      qc.invalidateQueries({ queryKey: ['expediente-equipo', id] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  if (isLoading) return <Spin />
  if (error || !data) return <Alert type="error" message="No se encontró el expediente del equipo." />

  const { equipo, cliente, series } = data

  const darDeBaja = () => {
    let motivo = ''
    modal.confirm({
      title: `Dar de baja el equipo ${equipo.marca} ${equipo.modelo} (${equipo.seriePrincipal})`,
      okText: 'Confirmar Baja Definitiva',
      okButtonProps: { danger: true },
      cancelText: 'Cancelar',
      content: (
        <div>
          <p>El equipo pasará a estatus <strong>Baja</strong> conservando todo su historial técnico, certificados y órdenes de servicio.</p>
          <Input.TextArea
            rows={3}
            placeholder="Motivo de la baja (ej. siniestro, obsolescencia, robo, desincorporación)..."
            onChange={(e) => (motivo = e.target.value)}
          />
        </div>
      ),
      onOk: async () => {
        try {
          await api.post(`/equipos/${equipo.equipoId}/baja`, { motivoBaja: motivo })
          message.success('Baja registrada en el expediente')
          qc.invalidateQueries({ queryKey: ['expediente-equipo', id] })
        } catch (e) {
          message.error(errorMessage(e))
          throw e
        }
      },
    })
  }

  const estatusColor =
    equipo.estatus === 'Activo' ? 'green' : equipo.estatus === 'EnTaller' ? 'blue' : equipo.estatus === 'Baja' ? 'red' : 'orange'

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14, flexWrap: 'wrap', gap: 8 }}>
        <Space>
          <Button icon={<ArrowLeftOutlined />} onClick={() => nav('/equipos')}>Equipos</Button>
          <div>
            <h1 className="page-title" style={{ margin: 0 }}>
              Expediente Digital 360° · {equipo.tipoEquipo} {equipo.marca} {equipo.modelo}
            </h1>
            <Space size={8}>
              <Tag color={estatusColor}>{equipo.estatus ?? 'Activo'}</Tag>
              <Typography.Text type="secondary">Serie Principal: <strong>{equipo.seriePrincipal}</strong></Typography.Text>
              <Typography.Text type="secondary">· QR: <code>{equipo.codigoQR}</code></Typography.Text>
              {equipo.numeroInterno && <Typography.Text type="secondary">· No. Interno: <strong>{equipo.numeroInterno}</strong></Typography.Text>}
            </Space>
          </div>
        </Space>
        <Space wrap>
          {canEdit && equipo.estatus !== 'Baja' && (
            <>
              <Button type="primary" icon={<ToolOutlined />} onClick={() => nav(`/ordenes-servicio?equipoId=${equipo.equipoId}&nueva=1`)}>
                Nueva Orden de Servicio
              </Button>
              <Button icon={<FileProtectOutlined />} onClick={() => nav(`/certificados?equipoId=${equipo.equipoId}`)}>
                Emitir Certificado
              </Button>
            </>
          )}
          {canEdit && (
            <Button icon={<UploadOutlined />} onClick={() => setArchivoModal(true)}>
              Subir Archivo / Foto
            </Button>
          )}
          {isAdmin && equipo.estatus !== 'Baja' && (
            <Button danger icon={<StopOutlined />} onClick={darDeBaja}>
              Dar de Baja
            </Button>
          )}
        </Space>
      </div>

      {equipo.estatus === 'Baja' && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 14 }}
          message={`EQUIPO DADO DE BAJA el ${fechaHora(equipo.fechaBaja)} · Motivo: ${equipo.motivoBaja ?? 'Sin especificar'}`}
        />
      )}

      <Row gutter={[16, 16]}>
        <Col xs={24} lg={14}>
          <Card size="small" title="Datos Generales del Equipo">
            <Descriptions size="small" column={{ xs: 1, sm: 2 }} bordered>
              <Descriptions.Item label="Tipo de Equipo">{equipo.tipoEquipo}</Descriptions.Item>
              <Descriptions.Item label="Marca / Modelo"><strong>{equipo.marca} {equipo.modelo}</strong></Descriptions.Item>
              <Descriptions.Item label="Serie Principal"><strong>{equipo.seriePrincipal}</strong></Descriptions.Item>
              <Descriptions.Item label="Número Interno">{equipo.numeroInterno ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="Código QR"><QrcodeOutlined /> {equipo.codigoQR}</Descriptions.Item>
              <Descriptions.Item label="Código de Barras">{equipo.codigoBarras ?? equipo.seriePrincipal}</Descriptions.Item>
              <Descriptions.Item label="Fecha de Compra">{fecha(equipo.fechaCompra)}</Descriptions.Item>
              <Descriptions.Item label="Estatus Operativo"><Badge status={equipo.estatus === 'Activo' ? 'success' : 'processing'} text={equipo.estatus} /></Descriptions.Item>
              <Descriptions.Item label="Observaciones" span={2}>{equipo.observaciones ?? '—'}</Descriptions.Item>
            </Descriptions>
          </Card>
        </Col>
        <Col xs={24} lg={10}>
          <Card size="small" title="Cliente Propietario">
            <Descriptions size="small" column={1} bordered>
              <Descriptions.Item label="Razón Social"><strong>{cliente.razonSocial}</strong></Descriptions.Item>
              <Descriptions.Item label="Nombre Comercial">{cliente.nombreComercial ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="RFC"><code>{cliente.rfc}</code></Descriptions.Item>
              <Descriptions.Item label="Contacto">{cliente.contacto ?? '—'} · {cliente.telefono ?? ''}</Descriptions.Item>
              <Descriptions.Item label="Correo">{cliente.correo ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="Ubicación">
                {[cliente.direccion, cliente.ciudad, cliente.estado, cliente.pais].filter(Boolean).join(', ') || '—'}
              </Descriptions.Item>
            </Descriptions>
          </Card>
        </Col>
      </Row>

      <Card size="small" style={{ marginTop: 16 }}>
        <Tabs
          items={[
            {
              key: 'series',
              label: `Series (${series.length})`,
              children: (
                <Table
                  size="small"
                  rowKey={(r, i) => `${r.tipoSerie}-${r.valor}-${i}`}
                  dataSource={series}
                  pagination={false}
                  bordered
                  columns={[
                    { title: 'Tipo de Serie / Identificador', dataIndex: 'tipoSerie', width: 240, render: (v: string) => <Tag color="blue">{v}</Tag> },
                    { title: 'Número de Serie / IMEI', dataIndex: 'valor', render: (v: string) => <strong>{v}</strong> },
                    { title: 'Observaciones', dataIndex: 'observaciones', render: (v?: string) => v ?? '—' },
                  ]}
                />
              ),
            },
            {
              key: 'ordenes',
              label: `Órdenes de Servicio (${data.ordenesServicio.length})`,
              children: (
                <Table
                  size="small"
                  rowKey="ordenServicioId"
                  dataSource={data.ordenesServicio}
                  bordered
                  columns={[
                    { title: 'Folio OS', dataIndex: 'folio', render: (v: string) => <strong>{v}</strong> },
                    { title: 'Recepción', dataIndex: 'fechaRecepcion', render: fechaHora },
                    { title: 'Tipo de Servicio', dataIndex: 'tipoServicio', render: (v: string) => <Tag color="geekblue">{v}</Tag> },
                    { title: 'Estatus', dataIndex: 'estatus', render: (v: string) => <Tag color={v === 'Entregado' ? 'green' : v === 'Cancelado' ? 'red' : 'processing'}>{v}</Tag> },
                    { title: 'Diagnóstico / Actividades', render: (_, r) => r.diagnostico || r.fallaReportada || '—' },
                    { title: 'Técnico', dataIndex: 'tecnicoNombre' },
                    { title: 'Total', dataIndex: 'total', align: 'right', render: money },
                    {
                      title: 'Reporte PDF',
                      width: 120,
                      render: (_, r) => (
                        <Button size="small" icon={<DownloadOutlined />} onClick={() => downloadOrdenPdf(r.ordenServicioId, r.folio)}>
                          Reporte PDF
                        </Button>
                      ),
                    },
                  ]}
                />
              ),
            },
            {
              key: 'certificados',
              label: `Certificados y Calibraciones (${data.certificados.length})`,
              children: (
                <Table
                  size="small"
                  rowKey="certificadoId"
                  dataSource={data.certificados}
                  bordered
                  columns={[
                    { title: 'Folio', dataIndex: 'folio', render: (v: string) => <strong>{v}</strong> },
                    { title: 'Tipo', dataIndex: 'tipoRegistro', render: (v?: string) => <Tag color={v === 'Certificación' ? 'purple' : 'blue'}>{v ?? 'Calibración'}</Tag> },
                    { title: 'Fecha Calibración', dataIndex: 'fechaCalibracion', render: fecha },
                    { title: 'Vencimiento', dataIndex: 'fechaVencimiento', render: fecha },
                    { title: 'Dictamen', dataIndex: 'resultado' },
                    {
                      title: 'Vigencia',
                      dataIndex: 'estadoVigencia',
                      render: (v: string) => (
                        <Tag color={v === 'Vigente' ? colors.ok : v === 'PorVencer' ? colors.warn : colors.bad}>{v}</Tag>
                      ),
                    },
                    { title: 'Orden Vinculada', dataIndex: 'folioOrdenServicio', render: (v?: string) => v ?? '—' },
                    {
                      title: 'PDF',
                      width: 100,
                      render: (_, r) => (
                        <Button size="small" icon={<DownloadOutlined />} onClick={() => downloadPdf(r.certificadoId, r.folio)}>
                          PDF
                        </Button>
                      ),
                    },
                  ]}
                />
              ),
            },
            {
              key: 'mantenimientos',
              label: `Mantenimientos y Reparaciones (${data.mantenimientos.length + data.reparaciones.length})`,
              children: (
                <Table
                  size="small"
                  rowKey="mantenimientoId"
                  dataSource={[...data.mantenimientos, ...data.reparaciones]}
                  bordered
                  columns={[
                    { title: 'Folio', dataIndex: 'folio', render: (v: string) => <strong>{v}</strong> },
                    { title: 'Tipo', dataIndex: 'tipo', render: (v: string) => <Tag color={v === 'Reparación' ? 'volcano' : 'cyan'}>{v}</Tag> },
                    { title: 'Estatus', dataIndex: 'estatus', render: (v: string) => <Tag>{v}</Tag> },
                    { title: 'Fecha', dataIndex: 'fechaProgramada', render: fecha },
                    { title: 'Próximo Mto.', dataIndex: 'proximoMantenimiento', render: fecha },
                    { title: 'Orden Servicio', dataIndex: 'folioOrdenServicio', render: (v?: string) => v ?? '—' },
                    { title: 'Descripción / Actividades', render: (_, r) => r.actividades || r.descripcion || '—' },
                    { title: 'Costo', dataIndex: 'costo', align: 'right', render: money },
                  ]}
                />
              ),
            },
            {
              key: 'refacciones',
              label: `Refacciones Utilizadas (${data.refaccionesUtilizadas.length})`,
              children: (
                <Table
                  size="small"
                  rowKey="ordenServicioRefaccionId"
                  dataSource={data.refaccionesUtilizadas}
                  bordered
                  columns={[
                    { title: 'Fecha', dataIndex: 'fecha', render: fecha },
                    { title: 'Orden de Servicio', dataIndex: 'folioOrden', render: (v: string) => <strong>{v}</strong> },
                    { title: 'SKU', dataIndex: 'sku' },
                    { title: 'Refacción / Componente', dataIndex: 'nombreProducto' },
                    { title: 'Cantidad', dataIndex: 'cantidad', align: 'right' },
                    { title: 'Precio Unit.', dataIndex: 'precioUnitario', align: 'right', render: money },
                    { title: 'Importe', dataIndex: 'importe', align: 'right', render: money },
                  ]}
                />
              ),
            },
            {
              key: 'garantias',
              label: `Garantías y Ventas (${data.garantias.length + data.ventas.length})`,
              children: (
                <Row gutter={[16, 16]}>
                  <Col xs={24} md={12}>
                    <Card size="small" title="Pólizas de Garantía">
                      <Table
                        size="small"
                        rowKey="garantiaId"
                        dataSource={data.garantias}
                        pagination={false}
                        columns={[
                          { title: 'Folio', dataIndex: 'folio' },
                          { title: 'Tipo', dataIndex: 'tipoGarantia' },
                          { title: 'Vigencia', render: (_, r) => `${fecha(r.fechaInicio)} al ${fecha(r.fechaFin)}` },
                          { title: 'Estatus', dataIndex: 'estatus', render: (v: string) => <Tag color={v === 'Vigente' ? 'green' : 'default'}>{v}</Tag> },
                          { title: 'Cobertura', dataIndex: 'cobertura' },
                        ]}
                      />
                    </Card>
                  </Col>
                  <Col xs={24} md={12}>
                    <Card size="small" title="Ventas y Cotizaciones Relacionadas">
                      <Table
                        size="small"
                        rowKey="ventaId"
                        dataSource={data.ventas}
                        pagination={false}
                        columns={[
                          { title: 'Folio', dataIndex: 'folio' },
                          { title: 'Documento', dataIndex: 'tipoDocumento' },
                          { title: 'Fecha', dataIndex: 'fecha', render: fecha },
                          { title: 'Estatus', dataIndex: 'estatus', render: (v: string) => <Tag color="blue">{v}</Tag> },
                          { title: 'Total', dataIndex: 'total', align: 'right', render: money },
                        ]}
                      />
                    </Card>
                  </Col>
                </Row>
              ),
            },
            {
              key: 'archivos',
              label: `Fotografías, Archivos y PDF (${data.fotografias.length + data.archivos.length + data.certificados.length + data.ordenesServicio.length})`,
              children: (
                <div>
                  <Typography.Title level={5}><PictureOutlined /> Evidencia Fotográfica del Equipo</Typography.Title>
                  {data.fotografias.length === 0 && data.archivos.filter((a) => a.tipoArchivo === 'Fotografia').length === 0 ? (
                    <Empty description="Sin fotografías registradas aún" />
                  ) : (
                    <Image.PreviewGroup>
                      <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 20 }}>
                        {data.fotografias.map((f) => (
                          <Card key={`osfoto-${f.ordenServicioFotoId}`} size="small" style={{ width: 200 }}>
                            <Image src={f.imagenUrl} height={120} style={{ objectFit: 'cover', width: '100%' }} />
                            <div style={{ marginTop: 6 }}>
                              <Tag color="blue">{f.categoria}</Tag>
                              <div style={{ fontSize: 12 }}>{f.descripcion ?? 'Evidencia OS'}</div>
                              <div style={{ fontSize: 11, color: '#6b7280' }}>{fechaHora(f.fecha)}</div>
                            </div>
                          </Card>
                        ))}
                      </div>
                    </Image.PreviewGroup>
                  )}

                  <Typography.Title level={5}><FilePdfOutlined /> Documentos PDF y Archivos del Expediente</Typography.Title>
                  <Space wrap style={{ marginBottom: 16 }}>
                    {data.certificados.map((c) => (
                      <Button key={`certpdf-${c.certificadoId}`} icon={<FilePdfOutlined />} onClick={() => downloadPdf(c.certificadoId, c.folio)}>
                        Certificado {c.folio}.pdf
                      </Button>
                    ))}
                    {data.ordenesServicio.map((o) => (
                      <Button key={`ospdf-${o.ordenServicioId}`} icon={<FilePdfOutlined />} onClick={() => downloadOrdenPdf(o.ordenServicioId, o.folio)}>
                        Reporte Técnico {o.folio}.pdf
                      </Button>
                    ))}
                  </Space>

                  {data.archivos.length > 0 && (
                    <Table
                      size="small"
                      rowKey="equipoArchivoId"
                      dataSource={data.archivos}
                      pagination={false}
                      columns={[
                        { title: 'Fecha', dataIndex: 'fecha', render: fechaHora },
                        { title: 'Tipo', dataIndex: 'tipoArchivo' },
                        { title: 'Categoría', dataIndex: 'categoria' },
                        { title: 'Nombre', dataIndex: 'nombreArchivo' },
                        { title: 'Descripción', dataIndex: 'descripcion' },
                        { title: 'Subido por', dataIndex: 'subidoPor' },
                      ]}
                    />
                  )}
                </div>
              ),
            },
            {
              key: 'historial',
              label: `Historial Técnico Completo (${data.historialCompleto.length})`,
              children: (
                <div style={{ padding: '12px 8px' }}>
                  <Timeline
                    items={data.historialCompleto.map((h) => ({
                      color: h.tipoEvento === 'Baja' ? 'red' : h.tipoEvento === 'Calibracion' || h.tipoEvento === 'Certificacion' ? 'green' : 'blue',
                      children: (
                        <div>
                          <Space>
                            <Tag color="geekblue">{h.tipoEvento}</Tag>
                            <strong>{h.titulo}</strong>
                            {h.folioReferencia && <Tag>{h.folioReferencia}</Tag>}
                          </Space>
                          <div style={{ color: '#4b5563', marginTop: 2 }}>{h.detalle}</div>
                          <div style={{ fontSize: 11, color: '#9ca3af' }}>
                            {fechaHora(h.fecha)} · Usuario: {h.usuarioNombre ?? 'Sistema'}
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
      </Card>

      <Modal
        open={archivoModal}
        title="Agregar Archivo o Fotografía al Expediente del Equipo"
        onCancel={() => setArchivoModal(false)}
        onOk={() => formArchivo.submit()}
        confirmLoading={subirArchivo.isPending}
        okText="Guardar en Expediente"
        destroyOnClose
      >
        <Form form={formArchivo} layout="vertical" initialValues={{ tipoArchivo: 'Fotografia', categoria: 'Equipo completo' }} onFinish={(v) => subirArchivo.mutate(v)}>
          <Form.Item name="tipoArchivo" label="Tipo de Archivo" rules={[{ required: true }]}>
            <Select options={[
              { value: 'Fotografia', label: 'Fotografía' },
              { value: 'PDF', label: 'Documento PDF' },
              { value: 'Documento', label: 'Manual / Ficha Técnica' },
              { value: 'Firmware', label: 'Respaldo / Firmware' },
            ]} />
          </Form.Item>
          <Form.Item name="categoria" label="Categoría" rules={[{ required: true }]}>
            <Select options={[
              { value: 'Equipo completo', label: 'Equipo completo' },
              { value: 'Número de serie', label: 'Número de serie' },
              { value: 'Golpes', label: 'Golpes / Daños' },
              { value: 'Accesorios recibidos', label: 'Accesorios' },
              { value: 'Documentación', label: 'Documentación Técnica' },
            ]} />
          </Form.Item>
          <Form.Item label="Seleccionar imagen o archivo">
            <Upload
              beforeUpload={(file) => {
                const reader = new FileReader()
                reader.onload = () => setB64File({ name: file.name, dataUrl: String(reader.result) })
                reader.readAsDataURL(file)
                return false
              }}
              maxCount={1}
            >
              <Button icon={<UploadOutlined />}>Elegir archivo local</Button>
            </Upload>
          </Form.Item>
          <Form.Item name="descripcion" label="Descripción / Observaciones">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
