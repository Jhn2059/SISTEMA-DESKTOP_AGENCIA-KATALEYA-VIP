using Kataleya.Escritorio.Features.AcercaDelSistema;
using Microsoft.Extensions.DependencyInjection;

namespace Kataleya.Escritorio.UIHelpers;

// Menú del bloque del usuario, en la barra superior: «Acerca del sistema» y «Cerrar sesión» (IS-01).
public static class MenuDeLaCuenta
{
    private const string Acerca = "Acerca del sistema";
    private const string CerrarSesion = "Cerrar sesión";

    public static async Task AbrirAsync(Func<Task>? cerrarSesion)
    {
        var pagina = Shell.Current?.CurrentPage;
        if (pagina is null)
        {
            return;
        }

        var opcion = await pagina.DisplayActionSheetAsync("Cuenta", "Cancelar", null, Acerca, CerrarSesion);

        if (opcion == Acerca)
        {
            var acerca = IPlatformApplication.Current!.Services.GetRequiredService<AcercaDelSistemaPage>();
            await pagina.Navigation.PushModalAsync(acerca);
        }
        else if (opcion == CerrarSesion && cerrarSesion is not null)
        {
            await cerrarSesion();
        }
    }
}
