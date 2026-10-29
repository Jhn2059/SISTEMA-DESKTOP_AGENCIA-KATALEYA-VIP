BEGIN;

INSERT INTO tipo_vehiculo (nombre, numero_asientos)
VALUES
    ('Auto', 4),
    ('Camioneta', 6),
    ('Minivan', 8),
    ('Minivan', 10),
    ('Combi', 15),
    ('Combi', 18),
    ('Combi', 20)
ON CONFLICT (nombre, numero_asientos) DO NOTHING;

INSERT INTO parametro (nombre, valor, usuario_id)
SELECT 'horarios_salida', '06:00,10:00,13:00,15:00,20:00', u.id
FROM usuario u WHERE u.nombre_usuario = 'admin'
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO parametro (nombre, valor, usuario_id)
SELECT 'plazo_maximo_reserva_dias', '7', u.id
FROM usuario u WHERE u.nombre_usuario = 'admin'
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO parametro (nombre, valor, usuario_id)
SELECT 'aviso_previo_horas', '3', u.id
FROM usuario u WHERE u.nombre_usuario = 'admin'
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO tarifa (tipo, nombre, importe, sentido, usuario_id)
SELECT 'Regular', 'Tarifa regular Huancayo - Ica', 120.00, 'Ida', u.id
FROM usuario u WHERE u.nombre_usuario = 'admin'
ON CONFLICT DO NOTHING;

INSERT INTO tarifa (tipo, nombre, importe, sentido, usuario_id)
SELECT 'Regular', 'Tarifa regular Ica - Huancayo', 120.00, 'Vuelta', u.id
FROM usuario u WHERE u.nombre_usuario = 'admin'
ON CONFLICT DO NOTHING;

INSERT INTO tarifa (tipo, nombre, importe, vigencia_desde, vigencia_hasta, sentido, usuario_id)
SELECT 'Programada', 'Fiestas Patrias', 150.00, DATE '2026-07-26', DATE '2026-07-31', 'Ambos', u.id
FROM usuario u WHERE u.nombre_usuario = 'admin'
AND NOT EXISTS (SELECT 1 FROM tarifa WHERE nombre = 'Fiestas Patrias');

INSERT INTO tarifa (tipo, nombre, importe, vigencia_desde, vigencia_hasta, sentido, usuario_id)
SELECT 'Programada', 'Navidad y Año Nuevo', 150.00, DATE '2026-12-22', DATE '2027-01-02', 'Ambos', u.id
FROM usuario u WHERE u.nombre_usuario = 'admin'
AND NOT EXISTS (SELECT 1 FROM tarifa WHERE nombre = 'Navidad y Año Nuevo');

INSERT INTO correlativo_boleto (serie, ultimo_numero, sede_id)
SELECT 'H001', 0, s.id FROM sede s WHERE s.nombre = 'Huancayo'
ON CONFLICT (serie) DO NOTHING;

INSERT INTO correlativo_boleto (serie, ultimo_numero, sede_id)
SELECT 'I001', 0, s.id FROM sede s WHERE s.nombre = 'Ica'
ON CONFLICT (serie) DO NOTHING;

COMMIT;
