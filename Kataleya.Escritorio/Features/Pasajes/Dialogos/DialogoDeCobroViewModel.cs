using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.Servicios;

namespace Kataleya.Escritorio.Features.Pasajes.Dialogos;

public partial class DialogoDeCobroViewModel : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private readonly INavegacion _navegacion;
    private readonly IDialogos _dialogos;
    private readonly TaskCompletionSource<SolicitudDeCobro?> _resultado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private decimal _saldo;

    public DialogoDeCobroViewModel(INavegacion navegacion, IDialogos dialogos)
    {
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
    }

    public Task<SolicitudDeCobro?> Resultado => _resultado.Task;

    public IReadOnlyList<string> Medios { get; } = ["Efectivo", "Yape"];

    [ObservableProperty]
    private string descripcion = string.Empty;

    [ObservableProperty]
    private string monto = string.Empty;

    [ObservableProperty]
    private int medioElegido;

    [ObservableProperty]
    private string errorMonto = string.Empty;

    public void Inicializar(DatosDePasaje pasaje)
    {
        ArgumentNullException.ThrowIfNull(pasaje);

        _saldo = pasaje.Saldo;
        Descripcion = $"Pasaje P-{pasaje.Id:0000} · saldo pendiente: S/ {_saldo.ToString("N2", Cultura)}.";
        Monto = _saldo.ToString("0.00", CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private async Task AceptarAsync()
    {
        var texto = Monto.Replace("S/", string.Empty).Replace(',', '.').Trim();

        if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var importe) ||
            importe <= 0)
        {
            ErrorMonto = "Escriba un monto mayor que cero.";
            return;
        }

        ErrorMonto = string.Empty;

        if (importe > _saldo)
        {
            await _dialogos.MostrarMensajeAsync(
                "[PAG-01] No se registró el cobro",
                $"El monto (S/ {importe.ToString("N2", Cultura)}) es mayor que el saldo " +
                $"(S/ {_saldo.ToString("N2", Cultura)}). Corrija el monto.");
            return;
        }

        _resultado.TrySetResult(new SolicitudDeCobro
        {
            Importe = importe,
            Medio = MedioElegido == 1 ? MedioDePago.Yape : MedioDePago.Efectivo
        });
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
