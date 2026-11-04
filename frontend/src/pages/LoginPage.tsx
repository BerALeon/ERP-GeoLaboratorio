import { Alert, Button, Card, Form, Input } from 'antd'
import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { errorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'

export default function LoginPage() {
  const { user, login } = useAuth()
  const nav = useNavigate()
  const [error, setError] = useState<string>()
  const [loading, setLoading] = useState(false)

  if (user) return <Navigate to="/" replace />

  const onFinish = async (v: { correo: string; password: string }) => {
    setLoading(true); setError(undefined)
    try { await login(v.correo, v.password); nav('/') }
    catch (e) { setError(errorMessage(e)) }
    finally { setLoading(false) }
  }

  return (
    <div style={{ minHeight: '100vh', display: 'flex', justifyContent: 'center', alignItems: 'center', background: '#EEF0F3', padding: 16 }}>
      <Card style={{ width: 400, maxWidth: '100%' }} styles={{ body: { padding: 32 } }}>
        <img src="/logo.png" alt="GEOLABORATORIO" style={{ width: '70%', display: 'block', margin: '0 auto 8px' }} />
        <p style={{ textAlign: 'center', color: '#6b7280', marginBottom: 24 }}>ERP Especializado en Ingeniería y Equipos Topográficos</p>
        {error && <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} />}
        <Form layout="vertical" onFinish={onFinish} requiredMark={false}>
          <Form.Item name="correo" label="Correo electrónico" rules={[{ required: true, type: 'email' }]}><Input autoFocus /></Form.Item>
          <Form.Item name="password" label="Contraseña" rules={[{ required: true }]}><Input.Password /></Form.Item>
          <Button type="primary" htmlType="submit" block loading={loading}>Iniciar sesión</Button>
        </Form>
      </Card>
    </div>
  )
}
