import {
  ArrowDownOutlined,
  ArrowUpOutlined,
  DeleteOutlined,
  EnvironmentOutlined,
  HistoryOutlined,
  PlusOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons'
import {
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
import type { Compra, MovimientoInventario, Paged, Producto } from '../api/types'

const GRUPOS_CATEGORIAS: Record<string, string[]> = {
  'Equipo Nuevo': ['Estaciones Totales', 'GNSS', 'GPS', 'Drones', 'Niveles', 'Escáneres'],
  Refacciones: ['Pantallas', 'Tarjetas', 'Antenas', 'Teclados', 'Radios', 'Baterías'],
  Accesorios: ['Trípodes', 'Prismas', 'Bastones', 'Adaptadores', 'Cables'],
  Consumibles: ['Pintura', 'Estacas', 'Etiquetas'],
  Herramientas: ['Herramientas'],
}

const SUBTIPOS_ENTRADA = ['Compra', 'Ajuste', 'Devolución', 'Transferencia']
const SUBTIPOS_SALIDA = ['Venta', 'Reparación', 'Garantía', 'Consumo interno', 'Transferencia']

const money = (v: number) =>
  new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' }).format(v || 0)

export default function InventarioPage() {
  const { message } = App.useApp()
  const qc = useQueryClient()

  // Filtros Productos
  const [q, setQ] = useState('')
  const [grupo, setGrupo] = useState<string>()
  const [categoria, setCategoria] = useState<string>()
  const [bajoStock, setBajoStock] = useState(false)
  const [pageProd, setPageProd] = useState(1)

  // Filtros Kardex
  const [qKardex, setQKardex] = useState('')
  const [prodIdKardex, setProdIdKardex] = useState<number>()
  const [tipoMovFiltro, setTipoMovFiltro] = useState<string>()
  const [pageKardex, setPageKardex] = useState(1)

  // Modales
  const [editingProd, setEditingProd] = useState<Partial<Producto> | null>(null)
  const [movModal, setMovModal] = useState<{ producto?: Producto; tipo: 'Entrada' | 'Salida' } | null>(null)
  const [compraModal, setCompraModal] = useState(false)

  const [formProd] = Form.useForm()
  const [formMov] = Form.useForm()
  const [formCompra] = Form.useForm()
  const grupoSeleccionado = Form.useWatch('grupoCategoria', formProd) as string | undefined

  const { data: productos, isLoading: loadingProd } = useQuery({
    queryKey: ['inventario-productos', q, grupo, categoria, bajoStock, pageProd],
    queryFn: async () =>
      (
        await api.get<Paged<Producto>>('/inventario/productos', {
          params: { q, grupo, categoria, bajoStock: bajoStock || undefined, page: pageProd, size: 15 },
        })
      ).data,
  })

  const { data: kardex, isLoading: loadingKardex } = useQuery({
    queryKey: ['inventario-kardex', prodIdKardex, tipoMovFiltro, qKardex, pageKardex],
    queryFn: async () =>
      (
        await api.get<Paged<MovimientoInventario>>('/inventario/kardex', {
          params: { productoId: prodIdKardex, tipo: tipoMovFiltro, q: qKardex, page: pageKardex, size: 25 },
        })
      ).data,
  })

  const { data: compras, isLoading: loadingCompras } = useQuery({
    queryKey: ['compras'],
    queryFn: async () => (await api.get<Paged<Compra>>('/compras', { params: { size: 50 } })).data,
  })

  const saveProd = useMutation({
    mutationFn: (v: Record<string, unknown>) =>
      editingProd?.productoId
        ? api.put(`/inventario/productos/${editingProd.productoId}`, v)
        : api.post('/inventario/productos', v),
    onSuccess: () => {
      message.success('Producto guardado con ubicación física actualizada')
      setEditingProd(null)
      qc.invalidateQueries({ queryKey: ['inventario-productos'] })
      qc.invalidateQueries({ queryKey: ['inventario-kardex'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const registrarMov = useMutation({
    mutationFn: (v: Record<string, unknown>) => api.post('/inventario/movimientos', v),
    onSuccess: () => {
      message.success('Movimiento registrado en el Kardex de Inventario')
      setMovModal(null)
      formMov.resetFields()
      qc.invalidateQueries({ queryKey: ['inventario-productos'] })
      qc.invalidateQueries({ queryKey: ['inventario-kardex'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const crearCompra = useMutation({
    mutationFn: (v: Record<string, unknown>) => api.post('/compras', v),
    onSuccess: () => {
      message.success('Orden de compra registrada y existencias actualizadas')
      setCompraModal(false)
      formCompra.resetFields()
      qc.invalidateQueries({ queryKey: ['compras'] })
      qc.invalidateQueries({ queryKey: ['inventario-productos'] })
      qc.invalidateQueries({ queryKey: ['inventario-kardex'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (e) => message.error(errorMessage(e)),
  })

  const openProd = (p: Partial<Producto>) => {
    setEditingProd(p)
    formProd.resetFields()
    formProd.setFieldsValue({
      grupoCategoria: 'Refacciones',
      categoria: 'Pantallas',
      unidad: 'PZA',
      stockActual: 0,
      stockMinimo: 2,
      stockMaximo: 50,
      almacen: 'ALM01',
      pasillo: 'PAS02',
      rack: 'R03',
      anaquel: 'ANB',
      nivel: 'N2',
      posicion: 'P1',
      activo: true,
      ...p,
    })
  }

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12, flexWrap: 'wrap', gap: 8 }}>
        <div>
          <h1 className="page-title" style={{ marginBottom: 2 }}>Módulo de Inventario, Almacén, Kardex y Compras</h1>
          <Typography.Text type="secondary">
            Control de equipo nuevo, refacciones, accesorios, consumibles y herramientas con trazabilidad de ubicación física (ALM-PAS-R-AN-N)
          </Typography.Text>
        </div>
        <Space wrap>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => openProd({})}>
            Nuevo Producto / Refacción
          </Button>
          <Button
            icon={<ArrowUpOutlined />}
            onClick={() => {
              setMovModal({ tipo: 'Entrada' })
              formMov.resetFields()
              formMov.setFieldsValue({ tipoMovimiento: 'Entrada', subtipo: 'Compra', cantidad: 1 })
            }}
          >
            Registrar Entrada
          </Button>
          <Button
            icon={<ArrowDownOutlined />}
            onClick={() => {
              setMovModal({ tipo: 'Salida' })
              formMov.resetFields()
              formMov.setFieldsValue({ tipoMovimiento: 'Salida', subtipo: 'Consumo interno', cantidad: 1 })
            }}
          >
            Registrar Salida
          </Button>
          <Button
            icon={<ShoppingCartOutlined />}
            onClick={() => {
              setCompraModal(true)
              formCompra.resetFields()
              formCompra.setFieldsValue({ estatus: 'Recibida', registrarEntradaInmediata: true, partidas: [{ cantidad: 1, costoUnitario: 0 }] })
            }}
          >
            Nueva Compra a Proveedor
          </Button>
        </Space>
      </div>

      <Card size="small">
        <Tabs
          items={[
            {
              key: 'catalogo',
              label: 'Catálogo de Productos y Ubicaciones Físicas',
              children: (
                <>
                  <div className="toolbar">
                    <Space wrap>
                      <Input.Search
                        placeholder="Buscar SKU, código barras, nombre, marca o ubicación (ALM01...)"
                        allowClear
                        style={{ width: 340 }}
                        onSearch={(v) => {
                          setQ(v)
                          setPageProd(1)
                        }}
                      />
                      <Select
                        placeholder="Grupo"
                        allowClear
                        style={{ width: 170 }}
                        value={grupo}
                        onChange={(v) => {
                          setGrupo(v)
                          setCategoria(undefined)
                          setPageProd(1)
                        }}
                        options={Object.keys(GRUPOS_CATEGORIAS).map((g) => ({ value: g, label: g }))}
                      />
                      <Select
                        placeholder="Categoría"
                        allowClear
                        style={{ width: 190 }}
                        value={categoria}
                        onChange={(v) => {
                          setCategoria(v)
                          setPageProd(1)
                        }}
                        options={(grupo ? GRUPOS_CATEGORIAS[grupo] : Object.values(GRUPOS_CATEGORIAS).flat()).map((c) => ({
                          value: c,
                          label: c,
                        }))}
                      />
                      <Checkbox
                        checked={bajoStock}
                        onChange={(e) => {
                          setBajoStock(e.target.checked)
                          setPageProd(1)
                        }}
                      >
                        Solo stock bajo / crítico
                      </Checkbox>
                    </Space>
                  </div>

                  <Table
                    rowKey="productoId"
                    loading={loadingProd}
                    dataSource={productos?.items}
                    size="small"
                    bordered
                    scroll={{ x: 'max-content' }}
                    pagination={{ current: pageProd, pageSize: 15, total: productos?.total, onChange: setPageProd, showSizeChanger: false }}
                    columns={[
                      { title: 'SKU', dataIndex: 'sku', width: 140, render: (v: string) => <strong>{v}</strong> },
                      {
                        title: 'Categoría',
                        width: 190,
                        render: (_, r) => (
                          <Space size={4}>
                            <Tag color="blue">{r.grupoCategoria}</Tag>
                            <span>{r.categoria}</span>
                          </Space>
                        ),
                      },
                      {
                        title: 'Producto / Descripción',
                        width: 300,
                        render: (_, r) => (
                          <>
                            <strong>{r.nombre}</strong>{' '}
                            {r.marca && <Typography.Text type="secondary">({r.marca})</Typography.Text>}
                          </>
                        ),
                      },
                      {
                        title: 'Ubicación Física',
                        dataIndex: 'codigoUbicacion',
                        width: 210,
                        render: (v: string) => <Tag icon={<EnvironmentOutlined />} color="geekblue"><code>{v}</code></Tag>,
                      },
                      {
                        title: 'Stock Actual',
                        dataIndex: 'stockActual',
                        width: 120,
                        align: 'right',
                        render: (v: number, r) => (
                          <Tag color={r.estadoStock === 'SinStock' ? 'red' : r.estadoStock === 'Bajo' ? 'orange' : 'green'}>
                            <strong>{v}</strong> {r.unidad}
                          </Tag>
                        ),
                      },
                      { title: 'Mín.', dataIndex: 'stockMinimo', width: 70, align: 'right' },
                      { title: 'Costo Unit.', dataIndex: 'costoUnitario', width: 115, align: 'right', render: money },
                      { title: 'Precio Venta', dataIndex: 'precioVenta', width: 120, align: 'right', render: money },
                      {
                        title: 'Acciones',
                        fixed: 'right',
                        width: 210,
                        render: (_, r) => (
                          <Space size={4}>
                            <Button
                              size="small"
                              onClick={() => {
                                setMovModal({ producto: r, tipo: 'Entrada' })
                                formMov.setFieldsValue({
                                  productoId: r.productoId,
                                  tipoMovimiento: 'Entrada',
                                  subtipo: 'Compra',
                                  cantidad: 1,
                                  costoUnitario: r.costoUnitario,
                                })
                              }}
                            >
                              + Entrada
                            </Button>
                            <Button
                              size="small"
                              onClick={() => {
                                setMovModal({ producto: r, tipo: 'Salida' })
                                formMov.setFieldsValue({
                                  productoId: r.productoId,
                                  tipoMovimiento: 'Salida',
                                  subtipo: 'Reparación',
                                  cantidad: 1,
                                })
                              }}
                            >
                              - Salida
                            </Button>
                            <Button size="small" onClick={() => openProd(r)}>
                              Editar
                            </Button>
                          </Space>
                        ),
                      },
                    ]}
                  />
                </>
              ),
            },
            {
              key: 'kardex',
              label: <><HistoryOutlined /> Kardex Completo de Movimientos</>,
              children: (
                <>
                  <div className="toolbar">
                    <Space wrap>
                      <Input.Search
                        placeholder="Buscar por concepto, folio OS/VTA/COM, SKU o producto..."
                        allowClear
                        style={{ width: 340 }}
                        onSearch={(v) => {
                          setQKardex(v)
                          setPageKardex(1)
                        }}
                      />
                      <Select
                        placeholder="Filtrar por Producto"
                        allowClear
                        showSearch
                        optionFilterProp="label"
                        style={{ width: 260 }}
                        value={prodIdKardex}
                        onChange={(v) => {
                          setProdIdKardex(v)
                          setPageKardex(1)
                        }}
                        options={productos?.items.map((p) => ({ value: p.productoId, label: `${p.sku} · ${p.nombre}` }))}
                      />
                      <Select
                        placeholder="Tipo Movimiento"
                        allowClear
                        style={{ width: 150 }}
                        value={tipoMovFiltro}
                        onChange={(v) => {
                          setTipoMovFiltro(v)
                          setPageKardex(1)
                        }}
                        options={[
                          { value: 'Entrada', label: 'Entradas' },
                          { value: 'Salida', label: 'Salidas' },
                        ]}
                      />
                    </Space>
                  </div>

                  <Table
                    rowKey="movimientoId"
                    loading={loadingKardex}
                    dataSource={kardex?.items}
                    size="small"
                    bordered
                    scroll={{ x: 'max-content' }}
                    pagination={{ current: pageKardex, pageSize: 25, total: kardex?.total, onChange: setPageKardex, showSizeChanger: false }}
                    columns={[
                      { title: 'Fecha y Hora', dataIndex: 'fecha', width: 145, render: (v: string) => dayjs(v).format('DD/MM/YYYY HH:mm') },
                      {
                        title: 'Tipo',
                        dataIndex: 'tipoMovimiento',
                        width: 95,
                        render: (v: string) => <Tag color={v === 'Entrada' ? 'green' : 'volcano'}>{v}</Tag>,
                      },
                      { title: 'Motivo / Subtipo', dataIndex: 'subtipo', width: 135, render: (v: string) => <Tag>{v}</Tag> },
                      { title: 'SKU / Producto', width: 250, render: (_, r) => <><strong>{r.sku}</strong> · {r.nombreProducto}</> },
                      { title: 'Concepto', dataIndex: 'concepto', width: 220 },
                      { title: 'Ref. Folio', dataIndex: 'referenciaFolio', width: 140, render: (v?: string) => (v ? <strong>{v}</strong> : '—') },
                      {
                        title: 'Cantidad',
                        dataIndex: 'cantidad',
                        width: 90,
                        align: 'right',
                        render: (v: number, r) => (
                          <strong style={{ color: r.tipoMovimiento === 'Entrada' ? '#2E7D32' : '#B71C1C' }}>
                            {r.tipoMovimiento === 'Entrada' ? `+${v}` : `-${v}`}
                          </strong>
                        ),
                      },
                      { title: 'Stock Ant.', dataIndex: 'stockAnterior', width: 90, align: 'right' },
                      { title: 'Stock Post.', dataIndex: 'stockPosterior', width: 90, align: 'right', render: (v: number) => <strong>{v}</strong> },
                      { title: 'Ubicación', width: 190, render: (_, r) => <code>{r.ubicacionDestino || r.ubicacionOrigen || r.codigoUbicacion}</code> },
                      { title: 'Usuario', dataIndex: 'usuarioNombre', width: 130 },
                    ]}
                  />
                </>
              ),
            },
            {
              key: 'compras',
              label: `Compras y Abastecimiento (${compras?.total ?? 0})`,
              children: (
                <Table
                  rowKey="compraId"
                  loading={loadingCompras}
                  dataSource={compras?.items}
                  size="small"
                  bordered
                  columns={[
                    { title: 'Folio Compra', dataIndex: 'folio', render: (v: string) => <strong>{v}</strong> },
                    { title: 'Fecha', dataIndex: 'fechaCompra', render: (v: string) => dayjs(v).format('DD/MM/YYYY') },
                    { title: 'Proveedor', dataIndex: 'proveedor' },
                    { title: 'Factura Proveedor', dataIndex: 'facturaProveedor' },
                    { title: 'Estatus', dataIndex: 'estatus', render: (v: string) => <Tag color={v === 'Recibida' ? 'green' : 'orange'}>{v}</Tag> },
                    { title: 'Partidas', render: (_, r) => r.partidas.map((p) => `${p.descripcion} (x${p.cantidad})`).join(', ') },
                    { title: 'Subtotal', dataIndex: 'subtotal', align: 'right', render: money },
                    { title: 'IVA', dataIndex: 'iva', align: 'right', render: money },
                    { title: 'Total', dataIndex: 'total', align: 'right', render: (v: number) => <strong>{money(v)}</strong> },
                  ]}
                />
              ),
            },
          ]}
        />
      </Card>

      {/* ───────────── MODAL: ALTA / EDICIÓN DE PRODUCTO Y UBICACIÓN FÍSICA ───────────── */}
      <Modal
        open={!!editingProd}
        title={editingProd?.productoId ? `Editar Producto · ${editingProd.sku}` : 'Registrar Producto en Inventario'}
        onCancel={() => setEditingProd(null)}
        onOk={() => formProd.submit()}
        confirmLoading={saveProd.isPending}
        width={780}
        okText="Guardar Producto"
        destroyOnClose
      >
        <Form form={formProd} layout="vertical" onFinish={(v) => saveProd.mutate(v)}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
            <Form.Item name="sku" label="SKU / Código Interno" rules={[{ required: true }]}>
              <Input placeholder="Ej. REF-PAN-LEI01" style={{ textTransform: 'uppercase' }} />
            </Form.Item>
            <Form.Item name="grupoCategoria" label="Grupo Principal" rules={[{ required: true }]}>
              <Select
                onChange={(g) => formProd.setFieldsValue({ categoria: GRUPOS_CATEGORIAS[g]?.[0] })}
                options={Object.keys(GRUPOS_CATEGORIAS).map((g) => ({ value: g, label: g }))}
              />
            </Form.Item>
            <Form.Item name="categoria" label="Categoría Específica" rules={[{ required: true }]}>
              <Select
                options={(GRUPOS_CATEGORIAS[grupoSeleccionado ?? 'Refacciones'] ?? []).map((c) => ({ value: c, label: c }))}
              />
            </Form.Item>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: 12 }}>
            <Form.Item name="nombre" label="Nombre del Producto / Refacción" rules={[{ required: true }]}>
              <Input placeholder="Ej. Pantalla Leica TS16, Tarjeta GNSS, Trípode..." />
            </Form.Item>
            <Form.Item name="marca" label="Marca">
              <Input placeholder="Leica, Trimble, Seco..." />
            </Form.Item>
            <Form.Item name="modeloCompatible" label="Modelo Compatible">
              <Input placeholder="TS16, R12i, Universal..." />
            </Form.Item>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr 1fr', gap: 10 }}>
            <Form.Item name="costoUnitario" label="Costo Unitario ($)" rules={[{ required: true }]}>
              <InputNumber min={0} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="precioVenta" label="Precio Venta ($)" rules={[{ required: true }]}>
              <InputNumber min={0} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="stockActual" label="Stock Inicial" rules={[{ required: true }]}>
              <InputNumber min={0} disabled={!!editingProd?.productoId} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="stockMinimo" label="Stock Mínimo" rules={[{ required: true }]}>
              <InputNumber min={0} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="unidad" label="Unidad">
              <Input placeholder="PZA, KIT, LT" />
            </Form.Item>
          </div>

          <Divider style={{ margin: '6px 0 12px' }}>
            Ubicación Física en Almacén (Ej. ALM01-PAS02-R03-ANB-N2)
          </Divider>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', gap: 8 }}>
            <Form.Item name="almacen" label="Almacén" rules={[{ required: true }]}><Input placeholder="ALM01" /></Form.Item>
            <Form.Item name="pasillo" label="Pasillo" rules={[{ required: true }]}><Input placeholder="PAS02" /></Form.Item>
            <Form.Item name="rack" label="Rack" rules={[{ required: true }]}><Input placeholder="R03" /></Form.Item>
            <Form.Item name="anaquel" label="Anaquel" rules={[{ required: true }]}><Input placeholder="ANB" /></Form.Item>
            <Form.Item name="nivel" label="Nivel" rules={[{ required: true }]}><Input placeholder="N2" /></Form.Item>
            <Form.Item name="posicion" label="Posición" rules={[{ required: true }]}><Input placeholder="P1" /></Form.Item>
          </div>
        </Form>
      </Modal>

      {/* ───────────── MODAL: MOVIMIENTO DE INVENTARIO (ENTRADA / SALIDA KARDEX) ───────────── */}
      <Modal
        open={!!movModal}
        title={movModal?.tipo === 'Entrada' ? 'Registrar Entrada de Inventario (Kardex)' : 'Registrar Salida de Inventario (Kardex)'}
        onCancel={() => setMovModal(null)}
        onOk={() => formMov.submit()}
        confirmLoading={registrarMov.isPending}
        okText="Aplicar Movimiento en Kardex"
        destroyOnClose
      >
        <Form form={formMov} layout="vertical" onFinish={(v) => registrarMov.mutate(v)}>
          <Form.Item name="tipoMovimiento" hidden><Input /></Form.Item>
          <Form.Item name="productoId" label="Producto" rules={[{ required: true }]}>
            <Select
              showSearch
              optionFilterProp="label"
              options={productos?.items.map((p) => ({
                value: p.productoId,
                label: `${p.sku} · ${p.nombre} (Disponible: ${p.stockActual} · ${p.codigoUbicacion})`,
              }))}
            />
          </Form.Item>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <Form.Item name="subtipo" label="Motivo / Tipo de Movimiento" rules={[{ required: true }]}>
              <Select
                options={(movModal?.tipo === 'Entrada' ? SUBTIPOS_ENTRADA : SUBTIPOS_SALIDA).map((s) => ({
                  value: s,
                  label: s,
                }))}
              />
            </Form.Item>
            <Form.Item name="cantidad" label="Cantidad" rules={[{ required: true }]}>
              <InputNumber min={0.01} style={{ width: '100%' }} />
            </Form.Item>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <Form.Item name="referenciaFolio" label="Folio de Referencia (OS, Venta, Factura)">
              <Input placeholder="Ej. OS-2026-000001 / FAC-901" />
            </Form.Item>
            <Form.Item name="concepto" label="Concepto en Kardex">
              <Input placeholder="Ej. Ajuste por conteo cíclico / Consumo taller" />
            </Form.Item>
          </div>
          <Form.Item name="observaciones" label="Observaciones">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>

      {/* ───────────── MODAL: NUEVA COMPRA A PROVEEDOR ───────────── */}
      <Modal
        open={compraModal}
        title="Registrar Compra de Inventario / Refacciones (COM-AAAA-XXXXXX)"
        onCancel={() => setCompraModal(false)}
        onOk={() => formCompra.submit()}
        confirmLoading={crearCompra.isPending}
        width={760}
        okText="Registrar Compra y Abastecer Almacén"
        destroyOnClose
      >
        <Form form={formCompra} layout="vertical" onFinish={(v) => crearCompra.mutate(v)}>
          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: 12 }}>
            <Form.Item name="proveedor" label="Proveedor / Fabricante" rules={[{ required: true }]}>
              <Input placeholder="Ej. Leica Geosystems México / Distribuidora Topográfica" />
            </Form.Item>
            <Form.Item name="rfcProveedor" label="RFC Proveedor">
              <Input />
            </Form.Item>
            <Form.Item name="facturaProveedor" label="No. Factura / Pedimento">
              <Input />
            </Form.Item>
          </div>

          <Divider>Partidas de Compra</Divider>
          <Form.List name="partidas">
            {(fields, { add, remove }) => (
              <>
                {fields.map(({ key, name, ...restField }) => (
                  <div key={key} style={{ display: 'grid', gridTemplateColumns: '1fr 110px 140px 40px', gap: 8, marginBottom: 8 }}>
                    <Form.Item {...restField} name={[name, 'productoId']} style={{ margin: 0 }} rules={[{ required: true }]}>
                      <Select
                        showSearch
                        optionFilterProp="label"
                        placeholder="Producto..."
                        onChange={(prodId) => {
                          const p = productos?.items.find((x) => x.productoId === prodId)
                          if (p) {
                            const curr = formCompra.getFieldValue('partidas')
                            curr[name].costoUnitario = p.costoUnitario
                            formCompra.setFieldsValue({ partidas: [...curr] })
                          }
                        }}
                        options={productos?.items.map((p) => ({ value: p.productoId, label: `${p.sku} · ${p.nombre}` }))}
                      />
                    </Form.Item>
                    <Form.Item {...restField} name={[name, 'cantidad']} style={{ margin: 0 }} rules={[{ required: true }]}>
                      <InputNumber min={1} style={{ width: '100%' }} placeholder="Cant." />
                    </Form.Item>
                    <Form.Item {...restField} name={[name, 'costoUnitario']} style={{ margin: 0 }} rules={[{ required: true }]}>
                      <InputNumber min={0} style={{ width: '100%' }} prefix="$" placeholder="Costo Unit." />
                    </Form.Item>
                    <Button danger icon={<DeleteOutlined />} onClick={() => remove(name)} />
                  </div>
                ))}
                <Button type="dashed" onClick={() => add({ cantidad: 1, costoUnitario: 0 })} block icon={<PlusOutlined />} style={{ marginBottom: 12 }}>
                  Agregar Producto a la Compra
                </Button>
              </>
            )}
          </Form.List>

          <Form.Item name="registrarEntradaInmediata" valuePropName="checked">
            <Checkbox>Ingresar existencias al almacén y generar entrada en Kardex inmediatamente</Checkbox>
          </Form.Item>
          <Form.Item name="observaciones" label="Observaciones">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
