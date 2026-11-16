-- Gestión de pasajes (CDU-03 y CDU-04).
-- Ejecutar después de 07-grupos.sql.

ALTER TABLE pasaje
    ADD COLUMN IF NOT EXISTS motivo_anulacion varchar(200);

ALTER TABLE pasaje
    ADD COLUMN IF NOT EXISTS fecha_anulacion timestamptz;

CREATE INDEX IF NOT EXISTS ix_pasaje_fecha_anulacion
    ON pasaje (fecha_anulacion);

ALTER TABLE pago
    DROP CONSTRAINT IF EXISTS pago_tipo_check;

ALTER TABLE pago
    DROP CONSTRAINT IF EXISTS ck_pago_tipo;

ALTER TABLE pago
    ADD CONSTRAINT pago_tipo_check
    CHECK (tipo IN ('Adelanto', 'Saldo', 'PagoTotal', 'Adicional', 'Devolucion'));
