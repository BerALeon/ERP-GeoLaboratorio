import { DeleteOutlined, PhoneOutlined, PlusOutlined, UserAddOutlined } from '@ant-design/icons'
import { App, Button, Card, Divider, Form, Input, Modal, Popover, Space, Table, Tag, Typography } from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api, errorMessage } from '../api/client'
import type { Cliente, ClienteContacto, Paged } from '../api/types'
import { useAuth } from '../auth/AuthContext'

export default function ClientesPage() {
  const { hasRole } = useAuth()
  const { message } = App.useApp()
  const qc = useQueryClient()
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<Partial<Cliente> | null>(null)
  const [contactos, setContactos] = useState<ClienteContacto[]>([])
  const [subFormOpen, setSubFormOpen] = useState(false)
  const [nuevoContacto, setNuevoContacto] = useState<ClienteContacto>({ nombreCompleto: '', telefono: '', nota: '' })
  const [form] = Form.useForm()
  const canEdit = hasRole('Administrador', 'Tecnico')

  const { data, isLoading } = useQuery({
    queryKey: ['clientes', q, page],
    queryFn: async () => (await api.get<Paged<Cliente>>('/clientes', { params: { q, page, size: 15 } })).data,
  })

  const save = useMutation({
    mutationFn: (v: Partial<Cliente>) => {
      // Si quedó algún contacto escrito en el subformulario abierto sin presionar "Agregar a la lista", lo incluimos automáticamente
      const listaFinal = [...contactos]
      if (subFormOpen && (nuevoContacto.nombreCompleto.trim() || nuevoContacto.telefono.trim())) {
        listaFinal.push({
          nombreCompleto: nuevoContacto.nombreCompleto.trim(),
          telefono: nuevoContacto.telefono.trim(),
          nota: nuevoContacto.nota?.trim() || undefined,
        })
      }
      const payload = { ...v, contactos: listaFinal }
      return editing?.clienteId ? api.put(`/clientes/${editing.clienteId}`, payload) : api.post('/clientes', payload)
    },
    onSuccess: () => {
      message.success('Cliente guardado')
      setEditing(null)
      setSubFormOpen(false)
      qc.invalidateQueries({ queryKey: ['clientes'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const open = (c: Partial<Cliente>, abrirSubForm = false) => {
    setEditing(c)
    setContactos(c.contactos ?? [])
    setSubFormOpen(abrirSubForm)
    setNuevoContacto({ nombreCompleto: '', telefono: '', nota: '' })
    form.resetFields()
    form.setFieldsValue({ pais: 'México', ...c })
  }

  const agregarContactoSubForm = () => {
    if (!nuevoContacto.nombreCompleto.trim() || !nuevoContacto.telefono.trim()) {
      message.warning('Ingrese el nombre completo y el teléfono del contacto')
      return
    }
    setContactos((prev) => [
      ...prev,
      {
        nombreCompleto: nuevoContacto.nombreCompleto.trim(),
        telefono: nuevoContacto.telefono.trim(),
        nota: nuevoContacto.nota?.trim() || undefined,
      },
    ])
    setNuevoContacto({ nombreCompleto: '', telefono: '', nota: '' })
    setSubFormOpen(false)
    message.success('Contacto agregado al cliente')
  }

  const eliminarContacto = (idx: number) => {
    setContactos((prev) => prev.filter((_, i) => i !== idx))
  }

  return (
    <>
      <h1 className="page-title">Clientes</h1>
      <div className="toolbar">
        <Input.Search
          placeholder="Buscar por razón social, nombre comercial o RFC"
          allowClear
          style={{ maxWidth: 420 }}
          onSearch={(v) => {
            setQ(v)
            setPage(1)
          }}
        />
        {canEdit && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => open({})}>
            Nuevo cliente
          </Button>
        )}
      </div>
      <Table
        rowKey="clienteId"
        loading={isLoading}
        dataSource={data?.items}
        size="middle"
        bordered
        scroll={{ x: 'max-content' }}
        pagination={{ current: page, pageSize: 15, total: data?.total, onChange: setPage, showSizeChanger: false }}
        onRow={(r) => ({ onDoubleClick: () => canEdit && open(r) })}
        columns={[
          { title: 'Razón social', dataIndex: 'razonSocial' },
          { title: 'Nombre comercial', dataIndex: 'nombreComercial' },
          { title: 'RFC', dataIndex: 'rfc' },
          { title: 'Ciudad', dataIndex: 'ciudad' },
          { title: 'Contacto', dataIndex: 'contacto' },
          { title: 'Teléfono', dataIndex: 'telefono' },
          {
            title: 'Contactos adicionales',
            width: 220,
            render: (_: unknown, r: Cliente) => {
              const lista = r.contactos ?? []
              if (lista.length === 0) {
                return <Typography.Text type="secondary">Sin contactos extra</Typography.Text>
              }
              return (
                <Popover
                  title={`Contactos de ${r.nombreComercial || r.razonSocial}`}
                  content={
                    <div style={{ maxWidth: 340 }}>
                      {lista.map((c, i) => (
                        <div key={i} style={{ padding: '6px 0', borderBottom: i < lista.length - 1 ? '1px solid #f0f0f0' : 'none' }}>
                          <div><strong>{c.nombreCompleto}</strong> · <PhoneOutlined /> {c.telefono}</div>
                          {c.nota && <div style={{ fontSize: 12, color: '#6b7280' }}>Nota: {c.nota}</div>}
                        </div>
                      ))}
                    </div>
                  }
                >
                  <Space size={[4, 4]} wrap style={{ cursor: 'pointer' }}>
                    <Tag color="blue" icon={<PhoneOutlined />}>
                      {lista.length} {lista.length === 1 ? 'contacto' : 'contactos'}
                    </Tag>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {lista[0].nombreCompleto} ({lista[0].telefono})
                    </Typography.Text>
                  </Space>
                </Popover>
              )
            },
          },
          { title: 'Equipos', dataIndex: 'totalEquipos', width: 80 },
          ...(canEdit
            ? [
                {
                  title: 'Acciones',
                  width: 190,
                  render: (_: unknown, r: Cliente) => (
                    <Space size={4}>
                      <Button size="small" icon={<UserAddOutlined />} onClick={() => open(r, true)}>
                        + Contacto
                      </Button>
                      <Button size="small" onClick={() => open(r, false)}>
                        Editar
                      </Button>
                    </Space>
                  ),
                },
              ]
            : []),
        ]}
      />

      <Modal
        open={!!editing}
        title={editing?.clienteId ? 'Editar cliente' : 'Nuevo cliente'}
        onCancel={() => setEditing(null)}
        onOk={() => form.submit()}
        confirmLoading={save.isPending}
        okText="Guardar"
        cancelText="Cancelar"
        width={740}
        destroyOnClose
      >
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item name="razonSocial" label="Razón social" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="nombreComercial" label="Nombre comercial">
            <Input />
          </Form.Item>
          <Form.Item
            name="rfc"
            label="RFC"
            rules={[{ required: true, pattern: /^[A-ZÑ&a-zñ]{3,4}\d{6}[A-Za-z0-9]{3}$/, message: 'RFC inválido' }]}
          >
            <Input style={{ textTransform: 'uppercase' }} maxLength={13} />
          </Form.Item>
          <Form.Item name="direccion" label="Dirección">
            <Input />
          </Form.Item>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
            <Form.Item name="ciudad" label="Ciudad">
              <Input />
            </Form.Item>
            <Form.Item name="estado" label="Estado">
              <Input />
            </Form.Item>
            <Form.Item name="pais" label="País">
              <Input />
            </Form.Item>
            <Form.Item name="telefono" label="Teléfono">
              <Input />
            </Form.Item>
            <Form.Item name="correo" label="Correo electrónico" rules={[{ type: 'email' }]}>
              <Input />
            </Form.Item>
            <Form.Item name="contacto" label="Contacto principal">
              <Input />
            </Form.Item>
          </div>

          <Divider style={{ margin: '8px 0 12px' }}>Números de Contacto del Cliente</Divider>

          {contactos.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 12 }}>
              {contactos.map((c, idx) => (
                <Card key={idx} size="small" style={{ background: '#f8fafc', borderColor: '#e2e8f0' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
                    <div>
                      <div>
                        <strong>{c.nombreCompleto}</strong> · <Tag color="geekblue"><PhoneOutlined /> {c.telefono}</Tag>
                      </div>
                      {c.nota && (
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          Nota: {c.nota}
                        </Typography.Text>
                      )}
                    </div>
                    <Button size="small" danger type="text" icon={<DeleteOutlined />} onClick={() => eliminarContacto(idx)} />
                  </div>
                </Card>
              ))}
            </div>
          )}

          {!subFormOpen ? (
            <Button
              type="dashed"
              block
              icon={<UserAddOutlined />}
              onClick={() => setSubFormOpen(true)}
              style={{ marginBottom: 4 }}
            >
              Agregar contacto
            </Button>
          ) : (
            <Card
              size="small"
              title="Nuevo contacto del cliente"
              style={{ background: '#f9fafb', borderColor: '#0A2A8F', marginBottom: 8 }}
              extra={
                <Button size="small" type="text" onClick={() => setSubFormOpen(false)}>
                  Cerrar
                </Button>
              }
            >
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 10 }}>
                <div>
                  <div style={{ fontSize: 12, fontWeight: 500, marginBottom: 4 }}>Nombre completo *</div>
                  <Input
                    placeholder="Ej. Ing. Juan Pérez López"
                    value={nuevoContacto.nombreCompleto}
                    onChange={(e) => setNuevoContacto((prev) => ({ ...prev, nombreCompleto: e.target.value }))}
                  />
                </div>
                <div>
                  <div style={{ fontSize: 12, fontWeight: 500, marginBottom: 4 }}>Teléfono *</div>
                  <Input
                    placeholder="Ej. 477 123 4567"
                    value={nuevoContacto.telefono}
                    onChange={(e) => setNuevoContacto((prev) => ({ ...prev, telefono: e.target.value }))}
                  />
                </div>
              </div>
              <div style={{ marginBottom: 10 }}>
                <div style={{ fontSize: 12, fontWeight: 500, marginBottom: 4 }}>Nota</div>
                <Input.TextArea
                  rows={2}
                  placeholder="Espacio para una nota (ej. extensión, horario, puesto o indicaciones)"
                  value={nuevoContacto.nota}
                  onChange={(e) => setNuevoContacto((prev) => ({ ...prev, nota: e.target.value }))}
                />
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
                <Button size="small" onClick={() => setSubFormOpen(false)}>
                  Cancelar
                </Button>
                <Button size="small" type="primary" icon={<PlusOutlined />} onClick={agregarContactoSubForm}>
                  Agregar contacto
                </Button>
              </div>
            </Card>
          )}
        </Form>
      </Modal>
    </>
  )
}
