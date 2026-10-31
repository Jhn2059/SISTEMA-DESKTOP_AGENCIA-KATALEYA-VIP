CREATE TABLE IF NOT EXISTS tipo_vehiculo (
    id smallint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre varchar(30) NOT NULL,
    cantidad_asientos smallint NOT NULL CHECK (cantidad_asientos > 0),
    distribucion jsonb,
    UNIQUE (nombre, cantidad_asientos)
);

CREATE TABLE IF NOT EXISTS parametro_negocio (
    nombre varchar(40) PRIMARY KEY,
    valor text NOT NULL,
    usuario_id integer NOT NULL REFERENCES usuario (id),
    fecha_hora timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS tarifa (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo varchar(12) NOT NULL CHECK (tipo IN ('Regular', 'Programada')),
    nombre varchar(60),
    importe numeric(8, 2) NOT NULL CHECK (importe > 0),
    desde date,
    hasta date,
    usuario_id integer NOT NULL REFERENCES usuario (id),
    sentido varchar(12),
    CHECK (sentido IS NULL OR sentido IN ('HuancayoIca', 'IcaHuancayo')),
    CHECK (tipo <> 'Regular' OR sentido IS NOT NULL),
    CHECK (tipo <> 'Regular' OR (desde IS NULL AND hasta IS NULL)),
    CHECK (tipo <> 'Programada' OR nombre IS NOT NULL),
    CHECK (
        tipo <> 'Programada'
        OR (desde IS NOT NULL AND hasta IS NOT NULL AND hasta >= desde)
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_tarifa_regular_sentido
    ON tarifa (sentido)
    WHERE tipo = 'Regular';

CREATE INDEX IF NOT EXISTS ix_tarifa_programada_vigencia
    ON tarifa (desde, hasta, sentido)
    WHERE tipo = 'Programada';
