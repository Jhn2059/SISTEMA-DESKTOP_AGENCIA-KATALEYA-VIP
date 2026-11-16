-- Módulo de pasajes individuales (Etapa 8).
-- Reemplaza el prototipo anterior de unidades de salida y pasajes.
-- Ejecutar después de 01-esquema.sql y 05-tarifas-y-parametros.sql.

DROP TABLE IF EXISTS boleto CASCADE;
DROP TABLE IF EXISTS pago CASCADE;
DROP TABLE IF EXISTS pasaje CASCADE;
DROP TABLE IF EXISTS unidad_salida CASCADE;
DROP TABLE IF EXISTS boleto_correlativo CASCADE;

CREATE TABLE unidad_salida (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    numero smallint NOT NULL CHECK (numero > 0),
    grupo_id bigint,
    sentido varchar(12) NOT NULL
        CHECK (sentido IN ('HuancayoIca', 'IcaHuancayo')),
    tipo_vehiculo_id smallint NOT NULL REFERENCES tipo_vehiculo (id),
    fecha_hora_salida timestamptz NOT NULL,
    estado varchar(12) NOT NULL DEFAULT 'Programada'
        CHECK (estado IN ('Programada', 'EnViaje', 'Completada', 'Cancelada')),
    abierta_por integer NOT NULL REFERENCES usuario (id),
    UNIQUE (fecha_hora_salida, sentido, numero)
);

CREATE INDEX ix_unidad_salida_fecha_hora
    ON unidad_salida (fecha_hora_salida);

CREATE INDEX ix_unidad_salida_sentido_fecha
    ON unidad_salida (sentido, fecha_hora_salida);

CREATE TABLE pasaje (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    unidad_salida_id bigint NOT NULL REFERENCES unidad_salida (id),
    grupo_id bigint,
    numero_asiento smallint CHECK (numero_asiento > 0),
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
    importe numeric(8, 2) NOT NULL CHECK (importe > 0),
    estado varchar(24) NOT NULL DEFAULT 'Reservado'
        CHECK (estado IN ('Reservado', 'ReservadoConAdelanto', 'Pagado', 'Anulado', 'NoSePresento')),
    tipo_registro varchar(12) NOT NULL DEFAULT 'Individual'
        CHECK (tipo_registro IN ('Individual', 'Grupo')),
    emisor_id integer NOT NULL REFERENCES usuario (id),
    emisor varchar(100) NOT NULL,
    es_menor boolean NOT NULL DEFAULT false,
    nombre_padre_madre varchar(100),
    motivo_menor varchar(200),
    CONSTRAINT ck_pasaje_asiento_individual
        CHECK (tipo_registro = 'Grupo' OR numero_asiento IS NOT NULL)
);

CREATE UNIQUE INDEX ux_pasaje_asiento_vigente
    ON pasaje (unidad_salida_id, numero_asiento)
    WHERE estado <> 'Anulado' AND numero_asiento IS NOT NULL;

CREATE INDEX ix_pasaje_unidad_salida
    ON pasaje (unidad_salida_id);

CREATE INDEX ix_pasaje_estado_fecha_hora
    ON pasaje (estado, fecha_hora_salida);

CREATE INDEX ix_pasaje_numero_documento
    ON pasaje (numero_documento);

CREATE TABLE pago (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    pasaje_id bigint REFERENCES pasaje (id),
    grupo_id bigint,
    monto numeric(8, 2) NOT NULL CHECK (monto > 0),
    medio_pago varchar(20) NOT NULL
        CHECK (medio_pago IN ('Efectivo', 'Tarjeta', 'Yape', 'Plin')),
    tipo varchar(12) NOT NULL
        CHECK (tipo IN ('Adelanto', 'Saldo', 'PagoTotal')),
    fecha_pago timestamptz NOT NULL DEFAULT now(),
    cobrador_id integer NOT NULL REFERENCES usuario (id),
    cobrador varchar(100) NOT NULL,
    CONSTRAINT ck_pago_con_destino
        CHECK (pasaje_id IS NOT NULL OR grupo_id IS NOT NULL)
);

CREATE INDEX ix_pago_pasaje
    ON pago (pasaje_id);

CREATE TABLE boleto_correlativo (
    serie varchar(3) PRIMARY KEY,
    ultimo integer NOT NULL DEFAULT 0 CHECK (ultimo >= 0)
);

INSERT INTO boleto_correlativo (serie, ultimo)
VALUES ('001', 0)
ON CONFLICT (serie) DO NOTHING;

CREATE TABLE boleto (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    pasaje_id bigint NOT NULL UNIQUE REFERENCES pasaje (id),
    serie varchar(3) NOT NULL,
    correlativo integer NOT NULL CHECK (correlativo > 0),
    fecha_emision timestamptz NOT NULL DEFAULT now(),
    emitido_por_id integer NOT NULL REFERENCES usuario (id),
    emitido_por varchar(100) NOT NULL,
    UNIQUE (serie, correlativo)
);

CREATE INDEX ix_boleto_fecha_emision
    ON boleto (fecha_emision);
