import { DeleteOutlined, EyeOutlined, PlusOutlined, ToolOutlined } from '@ant-design/icons'
import { App, Button, DatePicker, Divider, Form, Input, Modal, Select, Space, Table, Tag, Typography } from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, errorMessage } from '../api/client'
import type { Cliente, Equipo, Paged } from '../api/types'
import { useAuth } from '../auth/AuthContext'

export const useTipos = () =>
  useQuery({
    queryKey: ['tipos'],
    staleTime: Infinity,
    queryFn: async () => (await api.get<{ id: number; nombre: string; value: string }[]>('/equipos/tipos')).data,
  })

const TIPOS_SERIE_DEFAULT = [
  'Serie Radio Interna',
  'Serie Radio Externa',
  'Serie Controladora',
  'Serie Antena',
  'IMEI 1',
  'IMEI 2',
  'Serie Batería',
  'Serie Prisma / Base',
  'Otro',
]

export default function EquiposPage() {
  const { hasRole } = useAuth()
  const { message } = App.useApp()
  const nav = useNavigate()
  const qc = useQueryClient()
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<Partial<Equipo> | null>(null)
  const [form] = Form.useForm()
  const canEdit = hasRole('Administrador', 'Tecnico')
  const { data: tipos } = useTipos()

  const { data, isLoading } = useQuery({
    queryKey: ['equipos', q, page],
    queryFn: async () => (await api.get<Paged<Equipo>>('/equipos', { params: { q, page, size: 15 } })).data,
  })
  const { data: clientes } = useQuery({
    queryKey: ['clientes-select'],
    enabled: !!editing,
    queryFn: async () => (await api.get<Paged<Cliente>>('/clientes', { params: { size: 200 } })).data.items,
  })

  const save = useMutation({
    mutationFn: (v: Record<string, unknown>) => {
      const body = {
        ...v,
        fechaCompra: v.fechaCompra ? (v.fechaCompra as dayjs.Dayjs).format('YYYY-MM-DD') : null,
      }
      return editing?.equipoId ? api.put(`/equipos/${editing.equipoId}`, body) : api.post('/equipos', body)
    },
    onSuccess: () => {
      message.success('Equipo y series guardados correctamente')
      setEditing(null)
      qc.invalidateQueries({ queryKey: ['equipos'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const open = (e: Partial<Equipo>) => {
    setEditing(e)
    form.resetFields()
    const seriesAdicionales = (e.series ?? []).filter((s) => s.tipoSerie !== 'Serie Principal')
    form.setFieldsValue({
      ...e,
      tipoEquipo: tipos?.find((t) => t.id === e.tipoEquipoId)?.value,
      fechaCompra: e.fechaCompra ? dayjs(e.fechaCompra) : undefined,
      series: seriesAdicionales.length > 0 ? seriesAdicionales : [
        { tipoSerie: 'Serie Radio Interna', valor: '' },
        { tipoSerie: 'Serie Controladora', valor: '' },
      ],
    })
  }

  return (
    <>
      <h1 className="page-title">Equipos Topográficos · Entidad Central ERP</h1>
      <div className="toolbar">
        <Input.Search
          placeholder="Buscar por QR, código de barras, serie principal/radio/antena, IMEI, no. interno o cliente"
          allowClear
          style={{ maxWidth: 540 }}
          onSearch={(v) => {
            setQ(v)
            setPage(1)
          }}
        />
        {canEdit && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => open({})}>
            Registrar nuevo equipo
          </Button>
        )}
      </div>

      <Table
        rowKey="equipoId"
        loading={isLoading}
        dataSource={data?.items}
        size="middle"
        bordered
        scroll={{ x: 'max-content' }}
        pagination={{ current: page, pageSize: 15, total: data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: 'QR / ID', dataIndex: 'codigoQR', width: 110, render: (v: string) => <code>{v}</code> },
          { title: 'Tipo', dataIndex: 'tipoEquipo' },
          { title: 'Marca / Modelo', render: (_, r) => <strong>{r.marca} {r.modelo}</strong> },
          { title: 'Serie Principal', dataIndex: 'seriePrincipal', render: (v: string) => <strong>{v}</strong> },
          {
            title: 'Series registradas',
            render: (_, r) => (
              <Space size={[4, 4]} wrap>
                {(r.series ?? []).slice(0, 3).map((s, idx) => (
                  <Tag key={idx} color="blue">{s.tipoSerie}: {s.valor}</Tag>
                ))}
                {(r.series?.length ?? 0) > 3 && <Tag>+{(r.series?.length ?? 0) - 3} más</Tag>}
              </Space>
            ),
          },
          { title: 'No. Interno', dataIndex: 'numeroInterno' },
          { title: 'Cliente Propietario', dataIndex: 'cliente' },
          {
            title: 'Estatus',
            dataIndex: 'estatus',
            width: 100,
            render: (v?: string) => (
              <Tag color={v === 'EnTaller' ? 'processing' : v === 'Baja' ? 'red' : 'green'}>{v ?? 'Activo'}</Tag>
            ),
          },
          {
            title: 'Acciones',
            fixed: 'right',
            width: 250,
            render: (_: unknown, r: Equipo) => (
              <Space size={4}>
                <Button size="small" type="primary" ghost icon={<EyeOutlined />} onClick={() => nav(`/equipos/${r.equipoId}`)}>
                  Expediente 360°
                </Button>
                {canEdit && (
                  <Button size="small" icon={<ToolOutlined />} onClick={() => nav(`/ordenes-servicio?equipoId=${r.equipoId}&nueva=1`)}>
                    OS
                  </Button>
                )}
                {canEdit && (
                  <Button size="small" onClick={() => open(r)}>
                    Editar
                  </Button>
                )}
              </Space>
            ),
          },
        ]}
      />

      <Modal
        open={!!editing}
        title={editing?.equipoId ? `Editar equipo · ${editing.seriePrincipal}` : 'Registrar nuevo equipo topográfico'}
        onCancel={() => setEditing(null)}
        onOk={() => form.submit()}
        confirmLoading={save.isPending}
        okText="Guardar equipo"
        cancelText="Cancelar"
        width={820}
        destroyOnClose
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={(v) => {
            const cleanSeries = ((v.series as { tipoSerie?: string; valor?: string; observaciones?: string }[]) ?? []).filter(
              (s) => s && s.tipoSerie && s.valor && s.valor.trim().length > 0,
            )
            save.mutate({ ...v, series: cleanSeries })
          }}
        >
          <Form.Item name="clienteId" label="Cliente propietario" rules={[{ required: true }]}>
            <Select
              showSearch
              optionFilterProp="label"
              options={clientes?.map((c) => ({ value: c.clienteId, label: `${c.razonSocial} (${c.rfc})` }))}
            />
          </Form.Item>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
            <Form.Item name="tipoEquipo" label="Tipo de equipo" rules={[{ required: true }]}>
              <Select options={tipos?.map((t) => ({ value: t.value, label: t.nombre }))} />
            </Form.Item>
            <Form.Item name="marca" label="Marca" rules={[{ required: true }]}>
              <Input placeholder="Ej. Leica, Trimble, Topcon, Sokkia" />
            </Form.Item>
            <Form.Item name="modelo" label="Modelo" rules={[{ required: true }]}>
              <Input placeholder="Ej. TS16, R12i, GM-52" />
            </Form.Item>
            <Form.Item name="seriePrincipal" label="Serie Principal" rules={[{ required: true }]}>
              <Input placeholder="No. de serie principal" />
            </Form.Item>
            <Form.Item name="numeroInterno" label="Número interno">
              <Input placeholder="Ej. INT-001" />
            </Form.Item>
            <Form.Item name="codigoBarras" label="Código de barras / Etiqueta">
              <Input placeholder="Opcional (por defecto Serie Principal)" />
            </Form.Item>
            <Form.Item name="imei1" label="IMEI 1" rules={[{ pattern: /^\d{15}$/, message: '15 dígitos' }]}>
              <Input maxLength={15} />
            </Form.Item>
            <Form.Item name="imei2" label="IMEI 2" rules={[{ pattern: /^\d{15}$/, message: '15 dígitos' }]}>
              <Input maxLength={15} />
            </Form.Item>
            <Form.Item name="fechaCompra" label="Fecha de compra">
              <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
            </Form.Item>
          </div>

          <Divider style={{ margin: '8px 0 12px' }}>
            Series Adicionales del Equipo (Cantidad Configurable)
          </Divider>
          <Typography.Paragraph type="secondary" style={{ marginTop: -8, fontSize: 12 }}>
            Agregue tantos números de serie como requiera el equipo (Serie Radio Interna, Serie Radio Externa, Serie Controladora, Serie Antena, IMEI, etc.).
          </Typography.Paragraph>

          <Form.List name="series">
            {(fields, { add, remove }) => (
              <>
                {fields.map(({ key, name, ...restField }) => (
                  <div key={key} style={{ display: 'grid', gridTemplateColumns: '220px 1fr 1fr 40px', gap: 8, marginBottom: 8 }}>
                    <Form.Item {...restField} name={[name, 'tipoSerie']} style={{ margin: 0 }}>
                      <Select
                        showSearch
                        placeholder="Tipo de serie"
                        options={TIPOS_SERIE_DEFAULT.map((t) => ({ value: t, label: t }))}
                      />
                    </Form.Item>
                    <Form.Item {...restField} name={[name, 'valor']} style={{ margin: 0 }}>
                      <Input placeholder="Número de serie / Identificador" />
                    </Form.Item>
                    <Form.Item {...restField} name={[name, 'observaciones']} style={{ margin: 0 }}>
                      <Input placeholder="Observaciones (opcional)" />
                    </Form.Item>
                    <Button danger icon={<DeleteOutlined />} onClick={() => remove(name)} />
                  </div>
                ))}
                <Button type="dashed" onClick={() => add({ tipoSerie: 'Serie Radio Interna', valor: '' })} block icon={<PlusOutlined />} style={{ marginBottom: 12 }}>
                  Agregar otro número de serie (Radio Interna, Externa, Controladora, Antena...)
                </Button>
              </>
            )}
          </Form.List>

          <Form.Item name="observaciones" label="Observaciones generales">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
