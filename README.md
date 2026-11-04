# GEOLABORATORIO · ERP Especializado para Empresas de Topografía y Calibración

```
CertificacionesGeoLab/
├─ backend/      ASP.NET Core 8 · Clean Architecture
│  └─ src/
│     ├─ Geolaboratorio.Domain          Entidades ERP y enums (Cliente, Equipo, EquipoSerie, OrdenServicio, Certificado, Producto, Kardex, Venta, Compra, Mantenimiento, Garantia)
│     ├─ Geolaboratorio.Application     DTOs, puertos (interfaces), validadores FluentValidation
│     ├─ Geolaboratorio.Infrastructure  EF Core + MySQL (Pomelo), repositorios/UoW, servicios ERP, PDF (QuestPDF), QR (QRCoder), JWT
│     └─ Geolaboratorio.Api             Controllers, Swagger, auth, rate-limit, manejo de errores
├─ frontend/     React 19 + Vite + TypeScript · Ant Design · React Query · Axios · React Router
├─ database/     01_schema.sql (tablas ERP integradas + triggers de inalterabilidad), 02_seed.sql
└─ docs/
```

## Arquitectura Funcional Integrada (Entidad Principal: EQUIPO)

```text
CLIENTE
│
└── EQUIPOS (Vista 360° / Expediente Digital)
    ├── SERIES CONFIGURABLES (Principal, Radio Interna/Externa, Controladora, Antena, IMEI 1/2...)
    ├── CERTIFICADOS Y CALIBRACIONES (GEO-AAAA-000001)
    ├── CERTIFICACIONES TÉCNICAS
    ├── ÓRDENES DE SERVICIO (OS-AAAA-000001) + REPORTE TÉCNICO PDF
    ├── MANTENIMIENTOS PREVENTIVOS / CORRECTIVOS / REVISIONES / REPARACIONES (MTO-AAAA-000001)
    ├── GARANTÍAS (GAR-AAAA-000001)
    ├── VENTAS / COTIZACIONES / REMISIONES / FACTURACIÓN (COT/PED/VTA/REM/FAC)
    └── HISTORIAL TÉCNICO COMPLETO (Desde alta del equipo hasta su baja)
```

## Arranque rápido

1. **MySQL 8**: `mysql -u root -p < database/01_schema.sql` y `02_seed.sql`
   (en desarrollo: `Database:AutoSeed=true` crea o migra automáticamente las tablas y columnas ERP y siembra el usuario admin y catálogo base de inventario).
2. **Backend** (`backend/src/Geolaboratorio.Api`):
   - Ajuste `ConnectionStrings:Default` en `appsettings.Development.json`.
   - `dotnet run --urls http://localhost:5000` → Swagger en `http://localhost:5000/swagger`.
   - Admin inicial: `admin@geolaboratorio.com.mx` / `Admin#2026!` (**cámbiela**; configurable en `Seed:*`).
3. **Frontend** (`frontend`): `npm install && npm run dev` → `http://localhost:5173` (proxy `/api` → `:5000`).

## API ERP (Swagger completo en `/swagger`)

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/auth/login` | JWT |
| GET/POST/PUT | `/api/usuarios` | Administración de usuarios |
| GET/POST/PUT | `/api/clientes` | Clientes |
| GET/POST/PUT | `/api/equipos` | Equipos y series múltiples configurables |
| GET | `/api/equipos/buscar?codigo=` | Identificación por QR, código de barras, serie o no. interno |
| GET | `/api/equipos/{id}/expediente` | Vista 360° / Expediente Digital del equipo |
| POST | `/api/equipos/{id}/baja` | Baja definitiva con conservación de historial |
| GET/POST/PUT | `/api/ordenes-servicio` | Recepción (`OS-AAAA-000001`), checklists, fotos, estatus, taller |
| GET | `/api/ordenes-servicio/{id}/pdf` | Reporte Técnico en PDF con evidencia fotográfica y firmas |
| GET/POST/PUT | `/api/inventario/productos` | Inventario con ubicación física (`ALM01-PAS02-R03-ANB-N2`) |
| GET/POST | `/api/inventario/kardex`, `/movimientos` | Kardex completo de entradas y salidas |
| GET/POST | `/api/ventas` | Cotizaciones, Pedidos, Ventas, Remisiones, Facturación y sincronización con almacén |
| GET/POST | `/api/compras` | Compras a proveedores y entrada automática a Kardex |
| GET/POST/PUT | `/api/mantenimientos`, `/api/garantias` | Mantenimientos preventivos/correctivos, reparaciones, revisiones y garantías |
| GET/POST/PUT | `/api/certificados` | Certificados de calibración y certificaciones (`GEO-AAAA-000001`) |
| GET | `/api/dashboard`, `/api/auditoria` | Dashboard Ejecutivo ERP (Generales, Técnicos, Inventario) y bitácora |
| GET | `/api/public/verificar/{folio}` | **Público**, con rate-limit |
