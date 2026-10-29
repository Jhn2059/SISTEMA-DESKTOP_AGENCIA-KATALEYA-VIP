using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Session;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.UIHelpers;

namespace Kataleya.Escritorio.Features.Autenticacion.InicioDeSesion;

public partial class InicioDeSesionViewModel : ObservableObject
{
    private readonly ClienteApi _cliente;
    private readonly INavegacion _navegacion;

    [ObservableProperty]
    private string nombreUsuario = string.Empty;

    [ObservableProperty]
    private string contrasena = string.Empty;

    [ObservableProperty]
    private string errorNombreUsuario = string.Empty;

    [ObservableProperty]
    private string errorContrasena = string.Empty;

    [ObservableProperty]
    private string mensajeDeError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeIngresar))]
    private bool estaCargando;

    public InicioDeSesionViewModel(ClienteApi cliente, INavegacion navegacion)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));
    }

    public bool PuedeIngresar => !EstaCargando;

    [RelayCommand]
    private void Limpiar()
    {
        NombreUsuario = string.Empty;
        Contrasena = string.Empty;
        ErrorNombreUsuario = string.Empty;
        ErrorContrasena = string.Empty;
        MensajeDeError = string.Empty;
        EstaCargando = false;
    }

    [RelayCommand]
    private async Task IngresarAsync()
    {
        ErrorNombreUsuario = string.Empty;
        ErrorContrasena = string.Empty;
        MensajeDeError = string.Empty;

        if (string.IsNullOrWhiteSpace(NombreUsuario))
        {
            ErrorNombreUsuario = "Escriba su usuario.";
        }

        if (string.IsNullOrEmpty(Contrasena))
        {
            ErrorContrasena = "Escriba su contraseña.";
        }

        if (!string.IsNullOrWhiteSpace(ErrorNombreUsuario) ||
            !string.IsNullOrWhiteSpace(ErrorContrasena))
        {
            return;
        }

        EstaCargando = true;

        try
        {
            var resultado = await _cliente.IniciarSesionAsync(
                NombreUsuario.Trim(),
                Contrasena);

            if (resultado.Cuenta is not null)
            {
                ServicioDeTema.AplicarDeLaCuenta(resultado.Cuenta.Tema);
                await _navegacion.IrAlPanelAsync();
                return;
            }

            MensajeDeError = MensajeDe(resultado);
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private static string MensajeDe(ResultadoDeInicioDeSesion resultado) =>
        resultado.Rechazo switch
        {
            MotivoDeRechazo.DatosIncorrectos =>
                "Usuario o contraseña incorrectos. Revise los datos y vuelva a intentarlo; después de 5 intentos fallidos, la cuenta se bloquea 3 minutos.",
            MotivoDeRechazo.CuentaBloqueada =>
                $"La cuenta está bloqueada hasta las {HoraDe(resultado.FinBloqueo)} por 5 intentos fallidos. Espere a esa hora o pida a un Administrador que la desbloquee.",
            MotivoDeRechazo.CuentaDesactivada =>
                "Esta cuenta está desactivada. Si debe usar el sistema, pida a un Administrador que la reactive.",
            MotivoDeRechazo.SinInternet =>
                "No hay conexión a internet, así que no se pudieron comprobar sus datos. Revise el wifi de la agencia; mientras tanto, emita con talonario y regístrelo después en «Registro diferido».",
            MotivoDeRechazo.ServidorNoResponde =>
                "El servidor no responde, así que no se pudieron comprobar sus datos. Espere unos minutos y vuelva a intentarlo; si no vuelve, emita con talonario y avise al equipo de soporte.",
            _ => "No se pudo iniciar sesión. Vuelva a intentarlo."
        };

    private static string HoraDe(DateTime? fecha) =>
        fecha?.ToLocalTime().ToString("HH:mm") ?? "la hora indicada";
}
