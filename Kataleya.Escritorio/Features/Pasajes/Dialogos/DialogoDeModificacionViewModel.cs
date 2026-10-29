using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.Entities;
using Kataleya.Escritorio.Servicios;

namespace Kataleya.Escritorio.Features.Pasajes.Dialogos;

// CDU-03 (modificar) y CDU-10 (cambiar el asiento) desde el detalle del pasaje.
public partial class DialogoDeModificacionViewModel : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string[] HorasPredeterminadas = ["06:00", "10:00", "13:00", "15:00", "20:00"];

    private readonly INavegacion _navegacion;
    private readonly ClienteApi _cliente;
    private readonly TaskCompletionSource<CambiosDelPasaje?> _resultado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private List<DatosDeUnidad> _unidades = [];
    private List<short> _asientos = [];
    private DatosDePasaje _pasaje = new();
    private bool _cargando;

    public DialogoDeModificacionViewModel(INavegacion navegacion, ClienteApi cliente)
    {
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
    }

    public Task<CambiosDelPasaje?> Resultado => _resultado.Task;

    public ObservableCollection<string> Horarios { get; } = [];

    public ObservableCollection<string> Unidades { get; } = [];

    public ObservableCollection<string> Asientos { get; } = [];

    [ObservableProperty]
    private string titulo = string.Empty;

    [ObservableProperty]
    private bool puedeCambiarLaSalida = true;

    [ObservableProperty]
    private DateTime? fecha;

    [ObservableProperty]
    private int horarioElegido = -1;

    [ObservableProperty]
    private int unidadElegida = -1;

    [ObservableProperty]
    private int asientoElegido = -1;

    [ObservableProperty]
    private string lugarDeEmbarque = string.Empty;

    [ObservableProperty]
    private string telefono = string.Empty;

    [ObservableProperty]
    private string aviso = string.Empty;

    [ObservableProperty]
    private string error = string.Empty;

    public async Task InicializarAsync(DatosDePasaje pasaje, bool soloAsiento)
    {
        ArgumentNullException.ThrowIfNull(pasaje);

        _cargando = true;
        _pasaje = pasaje;
        Titulo = soloAsiento ? "Cambiar el asiento" : $"Modificar el pasaje P-{pasaje.Id:0000}";
        PuedeCambiarLaSalida = !soloAsiento;
        Fecha = pasaje.FechaSalida.ToDateTime(TimeOnly.MinValue);
        LugarDeEmbarque = pasaje.LugarDeEmbarque;
        Telefono = pasaje.Telefono ?? string.Empty;

        var parametros = await _cliente.ObtenerParametrosAsync();
        var horas = parametros.Datos?
            .FirstOrDefault(parametro => parametro.Nombre == "horarios_salida")?.Valor
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? HorasPredeterminadas;
        var horaActual = pasaje.HoraSalida.ToString("HH:mm", Cultura);

        Horarios.Clear();
        foreach (var hora in horas.Append(horaActual).Distinct().Order())
        {
            Horarios.Add(hora);
        }

        HorarioElegido = Horarios.IndexOf(horaActual);

        var horasDeAviso = int.TryParse(
            parametros.Datos?.FirstOrDefault(parametro => parametro.Nombre == "aviso_previo_horas")?.Valor,
            out var valor)
            ? valor
            : 3;
        var faltan = pasaje.FechaSalida.ToDateTime(pasaje.HoraSalida) - DateTime.Now;

        Aviso = faltan >= TimeSpan.Zero && faltan < TimeSpan.FromHours(horasDeAviso)
            ? $"Faltan menos de {horasDeAviso} horas para la salida: solo puede cambiarse a una salida " +
              "con asiento libre, y puede registrar un cobro adicional."
            : string.Empty;

        _cargando = false;
        await CargarUnidadesAsync();
    }

    partial void OnFechaChanged(DateTime? value)
    {
        if (!_cargando)
        {
            _ = CargarUnidadesAsync();
        }
    }

    partial void OnHorarioElegidoChanged(int value)
    {
        if (!_cargando)
        {
            _ = CargarUnidadesAsync();
        }
    }

    partial void OnUnidadElegidaChanged(int value)
    {
        if (!_cargando)
        {
            MostrarAsientos();
        }
    }

    private async Task CargarUnidadesAsync()
    {
        if (Ruta.SentidoSegunElOrigen(_pasaje.Origen) is not { } sentido ||
            HorarioElegido < 0 ||
            !TimeOnly.TryParse(Horarios[HorarioElegido], Cultura, out var hora))
        {
            return;
        }

        var fecha = DateOnly.FromDateTime(Fecha ?? DateTime.Today);
        var resultado = await _cliente.ObtenerUnidadesAsync(fecha, hora, sentido);

        _cargando = true;
        _unidades = resultado.Datos ?? [];
        Unidades.Clear();
        foreach (var unidad in _unidades)
        {
            Unidades.Add($"Unidad {unidad.Numero} · {unidad.TipoVehiculo} de {unidad.NumeroAsientos}");
        }

        var actual = _unidades.FindIndex(unidad => unidad.Id == _pasaje.UnidadSalidaId);
        UnidadElegida = actual >= 0 ? actual : _unidades.Count > 0 ? 0 : -1;
        _cargando = false;

        Error = _unidades.Count == 0
            ? "No hay unidades abiertas para esa salida. Ábrala desde el registro de pasaje."
            : string.Empty;
        MostrarAsientos();
    }

    private void MostrarAsientos()
    {
        Asientos.Clear();
        _asientos = [];

        if (UnidadElegida < 0 || UnidadElegida >= _unidades.Count)
        {
            return;
        }

        var unidad = _unidades[UnidadElegida];
        var esLaActual = unidad.Id == _pasaje.UnidadSalidaId;

        for (short numero = 1; numero <= unidad.NumeroAsientos; numero++)
        {
            if (!unidad.AsientosOcupados.Contains(numero) || (esLaActual && numero == _pasaje.NumeroAsiento))
            {
                _asientos.Add(numero);
                Asientos.Add(numero == _pasaje.NumeroAsiento && esLaActual
                    ? $"{numero} (el actual)"
                    : numero.ToString(Cultura));
            }
        }

        AsientoElegido = esLaActual && _pasaje.NumeroAsiento is short asiento
            ? _asientos.IndexOf(asiento)
            : -1;
    }

    [RelayCommand]
    private async Task AceptarAsync()
    {
        if (UnidadElegida < 0 || AsientoElegido < 0 || AsientoElegido >= _asientos.Count)
        {
            Error = "Elija la unidad y un asiento libre.";
            return;
        }

        if (string.IsNullOrWhiteSpace(LugarDeEmbarque))
        {
            Error = "Escriba el lugar de embarque.";
            return;
        }

        Error = string.Empty;
        _resultado.TrySetResult(new CambiosDelPasaje
        {
            FechaSalida = DateOnly.FromDateTime(Fecha ?? DateTime.Today),
            HoraSalida = TimeOnly.Parse(Horarios[HorarioElegido], Cultura),
            UnidadSalidaId = _unidades[UnidadElegida].Id,
            NumeroAsiento = _asientos[AsientoElegido],
            LugarDeEmbarque = LugarDeEmbarque.Trim(),
            Telefono = Telefono.Trim()
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
