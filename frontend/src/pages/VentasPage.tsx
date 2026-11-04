import {
  CheckCircleOutlined,
  DeleteOutlined,
  ExclamationCircleOutlined,
  PlusOutlined,
  SwapOutlined,
  SyncOutlined,
} from '@ant-design/icons'
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Divider,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useState } from 'react'
import { api, errorMessage } from '../api/client'
import type { Cliente, Equipo, Paged, Producto, VentaDocumento } from '../api/types'

const TIPOS_DOC = ['Cotización', 'Pedido', 'Venta', 'Remisión', 'Factura']
const ESTATUS_VENTA = [
  'Borrador',
  'Enviada',
  'Autorizada',
  'Confirmada',
  'Pendiente Entrega',
  'Entregada',
  'Facturada',
  'Cancelada',
]

const money = (v: number) =>
  new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' }).format(v || 0)

export default function VentasPage() {
  const { message } = App.useApp()
  const qc = useQueryClient()

  const [q, setQ] = useState('')
  const [tipoDoc, setTipoDoc] = useState<string>()
  const [estatusFiltro, setEstatusFiltro] = useState<string>()
  const [soloSinSalida, setSoloSinSalida] = useState(false)
  const [page, setPage] = useState(1)

  const [createOpen, setCreateOpen] = useState(false)
  const [estatusModal, setEstatusModal] = useState<VentaDocumento | null>(null)
  const [form] = Form.useForm()
  const [formEstatus] = Form.useForm()

  const { data, isLoading } = useQuery({
    queryKey: ['ventas', q, tipoDoc, estatusFiltro, soloSinSalida, page],
    queryFn: async () =>
      (
        await api.get<Paged<VentaDocumento>>('/ventas', {
          params: {
            q,
            tipoDocumento: tipoDoc,
            estatus: estatusFiltro,
            sinSalidaInventario: soloSinSalida || undefined,
            page,
            size: 15,
          },
        })
      ).data,
  })

  const { data: clientes } = useQuery({
    queryKey: ['clientes-ventas'],
    enabled: createOpen,
    queryFn: async () => (await api.get<Paged<Cliente>>('/clientes', { params: { size: 200 } })).data.items,
  })

  const { data: productos } = useQuery({
    queryKey: ['productos-ventas'],
    enabled: createOpen,
    queryFn: async () => (await api.get<Paged<Producto>>('/inventario/productos', { params: { size: 200 } })).data.items,
  })

  const { data: equipos } = useQuery({
    queryKey: ['equipos-ventas'],
    enabled: createOpen,
    queryFn: async () => (await api.get<Paged<Equipo>>('/equipos', { params: { size: 200 } })).data.items,
  })

  const crearVenta = useMutation({
    mutationFn: (v: Record<string, unknown>) => api.post('/ventas', v),
    onSuccess: () => {
      message.success('Documento comercial registrado correctamente')
      setCreateOpen(false)
      form.resetFields()
      qc.invalidateQueries({ queryKey: ['ventas'] })
      qc.invalidateQueries({ queryKey: ['inventario-productos'] })
      qc.invalidateQueries({ queryKey: ['inventario-kardex'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const cambiarEstatus = useMutation({
    mutationFn: (v: Record<string, unknown>) => api.post(`/ventas/${estatusModal!.ventaId}/estatus`, v),
    onSuccess: () => {
      message.success('Estatus comercial y sincronización con inventario actualizados')
      setEstatusModal(null)
      formEstatus.resetFields()
      qc.invalidateQueries({ queryKey: ['ventas'] })
      qc.invalidateQueries({ queryKey: ['inventario-productos'] })
      qc.invalidateQueries({ queryKey: ['inventario-kardex'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const descontarManual = useMutation({
    mutationFn: (id: number) => api.post(`/ventas/${id}/salida-inventario`),
    onSuccess: () => {
      message.success('Salida de almacén registrada automáticamente en el Kardex')
      qc.invalidateQueries({ queryKey: ['ventas'] })
      qc.invalidateQueries({ queryKey: ['inventario-productos'] })
      qc.invalidateQueries({ queryKey: ['inventario-kardex'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const pendientesSalida = (data?.items ?? []).filter((v) => v.requiereAlertaSinSalida)

  const openNuevo = (tipo: string) => {
    setCreateOpen(true)
    form.resetFields()
    form.setFieldsValue({
      tipoDocumento: tipo,
      estatus: tipo === 'Cotización' ? 'Enviada' : tipo === 'Factura' ? 'Facturada' : 'Autorizada',
      descuentoAutomaticoInventario: true,
      metodoPago: 'PUE',
      formaPago: '03 - Transferencia electrónica',
      usoCFDI: 'G03 - Gastos en general',
      partidas: [{ cantidad: 1, precioUnitario: 0, descuento: 0 }],
    })
  }

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12, flexWrap: 'wrap', gap: 8 }}>
        <div>
          <h1 className="page-title" style={{ marginBottom: 2 }}>Módulo de Ventas, Cotizaciones, Remisiones y Facturación</h1>
          <Typography.Text type="secondary">
            Ciclo comercial completo con sincronización automática de salidas de inventario al pasar a Entregada o Facturada
          </Typography.Text>
        </div>
        <Space wrap>
          <Button onClick={() => openNuevo('Cotización')}>+ Nueva Cotización</Button>
          <Button onClick={() => openNuevo('Pedido')}>+ Nuevo Pedido</Button>
          <Button onClick={() => openNuevo('Remisión')}>+ Nueva Remisión</Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => openNuevo('Venta')}>
            + Nueva Venta / Factura
          </Button>
        </Space>
      </div>

      {pendientesSalida.length > 0 && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 14 }}
          message={`Alerta de Sincronización Ventas-Inventario: ${pendientesSalida.length} venta(s) en estatus Entregada/Facturada no tienen salida de almacén registrada.`}
          description="Utilice el botón «Generar Salida» en la fila correspondiente para descontar existencias en el Kardex."
        />
      )}

      <Card size="small">
        <Tabs
          activeKey={tipoDoc ?? 'TODOS'}
          onChange={(k) => {
            setTipoDoc(k === 'TODOS' ? undefined : k)
            setPage(1)
          }}
          items={[
            { key: 'TODOS', label: 'Todos los Documentos' },
            { key: 'Cotización', label: 'Cotizaciones' },
            { key: 'Pedido', label: 'Pedidos' },
            { key: 'Venta', label: 'Ventas' },
            { key: 'Remisión', label: 'Remisiones' },
            { key: 'Factura', label: 'Facturación CFDI' },
          ]}
        />

        <div className="toolbar">
          <Space wrap>
            <Input.Search
              placeholder="Buscar por folio (COT, PED, VTA, REM, FAC), cliente o CFDI..."
              allowClear
              style={{ width: 350 }}
              onSearch={(v) => {
                setQ(v)
                setPage(1)
              }}
            />
            <Select
              placeholder="Filtrar por Estatus"
              allowClear
              style={{ width: 190 }}
              value={estatusFiltro}
              onChange={(v) => {
                setEstatusFiltro(v)
                setPage(1)
              }}
              options={ESTATUS_VENTA.map((e) => ({ value: e, label: e }))}
            />
            <Checkbox
              checked={soloSinSalida}
              onChange={(e) => {
                setSoloSinSalida(e.target.checked)
                setPage(1)
              }}
            >
              Solo ventas sin salida de inventario registrada
            </Checkbox>
          </Space>
        </div>

        <Table
          rowKey="ventaId"
          loading={isLoading}
          dataSource={data?.items}
          size="small"
          bordered
          scroll={{ x: 'max-content' }}
          pagination={{ current: page, pageSize: 15, total: data?.total, onChange: setPage, showSizeChanger: false }}
          expandable={{
            expandedRowRender: (r) => (
              <Table
                size="small"
                rowKey="ventaPartidaId"
                dataSource={r.partidas}
                pagination={false}
                columns={[
                  { title: 'SKU', dataIndex: 'sku', width: 140 },
                  { title: 'Descripción / Equipo / Producto', dataIndex: 'descripcion' },
                  { title: 'No. Serie', dataIndex: 'numeroSerie', width: 150 },
                  { title: 'Cantidad', dataIndex: 'cantidad', width: 90, align: 'right' },
                  { title: 'Precio Unit.', dataIndex: 'precioUnitario', width: 120, align: 'right', render: money },
                  { title: 'Descuento', dataIndex: 'descuento', width: 110, align: 'right', render: money },
                  { title: 'Importe', dataIndex: 'importe', width: 120, align: 'right', render: (v: number) => <strong>{money(v)}</strong> },
                ]}
              />
            ),
          }}
          columns={[
            { title: 'Folio', dataIndex: 'folio', width: 145, render: (v: string) => <strong>{v}</strong> },
            { title: 'Tipo', dataIndex: 'tipoDocumento', width: 110, render: (v: string) => <Tag color="geekblue">{v}</Tag> },
            { title: 'Fecha', dataIndex: 'fecha', width: 110, render: (v: string) => dayjs(v).format('DD/MM/YYYY') },
            { title: 'Cliente', dataIndex: 'cliente', width: 240 },
            {
              title: 'Estatus',
              dataIndex: 'estatus',
              width: 135,
              render: (v: string) => (
                <Tag color={v === 'Facturada' || v === 'Entregada' ? 'green' : v === 'Cancelada' ? 'red' : 'blue'}>{v}</Tag>
              ),
            },
            {
              title: 'Sincronización Inventario',
              width: 195,
              render: (_, r) =>
                r.salidaInventarioRegistrada ? (
                  <Tag icon={<CheckCircleOutlined />} color="success">Salida Registrada</Tag>
                ) : r.requiereAlertaSinSalida ? (
                  <Tag icon={<ExclamationCircleOutlined />} color="error">Sin Salida Registrada</Tag>
                ) : (
                  <Tag color="default">Pendiente Entrega/Factura</Tag>
                ),
            },
            { title: 'UUID / CFDI', dataIndex: 'folioFacturaCFDI', width: 130, render: (v?: string) => v ?? '—' },
            { title: 'Subtotal', dataIndex: 'subtotal', width: 110, align: 'right', render: money },
            { title: 'IVA', dataIndex: 'iva', width: 100, align: 'right', render: money },
            { title: 'Total', dataIndex: 'total', width: 120, align: 'right', render: (v: number) => <strong>{money(v)}</strong> },
            {
              title: 'Acciones',
              fixed: 'right',
              width: 220,
              render: (_, r) => (
                <Space size={4}>
                  <Button
                    size="small"
                    icon={<SwapOutlined />}
                    onClick={() => {
                      setEstatusModal(r)
                      formEstatus.setFieldsValue({
                        nuevoEstatus: r.estatus,
                        nuevoTipoDocumento: r.tipoDocumento,
                        folioFacturaCFDI: r.folioFacturaCFDI,
                        usoCFDI: r.usoCFDI ?? 'G03 - Gastos en general',
                        metodoPago: r.metodoPago ?? 'PUE',
                        formaPago: r.formaPago ?? '03 - Transferencia electrónica',
                        descontarInventarioAhora: true,
                      })
                    }}
                  >
                    Evolucionar / Estatus
                  </Button>
                  {r.requiereAlertaSinSalida && (
                    <Button
                      size="small"
                      type="primary"
                      danger
                      icon={<SyncOutlined />}
                      loading={descontarManual.isPending}
                      onClick={() => descontarManual.mutate(r.ventaId)}
                    >
                      Generar Salida
                    </Button>
                  )}
                </Space>
              ),
            },
          ]}
        />
      </Card>

      {/* ───────────── MODAL: CREAR COTIZACIÓN / PEDIDO / VENTA / REMISIÓN / FACTURA ───────────── */}
      <Modal
        open={createOpen}
        title="Registrar Documento Comercial (Cotización, Pedido, Venta, Remisión o Factura)"
        onCancel={() => setCreateOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={crearVenta.isPending}
        width={880}
        okText="Guardar Documento Comercial"
        destroyOnClose
      >
        <Form form={form} layout="vertical" onFinish={(v) => crearVenta.mutate(v)}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 2fr', gap: 12 }}>
            <Form.Item name="tipoDocumento" label="Tipo de Documento" rules={[{ required: true }]}>
              <Select options={TIPOS_DOC.map((t) => ({ value: t, label: t }))} />
            </Form.Item>
            <Form.Item name="estatus" label="Estatus Inicial" rules={[{ required: true }]}>
              <Select options={ESTATUS_VENTA.map((e) => ({ value: e, label: e }))} />
            </Form.Item>
            <Form.Item name="clienteId" label="Cliente" rules={[{ required: true }]}>
              <Select
                showSearch
                optionFilterProp="label"
                options={clientes?.map((c) => ({ value: c.clienteId, label: `${c.razonSocial} (${c.rfc})` }))}
              />
            </Form.Item>
          </div>

          <Divider>Partidas de Productos / Equipos / Servicios</Divider>
          <Form.List name="partidas">
            {(fields, { add, remove }) => (
              <>
                {fields.map(({ key, name, ...restField }) => (
                  <div key={key} style={{ display: 'grid', gridTemplateColumns: '220px 1fr 140px 90px 120px 40px', gap: 8, marginBottom: 8 }}>
                    <Form.Item {...restField} name={[name, 'productoId']} style={{ margin: 0 }}>
                      <Select
                        allowClear
                        showSearch
                        optionFilterProp="label"
                        placeholder="Producto de Inventario..."
                        onChange={(prodId) => {
                          const p = productos?.find((x) => x.productoId === prodId)
                          if (p) {
                            const curr = form.getFieldValue('partidas')
                            curr[name].descripcion = `${p.sku} · ${p.nombre}`
                            curr[name].precioUnitario = p.precioVenta
                            form.setFieldsValue({ partidas: [...curr] })
                          }
                        }}
                        options={productos?.map((p) => ({
                          value: p.productoId,
                          label: `${p.sku} · ${p.nombre} (Stock: ${p.stockActual})`,
                        }))}
                      />
                    </Form.Item>
                    <Form.Item {...restField} name={[name, 'descripcion']} style={{ margin: 0 }} rules={[{ required: true }]}>
                      <Input placeholder="Descripción del producto o servicio" />
                    </Form.Item>
                    <Form.Item {...restField} name={[name, 'equipoId']} style={{ margin: 0 }}>
                      <Select
                        allowClear
                        showSearch
                        optionFilterProp="label"
                        placeholder="Equipo (opcional)"
                        options={equipos?.map((e) => ({ value: e.equipoId, label: `${e.seriePrincipal} (${e.modelo})` }))}
                      />
                    </Form.Item>
                    <Form.Item {...restField} name={[name, 'cantidad']} style={{ margin: 0 }} rules={[{ required: true }]}>
                      <InputNumber min={1} style={{ width: '100%' }} placeholder="Cant." />
                    </Form.Item>
                    <Form.Item {...restField} name={[name, 'precioUnitario']} style={{ margin: 0 }} rules={[{ required: true }]}>
                      <InputNumber min={0} style={{ width: '100%' }} prefix="$" placeholder="Precio" />
                    </Form.Item>
                    <Button danger icon={<DeleteOutlined />} onClick={() => remove(name)} />
                  </div>
                ))}
                <Button
                  type="dashed"
                  onClick={() => add({ cantidad: 1, precioUnitario: 0, descuento: 0 })}
                  block
                  icon={<PlusOutlined />}
                  style={{ marginBottom: 14 }}
                >
                  Agregar Partida
                </Button>
              </>
            )}
          </Form.List>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 10 }}>
            <Form.Item name="folioFacturaCFDI" label="Folio Fiscal CFDI / UUID">
              <Input placeholder="Opcional" />
            </Form.Item>
            <Form.Item name="usoCFDI" label="Uso CFDI">
              <Input />
            </Form.Item>
            <Form.Item name="metodoPago" label="Método de Pago">
              <Select options={[{ value: 'PUE', label: 'PUE - Una sola exhibición' }, { value: 'PPD', label: 'PPD - Parcialidades' }]} />
            </Form.Item>
            <Form.Item name="formaPago" label="Forma de Pago">
              <Input />
            </Form.Item>
          </div>

          <Form.Item name="descuentoAutomaticoInventario" valuePropName="checked">
            <Checkbox>
              Sincronización automática Ventas-Inventario: descontar existencias automáticamente al cambiar a Entregada o Facturada
            </Checkbox>
          </Form.Item>
          <Form.Item name="observaciones" label="Condiciones Comerciales / Observaciones">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>

      {/* ───────────── MODAL: CAMBIAR ESTATUS / FACTURAR / ENTREGAR ───────────── */}
      <Modal
        open={!!estatusModal}
        title={`Evolucionar Documento · ${estatusModal?.folio ?? ''}`}
        onCancel={() => setEstatusModal(null)}
        onOk={() => formEstatus.submit()}
        confirmLoading={cambiarEstatus.isPending}
        okText="Actualizar Documento"
        destroyOnClose
      >
        <Form form={formEstatus} layout="vertical" onFinish={(v) => cambiarEstatus.mutate(v)}>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message="Cuando una venta cambia a «Entregada» o «Facturada», el sistema descuenta automáticamente el inventario o genera una alerta si queda pendiente la salida."
          />
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <Form.Item name="nuevoTipoDocumento" label="Etapa / Tipo de Documento">
              <Select options={TIPOS_DOC.map((t) => ({ value: t, label: t }))} />
            </Form.Item>
            <Form.Item name="nuevoEstatus" label="Nuevo Estatus" rules={[{ required: true }]}>
              <Select options={ESTATUS_VENTA.map((e) => ({ value: e, label: e }))} />
            </Form.Item>
          </div>
          <Form.Item name="folioFacturaCFDI" label="Folio Fiscal CFDI / Factura">
            <Input placeholder="UUID o Folio de Factura" />
          </Form.Item>
          <Form.Item name="descontarInventarioAhora" valuePropName="checked">
            <Checkbox>Descontar inventario automáticamente si cambia a Entregada o Facturada</Checkbox>
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
