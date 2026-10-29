using Kataleya.Escritorio.Features.AcercaDelSistema;
using Kataleya.Escritorio.Features.Autenticacion.InicioDeSesion;
using Kataleya.Escritorio.Features.Bitacora;
using Kataleya.Escritorio.Features.Configuracion;
using Kataleya.Escritorio.Features.Configuracion.CuentasDeUsuario;
using Kataleya.Escritorio.Features.Configuracion.Equipo;
using Kataleya.Escritorio.Features.Configuracion.TarifasYParametros;
using Kataleya.Escritorio.Features.CuadreDeVentas;
using Kataleya.Escritorio.Features.Configuracion.CuentasDeUsuario.CrearCuenta;
using Kataleya.Escritorio.Features.Configuracion.CuentasDeUsuario.RestablecerContrasena;
using Kataleya.Escritorio.Features.PanelPrincipal;
using Kataleya.Escritorio.Features.PanelPrincipal.ReporteAnual;
using Kataleya.Escritorio.Features.Pasajes;
using Kataleya.Escritorio.Features.Pasajes.Dialogos;
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
        servicios.AddTransient<TarifasYParametrosViewModel>();
        servicios.AddTransient<CuentasDeUsuarioViewModel>();
        servicios.AddTransient<EquipoViewModel>();
        servicios.AddTransient<ConfiguracionViewModel>();
        servicios.AddTransient<ConfiguracionPage>();
        servicios.AddTransient<DialogoDeTarifaViewModel>();
        servicios.AddTransient<DialogoDeTarifaPage>();
        servicios.AddTransient<RegistroDePasajesViewModel>();
        servicios.AddTransient<RegistroDePasajesPage>();
        servicios.AddTransient<ListaDePasajesViewModel>();
        servicios.AddTransient<ListaDePasajesPage>();
        servicios.AddTransient<DetalleDePasajeViewModel>();
        servicios.AddTransient<DetalleDePasajePage>();
        servicios.AddTransient<DialogoDeCobroViewModel>();
        servicios.AddTransient<DialogoDeCobroPage>();
        servicios.AddTransient<DialogoDeAnulacionViewModel>();
        servicios.AddTransient<DialogoDeAnulacionPage>();
        servicios.AddTransient<DialogoDeModificacionViewModel>();
        servicios.AddTransient<DialogoDeModificacionPage>();
        servicios.AddTransient<CuadreDeVentasViewModel>();
        servicios.AddTransient<CuadreDeVentasPage>();
        servicios.AddTransient<RegistroDiferidoViewModel>();
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
