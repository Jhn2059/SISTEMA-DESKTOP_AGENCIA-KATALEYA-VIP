using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Pasajes;
using Kataleya.Escritorio.Features.GestionDeUsuarios.CrearCuenta;
using Kataleya.Escritorio.Features.GestionDeUsuarios.RestablecerContrasena;
using Kataleya.Escritorio.Features.PanelPrincipal.ReporteAnual;
using Kataleya.Escritorio.Features.Pasajes.Dialogos;
using Microsoft.Extensions.DependencyInjection;

namespace Kataleya.Escritorio.Servicios;

public sealed class DialogosDeMaui : IDialogos
{
    private readonly IServiceProvider _servicios;

    public DialogosDeMaui(IServiceProvider servicios) =>
        _servicios = servicios ?? throw new ArgumentNullException(nameof(servicios));

    public async Task<SolicitudDeCuenta?> PedirDatosDeCuentaAsync(
        IReadOnlyList<DatosDeRol> roles,
        IReadOnlyList<DatosDeSede> sedes,
        IReadOnlyDictionary<string, string>? errores = null,
        SolicitudDeCuenta? anterior = null)
    {
        var pagina = _servicios.GetRequiredService<DialogoDeCuentaPage>();
        pagina.Modelo.Inicializar(roles, sedes, errores, anterior);
        await Shell.Current.Navigation.PushModalAsync(pagina);
        return await pagina.Modelo.Resultado;
    }

    public async Task<string?> PedirContrasenaAsync(
        string nombreUsuario,
        IReadOnlyDictionary<string, string>? errores = null)
    {
        var pagina = _servicios.GetRequiredService<DialogoDeContrasenaPage>();
        pagina.Modelo.Inicializar(nombreUsuario, errores);
        await Shell.Current.Navigation.PushModalAsync(pagina);
        return await pagina.Modelo.Resultado;
    }

    public async Task<string?> PedirContrasenaDelReporteAsync(int anio)
    {
        var pagina = _servicios.GetRequiredService<DialogoDeReporteAnualPage>();
        pagina.Modelo.Inicializar(anio);
        await Shell.Current.Navigation.PushModalAsync(pagina);
        return await pagina.Modelo.Resultado;
    }

    public async Task<SolicitudDeCobro?> PedirCobroAsync(DatosDePasaje pasaje)
    {
        var pagina = _servicios.GetRequiredService<DialogoDeCobroPage>();
        pagina.Modelo.Inicializar(pasaje);
        await Shell.Current.Navigation.PushModalAsync(pagina);
        return await pagina.Modelo.Resultado;
    }

    public async Task<SolicitudDeAnulacion?> PedirAnulacionAsync(DatosDePasaje pasaje, RevisionDeAnulacion revision)
    {
        var pagina = _servicios.GetRequiredService<DialogoDeAnulacionPage>();
        pagina.Modelo.Inicializar(pasaje, revision);
        await Shell.Current.Navigation.PushModalAsync(pagina);
        return await pagina.Modelo.Resultado;
    }

    public async Task<CambiosDelPasaje?> PedirCambiosAsync(DatosDePasaje pasaje, bool soloAsiento)
    {
        var pagina = _servicios.GetRequiredService<DialogoDeModificacionPage>();
        await Shell.Current.Navigation.PushModalAsync(pagina);
        await pagina.Modelo.InicializarAsync(pasaje, soloAsiento);
        return await pagina.Modelo.Resultado;
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
