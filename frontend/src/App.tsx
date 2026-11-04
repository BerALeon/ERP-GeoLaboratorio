import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/AuthContext'
import type { Rol } from './api/types'
import AppLayout from './layout/AppLayout'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import ClientesPage from './pages/ClientesPage'
import EquiposPage from './pages/EquiposPage'
import ExpedienteEquipoPage from './pages/ExpedienteEquipoPage'
import CertificadosPage from './pages/CertificadosPage'
import OrdenesServicioPage from './pages/OrdenesServicioPage'
import InventarioPage from './pages/InventarioPage'
import VentasPage from './pages/VentasPage'
import MantenimientosGarantiasPage from './pages/MantenimientosGarantiasPage'
import UsuariosPage from './pages/UsuariosPage'
import AuditoriaPage from './pages/AuditoriaPage'
import VerificarPage from './pages/VerificarPage'

function Protected({ roles }: { roles?: Rol[] }) {
  const { user } = useAuth()
  if (!user) return <Navigate to="/login" replace />
  if (roles && !roles.includes(user.rol)) return <Navigate to="/" replace />
  return <Outlet />
}

function Home() {
  return <Navigate to="/dashboard" replace />
}

export default function App() {
  return (
    <Routes>
      {/* Portal público (sin autenticación) */}
      <Route path="/verificar/:folio" element={<VerificarPage />} />
      <Route path="/login" element={<LoginPage />} />

      <Route element={<Protected />}>
        <Route element={<AppLayout />}>
          <Route index element={<Home />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="certificados" element={<CertificadosPage />} />
          <Route element={<Protected roles={['Administrador', 'Tecnico']} />}>
            <Route path="clientes" element={<ClientesPage />} />
            <Route path="equipos" element={<EquiposPage />} />
            <Route path="equipos/:id" element={<ExpedienteEquipoPage />} />
            <Route path="ordenes-servicio" element={<OrdenesServicioPage />} />
            <Route path="inventario" element={<InventarioPage />} />
            <Route path="ventas" element={<VentasPage />} />
            <Route path="mantenimientos" element={<MantenimientosGarantiasPage />} />
          </Route>
          <Route element={<Protected roles={['Administrador']} />}>
            <Route path="usuarios" element={<UsuariosPage />} />
            <Route path="auditoria" element={<AuditoriaPage />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
