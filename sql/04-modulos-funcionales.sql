BEGIN;

CREATE TABLE IF NOT EXISTS tipo_vehiculo (
    id smallint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre varchar(40) NOT NULL,
    numero_asientos smallint NOT NULL CHECK (numero_asientos > 0),
    distribucion jsonb,
    UNIQUE (nombre, numero_asientos)
);

CREATE TABLE IF NOT EXISTS parametro (
    nombre varchar(60) PRIMARY KEY,
    valor text NOT NULL,
    usuario_id integer REFERENCES usuario (id),
    fecha_hora timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS tarifa (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo varchar(12) NOT NULL CHECK (tipo IN ('Regular', 'Programada')),
    nombre varchar(60) NOT NULL,
    importe numeric(10, 2) NOT NULL CHECK (importe > 0),
    vigencia_desde date,
    vigencia_hasta date,
    sentido varchar(8) NOT NULL DEFAULT 'Ambos'
        CHECK (sentido IN ('Ida', 'Vuelta', 'Ambos')),
    usuario_id integer NOT NULL REFERENCES usuario (id),
    fecha_hora timestamptz NOT NULL DEFAULT now(),
    CHECK (
        tipo = 'Regular'
        OR (
            vigencia_desde IS NOT NULL
            AND vigencia_hasta IS NOT NULL
            AND vigencia_hasta >= vigencia_desde
        )
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_tarifa_regular_sentido
    ON tarifa (sentido)
    WHERE tipo = 'Regular';

CREATE INDEX IF NOT EXISTS ix_tarifa_programada_vigencia
    ON tarifa (vigencia_desde, vigencia_hasta)
    WHERE tipo = 'Programada';

CREATE SEQUENCE IF NOT EXISTS secuencia_codigo_grupo;

CREATE TABLE IF NOT EXISTS grupo (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    codigo varchar(20) NOT NULL UNIQUE,
    telefono varchar(20) NOT NULL,
    precio_acordado numeric(10, 2) NOT NULL CHECK (precio_acordado > 0),
    origen varchar(80) NOT NULL,
    destino varchar(80) NOT NULL,
    lugar_recojo varchar(120) NOT NULL,
    fecha_salida date NOT NULL,
    hora_salida time NOT NULL,
    usuario_id integer NOT NULL REFERENCES usuario (id),
    fecha_hora timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS unidad_salida (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fecha_hora_salida timestamptz NOT NULL,
    numero varchar(10) NOT NULL,
    tipo_vehiculo_id smallint NOT NULL REFERENCES tipo_vehiculo (id),
    grupo_id bigint REFERENCES grupo (id),
    fecha_hora timestamptz NOT NULL DEFAULT now(),
    UNIQUE (fecha_hora_salida, numero)
);

CREATE INDEX IF NOT EXISTS ix_unidad_salida_fecha
    ON unidad_salida (fecha_hora_salida);

CREATE TABLE IF NOT EXISTS correlativo_boleto (
    serie varchar(10) PRIMARY KEY,
    ultimo_numero bigint NOT NULL DEFAULT 0 CHECK (ultimo_numero >= 0),
    sede_id smallint NOT NULL REFERENCES sede (id)
);

CREATE TABLE IF NOT EXISTS pasaje (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    pasajero_nombre varchar(120) NOT NULL,
    tipo_documento varchar(12) NOT NULL CHECK (tipo_documento IN ('DNI', 'Pasaporte')),
    numero_documento varchar(20) NOT NULL,
    telefono varchar(20),
    origen varchar(80) NOT NULL,
    destino varchar(80) NOT NULL,
    lugar_embarque varchar(120) NOT NULL,
    fecha_salida date NOT NULL,
    hora_salida time NOT NULL,
    unidad_salida_id bigint REFERENCES unidad_salida (id),
    numero_asiento smallint,
    grupo_id bigint REFERENCES grupo (id),
    es_responsable boolean NOT NULL DEFAULT false,
    importe numeric(10, 2) NOT NULL CHECK (importe >= 0),
    estado varchar(24) NOT NULL
        CHECK (estado IN ('Reservado', 'ReservadoConAdelanto', 'Pagado', 'Anulado', 'NoSePresento')),
    sede_id smallint NOT NULL REFERENCES sede (id),
    emisor_id integer NOT NULL REFERENCES usuario (id),
    fecha_hora_registro timestamptz NOT NULL DEFAULT now(),
    tipo_registro varchar(10) NOT NULL DEFAULT 'Normal'
        CHECK (tipo_registro IN ('Normal', 'Diferido')),
    hora_emision_real timestamptz,
    numero_talonario varchar(20) UNIQUE,
    motivo_anulacion text,
    nombre_padre varchar(120),
    nombre_madre varchar(120),
    motivo_viaje text,
    CHECK (
        (grupo_id IS NULL AND unidad_salida_id IS NOT NULL AND numero_asiento IS NOT NULL)
        OR (grupo_id IS NOT NULL AND unidad_salida_id IS NULL AND numero_asiento IS NULL)
    ),
    CHECK (
        (tipo_registro = 'Normal' AND numero_talonario IS NULL AND hora_emision_real IS NULL)
        OR (tipo_registro = 'Diferido' AND numero_talonario IS NOT NULL AND hora_emision_real IS NOT NULL)
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_pasaje_asiento
    ON pasaje (unidad_salida_id, numero_asiento)
    WHERE estado <> 'Anulado';

CREATE UNIQUE INDEX IF NOT EXISTS ux_pasaje_responsable
    ON pasaje (grupo_id)
    WHERE es_responsable;

CREATE INDEX IF NOT EXISTS ix_pasaje_fecha_salida
    ON pasaje (fecha_salida, hora_salida);

CREATE INDEX IF NOT EXISTS ix_pasaje_emisor
    ON pasaje (emisor_id);

CREATE INDEX IF NOT EXISTS ix_pasaje_documento
    ON pasaje (numero_documento);

CREATE TABLE IF NOT EXISTS pago (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    pasaje_id bigint REFERENCES pasaje (id),
    grupo_id bigint REFERENCES grupo (id),
    importe numeric(10, 2) NOT NULL CHECK (importe > 0),
    medio varchar(10) NOT NULL CHECK (medio IN ('Efectivo', 'Yape')),
    tipo_movimiento varchar(12) NOT NULL DEFAULT 'Pago'
        CHECK (tipo_movimiento IN ('Pago', 'Devolucion')),
    fecha_hora timestamptz NOT NULL DEFAULT now(),
    usuario_id integer NOT NULL REFERENCES usuario (id),
    CHECK ((pasaje_id IS NULL) <> (grupo_id IS NULL))
);

CREATE INDEX IF NOT EXISTS ix_pago_pasaje ON pago (pasaje_id);
CREATE INDEX IF NOT EXISTS ix_pago_grupo ON pago (grupo_id);
CREATE INDEX IF NOT EXISTS ix_pago_fecha ON pago (fecha_hora);

CREATE TABLE IF NOT EXISTS boleto_viaje (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    pasaje_id bigint NOT NULL UNIQUE REFERENCES pasaje (id),
    serie varchar(10) NOT NULL,
    numero bigint NOT NULL,
    fecha_hora_emision timestamptz NOT NULL DEFAULT now(),
    emisor_id integer NOT NULL REFERENCES usuario (id),
    reimpresiones integer NOT NULL DEFAULT 0 CHECK (reimpresiones >= 0),
    UNIQUE (serie, numero)
);

CREATE TABLE IF NOT EXISTS reporte_anual (
    anio smallint PRIMARY KEY,
    fecha_hora timestamptz NOT NULL DEFAULT now(),
    usuario_id integer NOT NULL REFERENCES usuario (id),
    numero_pasajes integer NOT NULL DEFAULT 0,
    ruta_pdf text
);

COMMIT;
