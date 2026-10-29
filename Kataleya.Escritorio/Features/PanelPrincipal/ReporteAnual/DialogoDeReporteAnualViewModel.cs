using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Escritorio.Servicios;

namespace Kataleya.Escritorio.Features.PanelPrincipal.ReporteAnual;

public partial class DialogoDeReporteAnualViewModel : ObservableObject
{
    private readonly INavegacion _navegacion;
    private readonly TaskCompletionSource<string?> _resultado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    [ObservableProperty]
    private int anio;

    [ObservableProperty]
    private string contrasena = string.Empty;

    [ObservableProperty]
    private string errorContrasena = string.Empty;

    public DialogoDeReporteAnualViewModel(INavegacion navegacion) =>
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));

    public Task<string?> Resultado => _resultado.Task;

    public string Titulo => $"Generar el reporte anual de {Anio}";

    public string Explicacion =>
        $"Se generará el PDF con los pasajes de {Anio}, sus pagos y su historia, protegido con la " +
        "contraseña que escriba, y se guardará en la carpeta de Google Drive del respaldo. " +
        "Después se eliminarán esos pasajes del sistema.";

    public void Inicializar(int anio) =>
        Anio = anio;

    partial void OnAnioChanged(int value)
    {
        OnPropertyChanged(nameof(Titulo));
        OnPropertyChanged(nameof(Explicacion));
    }

    // RF-32: no se exige un mínimo de caracteres.
    [RelayCommand]
    private async Task AceptarAsync()
    {
        if (string.IsNullOrEmpty(Contrasena))
        {
            ErrorContrasena = "Escriba una contraseña para el PDF.";
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
