import { PlusOutlined } from '@ant-design/icons'
import { App, Button, Form, Input, Modal, Select, Switch, Table, Tag } from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api, errorMessage } from '../api/client'
import type { Usuario } from '../api/types'

const ROLES = ['Administrador', 'Tecnico', 'Consulta'].map((v) => ({ value: v, label: v === 'Tecnico' ? 'Técnico' : v }))

export default function UsuariosPage() {
  const { message } = App.useApp()
  const qc = useQueryClient()
  const [editing, setEditing] = useState<Partial<Usuario> | null>(null)
  const [form] = Form.useForm()
  const { data, isLoading } = useQuery({ queryKey: ['usuarios'], queryFn: async () => (await api.get<Usuario[]>('/usuarios')).data })

  const save = useMutation({
    mutationFn: (v: Record<string, unknown>) => editing?.usuarioId ? api.put(`/usuarios/${editing.usuarioId}`, v) : api.post('/usuarios', v),
    onSuccess: () => { message.success('Usuario guardado'); setEditing(null); qc.invalidateQueries({ queryKey: ['usuarios'] }) },
    onError: (e) => message.error(errorMessage(e)),
  })
  const open = (u: Partial<Usuario>) => { setEditing(u); form.resetFields(); form.setFieldsValue({ activo: true, rol: 'Tecnico', ...u }) }

  return (
    <>
      <h1 className="page-title">Usuarios</h1>
      <div className="toolbar"><span /><Button type="primary" icon={<PlusOutlined />} onClick={() => open({})}>Nuevo usuario</Button></div>
      <Table rowKey="usuarioId" loading={isLoading} dataSource={data} bordered size="middle" pagination={false}
        columns={[
          { title: 'Nombre', dataIndex: 'nombre' }, { title: 'Correo', dataIndex: 'correo' }, { title: 'Rol', dataIndex: 'rol' },
          { title: 'Estado', dataIndex: 'activo', render: (a: boolean) => <Tag color={a ? 'green' : 'default'}>{a ? 'Activo' : 'Inactivo'}</Tag> },
          { title: '', width: 80, render: (_, r) => <Button size="small" onClick={() => open(r)}>Editar</Button> },
        ]} />
      <Modal open={!!editing} destroyOnClose title={editing?.usuarioId ? 'Editar usuario' : 'Nuevo usuario'} onCancel={() => setEditing(null)}
        onOk={() => form.submit()} confirmLoading={save.isPending} okText="Guardar" cancelText="Cancelar">
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item name="nombre" label="Nombre" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="correo" label="Correo" rules={[{ required: true, type: 'email' }]}><Input disabled={!!editing?.usuarioId} /></Form.Item>
          <Form.Item name="rol" label="Rol" rules={[{ required: true }]}><Select options={ROLES} /></Form.Item>
          <Form.Item name="password" label={editing?.usuarioId ? 'Nueva contraseña (opcional)' : 'Contraseña'} rules={[{ required: !editing?.usuarioId, min: 8 }]}><Input.Password autoComplete="new-password" /></Form.Item>
          {editing?.usuarioId && <Form.Item name="activo" label="Activo" valuePropName="checked"><Switch /></Form.Item>}
        </Form>
      </Modal>
    </>
  )
}
