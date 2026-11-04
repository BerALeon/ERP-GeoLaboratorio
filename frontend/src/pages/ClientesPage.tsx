import { PlusOutlined } from '@ant-design/icons'
import { App, Button, Form, Input, Modal, Table } from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api, errorMessage } from '../api/client'
import type { Cliente, Paged } from '../api/types'
import { useAuth } from '../auth/AuthContext'

export default function ClientesPage() {
  const { hasRole } = useAuth()
  const { message } = App.useApp()
  const qc = useQueryClient()
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<Partial<Cliente> | null>(null)
  const [form] = Form.useForm()
  const canEdit = hasRole('Administrador')

  const { data, isLoading } = useQuery({
    queryKey: ['clientes', q, page],
    queryFn: async () => (await api.get<Paged<Cliente>>('/clientes', { params: { q, page, size: 15 } })).data,
  })

  const save = useMutation({
    mutationFn: (v: Partial<Cliente>) => editing?.clienteId ? api.put(`/clientes/${editing.clienteId}`, v) : api.post('/clientes', v),
    onSuccess: () => { message.success('Cliente guardado'); setEditing(null); qc.invalidateQueries({ queryKey: ['clientes'] }) },
    onError: (e) => message.error(errorMessage(e)),
  })

  const open = (c: Partial<Cliente>) => { setEditing(c); form.resetFields(); form.setFieldsValue({ pais: 'México', ...c }) }

  return (
    <>
      <h1 className="page-title">Clientes</h1>
      <div className="toolbar">
        <Input.Search placeholder="Buscar por razón social, nombre comercial o RFC" allowClear style={{ maxWidth: 420 }} onSearch={(v) => { setQ(v); setPage(1) }} />
        {canEdit && <Button type="primary" icon={<PlusOutlined />} onClick={() => open({})}>Nuevo cliente</Button>}
      </div>
      <Table rowKey="clienteId" loading={isLoading} dataSource={data?.items} size="middle" bordered
        pagination={{ current: page, pageSize: 15, total: data?.total, onChange: setPage, showSizeChanger: false }}
        onRow={(r) => ({ onDoubleClick: () => canEdit && open(r) })}
        columns={[
          { title: 'Razón social', dataIndex: 'razonSocial' }, { title: 'Nombre comercial', dataIndex: 'nombreComercial' },
          { title: 'RFC', dataIndex: 'rfc' }, { title: 'Ciudad', dataIndex: 'ciudad' }, { title: 'Contacto', dataIndex: 'contacto' },
          { title: 'Teléfono', dataIndex: 'telefono' }, { title: 'Equipos', dataIndex: 'totalEquipos', width: 80 },
          ...(canEdit ? [{ title: '', width: 80, render: (_: unknown, r: Cliente) => <Button size="small" onClick={() => open(r)}>Editar</Button> }] : []),
        ]} />

      <Modal open={!!editing} title={editing?.clienteId ? 'Editar cliente' : 'Nuevo cliente'} onCancel={() => setEditing(null)}
        onOk={() => form.submit()} confirmLoading={save.isPending} okText="Guardar" cancelText="Cancelar" width={720} destroyOnClose>
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item name="razonSocial" label="Razón social" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="nombreComercial" label="Nombre comercial"><Input /></Form.Item>
          <Form.Item name="rfc" label="RFC" rules={[{ required: true, pattern: /^[A-ZÑ&a-zñ]{3,4}\d{6}[A-Za-z0-9]{3}$/, message: 'RFC inválido' }]}><Input style={{ textTransform: 'uppercase' }} maxLength={13} /></Form.Item>
          <Form.Item name="direccion" label="Dirección"><Input /></Form.Item>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
            <Form.Item name="ciudad" label="Ciudad"><Input /></Form.Item>
            <Form.Item name="estado" label="Estado"><Input /></Form.Item>
            <Form.Item name="pais" label="País"><Input /></Form.Item>
            <Form.Item name="telefono" label="Teléfono"><Input /></Form.Item>
            <Form.Item name="correo" label="Correo electrónico" rules={[{ type: 'email' }]}><Input /></Form.Item>
            <Form.Item name="contacto" label="Contacto principal"><Input /></Form.Item>
          </div>
        </Form>
      </Modal>
    </>
  )
}
