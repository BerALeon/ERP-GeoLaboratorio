import {
  AppstoreOutlined,
  AuditOutlined,
  DashboardOutlined,
  DollarOutlined,
  FileProtectOutlined,
  LogoutOutlined,
  ReconciliationOutlined,
  SafetyCertificateOutlined,
  TeamOutlined,
  ToolOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { Button, Layout, Menu, Space, Typography } from 'antd'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const { Header, Sider, Content } = Layout

export default function AppLayout() {
  const { user, logout, hasRole } = useAuth()
  const nav = useNavigate()
  const { pathname } = useLocation()

  const selectedKey = pathname.startsWith('/equipos/') ? '/equipos' : pathname

  const items = [
    { key: '/dashboard', icon: <DashboardOutlined />, label: 'Dashboard ERP' },
    hasRole('Administrador', 'Tecnico') && { key: '/equipos', icon: <ToolOutlined />, label: 'Equipos (360°)' },
    hasRole('Administrador', 'Tecnico') && { key: '/ordenes-servicio', icon: <ReconciliationOutlined />, label: 'Órdenes de Servicio' },
    { key: '/certificados', icon: <FileProtectOutlined />, label: 'Certificados y Calib.' },
    hasRole('Administrador', 'Tecnico') && { key: '/mantenimientos', icon: <SafetyCertificateOutlined />, label: 'Mantenimientos / Gar.' },
    hasRole('Administrador', 'Tecnico') && { key: '/inventario', icon: <AppstoreOutlined />, label: 'Inventario y Kardex' },
    hasRole('Administrador', 'Tecnico') && { key: '/ventas', icon: <DollarOutlined />, label: 'Ventas y Cotizaciones' },
    hasRole('Administrador', 'Tecnico') && { key: '/clientes', icon: <TeamOutlined />, label: 'Clientes' },
    hasRole('Administrador') && { key: '/usuarios', icon: <UserOutlined />, label: 'Usuarios' },
    hasRole('Administrador') && { key: '/auditoria', icon: <AuditOutlined />, label: 'Auditoría' },
  ].filter(Boolean) as { key: string; icon: React.ReactNode; label: string }[]

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider width={235} breakpoint="lg" collapsedWidth={64}>
        <div style={{ background: '#fff', padding: 12, textAlign: 'center' }}>
          <img src="/logo.png" alt="GEOLABORATORIO" style={{ maxWidth: '100%', maxHeight: 40 }} />
        </div>
        <Menu theme="dark" mode="inline" selectedKeys={[selectedKey]} items={items} onClick={(e) => nav(e.key)} />
      </Sider>
      <Layout>
        <Header style={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'center', borderBottom: '1px solid #D5D9DF', padding: '0 24px' }}>
          <Space>
            <Typography.Text type="secondary">{user?.nombre} · {user?.rol}</Typography.Text>
            <Button icon={<LogoutOutlined />} onClick={() => { logout(); nav('/login') }}>Salir</Button>
          </Space>
        </Header>
        <Content style={{ padding: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  )
}
