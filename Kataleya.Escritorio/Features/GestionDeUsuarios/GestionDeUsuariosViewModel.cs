using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Cuentas;
using Kataleya.Dominio.DataObjects;
using Kataleya.Escritorio.Servicios;

namespace Kataleya.Escritorio.Features.GestionDeUsuarios;

public partial class GestionDeUsuariosViewModel : ObservableObject
{
    private readonly ClienteApi _cliente;
    private readonly IDialogos _dialogos;
    private readonly INavegacion _navegacion;
    private readonly SemaphoreSlim _turnoDeCarga = new(1, 1);
    private IReadOnlyList<DatosDeRol> _roles = [];
    private IReadOnlyList<DatosDeSede> _sedes = [];

    [ObservableProperty]
    private string mensajeDeError = string.Empty;

    [ObservableProperty]
    private string mensajeDeExito = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeOperar))]
    private bool estaCargando;

    public GestionDeUsuariosViewModel(
        ClienteApi cliente,
        IDialogos dialogos,
        INavegacion navegacion)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));
    }

    public ObservableCollection<CuentaEnLista> Cuentas { get; } = [];

    public bool PuedeOperar => !EstaCargando;

    [RelayCommand]
    public async Task CargarAsync()
    {
        await _turnoDeCarga.WaitAsync();

        EstaCargando = true;
        MensajeDeError = string.Empty;

        try
        {
            var resultado = await _cliente.ObtenerCuentasAsync();

            if (!resultado.Exito || resultado.Datos is null)
            {
                await ManejarErrorAsync(resultado);
                return;
            }

            if (_roles.Count == 0 || _sedes.Count == 0)
            {
                var cargados = await CargarCatalogosAsync();
                if (!cargados)
                {
                    return;
                }
            }

            Cuentas.Clear();
            foreach (var cuenta in resultado.Datos)
            {
                Cuentas.Add(new CuentaEnLista(cuenta));
            }
        }
        finally
        {
            EstaCargando = false;
            _turnoDeCarga.Release();
        }
    }

    [RelayCommand]
    private async Task CrearCuentaAsync()
    {
        if (!await AsegurarCatalogosAsync())
        {
            return;
        }

        MensajeDeError = string.Empty;
        MensajeDeExito = string.Empty;
        IReadOnlyDictionary<string, string>? errores = null;
        SolicitudDeCuenta? anterior = null;

        while (true)
        {
            var solicitud = await _dialogos.PedirDatosDeCuentaAsync(_roles, _sedes, errores, anterior);
            if (solicitud is null)
            {
                return;
            }

            var resultado = await _cliente.CrearCuentaAsync(solicitud);

            if (resultado.Exito && resultado.Datos is not null)
            {
                MensajeDeExito = $"Cuenta {resultado.Datos.NombreUsuario} creada.";
                await CargarAsync();
                return;
            }

            if (PuedeReintentarEnDialogo(resultado))
            {
                anterior = solicitud;
                errores = ConvertirErrores(resultado);
                continue;
            }

            await ManejarErrorAsync(resultado);
            return;
        }
    }

    [RelayCommand]
    private async Task DesactivarAsync(CuentaEnLista? cuenta)
    {
        if (cuenta is null)
        {
            return;
        }

        var confirmado = await _dialogos.ConfirmarAsync(
            "Desactivar cuenta",
            $"¿Desactivar la cuenta {cuenta.NombreUsuario}? No podrá ingresar hasta que la reactiven.",
            "Sí",
            "No");

        if (!confirmado)
        {
            return;
        }

        var resultado = await _cliente.DesactivarAsync(cuenta.Id);

        if (resultado.Exito)
        {
            MensajeDeExito = $"Cuenta {cuenta.NombreUsuario} desactivada.";
            await CargarAsync();
            return;
        }

        await ManejarErrorAsync(resultado);
    }

    [RelayCommand]
    private async Task ReactivarAsync(CuentaEnLista? cuenta)
    {
        if (cuenta is null)
        {
            return;
        }

        var resultado = await _cliente.ReactivarAsync(cuenta.Id);

        if (resultado.Exito)
        {
            MensajeDeExito = $"Cuenta {cuenta.NombreUsuario} reactivada.";
            await CargarAsync();
            return;
        }

        await ManejarErrorAsync(resultado);
    }

    [RelayCommand]
    private async Task DesbloquearAsync(CuentaEnLista? cuenta)
    {
        if (cuenta is null)
        {
            return;
        }

        var resultado = await _cliente.DesbloquearAsync(cuenta.Id);

        if (resultado.Exito)
        {
            MensajeDeExito = $"Cuenta {cuenta.NombreUsuario} desbloqueada.";
            await CargarAsync();
            return;
        }

        await ManejarErrorAsync(resultado);
    }

    [RelayCommand]
    private async Task RestablecerContrasenaAsync(CuentaEnLista? cuenta)
    {
        if (cuenta is null)
        {
            return;
        }

        MensajeDeError = string.Empty;
        MensajeDeExito = string.Empty;
        IReadOnlyDictionary<string, string>? errores = null;

        while (true)
        {
            var contrasena = await _dialogos.PedirContrasenaAsync(cuenta.NombreUsuario, errores);
            if (contrasena is null)
            {
                return;
            }

            var resultado = await _cliente.RestablecerContrasenaAsync(cuenta.Id, contrasena);

            if (resultado.Exito)
            {
                MensajeDeExito = "Contraseña restablecida. Entréguela al usuario.";
                return;
            }

            if (PuedeReintentarEnDialogo(resultado))
            {
                errores = ConvertirErrores(resultado);
                continue;
            }

            await ManejarErrorAsync(resultado);
            return;
        }
    }

    [RelayCommand]
    private Task VolverAsync() =>
        _navegacion.VolverAsync();

    private async Task<bool> AsegurarCatalogosAsync()
    {
        if (_roles.Count > 0 && _sedes.Count > 0)
        {
            return true;
        }

        return await CargarCatalogosAsync();
    }

    private async Task<bool> CargarCatalogosAsync()
    {
        var roles = await _cliente.ObtenerRolesAsync();
        var sedes = await _cliente.ObtenerSedesAsync();

        if (!roles.Exito || roles.Datos is null)
        {
            await ManejarErrorAsync(roles);
            return false;
        }

        if (!sedes.Exito || sedes.Datos is null)
        {
            await ManejarErrorAsync(sedes);
            return false;
        }

        _roles = roles.Datos;
        _sedes = sedes.Datos;
        return true;
    }

    private async Task ManejarErrorAsync(ResultadoDeLaApi resultado)
    {
        MensajeDeExito = string.Empty;

        if (resultado.SesionExpirada)
        {
            _cliente.CerrarSesion();
            MensajeDeError = "La sesión venció. Inicie sesión de nuevo.";
            await _navegacion.IrAlInicioDeSesionAsync();
            return;
        }

        MensajeDeError = MensajeDe(resultado);
    }

    private static string MensajeDe(ResultadoDeLaApi resultado)
    {
        if (resultado.SinPermiso)
        {
            return "No tiene permiso para usar esta función.";
        }

        if (resultado.CodigoDeError == "CON-01")
        {
            return "No hay conexión a internet, así que no se guardó nada. Revise el wifi de la agencia.";
        }

        if (resultado.CodigoDeError == "CON-02")
        {
            return "El servidor no responde, así que no se guardó nada. Espere unos minutos y vuelva a intentarlo.";
        }

        if (resultado.Errores.Count > 0)
        {
            return string.Join(Environment.NewLine, resultado.Errores.Select(error => error.Mensaje));
        }

        return "No se pudo completar la operación. Vuelva a intentarlo.";
    }

    private static bool PuedeReintentarEnDialogo(ResultadoDeLaApi resultado) =>
        !resultado.SesionExpirada &&
        !resultado.SinPermiso &&
        string.IsNullOrWhiteSpace(resultado.CodigoDeError) &&
        resultado.Errores.Count > 0;

    private static IReadOnlyDictionary<string, string> ConvertirErrores(
        ResultadoDeLaApi resultado) =>
        resultado.Errores
            .GroupBy(error => error.Campo)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => string.Join(Environment.NewLine, grupo.Select(error => error.Mensaje)));
}
