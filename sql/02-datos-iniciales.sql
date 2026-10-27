INSERT INTO rol (nombre)
VALUES ('Administrador'), ('Encargado de ventas')
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO sede (nombre)
VALUES ('Huancayo'), ('Ica')
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO usuario
    (nombre, nombre_usuario, resumen_contrasena, sal, rol_id, sede_id, estado, intentos_fallidos)
SELECT
    'Administrador',
    'admin',
    '7eynzfNLl/B95f++Mk/9i+DjoWvTjj6aoSTRkE9QX38=',
    '23XniZ8foOo1kEOm/GjGKA==',
    r.id,
    s.id,
    'Activa',
    0
FROM rol r
CROSS JOIN sede s
WHERE r.nombre = 'Administrador'
  AND s.nombre = 'Huancayo'
ON CONFLICT (nombre_usuario) DO NOTHING;
