param(
    [string]$Servidor = "localhost",
    [int]$Puerto = 5432,
    [string]$Usuario = "kataleya",
    [string]$Contrasena = "kataleya",
    [string]$BaseDeDatos = "kataleya",
    [string]$Psql = "psql"
)

$ErrorActionPreference = "Stop"
$env:PGPASSWORD = $Contrasena

$scripts = @(
    "01-esquema.sql",
    "02-datos-iniciales.sql",
    "04-modulos-funcionales.sql",
    "05-datos-funcionales.sql",
    "06-sentido-de-las-unidades.sql",
    "07-copia-de-respaldo.sql",
    "09-nombres-de-la-cuenta.sql",
    "10-preferencias.sql"
)

$carpeta = Split-Path -Parent $MyInvocation.MyCommand.Path

foreach ($script in $scripts) {
    $ruta = Join-Path $carpeta $script
    Write-Host "Ejecutando $script ..." -ForegroundColor Cyan
    & $Psql -h $Servidor -p $Puerto -U $Usuario -d $BaseDeDatos -v ON_ERROR_STOP=1 -q -f $ruta
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo al ejecutar $script (codigo $LASTEXITCODE)."
    }
}

Write-Host "Base '$BaseDeDatos' lista. Cuenta inicial: admin / Kataleya2026" -ForegroundColor Green
