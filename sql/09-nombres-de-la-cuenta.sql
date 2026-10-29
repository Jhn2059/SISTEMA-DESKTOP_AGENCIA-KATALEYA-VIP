BEGIN;

-- Nombres revisados con Clean Code: NombrePersona y CantidadDeIntentosFallidos.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_name = 'usuario' AND column_name = 'nombre') THEN
        ALTER TABLE usuario RENAME COLUMN nombre TO nombre_persona;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_name = 'usuario' AND column_name = 'intentos_fallidos') THEN
        ALTER TABLE usuario RENAME COLUMN intentos_fallidos TO cantidad_intentos_fallidos;
    END IF;
END $$;

-- La cuenta principal es la de la administradora: no se puede desactivar (RF-28).
ALTER TABLE usuario
    ADD COLUMN IF NOT EXISTS es_cuenta_principal boolean NOT NULL DEFAULT false;

UPDATE usuario
SET es_cuenta_principal = true
WHERE id = (
        SELECT min(u.id)
        FROM usuario u
        INNER JOIN rol r ON r.id = u.rol_id
        WHERE r.nombre = 'Administrador'
    )
  AND NOT EXISTS (SELECT 1 FROM usuario WHERE es_cuenta_principal);

CREATE UNIQUE INDEX IF NOT EXISTS ux_usuario_cuenta_principal
    ON usuario (es_cuenta_principal)
    WHERE es_cuenta_principal;

COMMIT;
