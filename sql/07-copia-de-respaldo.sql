BEGIN;

CREATE TABLE IF NOT EXISTS copia_respaldo (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fecha_hora timestamptz NOT NULL DEFAULT now(),
    correcta boolean NOT NULL,
    nombre_archivo text
);

CREATE INDEX IF NOT EXISTS ix_copia_respaldo_fecha_hora
    ON copia_respaldo (fecha_hora DESC);

COMMIT;
