using Kataleya.Escritorio.Features.Autenticacion.InicioDeSesion;
using Kataleya.Escritorio.Features.GestionDeUsuarios;
using Kataleya.Escritorio.Features.GestionDeUsuarios.CrearCuenta;
using Kataleya.Escritorio.Features.GestionDeUsuarios.RestablecerContrasena;
using Kataleya.Escritorio.Features.PanelPrincipal;
using Microsoft.Extensions.DependencyInjection;

namespace Kataleya.Escritorio.UIHelpers.Extensions;

public static class RegistroDeVistasExtensions
{
    public static IServiceCollection AgregarVistasDeKataleya(
        this IServiceCollection servicios)
    {
        servicios.AddTransient<InicioDeSesionViewModel>();
        servicios.AddTransient<InicioDeSesionPage>();
        servicios.AddTransient<PanelPrincipalViewModel>();
        servicios.AddTransient<PanelPrincipalPage>();
        servicios.AddTransient<GestionDeUsuariosViewModel>();
        servicios.AddTransient<GestionDeUsuariosPage>();
        servicios.AddTransient<DialogoDeCuentaViewModel>();
        servicios.AddTransient<DialogoDeCuentaPage>();
        servicios.AddTransient<DialogoDeContrasenaViewModel>();
        servicios.AddTransient<DialogoDeContrasenaPage>();
        servicios.AddSingleton<AppShell>();

        return servicios;
    }
}
