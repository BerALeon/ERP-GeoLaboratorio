using System.Text.Json;
using Geolaboratorio.Application;
using Geolaboratorio.Domain;
using Microsoft.EntityFrameworkCore;

namespace Geolaboratorio.Infrastructure.Services;

public class OrdenServicioService(IUnitOfWork uow, IPdfService pdf, IQrService qr, ICurrentUser user) : IOrdenServicioService
{
    private IQueryable<OrdenServicio> Full() => uow.OrdenesServicio.Query()
        .Include(o => o.Cliente)
        .Include(o => o.Equipo!).ThenInclude(e => e.Cliente)
        .Include(o => o.Equipo!).ThenInclude(e => e.Series)
        .Include(o => o.Tecnico)
        .Include(o => o.Supervisor)
        .Include(o => o.HistorialEstatus)
        .Include(o => o.Refacciones).ThenInclude(r => r.Producto)
        .Include(o => o.Fotografias);

    private async Task<OrdenServicio> Load(int id, CancellationToken ct)
        => await Full().FirstOrDefaultAsync(o => o.OrdenServicioId == id, ct)
           ?? throw new NotFoundException("Orden de servicio no encontrada.");

    public async Task<PagedResult<OrdenServicioDto>> ListAsync(
        string? q, string? estatus, string? tipoServicio, int? equipoId, int? clienteId, int page, int size, CancellationToken ct = default)
    {
        (page, size) = (Math.Max(page, 1), Math.Clamp(size, 1, 200));
        var query = Full().AsNoTracking();
        if (equipoId.HasValue) query = query.Where(o => o.EquipoId == equipoId);
        if (clienteId.HasValue) query = query.Where(o => o.ClienteId == clienteId);
        if (!string.IsNullOrWhiteSpace(estatus)) query = query.Where(o => o.Estatus == estatus);
        if (!string.IsNullOrWhiteSpace(tipoServicio)) query = query.Where(o => o.TipoServicio == tipoServicio);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(o =>
                o.Folio.Contains(q)
                || o.Cliente!.RazonSocial.Contains(q)
                || o.Equipo!.SeriePrincipal.Contains(q)
                || o.Equipo.Marca.Contains(q)
                || o.Equipo.Modelo.Contains(q)
                || (o.Equipo.NumeroInterno != null && o.Equipo.NumeroInterno.Contains(q)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(o => o.OrdenServicioId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(o => o.ToDto()).ToList(), total, page, size);
    }

    public async Task<OrdenServicioDto> GetAsync(int id, CancellationToken ct = default)
        => (await Load(id, ct)).ToDto();

    public async Task<OrdenServicioDto> CreateAsync(OrdenServicioCreateDto d, CancellationToken ct = default)
    {
        var equipo = await uow.Equipos.Query().Include(e => e.Cliente).FirstOrDefaultAsync(e => e.EquipoId == d.EquipoId, ct)
            ?? throw new BusinessException("El equipo seleccionado no existe.");

        await uow.BeginAsync(ct);
        try
        {
            var anio = DateTime.UtcNow.Year;
            var folio = await uow.NextFolioPrefijoAsync("OS", anio, ct);
            var consecutivo = int.Parse(folio.Split('-').Last());

            var os = new OrdenServicio
            {
                Folio = folio,
                Anio = anio,
                Consecutivo = consecutivo,
                ClienteId = equipo.ClienteId,
                EquipoId = equipo.EquipoId,
                TipoServicio = d.TipoServicio.Trim(),
                TiposServicioAdicionales = d.TiposServicioAdicionales is { Count: > 0 } ? JsonSerializer.Serialize(d.TiposServicioAdicionales) : null,
                Estatus = "Recibido",
                TecnicoId = d.TecnicoId ?? user.UserId,
                SupervisorId = d.SupervisorId,
                FechaRecepcion = DateTime.UtcNow,
                FechaCompromiso = d.FechaCompromiso,
                MetodoIdentificacion = string.IsNullOrWhiteSpace(d.MetodoIdentificacion) ? "Manual" : d.MetodoIdentificacion,
                AccesoriosChecklistJson = d.AccesoriosRecibidos is { Count: > 0 } ? JsonSerializer.Serialize(d.AccesoriosRecibidos) : "[]",
                AccesoriosPersonalizados = d.AccesoriosPersonalizados,
                CondicionChecklistJson = d.CondicionEquipo is { Count: > 0 } ? JsonSerializer.Serialize(d.CondicionEquipo) : "[]",
                FallaReportada = d.FallaReportada,
                ObservacionesRecepcion = d.ObservacionesRecepcion
            };

            await uow.OrdenesServicio.AddAsync(os, ct);
            equipo.Estatus = EstatusEquipo.EnTaller;
            await uow.SaveChangesAsync(ct);

            await uow.OrdenesServicioHistorial.AddAsync(new OrdenServicioEstatusHistorial
            {
                OrdenServicioId = os.OrdenServicioId,
                EstatusAnterior = null,
                EstatusNuevo = "Recibido",
                Fecha = DateTime.UtcNow,
                UsuarioId = user.UserId,
                UsuarioNombre = user.Nombre,
                Observaciones = string.IsNullOrWhiteSpace(d.ObservacionesRecepcion) ? "Recepción inicial del equipo en taller." : d.ObservacionesRecepcion
            }, ct);

            if (d.FotografiasIniciales is { Count: > 0 })
            {
                foreach (var f in d.FotografiasIniciales.Where(x => !string.IsNullOrWhiteSpace(x.ImagenUrl)))
                {
                    await uow.OrdenesServicioFotos.AddAsync(new OrdenServicioFoto
                    {
                        OrdenServicioId = os.OrdenServicioId,
                        Categoria = string.IsNullOrWhiteSpace(f.Categoria) ? "Equipo completo" : f.Categoria,
                        Descripcion = f.Descripcion,
                        ImagenUrl = f.ImagenUrl,
                        UsuarioNombre = user.Nombre,
                        Fecha = DateTime.UtcNow
                    }, ct);
                }
            }

            await uow.EquipoHistoriales.AddAsync(new EquipoHistorial
            {
                EquipoId = equipo.EquipoId,
                TipoEvento = "OrdenServicio",
                Titulo = $"Recepción en taller · {os.Folio} ({os.TipoServicio})",
                Detalle = $"Falla/Solicitud: {os.FallaReportada ?? "Revisión general"} · Identificación: {os.MetodoIdentificacion}",
                FolioReferencia = os.Folio,
                ReferenciaId = os.OrdenServicioId,
                UsuarioId = user.UserId,
                UsuarioNombre = user.Nombre
            }, ct);

            // Si el tipo de servicio es Mantenimiento, Reparación o Revisión, registrar también en Mantenimientos
            if (os.TipoServicio is "Mantenimiento Preventivo" or "Mantenimiento Correctivo" or "Reparación" or "Revisión")
            {
                var folioMto = await uow.NextFolioPrefijoAsync("MTO", anio, ct);
                var tipoMto = os.TipoServicio == "Revisión" ? "Revisión Técnica" : os.TipoServicio;
                await uow.Mantenimientos.AddAsync(new Mantenimiento
                {
                    Folio = folioMto,
                    EquipoId = equipo.EquipoId,
                    OrdenServicioId = os.OrdenServicioId,
                    Tipo = tipoMto,
                    Estatus = "En Proceso",
                    FechaProgramada = DateTime.UtcNow,
                    ProximoMantenimiento = os.TipoServicio == "Mantenimiento Preventivo" ? DateTime.UtcNow.AddMonths(6) : null,
                    TecnicoId = os.TecnicoId,
                    Descripcion = os.FallaReportada ?? $"Generado desde orden de servicio {os.Folio}"
                }, ct);
            }

            await uow.SaveChangesAsync(ct);
            await uow.CommitAsync(ct);
            return (await Load(os.OrdenServicioId, ct)).ToDto();
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<OrdenServicioDto> UpdateTecnicoAsync(int id, OrdenServicioUpdateTecnicoDto d, CancellationToken ct = default)
    {
        var os = await Load(id, ct);
        if (!string.IsNullOrWhiteSpace(d.TipoServicio)) os.TipoServicio = d.TipoServicio.Trim();
        if (d.TecnicoId.HasValue) os.TecnicoId = d.TecnicoId;
        if (d.SupervisorId.HasValue) os.SupervisorId = d.SupervisorId;
        if (d.FechaCompromiso.HasValue) os.FechaCompromiso = d.FechaCompromiso;
        os.Diagnostico = d.Diagnostico;
        os.ActividadesRealizadas = d.ActividadesRealizadas;
        os.ObservacionesTecnicas = d.ObservacionesTecnicas;
        os.CostoManoObra = Math.Max(0, d.CostoManoObra);
        if (!string.IsNullOrWhiteSpace(d.FirmaTecnico)) os.FirmaTecnico = d.FirmaTecnico;
        if (!string.IsNullOrWhiteSpace(d.FirmaSupervisor)) os.FirmaSupervisor = d.FirmaSupervisor;
        if (!string.IsNullOrWhiteSpace(d.FirmaCliente)) os.FirmaCliente = d.FirmaCliente;
        if (!string.IsNullOrWhiteSpace(d.NombreClienteFirma)) os.NombreClienteFirma = d.NombreClienteFirma;

        if (d.Refacciones is not null)
        {
            // Solo reemplazar refacciones que aún no han sido descontadas del inventario, o agregar nuevas
            var noDescontadas = os.Refacciones.Where(r => !r.Descontado).ToList();
            foreach (var r in noDescontadas) uow.OrdenesServicioRefacciones.Remove(r);

            foreach (var item in d.Refacciones.Where(x => x.ProductoId > 0 && x.Cantidad > 0))
            {
                var prod = await uow.Productos.GetByIdAsync(item.ProductoId, ct)
                    ?? throw new BusinessException($"El producto #{item.ProductoId} no existe en inventario.");

                // Si ya estaba descontado con la misma cantidad, omitir duplicado
                var yaDescontada = os.Refacciones.FirstOrDefault(r => r.Descontado && r.ProductoId == item.ProductoId);
                if (yaDescontada != null && yaDescontada.Cantidad == item.Cantidad) continue;

                var precio = item.PrecioUnitario ?? prod.PrecioVenta;
                await uow.OrdenesServicioRefacciones.AddAsync(new OrdenServicioRefaccion
                {
                    OrdenServicioId = os.OrdenServicioId,
                    ProductoId = prod.ProductoId,
                    SKU = prod.SKU,
                    NombreProducto = prod.Nombre,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = precio,
                    Importe = Math.Round(item.Cantidad * precio, 2),
                    Descontado = false
                }, ct);
            }
        }

        await uow.SaveChangesAsync(ct);

        // Recalcular totales
        var refas = await uow.OrdenesServicioRefacciones.Query().Where(r => r.OrdenServicioId == id).ToListAsync(ct);
        os.SubtotalRefacciones = refas.Sum(r => r.Importe);
        os.Total = os.CostoManoObra + os.SubtotalRefacciones;

        // Sincronizar con registro de Mantenimiento vinculado si existe
        var mto = await uow.Mantenimientos.Query().FirstOrDefaultAsync(m => m.OrdenServicioId == id, ct);
        if (mto != null)
        {
            mto.Hallazgos = os.Diagnostico;
            mto.Actividades = os.ActividadesRealizadas;
            mto.Recomendaciones = os.ObservacionesTecnicas;
            mto.Costo = os.Total;
            mto.TecnicoId = os.TecnicoId;
        }

        // Si la orden ya está cerrada/terminada y se agregaron refacciones pendientes, descontarlas automáticamente
        if (os.Estatus is "Terminado" or "Listo para Entrega" or "Entregado")
        {
            await DescontarRefaccionesPendientesAsync(os, ct);
        }

        await uow.SaveChangesAsync(ct);
        return (await Load(id, ct)).ToDto();
    }

    private async Task DescontarRefaccionesPendientesAsync(OrdenServicio os, CancellationToken ct)
    {
        var pendientes = await uow.OrdenesServicioRefacciones.Query()
            .Where(r => r.OrdenServicioId == os.OrdenServicioId && !r.Descontado)
            .ToListAsync(ct);

        if (pendientes.Count == 0) return;

        foreach (var r in pendientes)
        {
            var prod = await uow.Productos.GetByIdAsync(r.ProductoId, ct)
                ?? throw new BusinessException($"Refacción {r.NombreProducto} no encontrada.");

            var anterior = prod.StockActual;
            prod.StockActual = anterior - r.Cantidad;
            r.Descontado = true;

            await uow.MovimientosInventario.AddAsync(new MovimientoInventario
            {
                ProductoId = prod.ProductoId,
                TipoMovimiento = "Salida",
                Subtipo = "Reparación",
                Concepto = "Consumo por Orden de Servicio",
                ReferenciaFolio = os.Folio,
                OrdenServicioId = os.OrdenServicioId,
                Cantidad = r.Cantidad,
                StockAnterior = anterior,
                StockPosterior = prod.StockActual,
                CostoUnitario = prod.CostoUnitario,
                UbicacionOrigen = prod.CodigoUbicacion,
                Observaciones = $"Descuento automático por Orden de Servicio {os.Folio} · Equipo {os.Equipo?.SeriePrincipal}",
                UsuarioId = user.UserId,
                UsuarioNombre = user.Nombre,
                Fecha = DateTime.UtcNow
            }, ct);
        }

        os.RefaccionesDescontadas = true;
    }

    public async Task<OrdenServicioDto> CambiarEstatusAsync(int id, OrdenServicioCambiarEstatusDto d, CancellationToken ct = default)
    {
        var os = await Load(id, ct);
        var anterior = os.Estatus;
        var nuevo = d.NuevoEstatus.Trim();
        os.Estatus = nuevo;

        if (!string.IsNullOrWhiteSpace(d.FirmaTecnico)) os.FirmaTecnico = d.FirmaTecnico;
        if (!string.IsNullOrWhiteSpace(d.FirmaSupervisor)) os.FirmaSupervisor = d.FirmaSupervisor;
        if (!string.IsNullOrWhiteSpace(d.FirmaCliente)) os.FirmaCliente = d.FirmaCliente;
        if (!string.IsNullOrWhiteSpace(d.NombreClienteFirma)) os.NombreClienteFirma = d.NombreClienteFirma;

        var ahora = DateTime.UtcNow;
        if (nuevo is "Terminado" or "Listo para Entrega") os.FechaTermino ??= ahora;
        if (nuevo == "Entregado")
        {
            os.FechaTermino ??= ahora;
            os.FechaEntrega = ahora;
        }

        await uow.OrdenesServicioHistorial.AddAsync(new OrdenServicioEstatusHistorial
        {
            OrdenServicioId = os.OrdenServicioId,
            EstatusAnterior = anterior,
            EstatusNuevo = nuevo,
            Fecha = ahora,
            UsuarioId = user.UserId,
            UsuarioNombre = user.Nombre,
            Observaciones = d.Observaciones
        }, ct);

        await uow.EquipoHistoriales.AddAsync(new EquipoHistorial
        {
            EquipoId = os.EquipoId,
            TipoEvento = "CambioEstatusOS",
            Titulo = $"Orden {os.Folio} cambió a «{nuevo}»",
            Detalle = string.IsNullOrWhiteSpace(d.Observaciones) ? $"Estatus anterior: {anterior}" : $"{d.Observaciones} (Anterior: {anterior})",
            FolioReferencia = os.Folio,
            ReferenciaId = os.OrdenServicioId,
            UsuarioId = user.UserId,
            UsuarioNombre = user.Nombre,
            Fecha = ahora
        }, ct);

        // Integración Inventario - Taller: al cerrar/terminar la orden, descontar automáticamente existencias
        if (nuevo is "Terminado" or "Listo para Entrega" or "Entregado")
        {
            await DescontarRefaccionesPendientesAsync(os, ct);
        }

        // Actualizar estatus del equipo y mantenimiento vinculado
        var equipo = await uow.Equipos.GetByIdAsync(os.EquipoId, ct);
        if (equipo != null && equipo.Estatus != EstatusEquipo.Baja)
        {
            equipo.Estatus = nuevo is "Entregado" or "Cancelado" ? EstatusEquipo.Activo : EstatusEquipo.EnTaller;
        }

        var mto = await uow.Mantenimientos.Query().FirstOrDefaultAsync(m => m.OrdenServicioId == id, ct);
        if (mto != null)
        {
            if (nuevo is "Terminado" or "Listo para Entrega" or "Entregado")
            {
                mto.Estatus = "Completado";
                mto.FechaRealizada ??= ahora;
            }
            else if (nuevo == "Cancelado") mto.Estatus = "Cancelado";
        }

        await uow.SaveChangesAsync(ct);
        return (await Load(id, ct)).ToDto();
    }

    public async Task<OrdenServicioFotoDto> AgregarFotoAsync(int id, OrdenServicioFotoCreateDto d, CancellationToken ct = default)
    {
        var os = await Load(id, ct);
        var f = new OrdenServicioFoto
        {
            OrdenServicioId = os.OrdenServicioId,
            Categoria = string.IsNullOrWhiteSpace(d.Categoria) ? "Equipo completo" : d.Categoria.Trim(),
            Descripcion = d.Descripcion,
            ImagenUrl = d.ImagenUrl,
            UsuarioNombre = user.Nombre,
            Fecha = DateTime.UtcNow
        };
        await uow.OrdenesServicioFotos.AddAsync(f, ct);

        await uow.EquipoArchivos.AddAsync(new EquipoArchivo
        {
            EquipoId = os.EquipoId,
            OrdenServicioId = os.OrdenServicioId,
            TipoArchivo = "Fotografia",
            Categoria = f.Categoria,
            NombreArchivo = $"{os.Folio}_{f.Categoria}.png",
            ArchivoUrl = f.ImagenUrl,
            Descripcion = f.Descripcion ?? $"Evidencia fotográfica de orden {os.Folio}",
            SubidoPor = user.Nombre,
            Fecha = f.Fecha
        }, ct);

        await uow.SaveChangesAsync(ct);
        return new OrdenServicioFotoDto(f.OrdenServicioFotoId, f.OrdenServicioId, f.Categoria, f.Descripcion, f.ImagenUrl, f.UsuarioNombre, f.Fecha);
    }

    public async Task EliminarFotoAsync(int id, int fotoId, CancellationToken ct = default)
    {
        var f = await uow.OrdenesServicioFotos.Query()
            .FirstOrDefaultAsync(x => x.OrdenServicioId == id && x.OrdenServicioFotoId == fotoId, ct)
            ?? throw new NotFoundException("Fotografía no encontrada.");
        uow.OrdenesServicioFotos.Remove(f);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<PdfFile> GetReportePdfAsync(int id, CancellationToken ct = default)
    {
        var os = await Load(id, ct);
        var bytes = pdf.GenerateReporteTecnico(os, qr.GeneratePng(os.Folio));
        return new PdfFile(bytes, $"Reporte_{os.Folio}.pdf");
    }
}

public class InventarioService(IUnitOfWork uow, ICurrentUser user) : IInventarioService
{
    public async Task<PagedResult<ProductoDto>> ListProductosAsync(
        string? q, string? grupo, string? categoria, bool? bajoStock, int page, int size, CancellationToken ct = default)
    {
        (page, size) = (Math.Max(page, 1), Math.Clamp(size, 1, 200));
        var query = uow.Productos.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(grupo)) query = query.Where(p => p.GrupoCategoria == grupo);
        if (!string.IsNullOrWhiteSpace(categoria)) query = query.Where(p => p.Categoria == categoria);
        if (bajoStock == true) query = query.Where(p => p.StockActual <= p.StockMinimo);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(p =>
                p.SKU.Contains(q)
                || p.Nombre.Contains(q)
                || (p.Marca != null && p.Marca.Contains(q))
                || (p.ModeloCompatible != null && p.ModeloCompatible.Contains(q))
                || p.CodigoUbicacion.Contains(q)
                || (p.CodigoBarras != null && p.CodigoBarras.Contains(q)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(p => p.GrupoCategoria).ThenBy(p => p.Nombre).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(p => p.ToDto()).ToList(), total, page, size);
    }

    public async Task<ProductoDto> GetProductoAsync(int id, CancellationToken ct = default)
        => (await uow.Productos.GetByIdAsync(id, ct) ?? throw new NotFoundException("Producto no encontrado.")).ToDto();

    private static void Apply(Producto p, ProductoSaveDto d)
    {
        p.SKU = d.SKU.Trim().ToUpperInvariant();
        p.CodigoBarras = string.IsNullOrWhiteSpace(d.CodigoBarras) ? p.SKU : d.CodigoBarras.Trim();
        p.Nombre = d.Nombre.Trim();
        p.Descripcion = d.Descripcion;
        p.GrupoCategoria = d.GrupoCategoria.Trim();
        p.Categoria = d.Categoria.Trim();
        p.Marca = d.Marca;
        p.ModeloCompatible = d.ModeloCompatible;
        p.Unidad = string.IsNullOrWhiteSpace(d.Unidad) ? "PZA" : d.Unidad.Trim().ToUpperInvariant();
        p.CostoUnitario = d.CostoUnitario;
        p.PrecioVenta = d.PrecioVenta;
        p.StockMinimo = d.StockMinimo;
        p.StockMaximo = d.StockMaximo <= 0 ? 100 : d.StockMaximo;
        p.Almacen = string.IsNullOrWhiteSpace(d.Almacen) ? "ALM01" : d.Almacen.Trim().ToUpperInvariant();
        p.Pasillo = string.IsNullOrWhiteSpace(d.Pasillo) ? "PAS01" : d.Pasillo.Trim().ToUpperInvariant();
        p.Rack = string.IsNullOrWhiteSpace(d.Rack) ? "R01" : d.Rack.Trim().ToUpperInvariant();
        p.Anaquel = string.IsNullOrWhiteSpace(d.Anaquel) ? "ANA" : d.Anaquel.Trim().ToUpperInvariant();
        p.Nivel = string.IsNullOrWhiteSpace(d.Nivel) ? "N1" : d.Nivel.Trim().ToUpperInvariant();
        p.Posicion = string.IsNullOrWhiteSpace(d.Posicion) ? "P1" : d.Posicion.Trim().ToUpperInvariant();
        p.CodigoUbicacion = $"{p.Almacen}-{p.Pasillo}-{p.Rack}-{p.Anaquel}-{p.Nivel}";
        p.Activo = d.Activo;
    }

    public async Task<ProductoDto> CreateProductoAsync(ProductoSaveDto d, CancellationToken ct = default)
    {
        var sku = d.SKU.Trim().ToUpperInvariant();
        if (await uow.Productos.Query().AnyAsync(x => x.SKU == sku, ct))
            throw new BusinessException("Ya existe un producto registrado con ese SKU.");

        var p = new Producto { StockActual = d.StockActual };
        Apply(p, d);
        await uow.Productos.AddAsync(p, ct);
        await uow.SaveChangesAsync(ct);

        if (d.StockActual > 0)
        {
            await uow.MovimientosInventario.AddAsync(new MovimientoInventario
            {
                ProductoId = p.ProductoId,
                TipoMovimiento = "Entrada",
                Subtipo = "Ajuste",
                Concepto = "Inventario inicial de alta de producto",
                ReferenciaFolio = p.SKU,
                Cantidad = d.StockActual,
                StockAnterior = 0,
                StockPosterior = d.StockActual,
                CostoUnitario = p.CostoUnitario,
                UbicacionDestino = p.CodigoUbicacion,
                UsuarioId = user.UserId,
                UsuarioNombre = user.Nombre,
                Fecha = DateTime.UtcNow
            }, ct);
            await uow.SaveChangesAsync(ct);
        }

        return p.ToDto();
    }

    public async Task<ProductoDto> UpdateProductoAsync(int id, ProductoSaveDto d, CancellationToken ct = default)
    {
        var p = await uow.Productos.GetByIdAsync(id, ct) ?? throw new NotFoundException("Producto no encontrado.");
        var sku = d.SKU.Trim().ToUpperInvariant();
        if (await uow.Productos.Query().AnyAsync(x => x.SKU == sku && x.ProductoId != id, ct))
            throw new BusinessException("Ya existe otro producto con ese SKU.");

        Apply(p, d);
        await uow.SaveChangesAsync(ct);
        return p.ToDto();
    }

    public async Task<MovimientoInventarioDto> RegistrarMovimientoAsync(MovimientoCreateDto d, CancellationToken ct = default)
    {
        var p = await uow.Productos.GetByIdAsync(d.ProductoId, ct) ?? throw new NotFoundException("Producto no encontrado.");
        var esEntrada = d.TipoMovimiento == "Entrada";
        if (!esEntrada && p.StockActual < d.Cantidad)
            throw new BusinessException($"Stock insuficiente para {p.Nombre} (Disponible: {p.StockActual}, Solicitado: {d.Cantidad}).");

        var anterior = p.StockActual;
        p.StockActual = esEntrada ? anterior + d.Cantidad : anterior - d.Cantidad;
        if (esEntrada && d.CostoUnitario.HasValue && d.CostoUnitario.Value > 0)
            p.CostoUnitario = d.CostoUnitario.Value;

        var m = new MovimientoInventario
        {
            ProductoId = p.ProductoId,
            TipoMovimiento = d.TipoMovimiento,
            Subtipo = d.Subtipo,
            Concepto = string.IsNullOrWhiteSpace(d.Concepto) ? $"{d.TipoMovimiento} por {d.Subtipo}" : d.Concepto.Trim(),
            ReferenciaFolio = d.ReferenciaFolio,
            Cantidad = d.Cantidad,
            StockAnterior = anterior,
            StockPosterior = p.StockActual,
            CostoUnitario = d.CostoUnitario ?? p.CostoUnitario,
            UbicacionOrigen = esEntrada ? d.UbicacionOrigen : (d.UbicacionOrigen ?? p.CodigoUbicacion),
            UbicacionDestino = esEntrada ? (d.UbicacionDestino ?? p.CodigoUbicacion) : d.UbicacionDestino,
            Observaciones = d.Observaciones,
            UsuarioId = user.UserId,
            UsuarioNombre = user.Nombre,
            Fecha = DateTime.UtcNow
        };

        await uow.MovimientosInventario.AddAsync(m, ct);
        await uow.SaveChangesAsync(ct);
        m.Producto = p;
        return m.ToDto();
    }

    public async Task<PagedResult<MovimientoInventarioDto>> ListKardexAsync(
        int? productoId, string? tipo, string? subtipo, string? q, int page, int size, CancellationToken ct = default)
    {
        (page, size) = (Math.Max(page, 1), Math.Clamp(size, 1, 200));
        var query = uow.MovimientosInventario.Query().AsNoTracking().Include(m => m.Producto).AsQueryable();
        if (productoId.HasValue) query = query.Where(m => m.ProductoId == productoId);
        if (!string.IsNullOrWhiteSpace(tipo)) query = query.Where(m => m.TipoMovimiento == tipo);
        if (!string.IsNullOrWhiteSpace(subtipo)) query = query.Where(m => m.Subtipo == subtipo);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(m =>
                m.Concepto.Contains(q)
                || (m.ReferenciaFolio != null && m.ReferenciaFolio.Contains(q))
                || m.Producto!.Nombre.Contains(q)
                || m.Producto.SKU.Contains(q));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.MovimientoId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(m => m.ToDto()).ToList(), total, page, size);
    }
}

public class VentaService(IUnitOfWork uow, ICurrentUser user) : IVentaService
{
    private IQueryable<VentaDocumento> Full() => uow.Ventas.Query()
        .Include(v => v.Cliente)
        .Include(v => v.Partidas).ThenInclude(p => p.Producto);

    private async Task<VentaDocumento> Load(int id, CancellationToken ct)
        => await Full().FirstOrDefaultAsync(v => v.VentaId == id, ct)
           ?? throw new NotFoundException("Documento de venta no encontrado.");

    private static string PrefijoPorTipo(string tipo) => tipo switch
    {
        "Cotización" => "COT",
        "Pedido" => "PED",
        "Remisión" => "REM",
        "Factura" => "FAC",
        _ => "VTA"
    };

    public async Task<PagedResult<VentaDocumentoDto>> ListAsync(
        string? q, string? tipoDocumento, string? estatus, int? clienteId, bool? sinSalidaInventario, int page, int size, CancellationToken ct = default)
    {
        (page, size) = (Math.Max(page, 1), Math.Clamp(size, 1, 200));
        var query = Full().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(tipoDocumento)) query = query.Where(v => v.TipoDocumento == tipoDocumento);
        if (!string.IsNullOrWhiteSpace(estatus)) query = query.Where(v => v.Estatus == estatus);
        if (clienteId.HasValue) query = query.Where(v => v.ClienteId == clienteId);
        if (sinSalidaInventario == true)
            query = query.Where(v => (v.Estatus == "Entregada" || v.Estatus == "Facturada") && !v.SalidaInventarioRegistrada);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(v =>
                v.Folio.Contains(q)
                || v.Cliente!.RazonSocial.Contains(q)
                || (v.FolioFacturaCFDI != null && v.FolioFacturaCFDI.Contains(q)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(v => v.VentaId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(v => v.ToDto()).ToList(), total, page, size);
    }

    public async Task<VentaDocumentoDto> GetAsync(int id, CancellationToken ct = default)
        => (await Load(id, ct)).ToDto();

    public async Task<VentaDocumentoDto> CreateAsync(VentaCreateDto d, CancellationToken ct = default)
    {
        if (!await uow.Clientes.Query().AnyAsync(c => c.ClienteId == d.ClienteId, ct))
            throw new BusinessException("El cliente especificado no existe.");
        if (d.Partidas is null || d.Partidas.Count == 0)
            throw new BusinessException("Agregue al menos una partida al documento.");

        await uow.BeginAsync(ct);
        try
        {
            var anio = DateTime.UtcNow.Year;
            var prefijo = PrefijoPorTipo(d.TipoDocumento);
            var folio = await uow.NextFolioPrefijoAsync(prefijo, anio, ct);

            var v = new VentaDocumento
            {
                Folio = folio,
                TipoDocumento = d.TipoDocumento,
                Estatus = string.IsNullOrWhiteSpace(d.Estatus) ? "Borrador" : d.Estatus,
                ClienteId = d.ClienteId,
                OrdenServicioId = d.OrdenServicioId,
                DocumentoOrigenId = d.DocumentoOrigenId,
                Fecha = DateTime.UtcNow,
                FechaVigencia = d.FechaVigencia ?? DateTime.UtcNow.AddDays(15),
                FolioFacturaCFDI = d.FolioFacturaCFDI,
                UsoCFDI = d.UsoCFDI,
                MetodoPago = d.MetodoPago,
                FormaPago = d.FormaPago,
                DescuentoAutomaticoInventario = d.DescuentoAutomaticoInventario,
                Observaciones = d.Observaciones,
                VendedorId = user.UserId,
                VendedorNombre = user.Nombre
            };

            decimal subtotal = 0, descTotal = 0;
            foreach (var item in d.Partidas)
            {
                var importeBruto = item.Cantidad * item.PrecioUnitario;
                var importeNeto = Math.Max(0, importeBruto - item.Descuento);
                subtotal += importeBruto;
                descTotal += item.Descuento;

                v.Partidas.Add(new VentaPartida
                {
                    ProductoId = item.ProductoId,
                    EquipoId = item.EquipoId,
                    Descripcion = item.Descripcion,
                    NumeroSerie = item.NumeroSerie,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = item.PrecioUnitario,
                    Descuento = item.Descuento,
                    Importe = Math.Round(importeNeto, 2)
                });
            }

            var baseGravable = Math.Max(0, subtotal - descTotal);
            v.Subtotal = Math.Round(subtotal, 2);
            v.Descuento = Math.Round(descTotal, 2);
            v.IVA = Math.Round(baseGravable * 0.16m, 2);
            v.Total = Math.Round(baseGravable + v.IVA, 2);

            await uow.Ventas.AddAsync(v, ct);
            await uow.SaveChangesAsync(ct);

            if ((v.Estatus is "Entregada" or "Facturada") && v.DescuentoAutomaticoInventario)
            {
                await DescontarInventarioVentaInternoAsync(v, ct);
                await uow.SaveChangesAsync(ct);
            }

            await uow.CommitAsync(ct);
            return (await Load(v.VentaId, ct)).ToDto();
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }

    private async Task DescontarInventarioVentaInternoAsync(VentaDocumento v, CancellationToken ct)
    {
        if (v.SalidaInventarioRegistrada) return;

        var partidasConProducto = v.Partidas.Where(p => p.ProductoId.HasValue && p.Cantidad > 0).ToList();
        foreach (var part in partidasConProducto)
        {
            var prod = await uow.Productos.GetByIdAsync(part.ProductoId!.Value, ct)
                ?? throw new BusinessException($"El producto {part.Descripcion} no existe en inventario.");

            var anterior = prod.StockActual;
            prod.StockActual = anterior - part.Cantidad;

            await uow.MovimientosInventario.AddAsync(new MovimientoInventario
            {
                ProductoId = prod.ProductoId,
                TipoMovimiento = "Salida",
                Subtipo = "Venta",
                Concepto = $"Salida por {v.TipoDocumento} {v.Folio}",
                ReferenciaFolio = v.Folio,
                VentaId = v.VentaId,
                Cantidad = part.Cantidad,
                StockAnterior = anterior,
                StockPosterior = prod.StockActual,
                CostoUnitario = prod.CostoUnitario,
                UbicacionOrigen = prod.CodigoUbicacion,
                Observaciones = $"Sincronización Ventas-Inventario ({v.Estatus})",
                UsuarioId = user.UserId,
                UsuarioNombre = user.Nombre,
                Fecha = DateTime.UtcNow
            }, ct);

            if (part.EquipoId.HasValue)
            {
                await uow.EquipoHistoriales.AddAsync(new EquipoHistorial
                {
                    EquipoId = part.EquipoId.Value,
                    TipoEvento = "Venta",
                    Titulo = $"Equipo incluido en {v.TipoDocumento} {v.Folio}",
                    Detalle = $"Importe partida: ${part.Importe:N2} · Estatus: {v.Estatus}",
                    FolioReferencia = v.Folio,
                    ReferenciaId = v.VentaId,
                    UsuarioId = user.UserId,
                    UsuarioNombre = user.Nombre
                }, ct);
            }
        }

        v.SalidaInventarioRegistrada = true;
    }

    public async Task<VentaDocumentoDto> CambiarEstatusAsync(int id, VentaCambiarEstatusDto d, CancellationToken ct = default)
    {
        var v = await Load(id, ct);
        v.Estatus = d.NuevoEstatus.Trim();
        if (!string.IsNullOrWhiteSpace(d.NuevoTipoDocumento)) v.TipoDocumento = d.NuevoTipoDocumento.Trim();
        if (!string.IsNullOrWhiteSpace(d.FolioFacturaCFDI)) v.FolioFacturaCFDI = d.FolioFacturaCFDI.Trim();
        if (!string.IsNullOrWhiteSpace(d.UsoCFDI)) v.UsoCFDI = d.UsoCFDI.Trim();
        if (!string.IsNullOrWhiteSpace(d.MetodoPago)) v.MetodoPago = d.MetodoPago.Trim();
        if (!string.IsNullOrWhiteSpace(d.FormaPago)) v.FormaPago = d.FormaPago.Trim();

        var descontar = d.DescontarInventarioAhora ?? v.DescuentoAutomaticoInventario;
        if ((v.Estatus is "Entregada" or "Facturada") && descontar && !v.SalidaInventarioRegistrada)
        {
            await DescontarInventarioVentaInternoAsync(v, ct);
        }

        await uow.SaveChangesAsync(ct);
        return (await Load(id, ct)).ToDto();
    }

    public async Task<VentaDocumentoDto> RegistrarSalidaInventarioAsync(int id, CancellationToken ct = default)
    {
        var v = await Load(id, ct);
        if (v.SalidaInventarioRegistrada)
            throw new BusinessException("La salida de inventario para esta venta ya fue registrada.");

        await DescontarInventarioVentaInternoAsync(v, ct);
        await uow.SaveChangesAsync(ct);
        return (await Load(id, ct)).ToDto();
    }
}

public class CompraService(IUnitOfWork uow, ICurrentUser user) : ICompraService
{
    private IQueryable<Compra> Full() => uow.Compras.Query()
        .Include(c => c.Partidas).ThenInclude(p => p.Producto);

    public async Task<PagedResult<CompraDto>> ListAsync(string? q, string? estatus, int page, int size, CancellationToken ct = default)
    {
        (page, size) = (Math.Max(page, 1), Math.Clamp(size, 1, 200));
        var query = Full().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(estatus)) query = query.Where(c => c.Estatus == estatus);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(c => c.Folio.Contains(q) || c.Proveedor.Contains(q) || (c.FacturaProveedor != null && c.FacturaProveedor.Contains(q)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(c => c.CompraId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(c => c.ToDto()).ToList(), total, page, size);
    }

    public async Task<CompraDto> CreateAsync(CompraCreateDto d, CancellationToken ct = default)
    {
        if (d.Partidas is null || d.Partidas.Count == 0)
            throw new BusinessException("Incluya al menos un producto en la compra.");

        await uow.BeginAsync(ct);
        try
        {
            var anio = DateTime.UtcNow.Year;
            var folio = await uow.NextFolioPrefijoAsync("COM", anio, ct);

            var c = new Compra
            {
                Folio = folio,
                Proveedor = d.Proveedor.Trim(),
                RFCProveedor = d.RFCProveedor,
                FacturaProveedor = d.FacturaProveedor,
                FechaCompra = DateTime.UtcNow,
                Estatus = string.IsNullOrWhiteSpace(d.Estatus) ? "Recibida" : d.Estatus,
                Observaciones = d.Observaciones,
                UsuarioNombre = user.Nombre
            };

            decimal subtotal = 0;
            foreach (var item in d.Partidas)
            {
                var prod = await uow.Productos.GetByIdAsync(item.ProductoId, ct)
                    ?? throw new BusinessException($"El producto #{item.ProductoId} no existe.");
                var imp = Math.Round(item.Cantidad * item.CostoUnitario, 2);
                subtotal += imp;
                c.Partidas.Add(new CompraPartida
                {
                    ProductoId = prod.ProductoId,
                    Descripcion = prod.Nombre,
                    Cantidad = item.Cantidad,
                    CostoUnitario = item.CostoUnitario,
                    Importe = imp
                });
            }

            c.Subtotal = subtotal;
            c.IVA = Math.Round(subtotal * 0.16m, 2);
            c.Total = Math.Round(c.Subtotal + c.IVA, 2);

            await uow.Compras.AddAsync(c, ct);
            await uow.SaveChangesAsync(ct);

            if (d.RegistrarEntradaInmediata || c.Estatus == "Recibida")
            {
                await AplicarEntradaCompraAsync(c, ct);
                await uow.SaveChangesAsync(ct);
            }

            await uow.CommitAsync(ct);
            var saved = await Full().FirstAsync(x => x.CompraId == c.CompraId, ct);
            return saved.ToDto();
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }

    private async Task AplicarEntradaCompraAsync(Compra c, CancellationToken ct)
    {
        if (c.EntradaInventarioRegistrada) return;
        foreach (var part in c.Partidas)
        {
            var prod = await uow.Productos.GetByIdAsync(part.ProductoId, ct)
                ?? throw new BusinessException($"Producto #{part.ProductoId} no encontrado.");
            var anterior = prod.StockActual;
            prod.StockActual = anterior + part.Cantidad;
            if (part.CostoUnitario > 0) prod.CostoUnitario = part.CostoUnitario;

            await uow.MovimientosInventario.AddAsync(new MovimientoInventario
            {
                ProductoId = prod.ProductoId,
                TipoMovimiento = "Entrada",
                Subtipo = "Compra",
                Concepto = $"Entrada por Compra {c.Folio} · {c.Proveedor}",
                ReferenciaFolio = c.Folio,
                CompraId = c.CompraId,
                Cantidad = part.Cantidad,
                StockAnterior = anterior,
                StockPosterior = prod.StockActual,
                CostoUnitario = part.CostoUnitario,
                UbicacionDestino = prod.CodigoUbicacion,
                Observaciones = c.FacturaProveedor != null ? $"Factura proveedor: {c.FacturaProveedor}" : null,
                UsuarioId = user.UserId,
                UsuarioNombre = user.Nombre,
                Fecha = DateTime.UtcNow
            }, ct);
        }
        c.Estatus = "Recibida";
        c.FechaRecepcion = DateTime.UtcNow;
        c.EntradaInventarioRegistrada = true;
    }

    public async Task<CompraDto> RecibirCompraAsync(int id, CancellationToken ct = default)
    {
        var c = await Full().FirstOrDefaultAsync(x => x.CompraId == id, ct)
            ?? throw new NotFoundException("Compra no encontrada.");
        if (c.EntradaInventarioRegistrada)
            throw new BusinessException("Esta compra ya fue ingresada al almacén.");

        await AplicarEntradaCompraAsync(c, ct);
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }
}

public class MantenimientoService(IUnitOfWork uow, ICurrentUser user) : IMantenimientoService
{
    private IQueryable<Mantenimiento> Full() => uow.Mantenimientos.Query()
        .Include(m => m.Equipo!).ThenInclude(e => e.Cliente)
        .Include(m => m.OrdenServicio)
        .Include(m => m.Tecnico);

    public async Task<PagedResult<MantenimientoDto>> ListAsync(
        string? q, string? tipo, string? estatus, int? equipoId, int page, int size, CancellationToken ct = default)
    {
        (page, size) = (Math.Max(page, 1), Math.Clamp(size, 1, 200));
        var query = Full().AsNoTracking();
        if (equipoId.HasValue) query = query.Where(m => m.EquipoId == equipoId);
        if (!string.IsNullOrWhiteSpace(tipo)) query = query.Where(m => m.Tipo == tipo);
        if (!string.IsNullOrWhiteSpace(estatus)) query = query.Where(m => m.Estatus == estatus);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(m => m.Folio.Contains(q) || m.Equipo!.SeriePrincipal.Contains(q) || m.Equipo.Cliente!.RazonSocial.Contains(q));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(m => m.MantenimientoId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(m => m.ToDto()).ToList(), total, page, size);
    }

    public async Task<MantenimientoDto> CreateAsync(MantenimientoSaveDto d, CancellationToken ct = default)
    {
        if (!await uow.Equipos.Query().AnyAsync(e => e.EquipoId == d.EquipoId, ct))
            throw new BusinessException("El equipo no existe.");

        await uow.BeginAsync(ct);
        try
        {
            var folio = await uow.NextFolioPrefijoAsync("MTO", DateTime.UtcNow.Year, ct);
            var m = new Mantenimiento
            {
                Folio = folio,
                EquipoId = d.EquipoId,
                OrdenServicioId = d.OrdenServicioId,
                Tipo = d.Tipo,
                Estatus = d.Estatus,
                FechaProgramada = d.FechaProgramada,
                FechaRealizada = d.FechaRealizada,
                ProximoMantenimiento = d.ProximoMantenimiento,
                TecnicoId = d.TecnicoId ?? user.UserId,
                Descripcion = d.Descripcion,
                Actividades = d.Actividades,
                Hallazgos = d.Hallazgos,
                Recomendaciones = d.Recomendaciones,
                Costo = d.Costo
            };
            await uow.Mantenimientos.AddAsync(m, ct);

            await uow.EquipoHistoriales.AddAsync(new EquipoHistorial
            {
                EquipoId = d.EquipoId,
                TipoEvento = d.Tipo,
                Titulo = $"{d.Tipo} registrado · {folio}",
                Detalle = $"{d.Descripcion} · Estatus: {d.Estatus}",
                FolioReferencia = folio,
                UsuarioId = user.UserId,
                UsuarioNombre = user.Nombre
            }, ct);

            await uow.SaveChangesAsync(ct);
            await uow.CommitAsync(ct);
            return (await Full().FirstAsync(x => x.MantenimientoId == m.MantenimientoId, ct)).ToDto();
        }
        catch { await uow.RollbackAsync(ct); throw; }
    }

    public async Task<MantenimientoDto> UpdateAsync(int id, MantenimientoSaveDto d, CancellationToken ct = default)
    {
        var m = await uow.Mantenimientos.GetByIdAsync(id, ct) ?? throw new NotFoundException("Registro no encontrado.");
        m.EquipoId = d.EquipoId;
        m.OrdenServicioId = d.OrdenServicioId;
        m.Tipo = d.Tipo;
        m.Estatus = d.Estatus;
        m.FechaProgramada = d.FechaProgramada;
        m.FechaRealizada = d.FechaRealizada;
        m.ProximoMantenimiento = d.ProximoMantenimiento;
        m.TecnicoId = d.TecnicoId;
        m.Descripcion = d.Descripcion;
        m.Actividades = d.Actividades;
        m.Hallazgos = d.Hallazgos;
        m.Recomendaciones = d.Recomendaciones;
        m.Costo = d.Costo;
        await uow.SaveChangesAsync(ct);
        return (await Full().FirstAsync(x => x.MantenimientoId == id, ct)).ToDto();
    }
}

public class GarantiaService(IUnitOfWork uow, ICurrentUser user) : IGarantiaService
{
    private IQueryable<Garantia> Full() => uow.Garantias.Query()
        .Include(g => g.Equipo)
        .Include(g => g.Cliente);

    public async Task<PagedResult<GarantiaDto>> ListAsync(string? q, string? estatus, int? equipoId, int page, int size, CancellationToken ct = default)
    {
        (page, size) = (Math.Max(page, 1), Math.Clamp(size, 1, 200));
        var query = Full().AsNoTracking();
        if (equipoId.HasValue) query = query.Where(g => g.EquipoId == equipoId);
        if (!string.IsNullOrWhiteSpace(estatus)) query = query.Where(g => g.Estatus == estatus);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(g => g.Folio.Contains(q) || g.Equipo!.SeriePrincipal.Contains(q) || g.Cliente!.RazonSocial.Contains(q));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(g => g.GarantiaId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(g => g.ToDto()).ToList(), total, page, size);
    }

    public async Task<GarantiaDto> CreateAsync(GarantiaSaveDto d, CancellationToken ct = default)
    {
        await uow.BeginAsync(ct);
        try
        {
            var folio = await uow.NextFolioPrefijoAsync("GAR", DateTime.UtcNow.Year, ct);
            var g = new Garantia
            {
                Folio = folio,
                EquipoId = d.EquipoId,
                ClienteId = d.ClienteId,
                OrdenServicioId = d.OrdenServicioId,
                VentaId = d.VentaId,
                TipoGarantia = d.TipoGarantia,
                FechaInicio = d.FechaInicio,
                FechaFin = d.FechaFin,
                Cobertura = d.Cobertura,
                Condiciones = d.Condiciones,
                Estatus = string.IsNullOrWhiteSpace(d.Estatus) ? "Vigente" : d.Estatus,
                MotivoReclamacion = d.MotivoReclamacion,
                Resolucion = d.Resolucion
            };
            await uow.Garantias.AddAsync(g, ct);

            await uow.EquipoHistoriales.AddAsync(new EquipoHistorial
            {
                EquipoId = d.EquipoId,
                TipoEvento = "Garantia",
                Titulo = $"Póliza de Garantía registrada · {folio} ({d.TipoGarantia})",
                Detalle = $"Vigencia: {d.FechaInicio:dd/MM/yyyy} al {d.FechaFin:dd/MM/yyyy} · Cobertura: {d.Cobertura}",
                FolioReferencia = folio,
                UsuarioId = user.UserId,
                UsuarioNombre = user.Nombre
            }, ct);

            await uow.SaveChangesAsync(ct);
            await uow.CommitAsync(ct);
            return (await Full().FirstAsync(x => x.GarantiaId == g.GarantiaId, ct)).ToDto();
        }
        catch { await uow.RollbackAsync(ct); throw; }
    }

    public async Task<GarantiaDto> UpdateAsync(int id, GarantiaSaveDto d, CancellationToken ct = default)
    {
        var g = await uow.Garantias.GetByIdAsync(id, ct) ?? throw new NotFoundException("Garantía no encontrada.");
        g.EquipoId = d.EquipoId;
        g.ClienteId = d.ClienteId;
        g.OrdenServicioId = d.OrdenServicioId;
        g.VentaId = d.VentaId;
        g.TipoGarantia = d.TipoGarantia;
        g.FechaInicio = d.FechaInicio;
        g.FechaFin = d.FechaFin;
        g.Cobertura = d.Cobertura;
        g.Condiciones = d.Condiciones;
        g.Estatus = d.Estatus;
        g.MotivoReclamacion = d.MotivoReclamacion;
        g.Resolucion = d.Resolucion;
        await uow.SaveChangesAsync(ct);
        return (await Full().FirstAsync(x => x.GarantiaId == id, ct)).ToDto();
    }
}
