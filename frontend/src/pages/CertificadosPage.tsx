import { DownloadOutlined, EyeOutlined, HistoryOutlined, PlusOutlined, StopOutlined } from '@ant-design/icons'
import { App, Button, DatePicker, Drawer, Form, Input, Modal, Select, Space, Table, Tag, Timeline } from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { api, downloadPdf, errorMessage } from '../api/client'
import type { Certificado, Equipo, Historial, OrdenServicio, Paged } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { colors } from '../theme'

const ESTADO_TAG: Record<string, { color: string; text: string }> = {
  Vigente: { color: colors.ok, text: 'Vigente' },
  PorVencer: { color: colors.warn, text: 'Por vencer' },
  Vencido: { color: colors.bad, text: 'Vencido' },
  Cancelado: { color: '#6b7280', text: 'Cancelado' },
}
const RESULTADOS = ['Aceptado', 'Condicionado', 'Rechazado'].map((v) => ({ value: v, label: v }))
const fecha = (v: string) => dayjs(v).format('DD/MM/YYYY')

export default function CertificadosPage() {
  const { hasRole } = useAuth()
  const { message, modal } = App.useApp()
  const nav = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const qc = useQueryClient()
  const [q, setQ] = useState('')
  const [estado, setEstado] = useState<string>()
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<Partial<Certificado> | null>(null)
  const [histId, setHistId] = useState<Certificado | null>(null)
  const [form] = Form.useForm()
  const canCreate = hasRole('Administrador', 'Tecnico')
  const isAdmin = hasRole('Administrador')

  const { data, isLoading } = useQuery({
    queryKey: ['certificados', q, estado, page],
    queryFn: async () => (await api.get<Paged<Certificado>>('/certificados', { params: { q, estado, page, size: 15 } })).data,
  })
  const { data: equipos } = useQuery({
    queryKey: ['equipos-select'],
    enabled: !!editing && !editing.certificadoId,
    queryFn: async () => (await api.get<Paged<Equipo>>('/equipos', { params: { size: 200 } })).data.items,
  })
  const { data: ordenes } = useQuery({
    queryKey: ['ordenes-select'],
    enabled: !!editing && !editing.certificadoId,
    queryFn: async () => (await api.get<Paged<OrdenServicio>>('/ordenes-servicio', { params: { size: 100 } })).data.items,
  })
  const { data: hist } = useQuery({
    queryKey: ['historial', histId?.certificadoId],
    enabled: !!histId,
    queryFn: async () => (await api.get<Historial[]>(`/certificados/${histId!.certificadoId}/historial`)).data,
  })

  useEffect(() => {
    const eqId = searchParams.get('equipoId')
    const ordId = searchParams.get('ordenId')
    if (eqId && canCreate) {
      setEditing({})
      form.resetFields()
      form.setFieldsValue({
        equipoId: Number(eqId),
        ordenServicioId: ordId ? Number(ordId) : undefined,
        tipoRegistro: 'Calibración',
        fechaCalibracion: dayjs(),
        resultado: 'Aceptado',
      })
      setSearchParams({})
    }
  }, [searchParams, setSearchParams, canCreate, form])

  const save = useMutation({
    mutationFn: (v: Record<string, unknown>) => {
      const body = { ...v, fechaCalibracion: (v.fechaCalibracion as dayjs.Dayjs).format('YYYY-MM-DD') }
      return editing?.certificadoId ? api.put(`/certificados/${editing.certificadoId}`, body) : api.post('/certificados', body)
    },
    onSuccess: () => {
      message.success('Certificado guardado y ligado al expediente del equipo')
      setEditing(null)
      qc.invalidateQueries({ queryKey: ['certificados'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const cancelar = (c: Certificado) => {
    let motivo = ''
    modal.confirm({
      title: `Cancelar certificado ${c.folio}`,
      okText: 'Cancelar certificado',
      okButtonProps: { danger: true },
      cancelText: 'Volver',
      content: <Input.TextArea rows={3} placeholder="Motivo de la cancelación (obligatorio)" onChange={(e) => (motivo = e.target.value)} />,
      onOk: async () => {
        try {
          await api.post(`/certificados/${c.certificadoId}/cancelar`, { motivo })
          message.success('Certificado cancelado')
          qc.invalidateQueries({ queryKey: ['certificados'] })
        } catch (e) {
          message.error(errorMessage(e))
          throw e
        }
      },
    })
  }

  const pdf = async (c: Certificado) => {
    try {
      await downloadPdf(c.certificadoId, c.folio)
    } catch (e) {
      message.error(errorMessage(e))
    }
  }

  const open = (c: Partial<Certificado>) => {
    setEditing(c)
    form.resetFields()
    form.setFieldsValue(
      c.certificadoId
        ? { ...c, fechaCalibracion: dayjs(c.fechaCalibracion) }
        : { fechaCalibracion: dayjs(), resultado: 'Aceptado', tipoRegistro: 'Calibración' },
    )
  }

  return (
    <>
      <h1 className="page-title">Módulo de Certificados de Calibración y Certificaciones</h1>
      <div className="toolbar">
        <Space wrap>
          <Input.Search placeholder="Folio, serie o cliente" allowClear style={{ width: 300 }} onSearch={(v) => { setQ(v); setPage(1) }} />
          <Select
            placeholder="Estado"
            allowClear
            style={{ width: 160 }}
            onChange={(v) => {
              setEstado(v)
              setPage(1)
            }}
            options={Object.entries(ESTADO_TAG).map(([value, t]) => ({ value, label: t.text }))}
          />
        </Space>
        {canCreate && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => open({})}>
            Nuevo certificado / certificación
          </Button>
        )}
      </div>

      <Table
        rowKey="certificadoId"
        loading={isLoading}
        dataSource={data?.items}
        size="middle"
        bordered
        scroll={{ x: 'max-content' }}
        pagination={{ current: page, pageSize: 15, total: data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: 'Folio', dataIndex: 'folio', render: (v: string) => <strong>{v}</strong> },
          {
            title: 'Tipo',
            dataIndex: 'tipoRegistro',
            width: 115,
            render: (v?: string) => <Tag color={v === 'Certificación' ? 'purple' : 'blue'}>{v ?? 'Calibración'}</Tag>,
          },
          { title: 'Cliente', dataIndex: 'cliente' },
          { title: 'Equipo', render: (_, r) => `${r.tipoEquipo} · ${r.equipo}` },
          { title: 'Serie', dataIndex: 'seriePrincipal', render: (v: string) => <strong>{v}</strong> },
          { title: 'Orden Servicio', dataIndex: 'folioOrdenServicio', render: (v?: string) => v ?? '—' },
          { title: 'Calibración', dataIndex: 'fechaCalibracion', render: fecha },
          { title: 'Vence', dataIndex: 'fechaVencimiento', render: fecha },
          { title: 'Resultado', dataIndex: 'resultado' },
          { title: 'Estado', dataIndex: 'estadoVigencia', render: (v: string) => <Tag color={ESTADO_TAG[v].color}>{ESTADO_TAG[v].text}</Tag> },
          {
            title: 'Acciones',
            fixed: 'right',
            width: 270,
            render: (_, r) => (
              <Space size={4}>
                <Button size="small" icon={<DownloadOutlined />} onClick={() => pdf(r)}>
                  PDF
                </Button>
                <Button size="small" icon={<EyeOutlined />} title="Expediente 360° del Equipo" onClick={() => nav(`/equipos/${r.equipoId}`)}>
                  360°
                </Button>
                {canCreate && <Button size="small" icon={<HistoryOutlined />} onClick={() => setHistId(r)} />}
                {isAdmin && r.estatus !== 'Cancelado' && (
                  <Button size="small" onClick={() => open(r)}>
                    Editar
                  </Button>
                )}
                {isAdmin && r.estatus !== 'Cancelado' && <Button size="small" danger icon={<StopOutlined />} onClick={() => cancelar(r)} />}
              </Space>
            ),
          },
        ]}
      />

      <Modal
        open={!!editing}
        width={760}
        destroyOnClose
        onCancel={() => setEditing(null)}
        onOk={() => form.submit()}
        confirmLoading={save.isPending}
        okText="Guardar y generar PDF"
        cancelText="Cancelar"
        title={editing?.certificadoId ? `Modificar ${editing.folio}` : 'Emitir Certificado de Calibración / Certificación'}
      >
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          {!editing?.certificadoId && (
            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 12 }}>
              <Form.Item name="equipoId" label="Equipo Topográfico" rules={[{ required: true }]}>
                <Select
                  showSearch
                  optionFilterProp="label"
                  options={equipos?.map((e) => ({
                    value: e.equipoId,
                    label: `${e.tipoEquipo} ${e.marca} ${e.modelo} · ${e.seriePrincipal} · ${e.cliente}`,
                  }))}
                />
              </Form.Item>
              <Form.Item name="ordenServicioId" label="Orden de Servicio vinculada (opcional)">
                <Select
                  allowClear
                  showSearch
                  optionFilterProp="label"
                  options={ordenes?.map((o) => ({ value: o.ordenServicioId, label: `${o.folio} (${o.seriePrincipal})` }))}
                />
              </Form.Item>
            </div>
          )}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 12 }}>
            <Form.Item name="tipoRegistro" label="Tipo de Emisión" rules={[{ required: true }]}>
              <Select options={[{ value: 'Calibración', label: 'Certificado de Calibración' }, { value: 'Certificación', label: 'Certificación Técnica' }]} />
            </Form.Item>
            <Form.Item name="fechaCalibracion" label="Fecha de calibración" rules={[{ required: true }]}>
              <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" disabledDate={(d) => d.isAfter(dayjs())} />
            </Form.Item>
            <Form.Item name="vigenciaMeses" label="Vigencia" rules={[{ required: !!editing?.certificadoId }]} tooltip="Si se omite se usa la configurada para el tipo de equipo">
              <Select allowClear options={[{ value: 6, label: '6 meses' }, { value: 12, label: '12 meses' }]} />
            </Form.Item>
            <Form.Item name="resultado" label="Conclusión" rules={[{ required: true }]}>
              <Select options={RESULTADOS} />
            </Form.Item>
          </div>
          <Form.Item name="resultados" label="Resultados">
            <Input.TextArea rows={4} />
          </Form.Item>
          <Form.Item name="observaciones" label="Observaciones">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="conclusiones" label="Conclusiones">
            <Input.TextArea rows={2} />
          </Form.Item>
          {editing?.certificadoId && (
            <Form.Item name="motivoCambio" label="Motivo del cambio (queda en bitácora)" rules={[{ required: true, min: 5 }]}>
              <Input />
            </Form.Item>
          )}
        </Form>
      </Modal>

      <Drawer open={!!histId} onClose={() => setHistId(null)} width={480} title={`Historial · ${histId?.folio ?? ''}`}>
        <Timeline
          items={hist?.map((h) => ({
            children: (
              <>
                <strong>{h.accion}</strong> <span style={{ color: '#6b7280' }}>· {dayjs(h.fecha).format('DD/MM/YYYY HH:mm')} · {h.usuario}</span>
                {h.detalle && <div>{h.detalle}</div>}
                {h.pdfHash && <div style={{ fontSize: 11, color: '#6b7280', wordBreak: 'break-all' }}>SHA-256: {h.pdfHash}</div>}
              </>
            ),
          }))}
        />
      </Drawer>
    </>
  )
}
