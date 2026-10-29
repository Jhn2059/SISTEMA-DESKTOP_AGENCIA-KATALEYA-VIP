# Restaurar una copia semanal (equipo de soporte)

La Web API guarda cada domingo una copia cifrada de la base en la carpeta de Google Drive de la
empresa (RNF-17, CDU-22) y conserva las 8 últimas. Restaurarla lo hace el equipo de soporte, fuera
del sistema, en no más de dos días.

## Formato

`kataleya-copia-AAAA-MM-DD-HHMM.cifrada` = `KTLY1` + sal (16 bytes) + nonce (12) + etiqueta (16) +
datos cifrados con AES-256-GCM. La llave sale de la clave de cifrado de la Web API
(`Copia:ClaveDeCifrado`) con PBKDF2-SHA256, 200 000 iteraciones. Los datos descifrados son un .zip con
un CSV por tabla (con encabezado).

## Pasos

1. Descargar la copia de Google Drive y descifrarla:
   `python descifrar-copia.py kataleya-copia-AAAA-MM-DD-HHMM.cifrada "clave de cifrado"`
2. Descomprimir el .zip.
3. En una base vacía, ejecutar `01-esquema.sql`, `04-modulos-funcionales.sql`,
   `06-sentido-de-las-unidades.sql`, `07-copia-de-respaldo.sql`, `09-nombres-de-la-cuenta.sql` y
   `10-preferencias.sql` (no los de datos iniciales). Una copia anterior al script 09 no trae la columna
   `es_cuenta_principal`: se restaura sin ejecutar el 09 y se ejecuta después.
4. Cargar las tablas en este orden, desde la carpeta de los CSV:

```
psql "cadena de conexión" -v ON_ERROR_STOP=1 -f restaurar-copia.sql
```

5. Revisar que el sistema abra y que el último pasaje sea el esperado.
