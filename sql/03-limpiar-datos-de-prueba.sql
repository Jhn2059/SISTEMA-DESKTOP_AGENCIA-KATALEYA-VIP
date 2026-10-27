BEGIN;

TRUNCATE TABLE registro_bitacora RESTART IDENTITY;
DELETE FROM usuario WHERE nombre_usuario <> 'admin';
SELECT setval(
    pg_get_serial_sequence('usuario', 'id'),
    COALESCE((SELECT max(id) FROM usuario), 0),
    true
);

COMMIT;
