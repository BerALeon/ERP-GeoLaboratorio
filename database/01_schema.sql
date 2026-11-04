-- =============================================================
-- GEOLABORATORIO ERP · Esquema Integrado MySQL 8.0
-- Ejecutar con: mysql -u root -p < 01_schema.sql
-- Nota: los nombres de columna coinciden con el modelo EF Core.
-- =============================================================
CREATE DATABASE IF NOT EXISTS geolaboratorio CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE geolaboratorio;

CREATE TABLE IF NOT EXISTS Usuarios (
  UsuarioId     INT AUTO_INCREMENT PRIMARY KEY,
  Nombre        VARCHAR(150) NOT NULL,
  Correo        VARCHAR(150) NOT NULL,
  PasswordHash  VARCHAR(200) NOT NULL,
  Rol           VARCHAR(20)  NOT NULL,            -- Administrador | Tecnico | Consulta
  Activo        TINYINT(1)   NOT NULL DEFAULT 1,
  FirmaUrl      VARCHAR(500) NULL,
  CreadoEn      DATETIME(6)  NOT NULL,
  ModificadoEn  DATETIME(6)  NULL,
  SucursalId    INT          NOT NULL DEFAULT 1,
  UNIQUE KEY UX_Usuarios_Correo (Correo)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Clientes (
  ClienteId       INT AUTO_INCREMENT PRIMARY KEY,
  RazonSocial     VARCHAR(250) NOT NULL,
  NombreComercial VARCHAR(250) NULL,
  RFC             VARCHAR(13)  NOT NULL,
  Direccion       VARCHAR(500) NULL,
  Ciudad          VARCHAR(100) NULL,
  Estado          VARCHAR(100) NULL,
  Pais            VARCHAR(100) NULL,
  Telefono        VARCHAR(30)  NULL,
  Correo          VARCHAR(150) NULL,
  Contacto        VARCHAR(150) NULL,
  Activo          TINYINT(1)   NOT NULL DEFAULT 1,
  CreadoEn        DATETIME(6)  NOT NULL,
  ModificadoEn    DATETIME(6)  NULL,
  SucursalId      INT          NOT NULL DEFAULT 1,
  UNIQUE KEY UX_Clientes_RFC (RFC),
  KEY IX_Clientes_RazonSocial (RazonSocial)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Equipos (
  EquipoId        INT AUTO_INCREMENT PRIMARY KEY,
  ClienteId       INT NOT NULL,
  TipoEquipo      VARCHAR(30)  NOT NULL,
  Marca           VARCHAR(100) NOT NULL,
  Modelo          VARCHAR(100) NOT NULL,
  SeriePrincipal  VARCHAR(100) NOT NULL,
  SerieSecundaria VARCHAR(100) NULL,
  IMEI1           VARCHAR(20)  NULL,
  IMEI2           VARCHAR(20)  NULL,
  NumeroInterno   VARCHAR(50)  NULL,
  CodigoQR        VARCHAR(120) NULL,
  CodigoBarras    VARCHAR(120) NULL,
  FechaCompra     DATETIME(6)  NULL,
  Observaciones   VARCHAR(1000) NULL,
  Estatus         VARCHAR(30)  NOT NULL DEFAULT 'Activo',
  FechaBaja       DATETIME(6)  NULL,
  MotivoBaja      VARCHAR(500) NULL,
  CreadoEn        DATETIME(6)  NOT NULL,
  ModificadoEn    DATETIME(6)  NULL,
  SucursalId      INT          NOT NULL DEFAULT 1,
  UNIQUE KEY UX_Equipos_SeriePrincipal (SeriePrincipal),
  KEY IX_Equipos_ClienteId (ClienteId),
  CONSTRAINT FK_Equipos_Clientes FOREIGN KEY (ClienteId) REFERENCES Clientes (ClienteId) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS EquipoSeries (
  EquipoSerieId INT AUTO_INCREMENT PRIMARY KEY,
  EquipoId      INT NOT NULL,
  TipoSerie     VARCHAR(80) NOT NULL,
  Valor         VARCHAR(120) NOT NULL,
  Observaciones VARCHAR(300) NULL,
  KEY IX_EquipoSeries_Valor (Valor),
  CONSTRAINT FK_EquipoSeries_Equipos FOREIGN KEY (EquipoId) REFERENCES Equipos (EquipoId) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS EquipoHistorial (
  EquipoHistorialId BIGINT AUTO_INCREMENT PRIMARY KEY,
  EquipoId          INT NOT NULL,
  TipoEvento        VARCHAR(50) NOT NULL,
  Titulo            VARCHAR(200) NOT NULL,
  Detalle           VARCHAR(2000) NULL,
  FolioReferencia   VARCHAR(40) NULL,
  ReferenciaId      INT NULL,
  UsuarioId         INT NULL,
  UsuarioNombre     VARCHAR(150) NULL,
  Fecha             DATETIME(6) NOT NULL,
  KEY IX_EquipoHistorial_Equipo (EquipoId),
  CONSTRAINT FK_EquipoHistorial_Equipos FOREIGN KEY (EquipoId) REFERENCES Equipos (EquipoId) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS EquipoArchivos (
  EquipoArchivoId INT AUTO_INCREMENT PRIMARY KEY,
  EquipoId        INT NOT NULL,
  OrdenServicioId INT NULL,
  TipoArchivo     VARCHAR(40) NOT NULL,
  Categoria       VARCHAR(80) NOT NULL,
  NombreArchivo   VARCHAR(250) NOT NULL,
  ArchivoUrl      LONGTEXT NOT NULL,
  Descripcion     VARCHAR(500) NULL,
  SubidoPor       VARCHAR(150) NULL,
  Fecha           DATETIME(6) NOT NULL,
  KEY IX_EquipoArchivos_Equipo (EquipoId),
  CONSTRAINT FK_EquipoArchivos_Equipos FOREIGN KEY (EquipoId) REFERENCES Equipos (EquipoId) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS OrdenesServicio (
  OrdenServicioId          INT AUTO_INCREMENT PRIMARY KEY,
  Folio                    VARCHAR(30) NOT NULL,           -- OS-AAAA-000001
  Anio                     INT NOT NULL,
  Consecutivo              INT NOT NULL,
  ClienteId                INT NOT NULL,
  EquipoId                 INT NOT NULL,
  TipoServicio             VARCHAR(80) NOT NULL,
  TiposServicioAdicionales VARCHAR(500) NULL,
  Estatus                  VARCHAR(50) NOT NULL DEFAULT 'Recibido',
  TecnicoId                INT NULL,
  SupervisorId             INT NULL,
  FechaRecepcion           DATETIME(6) NOT NULL,
  FechaCompromiso          DATETIME(6) NULL,
  FechaTermino             DATETIME(6) NULL,
  FechaEntrega             DATETIME(6) NULL,
  MetodoIdentificacion     VARCHAR(40) NULL,
  AccesoriosChecklistJson  TEXT NULL,
  AccesoriosPersonalizados VARCHAR(1000) NULL,
  CondicionChecklistJson   TEXT NULL,
  FallaReportada           TEXT NULL,
  ObservacionesRecepcion   TEXT NULL,
  Diagnostico              TEXT NULL,
  ActividadesRealizadas    TEXT NULL,
  ObservacionesTecnicas    TEXT NULL,
  CostoManoObra            DECIMAL(14,2) NOT NULL DEFAULT 0,
  SubtotalRefacciones      DECIMAL(14,2) NOT NULL DEFAULT 0,
  Total                    DECIMAL(14,2) NOT NULL DEFAULT 0,
  RefaccionesDescontadas   TINYINT(1) NOT NULL DEFAULT 0,
  FirmaTecnico             LONGTEXT NULL,
  FirmaSupervisor          LONGTEXT NULL,
  FirmaCliente             LONGTEXT NULL,
  NombreClienteFirma       VARCHAR(150) NULL,
  ReportePdfUrl            VARCHAR(500) NULL,
  CreadoEn                 DATETIME(6) NOT NULL,
  ModificadoEn             DATETIME(6) NULL,
  SucursalId               INT NOT NULL DEFAULT 1,
  UNIQUE KEY UX_OS_Folio (Folio),
  UNIQUE KEY UX_OS_AnioConsecutivo (Anio, Consecutivo),
  KEY IX_OS_Equipo (EquipoId),
  KEY IX_OS_Cliente (ClienteId),
  CONSTRAINT FK_OS_Clientes FOREIGN KEY (ClienteId) REFERENCES Clientes (ClienteId) ON DELETE RESTRICT,
  CONSTRAINT FK_OS_Equipos  FOREIGN KEY (EquipoId)  REFERENCES Equipos (EquipoId)   ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS OrdenServicioEstatusHistorial (
  HistorialEstatusId BIGINT AUTO_INCREMENT PRIMARY KEY,
  OrdenServicioId    INT NOT NULL,
  EstatusAnterior    VARCHAR(50) NULL,
  EstatusNuevo       VARCHAR(50) NOT NULL,
  Fecha              DATETIME(6) NOT NULL,
  UsuarioId          INT NULL,
  UsuarioNombre      VARCHAR(150) NULL,
  Observaciones      VARCHAR(1000) NULL,
  KEY IX_OSHist_OS (OrdenServicioId),
  CONSTRAINT FK_OSHist_OS FOREIGN KEY (OrdenServicioId) REFERENCES OrdenesServicio (OrdenServicioId) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Productos (
  ProductoId       INT AUTO_INCREMENT PRIMARY KEY,
  SKU              VARCHAR(60) NOT NULL,
  CodigoBarras     VARCHAR(100) NULL,
  Nombre           VARCHAR(200) NOT NULL,
  Descripcion      VARCHAR(1000) NULL,
  GrupoCategoria   VARCHAR(60) NOT NULL,
  Categoria        VARCHAR(80) NOT NULL,
  Marca            VARCHAR(100) NULL,
  ModeloCompatible VARCHAR(150) NULL,
  Unidad           VARCHAR(20) NOT NULL DEFAULT 'PZA',
  CostoUnitario    DECIMAL(14,2) NOT NULL DEFAULT 0,
  PrecioVenta      DECIMAL(14,2) NOT NULL DEFAULT 0,
  StockActual      DECIMAL(12,2) NOT NULL DEFAULT 0,
  StockMinimo      DECIMAL(12,2) NOT NULL DEFAULT 0,
  StockMaximo      DECIMAL(12,2) NOT NULL DEFAULT 100,
  Almacen          VARCHAR(30) NOT NULL DEFAULT 'ALM01',
  Pasillo          VARCHAR(30) NOT NULL DEFAULT 'PAS01',
  Rack             VARCHAR(30) NOT NULL DEFAULT 'R01',
  Anaquel          VARCHAR(30) NOT NULL DEFAULT 'ANA',
  Nivel            VARCHAR(30) NOT NULL DEFAULT 'N1',
  Posicion         VARCHAR(30) NOT NULL DEFAULT 'P1',
  CodigoUbicacion  VARCHAR(120) NOT NULL DEFAULT 'ALM01-PAS01-R01-ANA-N1',
  Activo           TINYINT(1) NOT NULL DEFAULT 1,
  CreadoEn         DATETIME(6) NOT NULL,
  ModificadoEn     DATETIME(6) NULL,
  SucursalId       INT NOT NULL DEFAULT 1,
  UNIQUE KEY UX_Productos_SKU (SKU)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS OrdenServicioRefacciones (
  OrdenServicioRefaccionId INT AUTO_INCREMENT PRIMARY KEY,
  OrdenServicioId          INT NOT NULL,
  ProductoId               INT NOT NULL,
  NombreProducto           VARCHAR(200) NOT NULL,
  SKU                      VARCHAR(60) NULL,
  Cantidad                 DECIMAL(12,2) NOT NULL,
  PrecioUnitario           DECIMAL(14,2) NOT NULL,
  Importe                  DECIMAL(14,2) NOT NULL,
  Descontado               TINYINT(1) NOT NULL DEFAULT 0,
  KEY IX_OSRef_OS (OrdenServicioId),
  CONSTRAINT FK_OSRef_OS   FOREIGN KEY (OrdenServicioId) REFERENCES OrdenesServicio (OrdenServicioId) ON DELETE CASCADE,
  CONSTRAINT FK_OSRef_Prod FOREIGN KEY (ProductoId)      REFERENCES Productos (ProductoId)            ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS OrdenServicioFotos (
  OrdenServicioFotoId INT AUTO_INCREMENT PRIMARY KEY,
  OrdenServicioId     INT NOT NULL,
  Categoria           VARCHAR(80) NOT NULL,
  Descripcion         VARCHAR(500) NULL,
  ImagenUrl           LONGTEXT NOT NULL,
  UsuarioNombre       VARCHAR(150) NULL,
  Fecha               DATETIME(6) NOT NULL,
  KEY IX_OSFoto_OS (OrdenServicioId),
  CONSTRAINT FK_OSFoto_OS FOREIGN KEY (OrdenServicioId) REFERENCES OrdenesServicio (OrdenServicioId) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Certificados (
  CertificadoId     INT AUTO_INCREMENT PRIMARY KEY,
  Folio             VARCHAR(30) NOT NULL,           -- GEO-AAAA-000001
  Anio              INT NOT NULL,
  Consecutivo       INT NOT NULL,
  EquipoId          INT NOT NULL,
  OrdenServicioId   INT NULL,
  TipoRegistro      VARCHAR(30) NOT NULL DEFAULT 'Calibración',
  TecnicoId         INT NOT NULL,
  FechaCalibracion  DATETIME(6) NOT NULL,
  FechaEmision      DATETIME(6) NOT NULL,
  FechaVencimiento  DATETIME(6) NOT NULL,
  VigenciaMeses     INT NOT NULL,
  Resultado         VARCHAR(20) NOT NULL,           -- Aceptado | Condicionado | Rechazado
  Resultados        TEXT NULL,
  Observaciones     TEXT NULL,
  Conclusiones      TEXT NULL,
  PDFUrl            VARCHAR(500) NULL,
  QRUrl             VARCHAR(500) NULL,
  Estatus           VARCHAR(20) NOT NULL DEFAULT 'Vigente',   -- Vigente | Cancelado
  MotivoCancelacion VARCHAR(500) NULL,
  CanceladoEn       DATETIME(6) NULL,
  PdfHash           VARCHAR(64) NULL,
  Version           INT NOT NULL DEFAULT 1,
  CreadoEn          DATETIME(6) NOT NULL,
  ModificadoEn      DATETIME(6) NULL,
  SucursalId        INT NOT NULL DEFAULT 1,
  UNIQUE KEY UX_Certificados_Folio (Folio),
  UNIQUE KEY UX_Certificados_AnioConsecutivo (Anio, Consecutivo),
  KEY IX_Certificados_EquipoId (EquipoId),
  KEY IX_Certificados_FechaVencimiento (FechaVencimiento),
  CONSTRAINT FK_Certificados_Equipos  FOREIGN KEY (EquipoId)  REFERENCES Equipos (EquipoId)   ON DELETE RESTRICT,
  CONSTRAINT FK_Certificados_Usuarios FOREIGN KEY (TecnicoId) REFERENCES Usuarios (UsuarioId) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CertificadoHistorial (
  HistorialId   BIGINT AUTO_INCREMENT PRIMARY KEY,
  CertificadoId INT NOT NULL,
  Accion        VARCHAR(30) NOT NULL,
  Detalle       VARCHAR(1000) NULL,
  PdfHash       VARCHAR(64) NULL,
  UsuarioId     INT NULL,
  UsuarioNombre VARCHAR(150) NULL,
  Fecha         DATETIME(6) NOT NULL,
  KEY IX_CertHist_Cert (CertificadoId),
  CONSTRAINT FK_CertHist_Cert FOREIGN KEY (CertificadoId) REFERENCES Certificados (CertificadoId) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS MovimientosInventario (
  MovimientoId     BIGINT AUTO_INCREMENT PRIMARY KEY,
  ProductoId       INT NOT NULL,
  TipoMovimiento   VARCHAR(20) NOT NULL,
  Subtipo          VARCHAR(50) NOT NULL,
  Concepto         VARCHAR(250) NOT NULL,
  ReferenciaFolio  VARCHAR(50) NULL,
  OrdenServicioId  INT NULL,
  VentaId          INT NULL,
  CompraId         INT NULL,
  Cantidad         DECIMAL(12,2) NOT NULL,
  StockAnterior    DECIMAL(12,2) NOT NULL,
  StockPosterior   DECIMAL(12,2) NOT NULL,
  CostoUnitario    DECIMAL(14,2) NOT NULL,
  UbicacionOrigen  VARCHAR(120) NULL,
  UbicacionDestino VARCHAR(120) NULL,
  Observaciones    VARCHAR(1000) NULL,
  UsuarioId        INT NULL,
  UsuarioNombre    VARCHAR(150) NULL,
  Fecha            DATETIME(6) NOT NULL,
  KEY IX_MovInv_Prod (ProductoId),
  KEY IX_MovInv_Fecha (Fecha),
  CONSTRAINT FK_MovInv_Prod FOREIGN KEY (ProductoId) REFERENCES Productos (ProductoId) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Mantenimientos (
  MantenimientoId      INT AUTO_INCREMENT PRIMARY KEY,
  Folio                VARCHAR(30) NOT NULL,
  EquipoId             INT NOT NULL,
  OrdenServicioId      INT NULL,
  Tipo                 VARCHAR(60) NOT NULL,
  Estatus              VARCHAR(40) NOT NULL,
  FechaProgramada      DATETIME(6) NOT NULL,
  FechaRealizada       DATETIME(6) NULL,
  ProximoMantenimiento DATETIME(6) NULL,
  TecnicoId            INT NULL,
  Descripcion          VARCHAR(1000) NULL,
  Actividades          TEXT NULL,
  Hallazgos            TEXT NULL,
  Recomendaciones      TEXT NULL,
  Costo                DECIMAL(14,2) NOT NULL DEFAULT 0,
  CreadoEn             DATETIME(6) NOT NULL,
  ModificadoEn         DATETIME(6) NULL,
  SucursalId           INT NOT NULL DEFAULT 1,
  UNIQUE KEY UX_Mto_Folio (Folio)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Garantias (
  GarantiaId        INT AUTO_INCREMENT PRIMARY KEY,
  Folio             VARCHAR(30) NOT NULL,
  EquipoId          INT NOT NULL,
  ClienteId         INT NOT NULL,
  OrdenServicioId   INT NULL,
  VentaId           INT NULL,
  TipoGarantia      VARCHAR(60) NOT NULL,
  FechaInicio       DATETIME(6) NOT NULL,
  FechaFin          DATETIME(6) NOT NULL,
  Cobertura         VARCHAR(1000) NOT NULL,
  Condiciones       VARCHAR(1000) NULL,
  Estatus           VARCHAR(40) NOT NULL,
  MotivoReclamacion VARCHAR(1000) NULL,
  Resolucion        VARCHAR(1000) NULL,
  CreadoEn          DATETIME(6) NOT NULL,
  ModificadoEn      DATETIME(6) NULL,
  SucursalId        INT NOT NULL DEFAULT 1,
  UNIQUE KEY UX_Gar_Folio (Folio)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Ventas (
  VentaId                       INT AUTO_INCREMENT PRIMARY KEY,
  Folio                         VARCHAR(30) NOT NULL,
  TipoDocumento                 VARCHAR(40) NOT NULL,
  Estatus                       VARCHAR(40) NOT NULL,
  ClienteId                     INT NOT NULL,
  OrdenServicioId               INT NULL,
  DocumentoOrigenId             INT NULL,
  Fecha                         DATETIME(6) NOT NULL,
  FechaVigencia                 DATETIME(6) NULL,
  Subtotal                      DECIMAL(14,2) NOT NULL DEFAULT 0,
  Descuento                     DECIMAL(14,2) NOT NULL DEFAULT 0,
  IVA                           DECIMAL(14,2) NOT NULL DEFAULT 0,
  Total                         DECIMAL(14,2) NOT NULL DEFAULT 0,
  FolioFacturaCFDI              VARCHAR(80) NULL,
  UsoCFDI                       VARCHAR(40) NULL,
  MetodoPago                    VARCHAR(40) NULL,
  FormaPago                     VARCHAR(40) NULL,
  DescuentoAutomaticoInventario TINYINT(1) NOT NULL DEFAULT 1,
  SalidaInventarioRegistrada    TINYINT(1) NOT NULL DEFAULT 0,
  Observaciones                 VARCHAR(1000) NULL,
  VendedorId                    INT NULL,
  VendedorNombre                VARCHAR(150) NULL,
  CreadoEn                      DATETIME(6) NOT NULL,
  ModificadoEn                  DATETIME(6) NULL,
  SucursalId                    INT NOT NULL DEFAULT 1,
  UNIQUE KEY UX_Ventas_Folio (Folio)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS VentaPartidas (
  VentaPartidaId INT AUTO_INCREMENT PRIMARY KEY,
  VentaId        INT NOT NULL,
  ProductoId     INT NULL,
  EquipoId       INT NULL,
  Descripcion    VARCHAR(300) NOT NULL,
  NumeroSerie    VARCHAR(100) NULL,
  Cantidad       DECIMAL(12,2) NOT NULL,
  PrecioUnitario DECIMAL(14,2) NOT NULL,
  Descuento      DECIMAL(14,2) NOT NULL DEFAULT 0,
  Importe        DECIMAL(14,2) NOT NULL,
  KEY IX_VentaPartidas_Venta (VentaId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Compras (
  CompraId                    INT AUTO_INCREMENT PRIMARY KEY,
  Folio                       VARCHAR(30) NOT NULL,
  Proveedor                   VARCHAR(200) NOT NULL,
  RFCProveedor                VARCHAR(20) NULL,
  FacturaProveedor            VARCHAR(80) NULL,
  FechaCompra                 DATETIME(6) NOT NULL,
  FechaRecepcion              DATETIME(6) NULL,
  Estatus                     VARCHAR(40) NOT NULL,
  Subtotal                    DECIMAL(14,2) NOT NULL DEFAULT 0,
  IVA                         DECIMAL(14,2) NOT NULL DEFAULT 0,
  Total                       DECIMAL(14,2) NOT NULL DEFAULT 0,
  EntradaInventarioRegistrada TINYINT(1) NOT NULL DEFAULT 0,
  Observaciones               VARCHAR(1000) NULL,
  UsuarioNombre               VARCHAR(150) NULL,
  CreadoEn                    DATETIME(6) NOT NULL,
  ModificadoEn                DATETIME(6) NULL,
  SucursalId                  INT NOT NULL DEFAULT 1,
  UNIQUE KEY UX_Compras_Folio (Folio)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CompraPartidas (
  CompraPartidaId INT AUTO_INCREMENT PRIMARY KEY,
  CompraId        INT NOT NULL,
  ProductoId      INT NOT NULL,
  Descripcion     VARCHAR(300) NOT NULL,
  Cantidad        DECIMAL(12,2) NOT NULL,
  CostoUnitario   DECIMAL(14,2) NOT NULL,
  Importe         DECIMAL(14,2) NOT NULL,
  KEY IX_CompraPartidas_Compra (CompraId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS AuditLog (
  AuditLogId        BIGINT AUTO_INCREMENT PRIMARY KEY,
  Entidad           VARCHAR(50) NOT NULL,
  EntidadId         VARCHAR(50) NOT NULL,
  Accion            VARCHAR(10) NOT NULL,
  ValoresAnteriores JSON NULL,
  ValoresNuevos     JSON NULL,
  UsuarioId         INT NULL,
  UsuarioNombre     VARCHAR(150) NULL,
  Ip                VARCHAR(45) NULL,
  Fecha             DATETIME(6) NOT NULL,
  KEY IX_Audit_Entidad (Entidad, EntidadId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS FolioSecuencias (
  Anio   INT PRIMARY KEY,
  Ultimo INT NOT NULL DEFAULT 0
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS FolioSecuenciasPrefijo (
  Prefijo VARCHAR(10) NOT NULL,
  Anio    INT NOT NULL,
  Ultimo  INT NOT NULL DEFAULT 0,
  PRIMARY KEY (Prefijo, Anio)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS ConfiguracionVigencias (
  TipoEquipo VARCHAR(30) PRIMARY KEY,
  Meses      INT NOT NULL DEFAULT 12,
  CONSTRAINT CK_Vigencia CHECK (Meses IN (6, 12))
) ENGINE=InnoDB;

-- =============================================================
-- INALTERABILIDAD (defensa en profundidad a nivel de base de datos)
-- =============================================================
DELIMITER $$

CREATE TRIGGER TR_Certificados_NoDelete BEFORE DELETE ON Certificados
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Los certificados no pueden eliminarse; cancélelos.';
END$$

CREATE TRIGGER TR_Certificados_FolioInmutable BEFORE UPDATE ON Certificados
FOR EACH ROW BEGIN
  IF NEW.Folio <> OLD.Folio OR NEW.Anio <> OLD.Anio OR NEW.Consecutivo <> OLD.Consecutivo THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'El folio del certificado es inalterable.';
  END IF;
END$$

CREATE TRIGGER TR_AuditLog_NoUpdate BEFORE UPDATE ON AuditLog
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'La bitácora de auditoría es inalterable.';
END$$

CREATE TRIGGER TR_AuditLog_NoDelete BEFORE DELETE ON AuditLog
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'La bitácora de auditoría es inalterable.';
END$$

CREATE TRIGGER TR_CertHist_NoUpdate BEFORE UPDATE ON CertificadoHistorial
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'El historial es inalterable.';
END$$

CREATE TRIGGER TR_CertHist_NoDelete BEFORE DELETE ON CertificadoHistorial
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'El historial es inalterable.';
END$$

DELIMITER ;
