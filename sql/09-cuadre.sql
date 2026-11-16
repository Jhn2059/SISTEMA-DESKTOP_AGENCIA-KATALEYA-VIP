-- Módulo de cuadre de ventas (CDU-07).
-- Ejecutar después de 08-gestion-pasajes.sql.

CREATE INDEX IF NOT EXISTS ix_pago_fecha_pago
    ON pago (fecha_pago);

CREATE INDEX IF NOT EXISTS ix_pasaje_emisor_estado
    ON pasaje (emisor_id, estado);
