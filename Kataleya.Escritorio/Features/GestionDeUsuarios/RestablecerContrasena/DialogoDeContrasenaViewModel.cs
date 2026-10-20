using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Escritorio.Servicios;

namespace Kataleya.Escritorio.Features.GestionDeUsuarios.RestablecerContrasena;

public partial class DialogoDeContrasenaViewModel : ObservableObject
{
    private readonly INavegacion _navegacion;
    private readonly TaskCompletionSource<string?> _resultado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    [ObservableProperty]
    private string nombreUsuario = string.Empty;

    [ObservableProperty]
    private string contrasena = string.Empty;

    [ObservableProperty]
    private string errorContrasena = string.Empty;

    public DialogoDeContrasenaViewModel(INavegacion navegacion) =>
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));

    public Task<string?> Resultado => _resultado.Task;

    public void Inicializar(
        string nombreUsuario,
        IReadOnlyDictionary<string, string>? errores = null)
    {
        NombreUsuario = nombreUsuario;
        ErrorContrasena = errores is not null &&
            errores.TryGetValue("contrasenaNueva", out var mensaje)
                ? mensaje
                : string.Empty;
    }

    [RelayCommand]
    private async Task AceptarAsync()
    {
        if (string.IsNullOrEmpty(Contrasena) || Contrasena.Length < 8)
        {
            ErrorContrasena = "La contraseña debe tener al menos 8 caracteres.";
            return;
        }

        ErrorContrasena = string.Empty;
        _resultado.TrySetResult(Contrasena);
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
}
