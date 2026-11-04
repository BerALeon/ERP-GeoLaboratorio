-- Datos iniciales. El usuario administrador se crea al primer arranque de la API
-- (Database:AutoSeed) con contraseña hasheada con BCrypt; cámbiela de inmediato.
USE geolaboratorio;

INSERT IGNORE INTO ConfiguracionVigencias (TipoEquipo, Meses) VALUES
  ('EstacionTotal', 12), ('GPS', 12), ('GNSS', 12), ('NivelAutomatico', 12), ('NivelDigital', 12),
  ('DroneRTK', 12), ('EscanerLaser', 12), ('LiDAR', 12), ('Otro', 12);

-- Ejemplo: vigencia semestral para escáner láser y LiDAR
-- UPDATE ConfiguracionVigencias SET Meses = 6 WHERE TipoEquipo IN ('EscanerLaser','LiDAR');
