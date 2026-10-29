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

## Base de datos

En una base nueva se ejecutan, en orden: `01-esquema.sql`, `02-datos-iniciales.sql`,
`04-modulos-funcionales.sql`, `05-datos-funcionales.sql`, `06-sentido-de-las-unidades.sql`,
`07-copia-de-respaldo.sql`, `09-nombres-de-la-cuenta.sql` y `10-preferencias.sql`; después,
`08-programar-copia-semanal.sql` con la dirección de la Web API y la clave del programador.
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
