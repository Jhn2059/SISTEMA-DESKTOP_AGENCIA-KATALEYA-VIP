# kataleya

Sistema de escritorio de la Agencia Kataleya VIP: módulo de reservas y venta de pasajes.
La aplicación de escritorio (.NET MAUI, solo Windows) pide todo a la Web API (ASP.NET Core),
que es la única que se conecta a la base de datos (PostgreSQL en Supabase).

## Proyectos

| Proyecto | Qué tiene |
|---|---|
| Kataleya.Dominio | Entidades, objetos de datos e interfaces de los repositorios |
| Kataleya.Aplicacion | Servicios con las reglas del negocio y el PDF del reporte anual |
| Kataleya.Datos | Repositorios con Npgsql, la copia de la base y Google Drive |
| Kataleya.Contratos | Lo que viaja entre la aplicación de escritorio y la Web API |
| Kataleya.Api | Web API (rutas, sesión y copia semanal) |
| Kataleya.Cliente | ClienteApi, la copia local de los pasajes de hoy y el registro técnico |
| Kataleya.Escritorio | Pantallas en XAML con MVVM e impresión del boleto |
| Kataleya.Pruebas | Pruebas con xUnit |

## Compilar

- Web API y pruebas: `dotnet build Kataleya.Api` y `dotnet test Kataleya.Pruebas`.
- Escritorio: en Windows 10 o superior, `dotnet build Kataleya.Escritorio`. El workflow
  «Compilar Kataleya» lo compila en GitHub Actions y arma el instalador (artefacto
  `kataleya-instalador`).

## Puesta en marcha desde cero (otra PC)

Requisitos en la PC que va a probar o tomar capturas:

- Windows 10 1809 o superior, x64 (el escritorio MAUI solo corre en Windows).
- [.NET 10 SDK](https://dotnet.microsoft.com/download) y la carga de trabajo MAUI:
  `dotnet workload install maui`.
- PostgreSQL 16 con `psql` disponible en el PATH.
- Git.

Pasos:

1. Clonar el repositorio y entrar a la carpeta:

   ```powershell
   git clone https://github.com/Jhn2059/SISTEMA-DESKTOP_AGENCIA-KATALEYA-VIP.git
   cd SISTEMA-DESKTOP_AGENCIA-KATALEYA-VIP
   ```

   Si el equipo trabaja en la rama del escritorio, cambiar a ella primero
   (`git checkout escritorio-completo`).

2. Crear el rol y la base con el superusuario de PostgreSQL (una sola vez):

   ```powershell
   psql -U postgres -c "CREATE ROLE kataleya LOGIN PASSWORD 'kataleya';"
   psql -U postgres -c "CREATE DATABASE kataleya OWNER kataleya;"
   ```

   Si el rol ya existe, omitir el primer comando.

3. Crear el esquema y los datos iniciales:

   ```powershell
   powershell -ExecutionPolicy Bypass -File sql/crear-base.ps1
   ```

   El script ejecuta en orden `01`, `02`, `04`, `05`, `06`, `07`, `09` y `10`.
   No ejecutar `03` (borra datos) ni `08` (solo para Supabase con `pg_cron`/`pg_net`).

4. Ejecutar la API y el escritorio:

   ```powershell
   powershell -ExecutionPolicy Bypass -File ejecutar.ps1
   ```

   O en dos terminales:

   ```powershell
   dotnet run --project Kataleya.Api
   dotnet run --project Kataleya.Escritorio -f net10.0-windows10.0.19041.0
   ```

5. Entrar con la cuenta inicial **`admin` / `Kataleya2026`**. Es una cuenta de prueba:
   cámbiala antes de usar el sistema con datos reales.

Si la API va a correr en otra PC o servidor:

- En la PC de la API: `dotnet run --project Kataleya.Api --urls http://0.0.0.0:5080`
  y abrir el puerto 5080 en el firewall.
- En la PC del escritorio: `setx KATALEYA_API "http://IP-DEL-SERVIDOR:5080/"` y
  `setx KATALEYA_PERMITIR_HTTP "true"` (solo para red local; para Internet usar HTTPS).
  Cerrar y abrir la sesión de Windows para que tome las variables.

### Problemas comunes

- **«El proceso no puede obtener acceso al archivo» (MSB3027/MSB3028)**: hay una API o un
  escritorio abiertos. Cerrar con `Get-Process Kataleya* | Stop-Process -Force` y volver a
  compilar. `ejecutar.ps1` cierra la API al cerrar el escritorio.
- **No conecta con la API**: verificar que esté levantada y que el puerto 5080 esté libre
  (`Get-NetTCPConnection -LocalPort 5080`). Si está en otra PC, revisar `KATALEYA_API`.
  Si `KATALEYA_API` quedó apuntando a otro puerto (por ejemplo `5081`), corregirla o borrarla:
  el valor predeterminado es `http://localhost:5080/`. `ejecutar.ps1` fija esa dirección
  para el escritorio que abre.
- **Falta la carga de trabajo MAUI**: `dotnet workload install maui`.
- **Error de conexión a PostgreSQL**: revisar el servicio, la base `kataleya`, el usuario
  `kataleya` y la cadena `ConnectionStrings:Kataleya`.
- **Advertencia de la copia semanal**: sin credenciales de Google Drive la copia no se
  programa, pero el sistema funciona normalmente.

## Base de datos

En una base nueva se ejecutan, en orden: `01-esquema.sql`, `02-datos-iniciales.sql`,
`04-modulos-funcionales.sql`, `05-datos-funcionales.sql`, `06-sentido-de-las-unidades.sql`,
`07-copia-de-respaldo.sql`, `09-nombres-de-la-cuenta.sql` y `10-preferencias.sql`; después,
`08-programar-copia-semanal.sql` con la dirección de la Web API y la clave del programador.
El script `sql/crear-base.ps1` hace los ocho primeros pasos.
En una base que ya existe, solo los que falten, antes de publicar la Web API nueva.
`03-limpiar-datos-de-prueba.sql` borra los datos de prueba. Para restaurar una copia semanal,
ver `sql/restaurar-copia.md`.

## Configuración

- Web API (`appsettings.json` o variables de entorno): `ConnectionStrings:Kataleya`,
  `Sesion:ClaveSecreta`, `Copia:ClaveDelProgramador`, `Copia:ClaveDeCifrado` y la sección
  `GoogleDrive` (ClienteId, ClienteSecreto, TokenDeActualizacion y CarpetaId).
- Escritorio (variables de entorno): `KATALEYA_API` (dirección de la Web API),
  `KATALEYA_REINTENTOS`, `KATALEYA_CARPETA_RESPALDO` (carpeta del reporte anual) y
  `KATALEYA_PERMITIR_HTTP` (solo para pruebas).
