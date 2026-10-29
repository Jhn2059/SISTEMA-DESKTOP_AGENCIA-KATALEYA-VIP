using Kataleya.Contratos.Pasajes;
using Kataleya.Escritorio.Features.Bitacora;
using Kataleya.Escritorio.Features.Configuracion;
using Kataleya.Escritorio.Features.CuadreDeVentas;
using Kataleya.Escritorio.Features.PanelPrincipal;
using Kataleya.Escritorio.Features.Pasajes;
using Kataleya.Escritorio.Features.RegistroDePasajes;
using Kataleya.Escritorio.Features.RegistroDiferido;
using Microsoft.Extensions.DependencyInjection;

namespace Kataleya.Escritorio.UIHelpers.Paginas;

public static class NavegacionDeModulos
{
    private static IServiceProvider Servicios =>
        IPlatformApplication.Current!.Services;

    public static async Task IrAsync(string opcion)
    {
        if (string.IsNullOrWhiteSpace(opcion))
        {
            return;
        }

        var navegacion = Shell.Current.Navigation;
        var pila = navegacion.NavigationStack;

        if (opcion == "panel")
        {
            var panel = pila.OfType<PanelPrincipalPage>().LastOrDefault();
            if (panel is not null)
            {
                await RetrocederHastaAsync(panel);
            }

            return;
        }

        var actual = pila.LastOrDefault() as PaginaDeModulo;

        if (opcion == "pasajes" && actual is DetalleDePasajePage)
        {
            var lista = pila.OfType<ListaDePasajesPage>().LastOrDefault();
            if (lista is not null)
            {
                await RetrocederHastaAsync(lista);
                return;
            }
        }

        if (actual?.OpcionActiva == opcion)
        {
            return;
        }

        var existente = pila
            .OfType<PaginaDeModulo>()
            .LastOrDefault(pagina => pagina.OpcionActiva == opcion);

        if (existente is not null)
        {
            await RetrocederHastaAsync(existente);
            return;
        }

        Page? destino = opcion switch
        {
            "registrar-pasaje" => Servicios.GetRequiredService<RegistroDePasajesPage>(),
            "pasajes" => Servicios.GetRequiredService<ListaDePasajesPage>(),
            "cuadre-ventas" => Servicios.GetRequiredService<CuadreDeVentasPage>(),
            "registro-diferido" => Servicios.GetRequiredService<RegistroDiferidoPage>(),
            "bitacora" => Servicios.GetRequiredService<BitacoraPage>(),
            "configuracion" => Servicios.GetRequiredService<ConfiguracionPage>(),
            _ => null
        };

        if (destino is not null)
        {
            await navegacion.PushAsync(destino);
        }
    }

    public static async Task IrAlDetalleDePasajeAsync(long idPasaje)
    {
        var navegacion = Shell.Current.Navigation;
        var pagina = Servicios.GetRequiredService<DetalleDePasajePage>();
        pagina.IdPasaje = idPasaje;
        await navegacion.PushAsync(pagina);
    }

    // IU-P04 «Copiar datos para otro pasajero»: el registro se abre con el viaje del pasaje elegido.
    public static async Task IrAlRegistroConLosDatosDeAsync(DatosDePasaje pasaje)
    {
        var navegacion = Shell.Current.Navigation;
        var pagina = navegacion.NavigationStack.OfType<RegistroDePasajesPage>().LastOrDefault();

        if (pagina is null)
        {
            pagina = Servicios.GetRequiredService<RegistroDePasajesPage>();
            pagina.CopiarDatosDe(pasaje);
            await navegacion.PushAsync(pagina);
            return;
        }

        pagina.CopiarDatosDe(pasaje);
        await RetrocederHastaAsync(pagina);
    }

    private static async Task RetrocederHastaAsync(Page destino)
    {
        var navegacion = Shell.Current.Navigation;

        while (navegacion.NavigationStack.Count > 1 &&
               navegacion.NavigationStack[^1] != destino)
        {
            await navegacion.PopAsync();
        }
    }
}
