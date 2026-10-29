using Kataleya.Aplicacion.Services;

namespace Kataleya.Api.Copia;

// Hace la copia por detrás, para responder enseguida al programador de tareas de Supabase.
// Al arrancar, la Web API también la intenta: Render la despierta cuando alguien abre el sistema (CDU-22 A1).
public sealed class EjecutorDeCopias : BackgroundService
{
    private static readonly TimeSpan EsperaAlArrancar = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _ambitos;
    private readonly ILogger<EjecutorDeCopias> _registro;
    private readonly SemaphoreSlim _pedidos = new(0);

    public EjecutorDeCopias(IServiceScopeFactory ambitos, ILogger<EjecutorDeCopias> registro)
    {
        _ambitos = ambitos ?? throw new ArgumentNullException(nameof(ambitos));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
    }

    public void Pedir() =>
        _pedidos.Release();

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        await Task.Delay(EsperaAlArrancar, detener);
        await HacerCopiaAsync(soloSiEstaConfigurada: true);

        while (!detener.IsCancellationRequested)
        {
            await _pedidos.WaitAsync(detener);
            await HacerCopiaAsync(soloSiEstaConfigurada: false);
        }
    }

    private async Task HacerCopiaAsync(bool soloSiEstaConfigurada)
    {
        try
        {
            using var ambito = _ambitos.CreateScope();
            var servicio = ambito.ServiceProvider.GetRequiredService<ServicioCopiaDeDatos>();

            if (soloSiEstaConfigurada && !servicio.PuedeCopiar)
            {
                _registro.LogWarning("La copia semanal no está configurada: falta el permiso de Google Drive.");
                return;
            }

            if (await servicio.HacerCopiaSiCorrespondeAsync())
            {
                _registro.LogInformation("Copia semanal guardada en Google Drive.");
            }
        }
        catch (Exception excepcion)
        {
            _registro.LogError(excepcion, "La copia semanal no se pudo guardar.");
        }
    }
}
