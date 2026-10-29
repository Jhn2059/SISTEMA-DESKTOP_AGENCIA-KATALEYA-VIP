-- Se ejecuta una vez en el editor SQL de Supabase. Por definir: la dirección de la Web API en Render
-- y la clave del programador de tareas (la misma que Copia:ClaveDelProgramador de la Web API).
-- Domingo a las 3:00 de Perú = 8:00 UTC.
CREATE EXTENSION IF NOT EXISTS pg_cron;
CREATE EXTENSION IF NOT EXISTS pg_net;

SELECT cron.schedule(
    'copia-semanal-kataleya',
    '0 8 * * 0',
    $$
    SELECT net.http_post(
        url := 'https://DIRECCION-DE-LA-WEB-API.onrender.com/copia-de-datos/semanal',
        headers := jsonb_build_object('X-Clave-Programador', 'CLAVE-DEL-PROGRAMADOR'),
        body := '{}'::jsonb,
        timeout_milliseconds := 90000
    );
    $$
);
