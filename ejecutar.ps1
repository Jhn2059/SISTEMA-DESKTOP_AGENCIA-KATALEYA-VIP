param(
    [int]$PuertoApi = 5080,
    [switch]$SinCompilar
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
$argumentosApi = @("run", "--project", "Kataleya.Api")
if ($SinCompilar) {
    $argumentosApi += "--no-build"
}

Write-Host "Iniciando la Web API en http://localhost:$PuertoApi ..." -ForegroundColor Cyan
$api = Start-Process -FilePath "dotnet" -ArgumentList $argumentosApi -WorkingDirectory $raiz -PassThru

try {
    $limite = (Get-Date).AddSeconds(120)
    $conectado = $false
    while (-not $conectado -and (Get-Date) -lt $limite) {
        Start-Sleep -Seconds 2
        try {
            $cliente = New-Object System.Net.Sockets.TcpClient
            $cliente.Connect("localhost", $PuertoApi)
            $cliente.Close()
            $conectado = $true
        }
        catch {
            $conectado = $false
        }
    }

    if (-not $conectado) {
        throw "La Web API no respondio en el puerto $PuertoApi. Revisa la otra ventana."
    }

    Write-Host "API lista. Abriendo el escritorio (Windows 10 1809 o superior)..." -ForegroundColor Cyan
    $argumentosEscritorio = @("run", "--project", "Kataleya.Escritorio", "-f", "net10.0-windows10.0.19041.0")
    if ($SinCompilar) {
        $argumentosEscritorio += "--no-build"
    }

    & dotnet @argumentosEscritorio
}
finally {
    if (-not $api.HasExited) {
        Write-Host "Cerrando la Web API (PID $($api.Id))..." -ForegroundColor Yellow
        & taskkill /PID $api.Id /T /F | Out-Null
    }
}
