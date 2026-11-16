-- Módulo de grupos (CDU-11).
-- Ejecutar después de 06-unidades-salidas-pasajes.sql.

CREATE SEQUENCE IF NOT EXISTS grupo_codigo_seq START WITH 1;

CREATE TABLE IF NOT EXISTS grupo (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    codigo varchar(20) NOT NULL UNIQUE,
    telefono_contacto varchar(12) NOT NULL,
    precio_acordado numeric(8, 2) NOT NULL CHECK (precio_acordado > 0),
    origen varchar(50) NOT NULL,
    destino varchar(50) NOT NULL,
    lugar_recojo varchar(50) NOT NULL,
    fecha_hora_salida timestamptz NOT NULL,
    fecha_registro timestamptz NOT NULL DEFAULT now(),
    creado_por integer NOT NULL REFERENCES usuario (id),
    creado_por_nombre varchar(100) NOT NULL,
    clave_de_operacion uuid UNIQUE
);

CREATE INDEX IF NOT EXISTS ix_grupo_fecha_hora_salida
    ON grupo (fecha_hora_salida);

ALTER TABLE unidad_salida
    ALTER COLUMN sentido DROP NOT NULL;

ALTER TABLE unidad_salida
    DROP CONSTRAINT IF EXISTS fk_unidad_salida_grupo;

ALTER TABLE unidad_salida
    ADD CONSTRAINT fk_unidad_salida_grupo
    FOREIGN KEY (grupo_id) REFERENCES grupo (id);

ALTER TABLE unidad_salida
    DROP CONSTRAINT IF EXISTS ck_unidad_salida_sentido;

ALTER TABLE unidad_salida
    ADD CONSTRAINT ck_unidad_salida_sentido
    CHECK (grupo_id IS NOT NULL OR sentido IS NOT NULL);

CREATE UNIQUE INDEX IF NOT EXISTS ux_unidad_salida_grupo_numero
    ON unidad_salida (grupo_id, numero)
    WHERE grupo_id IS NOT NULL;

ALTER TABLE pasaje
    ALTER COLUMN unidad_salida_id DROP NOT NULL;

ALTER TABLE pasaje
    ALTER COLUMN celular DROP NOT NULL;

ALTER TABLE pasaje
    ADD COLUMN IF NOT EXISTS es_responsable boolean NOT NULL DEFAULT false;

ALTER TABLE pasaje
    DROP CONSTRAINT IF EXISTS ck_pasaje_asiento_individual;

ALTER TABLE pasaje
    DROP CONSTRAINT IF EXISTS ck_pasaje_individual_grupo;

ALTER TABLE pasaje
    ADD CONSTRAINT ck_pasaje_individual_grupo
    CHECK (
        (
            tipo_registro = 'Individual'
            AND grupo_id IS NULL
            AND unidad_salida_id IS NOT NULL
            AND numero_asiento IS NOT NULL
            AND es_responsable = false
        )
        OR
        (
            tipo_registro = 'Grupo'
            AND grupo_id IS NOT NULL
            AND unidad_salida_id IS NULL
            AND numero_asiento IS NULL
        )
    );

ALTER TABLE pasaje
    DROP CONSTRAINT IF EXISTS fk_pasaje_grupo;

ALTER TABLE pasaje
    ADD CONSTRAINT fk_pasaje_grupo
    FOREIGN KEY (grupo_id) REFERENCES grupo (id);

CREATE INDEX IF NOT EXISTS ix_pasaje_grupo
    ON pasaje (grupo_id);

ALTER TABLE pago
    DROP CONSTRAINT IF EXISTS ck_pago_con_destino;

ALTER TABLE pago
    DROP CONSTRAINT IF EXISTS ck_pago_un_destino;

ALTER TABLE pago
    ADD CONSTRAINT ck_pago_un_destino
    CHECK ((pasaje_id IS NULL) <> (grupo_id IS NULL));

ALTER TABLE pago
    DROP CONSTRAINT IF EXISTS fk_pago_grupo;

ALTER TABLE pago
    ADD CONSTRAINT fk_pago_grupo
    FOREIGN KEY (grupo_id) REFERENCES grupo (id);

CREATE INDEX IF NOT EXISTS ix_pago_grupo
    ON pago (grupo_id);
