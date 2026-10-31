BEGIN;

INSERT INTO tipo_vehiculo (nombre, cantidad_asientos)
VALUES
    ('Auto', 4),
    ('Camioneta', 6),
    ('Minivan', 8),
    ('Minivan', 10),
    ('Combi', 15),
    ('Combi', 18),
    ('Combi', 20)
ON CONFLICT (nombre, cantidad_asientos) DO NOTHING;

INSERT INTO parametro_negocio (nombre, valor, usuario_id)
SELECT 'horarios_salida', '06:00,10:00,13:00,15:00,20:00', u.id
FROM usuario u
WHERE u.nombre_usuario = 'admin'
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO parametro_negocio (nombre, valor, usuario_id)
SELECT 'plazo_maximo_reserva_dias', '7', u.id
FROM usuario u
WHERE u.nombre_usuario = 'admin'
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO parametro_negocio (nombre, valor, usuario_id)
SELECT 'aviso_previo_horas', '3', u.id
FROM usuario u
WHERE u.nombre_usuario = 'admin'
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO tarifa (tipo, nombre, importe, usuario_id, sentido)
SELECT 'Regular', NULL, 120.00, u.id, 'HuancayoIca'
FROM usuario u
WHERE u.nombre_usuario = 'admin'
  AND NOT EXISTS (
      SELECT 1 FROM tarifa
      WHERE tipo = 'Regular' AND sentido = 'HuancayoIca'
  );

INSERT INTO tarifa (tipo, nombre, importe, usuario_id, sentido)
SELECT 'Regular', NULL, 120.00, u.id, 'IcaHuancayo'
FROM usuario u
WHERE u.nombre_usuario = 'admin'
  AND NOT EXISTS (
      SELECT 1 FROM tarifa
      WHERE tipo = 'Regular' AND sentido = 'IcaHuancayo'
  );

COMMIT;
