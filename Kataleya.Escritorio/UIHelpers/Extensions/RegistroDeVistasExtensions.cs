using Kataleya.Escritorio.Features.AcercaDelSistema;
using Kataleya.Escritorio.Features.Autenticacion.InicioDeSesion;
using Kataleya.Escritorio.Features.Bitacora;
using Kataleya.Escritorio.Features.CuadreDeVentas;
using Kataleya.Escritorio.Features.GestionDeUsuarios;
using Kataleya.Escritorio.Features.GestionDeUsuarios.CrearCuenta;
using Kataleya.Escritorio.Features.GestionDeUsuarios.RestablecerContrasena;
using Kataleya.Escritorio.Features.PanelPrincipal;
using Kataleya.Escritorio.Features.PanelPrincipal.ReporteAnual;
using Kataleya.Escritorio.Features.Pasajes;
using Kataleya.Escritorio.Features.RegistroDePasajes;
using Kataleya.Escritorio.Features.RegistroDiferido;
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
        servicios.AddTransient<RegistroDePasajesPage>();
        servicios.AddTransient<ListaDePasajesPage>();
        servicios.AddTransient<DetalleDePasajePage>();
        servicios.AddTransient<CuadreDeVentasViewModel>();
        servicios.AddTransient<CuadreDeVentasPage>();
        servicios.AddTransient<RegistroDiferidoPage>();
        servicios.AddTransient<BitacoraViewModel>();
        servicios.AddTransient<BitacoraPage>();
        servicios.AddTransient<DialogoDeCuentaViewModel>();
        servicios.AddTransient<DialogoDeCuentaPage>();
        servicios.AddTransient<DialogoDeContrasenaViewModel>();
        servicios.AddTransient<DialogoDeContrasenaPage>();
        servicios.AddTransient<AcercaDelSistemaViewModel>();
        servicios.AddTransient<AcercaDelSistemaPage>();
        servicios.AddTransient<DialogoDeReporteAnualViewModel>();
        servicios.AddTransient<DialogoDeReporteAnualPage>();
        servicios.AddSingleton<AppShell>();

        return servicios;
    }
}
