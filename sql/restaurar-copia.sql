-- Carga los CSV de una copia descifrada en una base con el esquema ya creado.
-- Se ejecuta con psql desde la carpeta donde están los CSV (restaurar-copia.md).
BEGIN;

\copy rol FROM 'rol.csv' WITH (FORMAT csv, HEADER)
\copy sede FROM 'sede.csv' WITH (FORMAT csv, HEADER)
\copy usuario FROM 'usuario.csv' WITH (FORMAT csv, HEADER)
\copy tipo_vehiculo FROM 'tipo_vehiculo.csv' WITH (FORMAT csv, HEADER)
\copy parametro FROM 'parametro.csv' WITH (FORMAT csv, HEADER)
\copy tarifa FROM 'tarifa.csv' WITH (FORMAT csv, HEADER)
\copy correlativo_boleto FROM 'correlativo_boleto.csv' WITH (FORMAT csv, HEADER)
\copy grupo FROM 'grupo.csv' WITH (FORMAT csv, HEADER)
\copy unidad_salida FROM 'unidad_salida.csv' WITH (FORMAT csv, HEADER)
\copy pasaje FROM 'pasaje.csv' WITH (FORMAT csv, HEADER)
\copy pago FROM 'pago.csv' WITH (FORMAT csv, HEADER)
\copy boleto_viaje FROM 'boleto_viaje.csv' WITH (FORMAT csv, HEADER)
\copy registro_bitacora FROM 'registro_bitacora.csv' WITH (FORMAT csv, HEADER)
\copy reporte_anual FROM 'reporte_anual.csv' WITH (FORMAT csv, HEADER)
\copy copia_respaldo FROM 'copia_respaldo.csv' WITH (FORMAT csv, HEADER)

-- Las columnas de identidad siguen desde el último id cargado.
SELECT setval(pg_get_serial_sequence(tabla, 'id'), COALESCE(maximo, 0) + 1, false)
FROM (
    VALUES
        ('rol', (SELECT max(id) FROM rol)),
        ('sede', (SELECT max(id) FROM sede)),
        ('usuario', (SELECT max(id) FROM usuario)),
        ('tipo_vehiculo', (SELECT max(id) FROM tipo_vehiculo)),
        ('tarifa', (SELECT max(id) FROM tarifa)),
        ('grupo', (SELECT max(id) FROM grupo)),
        ('unidad_salida', (SELECT max(id) FROM unidad_salida)),
        ('pasaje', (SELECT max(id) FROM pasaje)),
        ('pago', (SELECT max(id) FROM pago)),
        ('boleto_viaje', (SELECT max(id) FROM boleto_viaje)),
        ('registro_bitacora', (SELECT max(id) FROM registro_bitacora)),
        ('copia_respaldo', (SELECT max(id)::bigint FROM copia_respaldo))
) AS identidades (tabla, maximo);

SELECT setval('secuencia_codigo_grupo', COALESCE(maximo, 1), maximo IS NOT NULL)
FROM (SELECT max(substring(codigo FROM 3)::bigint) AS maximo FROM grupo WHERE codigo ~ '^G-[0-9]+$') AS grupos;

COMMIT;
