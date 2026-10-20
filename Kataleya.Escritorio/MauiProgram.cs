using Kataleya.Cliente;
using Kataleya.Dominio.Interfaces;
using Kataleya.Escritorio.ModelosDeVista;
using Kataleya.Escritorio.Paginas;
using Kataleya.Escritorio.Servicios;
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
        builder.Services.AddSingleton<ClienteApi>();
        builder.Services.AddSingleton<INavegacion, Navegacion>();
        builder.Services.AddSingleton<IDialogos, DialogosDeMaui>();

        builder.Services.AddTransient<InicioDeSesionViewModel>();
        builder.Services.AddTransient<InicioDeSesionPage>();
        builder.Services.AddTransient<PanelPrincipalViewModel>();
        builder.Services.AddTransient<PanelPrincipalPage>();
        builder.Services.AddTransient<GestionDeUsuariosViewModel>();
        builder.Services.AddTransient<GestionDeUsuariosPage>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
