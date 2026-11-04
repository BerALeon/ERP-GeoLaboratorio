import { Alert, Button, Card, Col, Divider, Row, Space, Spin, Statistic, Table, Tabs, Tag, Typography } from 'antd'
import { useQuery } from '@tanstack/react-query'
import { Bar, BarChart, CartesianGrid, Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import dayjs from 'dayjs'
import { useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import type { Alerta, Dashboard } from '../api/types'
import { colors } from '../theme'

const PIE = [colors.ok, colors.warn, colors.bad, '#6b7280', '#0A2A8F', '#1976D2']

const alertCols = [
  { title: 'Folio', dataIndex: 'folio', render: (v: string) => <strong>{v}</strong> },
  { title: 'Cliente', dataIndex: 'cliente' },
  { title: 'Equipo', dataIndex: 'equipo' },
  { title: 'Vence', dataIndex: 'fechaVencimiento', render: (v: string) => dayjs(v).format('DD/MM/YYYY') },
  { title: 'Días', dataIndex: 'dias', width: 80, render: (d: number) => <Tag color={d <= 7 ? 'red' : 'orange'}>{d} d</Tag> },
]

const money = (v: number) =>
  new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN', maximumFractionDigits: 2 }).format(v || 0)

export default function DashboardPage() {
  const nav = useNavigate()
  const { data, isLoading, error } = useQuery({
    queryKey: ['dashboard'],
    queryFn: async () => (await api.get<Dashboard>('/dashboard')).data,
  })
  if (isLoading) return <Spin />
  if (error || !data) return <Alert type="error" message="No fue posible cargar el dashboard." />

  const k = data.kpis
  const g = data.generales ?? {
    clientesRegistrados: k.clientes,
    equiposRegistrados: k.equipos,
    ordenesServicioAbiertas: 0,
    equiposEnTaller: 0,
    certificadosVigentes: k.activos,
    certificadosPorVencer: k.porVencer,
    ventasDelMes: 0,
    cantidadVentasDelMes: 0,
    valorTotalInventario: 0,
    productosSinStock: 0,
    refaccionesStockCritico: 0,
    ventasSinSalidaInventario: 0,
  }
  const t = data.tecnicos ?? {
    equiposRecibidosHoy: 0,
    equiposEnDiagnostico: 0,
    equiposEnReparacion: 0,
    equiposEnCalibracion: 0,
    equiposTerminados: 0,
    equiposEntregados: 0,
  }
  const inv = data.inventario ?? {
    productosRegistrados: 0,
    stockBajo: 0,
    refaccionesMasUtilizadas: [],
    productosMasVendidos: [],
    entradasDelMes: 0,
    salidasDelMes: 0,
    unidadesEntradasMes: 0,
    unidadesSalidasMes: 0,
  }

  const indicadoresGenerales = [
    { label: 'Clientes registrados', value: g.clientesRegistrados, color: colors.primary, link: '/clientes' },
    { label: 'Equipos registrados', value: g.equiposRegistrados, color: colors.primary, link: '/equipos' },
    { label: 'Órdenes de servicio abiertas', value: g.ordenesServicioAbiertas, color: colors.warn, link: '/ordenes-servicio' },
    { label: 'Equipos en taller', value: g.equiposEnTaller, color: '#1976D2', link: '/ordenes-servicio' },
    { label: 'Certificados vigentes', value: g.certificadosVigentes, color: colors.ok, link: '/certificados' },
    { label: 'Certificados por vencer', value: g.certificadosPorVencer, color: colors.warn, link: '/certificados' },
    { label: 'Ventas del mes', value: money(g.ventasDelMes), color: colors.ok, link: '/ventas' },
    { label: 'Valor total del inventario', value: money(g.valorTotalInventario), color: colors.primary, link: '/inventario' },
    { label: 'Productos sin stock', value: g.productosSinStock, color: g.productosSinStock > 0 ? colors.bad : colors.ok, link: '/inventario' },
    { label: 'Refacciones con stock crítico', value: g.refaccionesStockCritico, color: g.refaccionesStockCritico > 0 ? colors.bad : colors.ok, link: '/inventario' },
  ]

  const indicadoresTecnicos = [
    { label: 'Equipos recibidos hoy', value: t.equiposRecibidosHoy, color: colors.primary },
    { label: 'Equipos en diagnóstico', value: t.equiposEnDiagnostico, color: colors.warn },
    { label: 'Equipos en reparación', value: t.equiposEnReparacion, color: '#D97706' },
    { label: 'Equipos en calibración', value: t.equiposEnCalibracion, color: '#1976D2' },
    { label: 'Equipos terminados', value: t.equiposTerminados, color: colors.ok },
    { label: 'Equipos entregados', value: t.equiposEntregados, color: '#374151' },
  ]

  const indicadoresInventario = [
    { label: 'Productos registrados', value: inv.productosRegistrados, color: colors.primary },
    { label: 'Stock bajo', value: inv.stockBajo, color: inv.stockBajo > 0 ? colors.warn : colors.ok },
    { label: 'Entradas del mes', value: `${inv.entradasDelMes} mov. (${inv.unidadesEntradasMes} u.)`, color: colors.ok },
    { label: 'Salidas del mes', value: `${inv.salidasDelMes} mov. (${inv.unidadesSalidasMes} u.)`, color: colors.primary },
  ]

  const tab = (label: string, rows: Alerta[]) => ({
    key: label,
    label: `${label} (${rows.length})`,
    children: <Table size="small" rowKey="certificadoId" columns={alertCols} dataSource={rows} pagination={false} locale={{ emptyText: 'Sin vencimientos próximos' }} />,
  })

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12, flexWrap: 'wrap', gap: 8 }}>
        <div>
          <h1 className="page-title" style={{ marginBottom: 2 }}>Dashboard Ejecutivo · ERP GEOLABORATORIO</h1>
          <Typography.Text type="secondary">Centro integral de trazabilidad de equipos topográficos, taller, calibración, inventario y ventas</Typography.Text>
        </div>
        <Space wrap>
          <Button type="primary" onClick={() => nav('/ordenes-servicio')}>Recepción de Equipo (Nueva OS)</Button>
          <Button onClick={() => nav('/equipos')}>Expedientes 360° de Equipos</Button>
          <Button onClick={() => nav('/certificados')}>Emitir Certificado</Button>
        </Space>
      </div>

      {g.ventasSinSalidaInventario > 0 && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message={`Sincronización Ventas-Inventario: Existen ${g.ventasSinSalidaInventario} venta(s) entregadas/facturadas pendientes de registrar salida de almacén.`}
          action={<Button size="small" type="primary" onClick={() => nav('/ventas')}>Revisar en Ventas</Button>}
        />
      )}

      <Divider style={{ margin: '8px 0 12px' }}>Indicadores Generales</Divider>
      <Row gutter={[12, 12]}>
        {indicadoresGenerales.map((item) => (
          <Col key={item.label} xs={12} md={8} xl={4}>
            <Card size="small" hoverable onClick={() => nav(item.link)} style={{ height: '100%' }}>
              <div className="kpi-label">{item.label}</div>
              <Statistic value={item.value} valueStyle={{ color: item.color, fontWeight: 600, fontSize: 20 }} />
            </Card>
          </Col>
        ))}
      </Row>

      <Divider style={{ margin: '20px 0 12px' }}>Indicadores Técnicos (Taller y Laboratorio)</Divider>
      <Row gutter={[12, 12]}>
        {indicadoresTecnicos.map((item) => (
          <Col key={item.label} xs={12} md={8} xl={4}>
            <Card size="small" hoverable onClick={() => nav('/ordenes-servicio')}>
              <div className="kpi-label">{item.label}</div>
              <Statistic value={item.value} valueStyle={{ color: item.color, fontWeight: 600, fontSize: 22 }} />
            </Card>
          </Col>
        ))}
      </Row>

      <Divider style={{ margin: '20px 0 12px' }}>Indicadores de Inventario y Kardex</Divider>
      <Row gutter={[12, 12]}>
        {indicadoresInventario.map((item) => (
          <Col key={item.label} xs={12} md={6}>
            <Card size="small" hoverable onClick={() => nav('/inventario')}>
              <div className="kpi-label">{item.label}</div>
              <Statistic value={item.value} valueStyle={{ color: item.color, fontWeight: 600, fontSize: 19 }} />
            </Card>
          </Col>
        ))}
      </Row>

      <Row gutter={[16, 16]} style={{ marginTop: 12 }}>
        <Col xs={24} md={12}>
          <Card size="small" title="Refacciones más utilizadas en taller">
            <Table
              size="small"
              rowKey="etiqueta"
              pagination={false}
              dataSource={inv.refaccionesMasUtilizadas}
              locale={{ emptyText: 'Sin consumos registrados aún' }}
              columns={[
                { title: 'Refacción', dataIndex: 'etiqueta' },
                { title: 'Unidades utilizadas', dataIndex: 'valor', width: 150, align: 'right', render: (v: number) => <strong>{v}</strong> },
              ]}
            />
          </Card>
        </Col>
        <Col xs={24} md={12}>
          <Card size="small" title="Productos más vendidos">
            <Table
              size="small"
              rowKey="etiqueta"
              pagination={false}
              dataSource={inv.productosMasVendidos}
              locale={{ emptyText: 'Sin ventas registradas aún' }}
              columns={[
                { title: 'Producto / Equipo', dataIndex: 'etiqueta' },
                { title: 'Unidades vendidas', dataIndex: 'valor', width: 150, align: 'right', render: (v: number) => <strong>{v}</strong> },
              ]}
            />
          </Card>
        </Col>
      </Row>

      <Card size="small" title="Alertas de vencimiento de certificados de calibración" style={{ marginTop: 16 }}>
        <Tabs items={[tab('7 días', data.vencen7), tab('15 días', data.vencen15), tab('30 días', data.vencen30)]} />
      </Card>

      <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
        <Col xs={24} xl={12}>
          <Card size="small" title="Certificados emitidos por mes">
            <ResponsiveContainer width="100%" height={250}>
              <BarChart data={data.porMes}>
                <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                <XAxis dataKey="etiqueta" />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="valor" name="Certificados" fill={colors.primary} isAnimationActive={false} />
              </BarChart>
            </ResponsiveContainer>
          </Card>
        </Col>
        <Col xs={24} xl={12}>
          <Card size="small" title="Estado de certificados">
            <ResponsiveContainer width="100%" height={250}>
              <PieChart>
                <Pie data={data.porEstado} dataKey="valor" nameKey="etiqueta" outerRadius={85} label isAnimationActive={false}>
                  {data.porEstado.map((_, i) => <Cell key={i} fill={PIE[i % PIE.length]} />)}
                </Pie>
                <Tooltip />
                <Legend />
              </PieChart>
            </ResponsiveContainer>
          </Card>
        </Col>
        <Col xs={24}>
          <Card size="small" title="Certificados emitidos por tipo de equipo">
            <ResponsiveContainer width="100%" height={250}>
              <BarChart data={data.porTipo} layout="vertical" margin={{ left: 40 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                <XAxis type="number" allowDecimals={false} />
                <YAxis type="category" dataKey="etiqueta" width={120} />
                <Tooltip />
                <Bar dataKey="valor" name="Certificados" fill={colors.dark} isAnimationActive={false} />
              </BarChart>
            </ResponsiveContainer>
          </Card>
        </Col>
      </Row>
      <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8 }}>
        Datos integrados en tiempo real sobre una única base de datos ERP.
      </Typography.Text>
    </>
  )
}
