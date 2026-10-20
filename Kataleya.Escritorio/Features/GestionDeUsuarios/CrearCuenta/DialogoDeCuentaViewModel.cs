using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Contratos.Cuentas;
using Kataleya.Escritorio.Servicios;

namespace Kataleya.Escritorio.Features.GestionDeUsuarios.CrearCuenta;

public partial class DialogoDeCuentaViewModel : ObservableObject
{
    private readonly INavegacion _navegacion;
    private readonly TaskCompletionSource<SolicitudDeCuenta?> _resultado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    [ObservableProperty]
    private string nombre = string.Empty;

    [ObservableProperty]
    private string nombreUsuario = string.Empty;

    [ObservableProperty]
    private string contrasena = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<DatosDeRol> roles = [];

    [ObservableProperty]
    private IReadOnlyList<DatosDeSede> sedes = [];

    [ObservableProperty]
    private DatosDeRol? rolSeleccionado;

    [ObservableProperty]
    private DatosDeSede? sedeSeleccionada;

    [ObservableProperty]
    private string errorNombre = string.Empty;

    [ObservableProperty]
    private string errorNombreUsuario = string.Empty;

    [ObservableProperty]
    private string errorContrasena = string.Empty;

    [ObservableProperty]
    private string errorRol = string.Empty;

    [ObservableProperty]
    private string errorSede = string.Empty;

    [ObservableProperty]
    private string errorGeneral = string.Empty;

    public DialogoDeCuentaViewModel(INavegacion navegacion) =>
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));

    public Task<SolicitudDeCuenta?> Resultado => _resultado.Task;

    public void Inicializar(
        IReadOnlyList<DatosDeRol> roles,
        IReadOnlyList<DatosDeSede> sedes,
        IReadOnlyDictionary<string, string>? errores = null,
        SolicitudDeCuenta? anterior = null)
    {
        Roles = roles ?? throw new ArgumentNullException(nameof(roles));
        Sedes = sedes ?? throw new ArgumentNullException(nameof(sedes));

        Nombre = anterior?.Nombre ?? string.Empty;
        NombreUsuario = anterior?.NombreUsuario ?? string.Empty;
        Contrasena = anterior?.ContrasenaInicial ?? string.Empty;
        RolSeleccionado = anterior is null
            ? null
            : Roles.FirstOrDefault(rol => rol.Id == anterior.IdRol);
        SedeSeleccionada = anterior is null
            ? null
            : Sedes.FirstOrDefault(sede => sede.Id == anterior.IdSede);

        LimpiarErrores();
        AplicarErrores(errores);
    }

    [RelayCommand]
    private async Task CrearAsync()
    {
        LimpiarErrores();

        var errores = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(Nombre))
        {
            errores["nombre"] = "Escriba el nombre completo.";
        }

        if (string.IsNullOrWhiteSpace(NombreUsuario))
        {
            errores["nombreUsuario"] = "Escriba el nombre de usuario.";
        }

        if (string.IsNullOrEmpty(Contrasena))
        {
            errores["contrasenaInicial"] = "Escriba la contraseña inicial.";
        }
        else if (Contrasena.Length < 8)
        {
            errores["contrasenaInicial"] = "La contraseña debe tener al menos 8 caracteres.";
        }

        if (RolSeleccionado is null)
        {
            errores["idRol"] = "Seleccione un rol.";
        }

        if (SedeSeleccionada is null)
        {
            errores["idSede"] = "Seleccione una sede.";
        }

        if (errores.Count > 0)
        {
            AplicarErrores(errores);
            return;
        }

        var solicitud = new SolicitudDeCuenta
        {
            Nombre = Nombre.Trim(),
            NombreUsuario = NombreUsuario.Trim(),
            ContrasenaInicial = Contrasena,
            IdRol = RolSeleccionado!.Id,
            IdSede = SedeSeleccionada!.Id
        };

        _resultado.TrySetResult(solicitud);
        await _navegacion.CerrarModalAsync();
    }

    [RelayCommand]
    private async Task CancelarAsync()
    {
        _resultado.TrySetResult(null);
        await _navegacion.CerrarModalAsync();
    }

    [RelayCommand]
    private void AlDesaparecer() =>
        _resultado.TrySetResult(null);

    private void LimpiarErrores()
    {
        ErrorNombre = string.Empty;
        ErrorNombreUsuario = string.Empty;
        ErrorContrasena = string.Empty;
        ErrorRol = string.Empty;
        ErrorSede = string.Empty;
        ErrorGeneral = string.Empty;
    }

    private void AplicarErrores(IReadOnlyDictionary<string, string>? errores)
    {
        ErrorNombre = ObtenerError(errores, "nombre");
        ErrorNombreUsuario = ObtenerError(errores, "nombreUsuario");
        ErrorContrasena = ObtenerError(errores, "contrasenaInicial");
        ErrorRol = ObtenerError(errores, "idRol");
        ErrorSede = ObtenerError(errores, "idSede");

        var conocidos = new[]
        {
            "nombre", "nombreUsuario", "contrasenaInicial", "idRol", "idSede"
        };

        ErrorGeneral = errores is null
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                errores
                    .Where(error => !conocidos.Contains(error.Key))
                    .Select(error => error.Value)
                    .Distinct());
    }

    private static string ObtenerError(
        IReadOnlyDictionary<string, string>? errores,
        string campo) =>
        errores is not null && errores.TryGetValue(campo, out var mensaje)
            ? mensaje
            : string.Empty;
}
