using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Contratos.Pasajes;
using Kataleya.Escritorio.Servicios;

namespace Kataleya.Escritorio.Features.Pasajes.Dialogos;

public partial class DialogoDeAnulacionViewModel : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private readonly INavegacion _navegacion;
    private readonly TaskCompletionSource<SolicitudDeAnulacion?> _resultado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DialogoDeAnulacionViewModel(INavegacion navegacion) =>
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));

    public Task<SolicitudDeAnulacion?> Resultado => _resultado.Task;

    [ObservableProperty]
    private string titulo = string.Empty;

    [ObservableProperty]
    private string avisoDeRetencion = string.Empty;

    [ObservableProperty]
    private string avisoDeDevolucion = string.Empty;

    [ObservableProperty]
    private bool puedeDevolverComoExcepcion;

    [ObservableProperty]
    private string textoDeLaExcepcion = string.Empty;

    [ObservableProperty]
    private bool devolverComoExcepcion;

    [ObservableProperty]
    private string motivo = string.Empty;

    [ObservableProperty]
    private string errorMotivo = string.Empty;

    // RF-04 y CDU-04 A1: con menos horas de aviso, el adelanto queda retenido salvo que se devuelva como excepción.
    public void Inicializar(DatosDePasaje pasaje, RevisionDeAnulacion revision)
    {
        ArgumentNullException.ThrowIfNull(pasaje);
        ArgumentNullException.ThrowIfNull(revision);

        Titulo = $"¿Anular el pasaje P-{pasaje.Id:0000}?";
        var pagado = pasaje.Pagado.ToString("N2", Cultura);
        var retiene = pasaje.Pagado > 0 && !revision.SeDevuelveElAdelanto;

        AvisoDeRetencion = retiene
            ? $"Faltan menos de {revision.HorasDeAviso} horas: el adelanto queda retenido. " +
              "Puede devolverlo solo como excepción, con su motivo."
            : string.Empty;
        AvisoDeDevolucion = revision.SeDevuelveElAdelanto
            ? $"Se devolverán S/ {revision.ImporteADevolver.ToString("N2", Cultura)} al pasajero."
            : string.Empty;
        PuedeDevolverComoExcepcion = retiene;
        TextoDeLaExcepcion = $"Devolver el adelanto (S/ {pagado}) como excepción";
    }

    [RelayCommand]
    private async Task AceptarAsync()
    {
        if (string.IsNullOrWhiteSpace(Motivo))
        {
            ErrorMotivo = "Escriba el motivo de la anulación.";
            return;
        }

        ErrorMotivo = string.Empty;
        _resultado.TrySetResult(new SolicitudDeAnulacion
        {
            Motivo = Motivo.Trim(),
            DevolverComoExcepcion = PuedeDevolverComoExcepcion && DevolverComoExcepcion
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
