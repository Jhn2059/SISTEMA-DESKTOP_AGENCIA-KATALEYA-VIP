BEGIN;

ALTER TABLE unidad_salida
    ADD COLUMN IF NOT EXISTS sentido varchar(8)
        CHECK (sentido IN ('Ida', 'Vuelta'));

-- Las unidades que ya tienen pasajes toman el sentido del origen de esos pasajes.
UPDATE unidad_salida u
SET sentido = CASE
        WHEN lower(p.origen) LIKE '%huancayo%' THEN 'Ida'
        ELSE 'Vuelta'
    END
FROM (
    SELECT DISTINCT ON (unidad_salida_id) unidad_salida_id, origen
    FROM pasaje
    WHERE unidad_salida_id IS NOT NULL
    ORDER BY unidad_salida_id, id
) p
WHERE p.unidad_salida_id = u.id
  AND u.sentido IS NULL
  AND u.grupo_id IS NULL;

-- NOT VALID: las unidades vacías que ya existían quedan sin sentido; las nuevas lo llevan siempre.
ALTER TABLE unidad_salida
    DROP CONSTRAINT IF EXISTS ck_unidad_salida_sentido;
ALTER TABLE unidad_salida
    ADD CONSTRAINT ck_unidad_salida_sentido
        CHECK (grupo_id IS NOT NULL OR sentido IS NOT NULL) NOT VALID;

ALTER TABLE unidad_salida
    DROP CONSTRAINT IF EXISTS unidad_salida_fecha_hora_salida_numero_key;
CREATE UNIQUE INDEX IF NOT EXISTS ux_unidad_salida_numero
    ON unidad_salida (fecha_hora_salida, sentido, numero);

COMMIT;
