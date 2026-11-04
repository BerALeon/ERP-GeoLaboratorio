import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'
import { api, TOKEN_KEY } from '../api/client'
import type { LoginResponse, Rol, Usuario } from '../api/types'

interface AuthCtx {
  user: Usuario | null
  login: (correo: string, password: string) => Promise<void>
  logout: () => void
  hasRole: (...roles: Rol[]) => boolean
}

const Ctx = createContext<AuthCtx>(null!)
export const useAuth = () => useContext(Ctx)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<Usuario | null>(() => {
    const raw = localStorage.getItem('geolab_user')
    return raw && localStorage.getItem(TOKEN_KEY) ? JSON.parse(raw) : null
  })

  const value = useMemo<AuthCtx>(() => ({
    user,
    async login(correo, password) {
      const { data } = await api.post<LoginResponse>('/auth/login', { correo, password })
      localStorage.setItem(TOKEN_KEY, data.token)
      localStorage.setItem('geolab_user', JSON.stringify(data.usuario))
      setUser(data.usuario)
    },
    logout() {
      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem('geolab_user')
      setUser(null)
    },
    hasRole: (...roles) => !!user && roles.includes(user.rol),
  }), [user])

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>
}
