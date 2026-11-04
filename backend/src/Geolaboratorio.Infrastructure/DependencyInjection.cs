using System.Net.Sockets;
using Geolaboratorio.Application;
using Geolaboratorio.Domain;
using Geolaboratorio.Infrastructure.Persistence;
using Geolaboratorio.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Geolaboratorio.Infrastructure;

public static class DependencyInjection
{
    private static bool IsMySqlReachable(string cs)
    {
        try
        {
            var host = "localhost";
            var port = 3306;
            foreach (var part in cs.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                var k = kv[0].Trim().ToLowerInvariant();
                var v = kv[1].Trim();
                if (k is "server" or "host" or "data source") host = v;
                else if (k == "port" && int.TryParse(v, out var p)) port = p;
            }
            using var tcp = new TcpClient();
            var ar = tcp.BeginConnect(host, port, null, null);
            return ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(400)) && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection s, IConfiguration cfg)
    {
        s.Configure<JwtOptions>(cfg.GetSection("Jwt"));
        s.Configure<AppOptions>(cfg.GetSection("App"));
        var cs = cfg.GetConnectionString("Default") ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");
        var provider = cfg["Database:Provider"];
        var useMySql = provider?.Equals("MySql", StringComparison.OrdinalIgnoreCase) == true
            || (string.IsNullOrWhiteSpace(provider) && IsMySqlReachable(cs));

        if (useMySql)
        {
            s.AddDbContext<AppDbContext>(o => o.UseMySql(
                cs,
                ServerVersion.Create(8, 0, 36, Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MySql),
                m => m.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        }
        else
        {
            var sqliteCs = cfg.GetConnectionString("Sqlite") ?? "Data Source=geolaboratorio_erp.db";
            s.AddDbContext<AppDbContext>(o => o.UseSqlite(sqliteCs));
        }

        s.AddScoped<IUnitOfWork, UnitOfWork>();
        s.AddScoped<IAuthService, AuthService>();
        s.AddScoped<IUsuarioService, UsuarioService>();
        s.AddScoped<IClienteService, ClienteService>();
        s.AddScoped<IEquipoService, EquipoService>();
        s.AddScoped<ICertificadoService, CertificadoService>();
        s.AddScoped<IOrdenServicioService, OrdenServicioService>();
        s.AddScoped<IInventarioService, InventarioService>();
        s.AddScoped<IVentaService, VentaService>();
        s.AddScoped<ICompraService, CompraService>();
        s.AddScoped<IMantenimientoService, MantenimientoService>();
        s.AddScoped<IGarantiaService, GarantiaService>();
        s.AddScoped<IDashboardService, DashboardService>();
        s.AddScoped<IAuditService, AuditService>();
        s.AddSingleton<IQrService, QrService>();
        s.AddSingleton<IPdfService, PdfService>();
        return s;
    }
}

public static class DbSeeder
{
    private static async Task EnsureColumnAsync(AppDbContext db, string table, string column, string definition)
    {
        var exists = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = {0} AND COLUMN_NAME = {1}",
            table, column).SingleAsync();
        if (exists == 0)
        {
            var ddl = $"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition}";
            await db.Database.ExecuteSqlRawAsync(ddl);
        }
    }

    /// <summary>Crea o actualiza el esquema de base de datos de forma idempotente y siembra datos iniciales.</summary>
    public static async Task SeedAsync(AppDbContext db, IConfiguration cfg)
    {
        await db.Database.EnsureCreatedAsync();

        var isSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        if (!isSqlite)
        {
            await EnsureColumnAsync(db, "Equipos", "CodigoQR", "VARCHAR(120) NULL");
            await EnsureColumnAsync(db, "Equipos", "CodigoBarras", "VARCHAR(120) NULL");
            await EnsureColumnAsync(db, "Equipos", "Estatus", "VARCHAR(30) NOT NULL DEFAULT 'Activo'");
            await EnsureColumnAsync(db, "Equipos", "FechaBaja", "DATETIME(6) NULL");
            await EnsureColumnAsync(db, "Equipos", "MotivoBaja", "VARCHAR(500) NULL");

            await EnsureColumnAsync(db, "Certificados", "OrdenServicioId", "INT NULL");
            await EnsureColumnAsync(db, "Certificados", "TipoRegistro", "VARCHAR(30) NOT NULL DEFAULT 'Calibración'");
        }

        if (!await db.ConfiguracionVigencias.AnyAsync())
        {
            foreach (var t in Enum.GetValues<TipoEquipo>())
                db.ConfiguracionVigencias.Add(new ConfiguracionVigencia
                {
                    TipoEquipo = t,
                    Meses = 12
                });
            await db.SaveChangesAsync();
        }

        if (!await db.Usuarios.AnyAsync())
        {
            var pwd = cfg["Seed:AdminPassword"] ?? "Admin#2026!";
            db.Usuarios.Add(new Usuario
            {
                Nombre = "Administrador", Correo = cfg["Seed:AdminEmail"] ?? "admin@geolaboratorio.com.mx",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(pwd), Rol = RolUsuario.Administrador
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Productos.AnyAsync())
        {
            var productosIniciales = new[]
            {
                new Producto { SKU = "REF-PAN-LEI01", CodigoBarras = "750100010001", Nombre = "Pantalla Leica TS16 LCD Touch", GrupoCategoria = "Refacciones", Categoria = "Pantallas", Marca = "Leica", ModeloCompatible = "TS16 / TS13", CostoUnitario = 8500m, PrecioVenta = 14200m, StockActual = 6, StockMinimo = 2, Almacen = "ALM01", Pasillo = "PAS02", Rack = "R03", Anaquel = "ANB", Nivel = "N2", Posicion = "P1", CodigoUbicacion = "ALM01-PAS02-R03-ANB-N2" },
                new Producto { SKU = "REF-TAR-GNS01", CodigoBarras = "750100010002", Nombre = "Tarjeta GNSS Multifrecuencia RTK", GrupoCategoria = "Refacciones", Categoria = "Tarjetas", Marca = "Trimble", ModeloCompatible = "R12i / R10", CostoUnitario = 19500m, PrecioVenta = 29800m, StockActual = 4, StockMinimo = 2, Almacen = "ALM01", Pasillo = "PAS02", Rack = "R03", Anaquel = "ANB", Nivel = "N3", Posicion = "P2", CodigoUbicacion = "ALM01-PAS02-R03-ANB-N3" },
                new Producto { SKU = "REF-TEC-TOP01", CodigoBarras = "750100010003", Nombre = "Teclado Alfanumérico Estación Total", GrupoCategoria = "Refacciones", Categoria = "Teclados", Marca = "Topcon", ModeloCompatible = "GM-52 / GM-100", CostoUnitario = 2400m, PrecioVenta = 4350m, StockActual = 8, StockMinimo = 3, Almacen = "ALM01", Pasillo = "PAS02", Rack = "R01", Anaquel = "ANA", Nivel = "N1", Posicion = "P3", CodigoUbicacion = "ALM01-PAS02-R01-ANA-N1" },
                new Producto { SKU = "REF-BAT-GEB221", CodigoBarras = "750100010004", Nombre = "Batería Ion-Litio GEB221 7.4V", GrupoCategoria = "Refacciones", Categoria = "Baterías", Marca = "Leica", ModeloCompatible = "Serie TS / GS", CostoUnitario = 1800m, PrecioVenta = 3200m, StockActual = 1, StockMinimo = 4, Almacen = "ALM01", Pasillo = "PAS01", Rack = "R02", Anaquel = "ANC", Nivel = "N1", Posicion = "P1", CodigoUbicacion = "ALM01-PAS01-R02-ANC-N1" },
                new Producto { SKU = "REF-RAD-UHF01", CodigoBarras = "750100010005", Nombre = "Módulo Radio Interna UHF 410-470MHz", GrupoCategoria = "Refacciones", Categoria = "Radios", Marca = "Sokkia", ModeloCompatible = "GCX3 / GRX3", CostoUnitario = 6200m, PrecioVenta = 9800m, StockActual = 0, StockMinimo = 2, Almacen = "ALM01", Pasillo = "PAS02", Rack = "R04", Anaquel = "ANA", Nivel = "N2", Posicion = "P2", CodigoUbicacion = "ALM01-PAS02-R04-ANA-N2" },
                new Producto { SKU = "ACC-TRI-MAD01", CodigoBarras = "750100020001", Nombre = "Trípode de Madera Uso Pesado Doble Seguro", GrupoCategoria = "Accesorios", Categoria = "Trípodes", Marca = "Seco", ModeloCompatible = "Universal 5/8\"", CostoUnitario = 2900m, PrecioVenta = 4850m, StockActual = 12, StockMinimo = 4, Almacen = "ALM01", Pasillo = "PAS03", Rack = "R01", Anaquel = "ANA", Nivel = "N1", Posicion = "P1", CodigoUbicacion = "ALM01-PAS03-R01-ANA-N1" },
                new Producto { SKU = "ACC-PRI-GPR1", CodigoBarras = "750100020002", Nombre = "Prisma Circular Profesional con Portaprisma", GrupoCategoria = "Accesorios", Categoria = "Prismas", Marca = "Leica", ModeloCompatible = "GPR1", CostoUnitario = 2100m, PrecioVenta = 3900m, StockActual = 10, StockMinimo = 3, Almacen = "ALM01", Pasillo = "PAS03", Rack = "R02", Anaquel = "ANB", Nivel = "N2", Posicion = "P1", CodigoUbicacion = "ALM01-PAS03-R02-ANB-N2" },
                new Producto { SKU = "ACC-BAS-FIB01", CodigoBarras = "750100020003", Nombre = "Bastón Fibra de Carbono 2.6m para GNSS", GrupoCategoria = "Accesorios", Categoria = "Bastones", Marca = "Seco", ModeloCompatible = "GNSS / Prisma", CostoUnitario = 2600m, PrecioVenta = 4400m, StockActual = 9, StockMinimo = 3, Almacen = "ALM01", Pasillo = "PAS03", Rack = "R02", Anaquel = "ANC", Nivel = "N1", Posicion = "P2", CodigoUbicacion = "ALM01-PAS03-R02-ANC-N1" },
                new Producto { SKU = "EQN-EST-TS16", CodigoBarras = "750100030001", Nombre = "Estación Total Robótica Leica TS16 1\"", GrupoCategoria = "Equipo Nuevo", Categoria = "Estaciones Totales", Marca = "Leica", ModeloCompatible = "TS16 P 1\" R1000", CostoUnitario = 385000m, PrecioVenta = 495000m, StockActual = 2, StockMinimo = 1, Almacen = "ALM01", Pasillo = "PAS01", Rack = "R01", Anaquel = "ANA", Nivel = "N1", Posicion = "P1", CodigoUbicacion = "ALM01-PAS01-R01-ANA-N1" },
                new Producto { SKU = "CON-ETQ-CAL01", CodigoBarras = "750100040001", Nombre = "Holograma / Etiqueta de Calibración Trazable", GrupoCategoria = "Consumibles", Categoria = "Etiquetas", Marca = "GeoLab", ModeloCompatible = "Certificados", CostoUnitario = 15m, PrecioVenta = 50m, StockActual = 250, StockMinimo = 50, Almacen = "ALM01", Pasillo = "PAS04", Rack = "R01", Anaquel = "ANA", Nivel = "N1", Posicion = "P1", CodigoUbicacion = "ALM01-PAS04-R01-ANA-N1" }
            };
            db.Productos.AddRange(productosIniciales);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Datos ficticios completos para demostración del ERP (Seed:Demo=true).</summary>
    public static async Task SeedDemoAsync(AppDbContext db)
    {
        if (await db.Clientes.AnyAsync()) return;
        var hoy = DateTime.UtcNow.Date;
        var hash = BCrypt.Net.BCrypt.HashPassword("Demo#2026!");
        var tec = new Usuario { Nombre = "Ing. Carlos Méndez", Correo = "tecnico@geolaboratorio.com.mx", PasswordHash = hash, Rol = RolUsuario.Tecnico };
        var sup = new Usuario { Nombre = "Ing. Roberto Vega", Correo = "supervisor@geolaboratorio.com.mx", PasswordHash = hash, Rol = RolUsuario.Tecnico };
        db.Usuarios.AddRange(tec, sup,
            new Usuario { Nombre = "Laura Ríos", Correo = "consulta@geolaboratorio.com.mx", PasswordHash = hash, Rol = RolUsuario.Consulta });
        var clientes = new[]
        {
            new Cliente { RazonSocial = "Constructora del Norte SA de CV", NombreComercial = "ConNorte", RFC = "CNO010203AB1", Direccion = "Av. Universidad 1200", Ciudad = "Monterrey", Estado = "Nuevo León", Telefono = "81 5555 0101", Correo = "contacto@connorte.mx", Contacto = "Ing. Ricardo Salas" },
            new Cliente { RazonSocial = "Topografía y Geomática del Bajío SC", NombreComercial = "GeoBajío", RFC = "TGB050607CD2", Direccion = "Blvd. Campestre 450", Ciudad = "León", Estado = "Guanajuato", Telefono = "477 555 0202", Correo = "info@geobajio.mx", Contacto = "Arq. Sofía Luna" },
            new Cliente { RazonSocial = "Ingeniería Vial del Pacífico SA de CV", NombreComercial = "VialPac", RFC = "IVP080910EF3", Direccion = "Calle Mar 88", Ciudad = "Mazatlán", Estado = "Sinaloa", Telefono = "669 555 0303", Correo = "ventas@vialpac.mx", Contacto = "Ing. Mario Beltrán" },
            new Cliente { RazonSocial = "Servicios Aéreos y Mapeo SA de CV", NombreComercial = "AeroMapeo", RFC = "SAM111213GH4", Direccion = "Parque Industrial 7", Ciudad = "Querétaro", Estado = "Querétaro", Telefono = "442 555 0404", Correo = "hola@aeromapeo.mx", Contacto = "Lic. Daniela Cruz" },
        };
        db.Clientes.AddRange(clientes);
        await db.SaveChangesAsync();

        (TipoEquipo t, string m, string mod, string s, int c)[] eq =
        [
            (TipoEquipo.EstacionTotal, "Leica", "TS16", "ST-100201", 0), (TipoEquipo.EstacionTotal, "Topcon", "GM-52", "ST-100202", 0),
            (TipoEquipo.GNSS, "Trimble", "R12i", "GN-200301", 1), (TipoEquipo.GPS, "Garmin", "GPSMAP 66", "GP-200302", 1),
            (TipoEquipo.NivelAutomatico, "Sokkia", "B40", "NA-300401", 2), (TipoEquipo.NivelDigital, "Leica", "LS15", "ND-300402", 2),
            (TipoEquipo.DroneRTK, "DJI", "Matrice 350 RTK", "DR-400501", 3), (TipoEquipo.EscanerLaser, "FARO", "Focus S 350", "EL-400502", 3),
            (TipoEquipo.LiDAR, "Livox", "Avia", "LI-400503", 3), (TipoEquipo.Otro, "Spectra", "SP80", "OT-500601", 1),
        ];
        var equipos = eq.Select((e, i) => new Equipo
        {
            ClienteId = clientes[e.c].ClienteId, TipoEquipo = e.t, Marca = e.m, Modelo = e.mod, SeriePrincipal = e.s,
            NumeroInterno = $"INT-{i + 1:D3}", CodigoQR = $"EQ-{i + 1:D6}", CodigoBarras = e.s,
            FechaCompra = hoy.AddYears(-2).AddDays(i * 20),
            Estatus = i is 0 or 1 or 2 ? EstatusEquipo.EnTaller : EstatusEquipo.Activo,
            IMEI1 = e.t is TipoEquipo.GNSS or TipoEquipo.DroneRTK ? $"35000000000{i:D4}" : null
        }).ToList();
        db.Equipos.AddRange(equipos);
        await db.SaveChangesAsync();

        // Series adicionales de ejemplo
        db.EquipoSeries.AddRange(
            new EquipoSerie { EquipoId = equipos[0].EquipoId, TipoSerie = "Serie Principal", Valor = "ST-100201" },
            new EquipoSerie { EquipoId = equipos[0].EquipoId, TipoSerie = "Serie Radio Interna", Valor = "RAD-INT-8821" },
            new EquipoSerie { EquipoId = equipos[0].EquipoId, TipoSerie = "Serie Controladora", Valor = "CS20-44109" },
            new EquipoSerie { EquipoId = equipos[2].EquipoId, TipoSerie = "Serie Principal", Valor = "GN-200301" },
            new EquipoSerie { EquipoId = equipos[2].EquipoId, TipoSerie = "Serie Radio Externa", Valor = "TDL450-9910" },
            new EquipoSerie { EquipoId = equipos[2].EquipoId, TipoSerie = "Serie Antena", Valor = "ANT-ZEPHYR-302" },
            new EquipoSerie { EquipoId = equipos[2].EquipoId, TipoSerie = "IMEI 1", Valor = "350000000000002" }
        );

        int[] diasCal = [30, 60, 90, 340, 350, 358, 400, 500, 20, 200, 10, 5, 150, 380];
        var anio = hoy.Year; var n = 0;
        foreach (var d in diasCal)
        {
            n++;
            var e = equipos[(n - 1) % equipos.Count];
            var cal = hoy.AddDays(-d);
            var res = n % 7 == 0 ? ResultadoCalibracion.Condicionado : n % 11 == 0 ? ResultadoCalibracion.Rechazado : ResultadoCalibracion.Aceptado;
            var cert = new Certificado
            {
                Anio = anio, Consecutivo = n, Folio = $"GEO-{anio}-{n:D6}", EquipoId = e.EquipoId, TecnicoId = tec.UsuarioId,
                FechaCalibracion = cal, FechaEmision = cal, VigenciaMeses = 12, FechaVencimiento = cal.AddMonths(12), Resultado = res,
                Resultados = "Error de medición angular: 1.5\" (tolerancia 2\")\nError de distancia: 1.0 mm + 1.5 ppm (tolerancia 2 mm + 2 ppm)\nEquipo verificado contra patrón trazable.",
                Observaciones = "Calibración realizada en laboratorio, 20 ± 2 °C.", Conclusiones = res == ResultadoCalibracion.Aceptado ? "El equipo cumple con las especificaciones del fabricante." : "Revisar observaciones.",
                QRUrl = $"https://certificados.geolaboratorio.com.mx/verificar/GEO-{anio}-{n:D6}"
            };
            if (n == 8) { cert.Estatus = EstatusCertificado.Cancelado; cert.MotivoCancelacion = "Error de captura (demo)"; cert.CanceladoEn = hoy; }
            db.Certificados.Add(cert);
        }
        db.FolioSecuencias.Add(new FolioSecuencia { Anio = anio, Ultimo = n });
        await db.SaveChangesAsync();

        // Sembrar Órdenes de Servicio, Mantenimientos, Garantías y Ventas de demostración
        var prods = await db.Productos.ToListAsync();
        var os1 = new OrdenServicio
        {
            Folio = $"OS-{anio}-000001", Anio = anio, Consecutivo = 1,
            ClienteId = clientes[0].ClienteId, EquipoId = equipos[0].EquipoId,
            TipoServicio = "Reparación", TiposServicioAdicionales = "[\"Calibración\"]",
            Estatus = "En Reparación", TecnicoId = tec.UsuarioId, SupervisorId = sup.UsuarioId,
            FechaRecepcion = DateTime.UtcNow.AddHours(-3), FechaCompromiso = hoy.AddDays(3),
            MetodoIdentificacion = "CodigoQR",
            AccesoriosChecklistJson = "[\"Batería\",\"Batería adicional\",\"Cargador\",\"Estuche\",\"Prisma\",\"Trípode\"]",
            CondicionChecklistJson = "[\"Golpes\",\"Pantalla dañada\"]",
            FallaReportada = "Pantalla táctil no responde en extremo inferior y desajuste angular horizontal.",
            Diagnostico = "Display LCD touch fracturado por impacto; requiere reemplazo de módulo de pantalla y ajuste en banco de colimadores.",
            ActividadesRealizadas = "Desensamble de carcasa, sustitución de pantalla original Leica TS16 y limpieza óptica.",
            CostoManoObra = 3500m, SubtotalRefacciones = 14200m, Total = 17700m
        };
        os1.Refacciones.Add(new OrdenServicioRefaccion
        {
            ProductoId = prods[0].ProductoId, SKU = prods[0].SKU, NombreProducto = prods[0].Nombre,
            Cantidad = 1, PrecioUnitario = prods[0].PrecioVenta, Importe = prods[0].PrecioVenta, Descontado = false
        });
        os1.HistorialEstatus.Add(new OrdenServicioEstatusHistorial { EstatusNuevo = "Recibido", UsuarioNombre = tec.Nombre, Fecha = DateTime.UtcNow.AddHours(-3), Observaciones = "Recepción inicial con accesorios completos." });
        os1.HistorialEstatus.Add(new OrdenServicioEstatusHistorial { EstatusAnterior = "Recibido", EstatusNuevo = "En Diagnóstico", UsuarioNombre = tec.Nombre, Fecha = DateTime.UtcNow.AddHours(-2), Observaciones = "Revisión de display y plomada láser." });
        os1.HistorialEstatus.Add(new OrdenServicioEstatusHistorial { EstatusAnterior = "En Diagnóstico", EstatusNuevo = "En Reparación", UsuarioNombre = sup.Nombre, Fecha = DateTime.UtcNow.AddHours(-1), Observaciones = "Autorizada instalación de pantalla Leica x1." });

        var os2 = new OrdenServicio
        {
            Folio = $"OS-{anio}-000002", Anio = anio, Consecutivo = 2,
            ClienteId = clientes[1].ClienteId, EquipoId = equipos[2].EquipoId,
            TipoServicio = "Calibración", Estatus = "En Calibración",
            TecnicoId = tec.UsuarioId, SupervisorId = sup.UsuarioId,
            FechaRecepcion = DateTime.UtcNow.AddHours(-1), FechaCompromiso = hoy.AddDays(1),
            MetodoIdentificacion = "NumeroSerie",
            AccesoriosChecklistJson = "[\"Batería\",\"Cargador\",\"Maleta rígida\",\"Radio\",\"Controladora\",\"Antena GNSS\"]",
            CondicionChecklistJson = "[\"Sin daños visibles\"]",
            FallaReportada = "Calibración anual preventiva y actualización de firmware RTK.",
            Diagnostico = "Receptor GNSS operando dentro de parámetros nominales.",
            CostoManoObra = 2800m, SubtotalRefacciones = 0m, Total = 2800m
        };
        os2.HistorialEstatus.Add(new OrdenServicioEstatusHistorial { EstatusNuevo = "Recibido", UsuarioNombre = tec.Nombre, Fecha = DateTime.UtcNow.AddHours(-1), Observaciones = "Ingreso para calibración." });
        os2.HistorialEstatus.Add(new OrdenServicioEstatusHistorial { EstatusAnterior = "Recibido", EstatusNuevo = "En Calibración", UsuarioNombre = tec.Nombre, Fecha = DateTime.UtcNow, Observaciones = "Pruebas de precisión GNSS en curso." });

        var os3 = new OrdenServicio
        {
            Folio = $"OS-{anio}-000003", Anio = anio, Consecutivo = 3,
            ClienteId = clientes[0].ClienteId, EquipoId = equipos[1].EquipoId,
            TipoServicio = "Diagnóstico", Estatus = "En Diagnóstico",
            TecnicoId = tec.UsuarioId, SupervisorId = sup.UsuarioId,
            FechaRecepcion = DateTime.UtcNow, FechaCompromiso = hoy.AddDays(2),
            MetodoIdentificacion = "NumeroInterno",
            AccesoriosChecklistJson = "[\"Batería\",\"Cargador\",\"Estuche\"]",
            CondicionChecklistJson = "[\"Rayones\",\"Teclado dañado\"]",
            FallaReportada = "Teclas de función F1 y F2 intermitentes.",
            CostoManoObra = 1200m, SubtotalRefacciones = 8700m, Total = 9900m
        };
        os3.Refacciones.Add(new OrdenServicioRefaccion
        {
            ProductoId = prods[2].ProductoId, SKU = prods[2].SKU, NombreProducto = prods[2].Nombre,
            Cantidad = 2, PrecioUnitario = prods[2].PrecioVenta, Importe = prods[2].PrecioVenta * 2, Descontado = false
        });
        os3.HistorialEstatus.Add(new OrdenServicioEstatusHistorial { EstatusNuevo = "En Diagnóstico", UsuarioNombre = tec.Nombre, Fecha = DateTime.UtcNow, Observaciones = "Equipo recibido hoy para diagnóstico de teclado." });

        db.OrdenesServicio.AddRange(os1, os2, os3);

        db.Mantenimientos.Add(new Mantenimiento
        {
            Folio = $"MTO-{anio}-000001", EquipoId = equipos[0].EquipoId,
            Tipo = "Mantenimiento Preventivo", Estatus = "Programado",
            FechaProgramada = hoy.AddDays(15), ProximoMantenimiento = hoy.AddMonths(6),
            TecnicoId = tec.UsuarioId, Descripcion = "Limpieza óptica semestral y lubricación de tornillos tangenciales.", Costo = 2200m
        });

        db.Garantias.Add(new Garantia
        {
            Folio = $"GAR-{anio}-000001", EquipoId = equipos[0].EquipoId, ClienteId = clientes[0].ClienteId,
            TipoGarantia = "Servicio / Reparación", FechaInicio = hoy.AddMonths(-1), FechaFin = hoy.AddMonths(5),
            Cobertura = "Garantía sobre pantalla LCD Touch Leica TS16 y mano de obra de calibración.", Estatus = "Vigente"
        });

        var vta1 = new VentaDocumento
        {
            Folio = $"VTA-{anio}-000001", TipoDocumento = "Factura", Estatus = "Facturada",
            ClienteId = clientes[0].ClienteId, Fecha = DateTime.UtcNow.AddDays(-2),
            Subtotal = 13650m, Descuento = 0m, IVA = 2184m, Total = 15834m,
            FolioFacturaCFDI = "550E8400-E29B-41D4-A716-446655440000", UsoCFDI = "G03 - Gastos en general",
            MetodoPago = "PUE", FormaPago = "03 - Transferencia electrónica",
            DescuentoAutomaticoInventario = true, SalidaInventarioRegistrada = true, VendedorNombre = "Administrador"
        };
        vta1.Partidas.Add(new VentaPartida { ProductoId = prods[5].ProductoId, Descripcion = prods[5].Nombre, Cantidad = 2, PrecioUnitario = prods[5].PrecioVenta, Importe = prods[5].PrecioVenta * 2 });
        vta1.Partidas.Add(new VentaPartida { ProductoId = prods[6].ProductoId, Descripcion = prods[6].Nombre, Cantidad = 1, PrecioUnitario = prods[6].PrecioVenta, Importe = prods[6].PrecioVenta });

        var vta2 = new VentaDocumento
        {
            Folio = $"VTA-{anio}-000002", TipoDocumento = "Venta", Estatus = "Entregada",
            ClienteId = clientes[1].ClienteId, Fecha = DateTime.UtcNow.AddDays(-1),
            Subtotal = 8800m, Descuento = 0m, IVA = 1408m, Total = 10208m,
            DescuentoAutomaticoInventario = false, SalidaInventarioRegistrada = false, VendedorNombre = "Administrador",
            Observaciones = "Pendiente confirmar salida física de almacén."
        };
        vta2.Partidas.Add(new VentaPartida { ProductoId = prods[7].ProductoId, Descripcion = prods[7].Nombre, Cantidad = 2, PrecioUnitario = prods[7].PrecioVenta, Importe = prods[7].PrecioVenta * 2 });

        db.Ventas.AddRange(vta1, vta2);

        db.MovimientosInventario.AddRange(
            new MovimientoInventario
            {
                ProductoId = prods[0].ProductoId, TipoMovimiento = "Entrada", Subtipo = "Compra",
                Concepto = "Abastecimiento inicial de refacciones Leica", ReferenciaFolio = $"COM-{anio}-000001",
                Cantidad = 6, StockAnterior = 0, StockPosterior = 6, CostoUnitario = prods[0].CostoUnitario,
                UbicacionDestino = prods[0].CodigoUbicacion, UsuarioNombre = "Administrador", Fecha = DateTime.UtcNow.AddDays(-5)
            },
            new MovimientoInventario
            {
                ProductoId = prods[5].ProductoId, TipoMovimiento = "Salida", Subtipo = "Venta",
                Concepto = $"Salida por Factura VTA-{anio}-000001", ReferenciaFolio = $"VTA-{anio}-000001",
                Cantidad = 2, StockAnterior = 14, StockPosterior = 12, CostoUnitario = prods[5].CostoUnitario,
                UbicacionOrigen = prods[5].CodigoUbicacion, UsuarioNombre = "Administrador", Fecha = DateTime.UtcNow.AddDays(-2)
            }
        );

        db.FolioSecuenciasPrefijo.AddRange(
            new FolioSecuenciaPrefijo { Prefijo = "OS", Anio = anio, Ultimo = 3 },
            new FolioSecuenciaPrefijo { Prefijo = "MTO", Anio = anio, Ultimo = 1 },
            new FolioSecuenciaPrefijo { Prefijo = "GAR", Anio = anio, Ultimo = 1 },
            new FolioSecuenciaPrefijo { Prefijo = "VTA", Anio = anio, Ultimo = 2 }
        );

        await db.SaveChangesAsync();
    }
}
