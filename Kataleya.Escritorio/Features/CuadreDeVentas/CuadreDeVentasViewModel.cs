using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Cuadre;
using Kataleya.Contratos.Cuentas;
using Kataleya.Escritorio.Servicios;

namespace Kataleya.Escritorio.Features.CuadreDeVentas;

public partial class CuadreDeVentasViewModel : ObservableObject
{
    private const string Todos = "Todos";
    private const string Todas = "Todas";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private readonly ClienteApi _cliente;
    private readonly IDialogos _dialogos;
    private List<DatosDeCuenta> _cuentas = [];
    private List<DatosDeSede> _sedes = [];
    private DatosDelCuadre? _cuadre;

    public CuadreDeVentasViewModel(ClienteApi cliente, IDialogos dialogos)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
    }

    public bool EsAdministrador => _cliente.EsAdministrador;

    public bool EsEncargado => !EsAdministrador;

    public string Subtitulo => EsAdministrador
        ? "Totales cobrados por emisor y por medio de pago"
        : "Sus totales cobrados, por medio de pago";

    public string Ayuda => EsAdministrador
        ? "Los pasajes anulados no suman al total. Elija un emisor y pulse «Ver detalle» para ver los pasajes y pagos que lo componen."
        : "Los pasajes anulados no suman al total. Pulse «Ver detalle» para ver los pasajes y pagos que lo componen.";

    public string MiUsuario => _cliente.CuentaActual?.NombreUsuario ?? string.Empty;

    public string MiSede => _cliente.CuentaActual?.Sede ?? string.Empty;

    public ObservableCollection<string> Emisores { get; } = [Todos];

    public ObservableCollection<string> Sedes { get; } = [Todas];

    public ObservableCollection<FilaDelCuadre> Filas { get; } = [];

    [ObservableProperty]
    private DateTime? desde = DateTime.Today;

    [ObservableProperty]
    private DateTime? hasta = DateTime.Today;

    [ObservableProperty]
    private int emisorElegido;

    [ObservableProperty]
    private int sedeElegida;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerDetalleCommand))]
    private FilaDelCuadre? filaElegida;

    [ObservableProperty]
    private string errorDeFechas = string.Empty;

    [ObservableProperty]
    private string resumen = string.Empty;

    [ObservableProperty]
    private bool sinCobros;

    public async Task CargarAsync()
    {
        OnPropertyChanged(nameof(EsAdministrador));
        OnPropertyChanged(nameof(EsEncargado));

        if (EsAdministrador && _cuentas.Count == 0)
        {
            var cuentas = await _cliente.ObtenerCuentasAsync();
            if (cuentas.Exito && cuentas.Datos is not null)
            {
                _cuentas = cuentas.Datos;
                Emisores.Clear();
                Emisores.Add(Todos);
                foreach (var cuenta in _cuentas)
                {
                    Emisores.Add(cuenta.NombreUsuario);
                }

                EmisorElegido = 0;
            }

            var sedes = await _cliente.ObtenerSedesAsync();
            if (sedes.Exito && sedes.Datos is not null)
            {
                _sedes = sedes.Datos;
                Sedes.Clear();
                Sedes.Add(Todas);
                foreach (var sede in _sedes)
                {
                    Sedes.Add(sede.Nombre);
                }

                var mia = _sedes.FindIndex(sede => sede.Nombre == MiSede);
                SedeElegida = mia >= 0 ? mia + 1 : 0;
            }
        }

        await GenerarAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task GenerarAsync()
    {
        var desde = DateOnly.FromDateTime(Desde ?? DateTime.Today);
        var hasta = DateOnly.FromDateTime(Hasta ?? DateTime.Today);

        if (desde > hasta)
        {
            ErrorDeFechas = "La fecha inicial no puede ser posterior a la final.";
            return;
        }

        ErrorDeFechas = string.Empty;

        var resultado = await _cliente.ObtenerCuadreAsync(new FiltroDeCuadre
        {
            Desde = desde,
            Hasta = hasta,
            EmisorId = EsAdministrador && EmisorElegido > 0 && EmisorElegido <= _cuentas.Count
                ? _cuentas[EmisorElegido - 1].Id
                : null,
            SedeId = EsAdministrador && SedeElegida > 0 && SedeElegida <= _sedes.Count
                ? _sedes[SedeElegida - 1].Id
                : null
        });

        if (!resultado.Exito || resultado.Datos is null)
        {
            await _dialogos.MostrarMensajeAsync(
                "No se pudo generar",
                resultado.SinPermiso
                    ? "No tiene permiso para consultar el cuadre."
                    : "No se pudo generar el cuadre de ventas.");
            return;
        }

        _cuadre = resultado.Datos;
        FilaElegida = null;
        Filas.Clear();

        foreach (var fila in _cuadre.Filas)
        {
            Filas.Add(new FilaDelCuadre(
                fila.Emisor,
                fila.Pasajes.ToString(Cultura),
                Soles(fila.Efectivo),
                Soles(fila.Yape),
                Soles(fila.AdelantosPorCobrar),
                $"{fila.Anulados} · {Soles(fila.ImporteAnulado)}",
                Soles(fila.TotalNeto)));
        }

        if (!EsAdministrador && Filas.Count == 1)
        {
            FilaElegida = Filas[0];
        }

        SinCobros = _cuadre.Filas.Sum(fila => fila.Efectivo + fila.Yape) == 0;
        Resumen =
            $"Generado el {_cuadre.GeneradoEl.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura)} por " +
            $"{_cuadre.GeneradoPor} · Sede {_cuadre.Sede}";
    }

    private bool PuedeVerDetalle() =>
        FilaElegida is not null;

    [RelayCommand(CanExecute = nameof(PuedeVerDetalle))]
    private async Task VerDetalleAsync()
    {
        if (_cuadre is null || FilaElegida is null)
        {
            return;
        }

        var pasajes = _cuadre.Pasajes
            .Where(pasaje => pasaje.Emisor == FilaElegida.Emisor)
            .ToList();

        var detalle = pasajes.Count == 0
            ? "No hay pasajes de este emisor en el periodo."
            : string.Join(
                Environment.NewLine,
                pasajes.Select(pasaje =>
                    $"P-{pasaje.Id:0000} · {pasaje.Pasajero} · {pasaje.Estado} · {Soles(pasaje.Pagado)}" +
                    (string.IsNullOrWhiteSpace(pasaje.Boleto) ? string.Empty : $" · {pasaje.Boleto}")));

        await _dialogos.MostrarMensajeAsync($"Pasajes de {FilaElegida.Emisor}", detalle);
    }

    private static string Soles(decimal importe) =>
        $"S/ {importe.ToString("N2", Cultura)}";
}

public sealed record FilaDelCuadre(
    string Emisor,
    string Pasajes,
    string Efectivo,
    string Yape,
    string Adelantos,
    string Anulados,
    string TotalNeto);
