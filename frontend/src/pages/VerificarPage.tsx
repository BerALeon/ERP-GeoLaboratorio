import { Descriptions, Result, Spin } from 'antd'
import { useQuery } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useParams } from 'react-router-dom'
import { api } from '../api/client'
import type { Verificacion } from '../api/types'
import { colors } from '../theme'

const BANNER = {
  Vigente: { bg: colors.ok, text: '✅ CERTIFICADO VIGENTE' },
  Vencido: { bg: colors.warn, text: '⚠️ CERTIFICADO EXPIRADO' },
  Cancelado: { bg: colors.bad, text: '❌ CERTIFICADO EXPIRADO' },
  NoEncontrado: { bg: colors.bad, text: '❌ CERTIFICADO NO EXISTENTE' },
} as const
const f = (v?: string) => (v ? dayjs(v).format('DD/MM/YYYY') : '—')

/** Portal público: se abre al escanear el QR del certificado. No requiere autenticación. */
export default function VerificarPage() {
  const { folio } = useParams()
  const { data, isLoading, isError } = useQuery({
    queryKey: ['verificar', folio], retry: false,
    queryFn: async () => (await api.get<Verificacion>(`/public/verificar/${encodeURIComponent(folio!)}`)).data,
  })

  return (
    <div style={{ minHeight: '100%', background: colors.light }}>
      <div style={{ background: '#fff', borderBottom: '1px solid #D5D9DF', padding: 16, textAlign: 'center' }}>
        <img src="/logo.png" alt="GEOLABORATORIO" style={{ height: 44 }} />
        <div style={{ color: '#6b7280', fontSize: 13 }}>Verificación de certificados de calibración</div>
      </div>
      <div style={{ maxWidth: 640, margin: '24px auto', padding: '0 16px' }}>
        {isLoading && <div style={{ textAlign: 'center' }}><Spin /></div>}
        {isError && <Result status="warning" title="No fue posible consultar el certificado" subTitle="Intente nuevamente en unos minutos." />}
        {data && (
          <div style={{ background: '#fff', border: '1px solid #D5D9DF' }}>
            <div style={{ background: BANNER[data.estado].bg, color: '#fff', padding: '18px 16px', fontSize: 20, fontWeight: 700, textAlign: 'center' }}>
              {BANNER[data.estado].text}
            </div>
            <div style={{ padding: 16 }}>
              {data.estado === 'NoEncontrado' && <p style={{ margin: 0, textAlign: 'center' }}>Este certificado no existe en los registros de GEOLABORATORIO.</p>}
              {data.estado === 'Cancelado' && <Descriptions column={1} bordered size="small"><Descriptions.Item label="Folio">{data.folio}</Descriptions.Item><Descriptions.Item label="Estatus">Expirado</Descriptions.Item></Descriptions>}
              {(data.estado === 'Vigente' || data.estado === 'Vencido') && (
                <Descriptions column={1} bordered size="small" labelStyle={{ width: 190 }}>
                  <Descriptions.Item label="Folio">{data.folio}</Descriptions.Item>
                  <Descriptions.Item label="Cliente">{data.cliente}</Descriptions.Item>
                  <Descriptions.Item label="Equipo">{data.equipo}</Descriptions.Item>
                  <Descriptions.Item label="Número de serie">{data.serieEquipo}</Descriptions.Item>
                  <Descriptions.Item label="Fecha de calibración">{f(data.fechaCalibracion)}</Descriptions.Item>
                  <Descriptions.Item label="Fecha de vencimiento">{f(data.fechaVencimiento)}</Descriptions.Item>
                  {data.estado === 'Vencido' && <Descriptions.Item label="Días vencido">{data.diasVencido}</Descriptions.Item>}
                </Descriptions>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
