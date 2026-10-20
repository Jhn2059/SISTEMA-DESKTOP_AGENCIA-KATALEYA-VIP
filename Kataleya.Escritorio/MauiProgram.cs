using Kataleya.Cliente;
using Kataleya.Dominio.Interfaces;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.UIHelpers.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Kataleya.Escritorio;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();

        builder.Services.AddSingleton(_ => new HttpClient
        {
            BaseAddress = Configuracion.DireccionApi,
            Timeout = TimeSpan.FromSeconds(10)
        });
        builder.Services.AddSingleton<IRegistroTecnico, RegistroTecnico>();
        builder.Services.AddSingleton(sp => new ClienteApi(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<IRegistroTecnico>(),
            Configuracion.Reintentos,
            Configuracion.PermitirHttpSinTls));
        builder.Services.AddSingleton<INavegacion, Navegacion>();
        builder.Services.AddSingleton<IDialogos, DialogosDeMaui>();

        builder.Services.AgregarVistasDeKataleya();

        return builder.Build();
    }
}
