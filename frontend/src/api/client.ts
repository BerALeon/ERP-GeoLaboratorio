import axios from 'axios'

export const TOKEN_KEY = 'geolab_token'

export const api = axios.create({ baseURL: import.meta.env.VITE_API_URL ?? '/api' })

api.interceptors.request.use((cfg) => {
  const t = localStorage.getItem(TOKEN_KEY)
  if (t) cfg.headers.Authorization = `Bearer ${t}`
  return cfg
})

api.interceptors.response.use(
  (r) => r,
  (err) => {
    const isLogin = err.config?.url?.includes('/auth/login')
    if (err.response?.status === 401 && !isLogin) {
      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem('geolab_user')
      window.location.href = '/login'
    }
    return Promise.reject(err)
  },
)

/** Extrae un mensaje legible de un error de la API (ProblemDetails / validación). */
export function errorMessage(e: unknown): string {
  const d = (e as { response?: { data?: { title?: string; errors?: Record<string, string[]> } } })?.response?.data
  if (d?.errors) return Object.values(d.errors).flat().join(' ')
  return d?.title ?? 'Ocurrió un error inesperado.'
}

/** Descarga un PDF de Certificado autenticado como archivo. */
export async function downloadPdf(id: number, folio: string, regenerar = false) {
  const r = await api.get(`/certificados/${id}/pdf`, { params: { regenerar }, responseType: 'blob' })
  const url = URL.createObjectURL(r.data)
  const a = document.createElement('a')
  a.href = url
  a.download = `${folio}.pdf`
  a.click()
  URL.revokeObjectURL(url)
}

/** Descarga el Reporte Técnico PDF de una Orden de Servicio. */
export async function downloadOrdenPdf(id: number, folio: string) {
  const r = await api.get(`/ordenes-servicio/${id}/pdf`, { responseType: 'blob' })
  const url = URL.createObjectURL(r.data)
  const a = document.createElement('a')
  a.href = url
  a.download = `Reporte_${folio}.pdf`
  a.click()
  URL.revokeObjectURL(url)
}
