using Kataleya.Contratos.Cuentas;
using Kataleya.Escritorio.Paginas.Dialogos;

namespace Kataleya.Escritorio.Servicios;

public sealed class DialogosDeMaui : IDialogos
{
    public async Task<SolicitudDeCuenta?> PedirDatosDeCuentaAsync(
        IReadOnlyList<DatosDeRol> roles,
        IReadOnlyList<DatosDeSede> sedes,
        IReadOnlyDictionary<string, string>? errores = null,
        SolicitudDeCuenta? anterior = null)
    {
        var pagina = new DialogoDeCuentaPage(roles, sedes, errores, anterior);
        await Shell.Current.Navigation.PushModalAsync(pagina);
        return await pagina.Resultado;
    }

    public async Task<string?> PedirContrasenaAsync(
        string nombreUsuario,
        IReadOnlyDictionary<string, string>? errores = null)
    {
        var pagina = new DialogoDeContrasenaPage(nombreUsuario, errores);
        await Shell.Current.Navigation.PushModalAsync(pagina);
        return await pagina.Resultado;
    }

    public Task<bool> ConfirmarAsync(
        string titulo,
        string mensaje,
        string textoAceptar,
        string textoCancelar) =>
        Shell.Current.CurrentPage.DisplayAlertAsync(
            titulo,
            mensaje,
            textoAceptar,
            textoCancelar);

    public Task MostrarMensajeAsync(string titulo, string mensaje) =>
        Shell.Current.CurrentPage.DisplayAlertAsync(titulo, mensaje, "Aceptar");
}
