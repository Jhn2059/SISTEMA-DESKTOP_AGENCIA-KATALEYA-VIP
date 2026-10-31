CREATE TABLE IF NOT EXISTS unidad_salida (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    sentido varchar(12) NOT NULL
        CHECK (sentido IN ('HuancayoIca', 'IcaHuancayo')),
    tipo_vehiculo_id smallint NOT NULL REFERENCES tipo_vehiculo (id),
    fecha_hora_salida timestamptz NOT NULL,
    estado varchar(12) NOT NULL DEFAULT 'Programada'
        CHECK (estado IN ('Programada', 'EnViaje', 'Completada', 'Cancelada')),
    usuario_id integer NOT NULL REFERENCES usuario (id)
);

CREATE INDEX IF NOT EXISTS ix_unidad_salida_fecha_hora
    ON unidad_salida (fecha_hora_salida);

CREATE INDEX IF NOT EXISTS ix_unidad_salida_sentido_fecha
    ON unidad_salida (sentido, fecha_hora_salida);

CREATE TABLE IF NOT EXISTS pasaje (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    unidad_salida_id bigint NOT NULL REFERENCES unidad_salida (id),
    numero_asiento smallint NOT NULL CHECK (numero_asiento > 0),
    nombre_pasajero varchar(100) NOT NULL,
    tipo_documento varchar(20) NOT NULL
        CHECK (tipo_documento IN ('DNI', 'Pasaporte', 'CarneExtranjeria')),
    numero_documento varchar(20) NOT NULL,
    celular varchar(12) NOT NULL,
    origen varchar(50) NOT NULL,
    destino varchar(50) NOT NULL,
    lugar_embarque varchar(50) NOT NULL,
    fecha_hora_salida timestamptz NOT NULL,
    fecha_venta timestamptz NOT NULL DEFAULT now(),
    adelanto numeric(8, 2) NOT NULL DEFAULT 0 CHECK (adelanto >= 0),
    estado varchar(12) NOT NULL DEFAULT 'Activo'
        CHECK (estado IN ('Activo', 'Anulado')),
    usuario_id integer NOT NULL REFERENCES usuario (id),
    vendida_por varchar(100) NOT NULL,
    es_menor boolean NOT NULL DEFAULT false,
    nombre_padre_madre varchar(100),
    motivo_menor varchar(200),
    UNIQUE (unidad_salida_id, numero_asiento)
);

CREATE INDEX IF NOT EXISTS ix_pasaje_estado_fecha_hora
    ON pasaje (estado, fecha_hora_salida);

CREATE INDEX IF NOT EXISTS ix_pasaje_unidad_salida
    ON pasaje (unidad_salida_id);

CREATE TABLE IF NOT EXISTS pago (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    pasaje_id bigint NOT NULL REFERENCES pasaje (id),
    monto numeric(8, 2) NOT NULL CHECK (monto > 0),
    medio_pago varchar(20) NOT NULL
        CHECK (medio_pago IN ('Efectivo', 'Tarjeta', 'Yape', 'Plin')),
    fecha_pago timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_pago_pasaje
    ON pago (pasaje_id);