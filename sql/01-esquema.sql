CREATE TABLE IF NOT EXISTS rol (
    id smallint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre varchar(30) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS sede (
    id smallint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre varchar(30) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS usuario (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre varchar(100) NOT NULL,
    nombre_usuario varchar(30) NOT NULL UNIQUE,
    resumen_contrasena text NOT NULL,
    sal text NOT NULL,
    rol_id smallint NOT NULL REFERENCES rol (id),
    sede_id smallint NOT NULL REFERENCES sede (id),
    estado varchar(12) NOT NULL DEFAULT 'Activa'
        CHECK (estado IN ('Activa', 'Bloqueada', 'Desactivada')),
    intentos_fallidos smallint NOT NULL DEFAULT 0
        CHECK (intentos_fallidos >= 0),
    fin_bloqueo timestamptz
);

CREATE TABLE IF NOT EXISTS registro_bitacora (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario_id integer NOT NULL REFERENCES usuario (id),
    fecha_hora timestamptz NOT NULL DEFAULT now(),
    operacion varchar(40) NOT NULL,
    pasaje_id bigint,
    valor_anterior jsonb,
    valor_nuevo jsonb,
    clave_de_operacion uuid UNIQUE
);

CREATE INDEX IF NOT EXISTS ix_registro_bitacora_fecha_hora
    ON registro_bitacora (fecha_hora DESC);

CREATE INDEX IF NOT EXISTS ix_registro_bitacora_usuario
    ON registro_bitacora (usuario_id);
