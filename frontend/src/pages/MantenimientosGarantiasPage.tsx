import { EyeOutlined, SafetyCertificateOutlined, ToolOutlined } from '@ant-design/icons'
import {
  App,
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, errorMessage } from '../api/client'
import type { Equipo, Garantia, Mantenimiento, Paged } from '../api/types'

const TIPOS_MTO = ['Mantenimiento Preventivo', 'Mantenimiento Correctivo', 'Reparación', 'Revisión Técnica']
const ESTATUS_MTO = ['Programado', 'En Proceso', 'Completado', 'Cancelado']
const TIPOS_GARANTIA = ['Servicio / Reparación', 'Equipo Nuevo / Fábrica', 'Refacción Instalada', 'Póliza Extendida']
const ESTATUS_GARANTIA = ['Vigente', 'En Reclamación', 'Aplicada', 'Vencida', 'Cancelada']

export default function MantenimientosGarantiasPage() {
  const { message } = App.useApp()
  const nav = useNavigate()
  const qc = useQueryClient()

  const [qMto, setQMto] = useState('')
  const [tipoMto, setTipoMto] = useState<string>()
  const [pageMto, setPageMto] = useState(1)

  const [qGar, setQGar] = useState('')
  const [pageGar, setPageGar] = useState(1)

  const [editingMto, setEditingMto] = useState<Partial<Mantenimiento> | null>(null)
  const [editingGar, setEditingGar] = useState<Partial<Garantia> | null>(null)

  const [formMto] = Form.useForm()
  const [formGar] = Form.useForm()

  const { data: mantenimientos, isLoading: loadingMto } = useQuery({
    queryKey: ['mantenimientos', qMto, tipoMto, pageMto],
    queryFn: async () =>
      (
        await api.get<Paged<Mantenimiento>>('/mantenimientos', {
          params: { q: qMto, tipo: tipoMto, page: pageMto, size: 15 },
        })
      ).data,
  })

  const { data: garantias, isLoading: loadingGar } = useQuery({
    queryKey: ['garantias', qGar, pageGar],
    queryFn: async () =>
      (await api.get<Paged<Garantia>>('/garantias', { params: { q: qGar, page: pageGar, size: 15 } })).data,
  })

  const { data: equipos } = useQuery({
    queryKey: ['equipos-mto-gar'],
    enabled: !!editingMto || !!editingGar,
    queryFn: async () => (await api.get<Paged<Equipo>>('/equipos', { params: { size: 200 } })).data.items,
  })

  const saveMto = useMutation({
    mutationFn: (v: Record<string, unknown>) => {
      const body = {
        ...v,
        fechaProgramada: v.fechaProgramada ? (v.fechaProgramada as dayjs.Dayjs).format('YYYY-MM-DD') : dayjs().format('YYYY-MM-DD'),
        fechaRealizada: v.fechaRealizada ? (v.fechaRealizada as dayjs.Dayjs).format('YYYY-MM-DD') : null,
        proximoMantenimiento: v.proximoMantenimiento ? (v.proximoMantenimiento as dayjs.Dayjs).format('YYYY-MM-DD') : null,
      }
      return editingMto?.mantenimientoId
        ? api.put(`/mantenimientos/${editingMto.mantenimientoId}`, body)
        : api.post('/mantenimientos', body)
    },
    onSuccess: () => {
      message.success('Registro de mantenimiento/reparación guardado en expediente del equipo')
      setEditingMto(null)
      qc.invalidateQueries({ queryKey: ['mantenimientos'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const saveGar = useMutation({
    mutationFn: (v: Record<string, unknown>) => {
      const eq = equipos?.find((e) => e.equipoId === v.equipoId)
      const body = {
        ...v,
        clienteId: eq?.clienteId ?? v.clienteId,
        fechaInicio: (v.fechaInicio as dayjs.Dayjs).format('YYYY-MM-DD'),
        fechaFin: (v.fechaFin as dayjs.Dayjs).format('YYYY-MM-DD'),
      }
      return editingGar?.garantiaId ? api.put(`/garantias/${editingGar.garantiaId}`, body) : api.post('/garantias', body)
    },
    onSuccess: () => {
      message.success('Póliza de garantía guardada en el expediente del equipo')
      setEditingGar(null)
      qc.invalidateQueries({ queryKey: ['garantias'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12, flexWrap: 'wrap', gap: 8 }}>
        <div>
          <h1 className="page-title" style={{ marginBottom: 2 }}>
            Mantenimientos Preventivos, Correctivos, Revisiones Técnicas y Garantías
          </h1>
          <Typography.Text type="secondary">
            Programación y bitácora de mantenimientos, reparaciones, revisiones técnicas y pólizas de garantía por equipo
          </Typography.Text>
        </div>
        <Space wrap>
          <Button
            type="primary"
            icon={<ToolOutlined />}
            onClick={() => {
              setEditingMto({})
              formMto.resetFields()
              formMto.setFieldsValue({
                tipo: 'Mantenimiento Preventivo',
                estatus: 'Programado',
                fechaProgramada: dayjs(),
                proximoMantenimiento: dayjs().add(6, 'month'),
                costo: 0,
              })
            }}
          >
            + Registrar Mantenimiento / Revisión
          </Button>
          <Button
            icon={<SafetyCertificateOutlined />}
            onClick={() => {
              setEditingGar({})
              formGar.resetFields()
              formGar.setFieldsValue({
                tipoGarantia: 'Servicio / Reparación',
                estatus: 'Vigente',
                fechaInicio: dayjs(),
                fechaFin: dayjs().add(6, 'month'),
              })
            }}
          >
            + Nueva Póliza de Garantía
          </Button>
        </Space>
      </div>

      <Card size="small">
        <Tabs
          items={[
            {
              key: 'mantenimientos',
              label: `Mantenimientos, Reparaciones y Revisiones Técnicas (${mantenimientos?.total ?? 0})`,
              children: (
                <>
                  <div className="toolbar">
                    <Space wrap>
                      <Input.Search
                        placeholder="Buscar por folio MTO, serie o cliente..."
                        allowClear
                        style={{ width: 320 }}
                        onSearch={(v) => {
                          setQMto(v)
                          setPageMto(1)
                        }}
                      />
                      <Select
                        placeholder="Filtrar por Tipo"
                        allowClear
                        style={{ width: 220 }}
                        value={tipoMto}
                        onChange={(v) => {
                          setTipoMto(v)
                          setPageMto(1)
                        }}
                        options={TIPOS_MTO.map((t) => ({ value: t, label: t }))}
                      />
                    </Space>
                  </div>

                  <Table
                    rowKey="mantenimientoId"
                    loading={loadingMto}
                    dataSource={mantenimientos?.items}
                    size="small"
                    bordered
                    pagination={{ current: pageMto, pageSize: 15, total: mantenimientos?.total, onChange: setPageMto, showSizeChanger: false }}
                    columns={[
                      { title: 'Folio', dataIndex: 'folio', width: 145, render: (v: string) => <strong>{v}</strong> },
                      {
                        title: 'Tipo',
                        dataIndex: 'tipo',
                        width: 190,
                        render: (v: string) => <Tag color={v === 'Reparación' ? 'volcano' : v === 'Mantenimiento Correctivo' ? 'orange' : 'blue'}>{v}</Tag>,
                      },
                      { title: 'Cliente', dataIndex: 'cliente' },
                      { title: 'Equipo', dataIndex: 'equipo' },
                      { title: 'Serie Principal', dataIndex: 'seriePrincipal', render: (v: string) => <strong>{v}</strong> },
                      { title: 'Fecha Prog.', dataIndex: 'fechaProgramada', render: (v: string) => dayjs(v).format('DD/MM/YYYY') },
                      { title: 'Próximo Mto.', dataIndex: 'proximoMantenimiento', render: (v?: string) => (v ? dayjs(v).format('DD/MM/YYYY') : '—') },
                      { title: 'Estatus', dataIndex: 'estatus', render: (v: string) => <Tag color={v === 'Completado' ? 'green' : 'processing'}>{v}</Tag> },
                      { title: 'Orden Servicio', dataIndex: 'folioOrdenServicio', render: (v?: string) => v ?? '—' },
                      {
                        title: 'Acciones',
                        width: 160,
                        render: (_, r) => (
                          <Space size={4}>
                            <Button size="small" icon={<EyeOutlined />} onClick={() => nav(`/equipos/${r.equipoId}`)}>
                              360°
                            </Button>
                            <Button
                              size="small"
                              onClick={() => {
                                setEditingMto(r)
                                formMto.setFieldsValue({
                                  ...r,
                                  fechaProgramada: dayjs(r.fechaProgramada),
                                  fechaRealizada: r.fechaRealizada ? dayjs(r.fechaRealizada) : undefined,
                                  proximoMantenimiento: r.proximoMantenimiento ? dayjs(r.proximoMantenimiento) : undefined,
                                })
                              }}
                            >
                              Editar
                            </Button>
                          </Space>
                        ),
                      },
                    ]}
                  />
                </>
              ),
            },
            {
              key: 'garantias',
              label: `Control de Garantías (${garantias?.total ?? 0})`,
              children: (
                <>
                  <div className="toolbar">
                    <Input.Search
                      placeholder="Buscar por folio GAR, serie del equipo o cliente..."
                      allowClear
                      style={{ width: 340 }}
                      onSearch={(v) => {
                        setQGar(v)
                        setPageGar(1)
                      }}
                    />
                  </div>

                  <Table
                    rowKey="garantiaId"
                    loading={loadingGar}
                    dataSource={garantias?.items}
                    size="small"
                    bordered
                    pagination={{ current: pageGar, pageSize: 15, total: garantias?.total, onChange: setPageGar, showSizeChanger: false }}
                    columns={[
                      { title: 'Folio', dataIndex: 'folio', width: 145, render: (v: string) => <strong>{v}</strong> },
                      { title: 'Tipo de Garantía', dataIndex: 'tipoGarantia', render: (v: string) => <Tag color="purple">{v}</Tag> },
                      { title: 'Cliente', dataIndex: 'cliente' },
                      { title: 'Equipo', dataIndex: 'equipo' },
                      { title: 'Serie Principal', dataIndex: 'seriePrincipal', render: (v: string) => <strong>{v}</strong> },
                      { title: 'Inicio', dataIndex: 'fechaInicio', render: (v: string) => dayjs(v).format('DD/MM/YYYY') },
                      { title: 'Vencimiento', dataIndex: 'fechaFin', render: (v: string) => dayjs(v).format('DD/MM/YYYY') },
                      {
                        title: 'Días Restantes',
                        dataIndex: 'diasRestantes',
                        width: 120,
                        render: (d: number) => <Tag color={d < 0 ? 'red' : d <= 30 ? 'orange' : 'green'}>{d} días</Tag>,
                      },
                      { title: 'Cobertura', dataIndex: 'cobertura' },
                      { title: 'Estatus', dataIndex: 'estatus', render: (v: string) => <Tag color={v === 'Vigente' ? 'green' : 'default'}>{v}</Tag> },
                      {
                        title: '',
                        width: 90,
                        render: (_, r) => (
                          <Button
                            size="small"
                            onClick={() => {
                              setEditingGar(r)
                              formGar.setFieldsValue({
                                ...r,
                                fechaInicio: dayjs(r.fechaInicio),
                                fechaFin: dayjs(r.fechaFin),
                              })
                            }}
                          >
                            Editar
                          </Button>
                        ),
                      },
                    ]}
                  />
                </>
              ),
            },
          ]}
        />
      </Card>

      {/* ───────────── MODAL: MANTENIMIENTO / REPARACIÓN / REVISIÓN ───────────── */}
      <Modal
        open={!!editingMto}
        title={editingMto?.mantenimientoId ? `Editar ${editingMto.folio}` : 'Registrar Mantenimiento, Reparación o Revisión Técnica'}
        onCancel={() => setEditingMto(null)}
        onOk={() => formMto.submit()}
        confirmLoading={saveMto.isPending}
        width={720}
        okText="Guardar Registro"
        destroyOnClose
      >
        <Form form={formMto} layout="vertical" onFinish={(v) => saveMto.mutate(v)}>
          <Form.Item name="equipoId" label="Equipo Topográfico" rules={[{ required: true }]}>
            <Select
              showSearch
              optionFilterProp="label"
              options={equipos?.map((e) => ({
                value: e.equipoId,
                label: `${e.seriePrincipal} · ${e.tipoEquipo} ${e.marca} ${e.modelo} · ${e.cliente}`,
              }))}
            />
          </Form.Item>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
            <Form.Item name="tipo" label="Tipo de Intervención" rules={[{ required: true }]}>
              <Select options={TIPOS_MTO.map((t) => ({ value: t, label: t }))} />
            </Form.Item>
            <Form.Item name="estatus" label="Estatus" rules={[{ required: true }]}>
              <Select options={ESTATUS_MTO.map((e) => ({ value: e, label: e }))} />
            </Form.Item>
            <Form.Item name="costo" label="Costo ($)">
              <InputNumber min={0} style={{ width: '100%' }} prefix="$" />
            </Form.Item>
            <Form.Item name="fechaProgramada" label="Fecha Programada" rules={[{ required: true }]}>
              <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
            </Form.Item>
            <Form.Item name="fechaRealizada" label="Fecha Realizada">
              <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
            </Form.Item>
            <Form.Item name="proximoMantenimiento" label="Próximo Mantenimiento">
              <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
            </Form.Item>
          </div>
          <Form.Item name="descripcion" label="Descripción / Alcance">
            <Input />
          </Form.Item>
          <Form.Item name="actividades" label="Actividades Técnicas Realizadas">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="recomendaciones" label="Hallazgos y Recomendaciones">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>

      {/* ───────────── MODAL: PÓLIZA DE GARANTÍA ───────────── */}
      <Modal
        open={!!editingGar}
        title={editingGar?.garantiaId ? `Editar Garantía ${editingGar.folio}` : 'Registrar Póliza de Garantía'}
        onCancel={() => setEditingGar(null)}
        onOk={() => formGar.submit()}
        confirmLoading={saveGar.isPending}
        width={680}
        okText="Guardar Garantía"
        destroyOnClose
      >
        <Form form={formGar} layout="vertical" onFinish={(v) => saveGar.mutate(v)}>
          <Form.Item name="equipoId" label="Equipo Cubierto" rules={[{ required: true }]}>
            <Select
              showSearch
              optionFilterProp="label"
              options={equipos?.map((e) => ({
                value: e.equipoId,
                label: `${e.seriePrincipal} · ${e.tipoEquipo} ${e.marca} ${e.modelo} · ${e.cliente}`,
              }))}
            />
          </Form.Item>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 10 }}>
            <Form.Item name="tipoGarantia" label="Tipo Garantía" rules={[{ required: true }]}>
              <Select options={TIPOS_GARANTIA.map((t) => ({ value: t, label: t }))} />
            </Form.Item>
            <Form.Item name="estatus" label="Estatus" rules={[{ required: true }]}>
              <Select options={ESTATUS_GARANTIA.map((e) => ({ value: e, label: e }))} />
            </Form.Item>
            <Form.Item name="fechaInicio" label="Inicio Vigencia" rules={[{ required: true }]}>
              <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
            </Form.Item>
            <Form.Item name="fechaFin" label="Fin Vigencia" rules={[{ required: true }]}>
              <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
            </Form.Item>
          </div>
          <Form.Item name="cobertura" label="Cobertura de la Garantía" rules={[{ required: true }]}>
            <Input placeholder="Ej. Mano de obra de calibración, reemplazo de tarjeta madre, defectos de fabricación..." />
          </Form.Item>
          <Form.Item name="condiciones" label="Términos y Condiciones">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="motivoReclamacion" label="Motivo de Reclamación / Resolución (si aplica)">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
