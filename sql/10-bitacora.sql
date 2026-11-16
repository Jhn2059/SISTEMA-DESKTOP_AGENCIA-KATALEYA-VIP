-- Consulta de bitácora (CDU-14).
-- Ejecutar después de 09-cuadre.sql.

CREATE INDEX IF NOT EXISTS ix_registro_bitacora_pasaje_id
    ON registro_bitacora (pasaje_id);

CREATE INDEX IF NOT EXISTS ix_registro_bitacora_usuario_fecha
    ON registro_bitacora (usuario_id, fecha_hora DESC);
