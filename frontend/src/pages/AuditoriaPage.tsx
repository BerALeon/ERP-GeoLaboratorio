import { Select, Table, Typography } from 'antd'
import { useQuery } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useState } from 'react'
import { api } from '../api/client'
import type { AuditLog, Paged } from '../api/types'

const ENTIDADES = [
  'Equipo',
  'OrdenServicio',
  'Certificado',
  'Producto',
  'VentaDocumento',
  'Compra',
  'Mantenimiento',
  'Garantia',
  'Cliente',
  'Usuario',
]

export default function AuditoriaPage() {
  const [entidad, setEntidad] = useState<string>()
  const [page, setPage] = useState(1)
  const { data, isLoading } = useQuery({
    queryKey: ['auditoria', entidad, page],
    queryFn: async () => (await api.get<Paged<AuditLog>>('/auditoria', { params: { entidad, page, size: 30 } })).data,
  })
  return (
    <>
      <h1 className="page-title">Bitácora de auditoría general ERP</h1>
      <div className="toolbar">
        <Select
          placeholder="Filtrar por Entidad"
          allowClear
          style={{ width: 220 }}
          onChange={(v) => {
            setEntidad(v)
            setPage(1)
          }}
          options={ENTIDADES.map((v) => ({ value: v, label: v }))}
        />
        <Typography.Text type="secondary">Registro inalterable de altas, cambios y bajas en todos los módulos del ERP.</Typography.Text>
      </div>
      <Table
        rowKey="auditLogId"
        loading={isLoading}
        dataSource={data?.items}
        size="small"
        bordered
        pagination={{ current: page, pageSize: 30, total: data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: 'Fecha', dataIndex: 'fecha', width: 150, render: (v: string) => dayjs(v).format('DD/MM/YYYY HH:mm:ss') },
          { title: 'Usuario', dataIndex: 'usuario', width: 160 },
          { title: 'Acción', dataIndex: 'accion', width: 80 },
          { title: 'Entidad', width: 170, render: (_, r) => `${r.entidad} #${r.entidadId}` },
          { title: 'IP', dataIndex: 'ip', width: 120 },
        ]}
        expandable={{
          expandedRowRender: (r) => (
            <pre style={{ margin: 0, fontSize: 11, whiteSpace: 'pre-wrap' }}>
              {r.valoresAnteriores && `Antes: ${r.valoresAnteriores}\n`}
              {r.valoresNuevos && `Después: ${r.valoresNuevos}`}
            </pre>
          ),
        }}
      />
    </>
  )
}
