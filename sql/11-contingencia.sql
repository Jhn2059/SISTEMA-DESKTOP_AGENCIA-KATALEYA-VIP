-- Registro diferido (CDU-08).
-- Ejecutar después de 10-bitacora.sql.

ALTER TABLE pasaje
    ADD COLUMN IF NOT EXISTS es_diferido boolean NOT NULL DEFAULT false;
