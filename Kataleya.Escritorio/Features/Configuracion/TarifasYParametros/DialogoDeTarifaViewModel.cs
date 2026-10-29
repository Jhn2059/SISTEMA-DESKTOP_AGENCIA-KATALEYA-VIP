using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Contratos.Configuracion;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.UIHelpers;

namespace Kataleya.Escritorio.Features.Configuracion.TarifasYParametros;

// Diálogo «Agregar tarifa programada» o «Editar tarifa programada» (IU-P10; CDU-06).
public partial class DialogoDeTarifaViewModel : ObservableObject
{
    private static readonly SentidoDelViaje[] Sentidos =
        [SentidoDelViaje.Ambos, SentidoDelViaje.Ida, SentidoDelViaje.Vuelta];

    private readonly INavegacion _navegacion;
    private readonly TaskCompletionSource<SolicitudDeTarifa?> _resultado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DialogoDeTarifaViewModel(INavegacion navegacion) =>
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));

    public Task<SolicitudDeTarifa?> Resultado => _resultado.Task;

    public IReadOnlyList<string> NombresDeSentidos { get; } =
        Sentidos.Select(FilaDeTarifa.TextoDelSentido).ToList();

    [ObservableProperty]
    private string titulo = "Agregar tarifa programada";

    [ObservableProperty]
    private string nombre = string.Empty;

    [ObservableProperty]
    private string importe = string.Empty;

    [ObservableProperty]
    private DateTime? desde = DateTime.Today;

    [ObservableProperty]
    private DateTime? hasta = DateTime.Today;

    [ObservableProperty]
    private int sentidoElegido;

    [ObservableProperty]
    private string errorNombre = string.Empty;

    [ObservableProperty]
    private string errorImporte = string.Empty;

    [ObservableProperty]
    private string errorFechas = string.Empty;

    [ObservableProperty]
    private string errorGeneral = string.Empty;

    public void Inicializar(DatosDeTarifa? tarifa, IReadOnlyDictionary<string, string>? errores)
    {
        Titulo = tarifa is null || tarifa.Id == 0 ? "Agregar tarifa programada" : "Editar tarifa programada";

        if (tarifa is not null)
        {
            Nombre = tarifa.Nombre;
            Importe = tarifa.Importe > 0
                ? $"S/ {tarifa.Importe.ToString("N2", CultureInfo.InvariantCulture)}"
                : string.Empty;
            Desde = tarifa.VigenciaDesde?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
            Hasta = tarifa.VigenciaHasta?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
            SentidoElegido = Math.Max(0, Array.IndexOf(Sentidos, tarifa.Sentido));
        }

        Selecciones.Refrescar(() => SentidoElegido, valor => SentidoElegido = valor);

        if (errores is null)
        {
            return;
        }

        foreach (var (campo, mensaje) in errores)
        {
            switch (campo)
            {
                case "nombre":
                    ErrorNombre = mensaje;
                    break;
                case "importe":
                    ErrorImporte = mensaje;
                    break;
                case "vigencia":
                case "vigenciaDesde":
                case "vigenciaHasta":
                    ErrorFechas = mensaje;
                    break;
                default:
                    ErrorGeneral = string.IsNullOrEmpty(ErrorGeneral) ? mensaje : $"{ErrorGeneral}{Environment.NewLine}{mensaje}";
                    break;
            }
        }
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        var texto = Importe.Replace("S/", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        var importeValido = decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) && valor > 0;
        var fechaDesde = DateOnly.FromDateTime(Desde ?? DateTime.Today);
        var fechaHasta = DateOnly.FromDateTime(Hasta ?? DateTime.Today);

        ErrorNombre = string.IsNullOrWhiteSpace(Nombre) ? "Escriba el nombre de la tarifa." : string.Empty;
        ErrorImporte = importeValido ? string.Empty : "Escriba un importe mayor que cero.";
        ErrorFechas = fechaHasta < fechaDesde ? "La fecha final no puede ser anterior a la inicial." : string.Empty;
        ErrorGeneral = string.Empty;

        if (ErrorNombre.Length > 0 || ErrorImporte.Length > 0 || ErrorFechas.Length > 0)
        {
            return;
        }

        _resultado.TrySetResult(new SolicitudDeTarifa
        {
            Tipo = TipoDeTarifa.Programada,
            Nombre = Nombre.Trim(),
            Importe = valor,
            VigenciaDesde = fechaDesde,
            VigenciaHasta = fechaHasta,
            Sentido = Sentidos[Math.Clamp(SentidoElegido, 0, Sentidos.Length - 1)]
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
