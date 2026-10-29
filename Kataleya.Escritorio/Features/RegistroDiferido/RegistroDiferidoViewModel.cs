using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Configuracion;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.Features.Pasajes;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.RegistroDiferido;

// CDU-08: pasajes que se emitieron con talonario mientras el sistema no estaba disponible (RF-27).
public partial class RegistroDiferidoViewModel : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string[] HorasPredeterminadas = ["06:00", "10:00", "13:00", "15:00", "20:00"];

    private readonly ClienteApi _cliente;
    private readonly IDialogos _dialogos;
    private List<DatosDeTipoDeVehiculo> _tipos = [];
    private List<(long Unidad, short Asiento)> _lugares = [];
    private bool _cargado;
    private decimal _importe;

    public RegistroDiferidoViewModel(ClienteApi cliente, IDialogos dialogos)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
    }

    public IReadOnlyList<string> TiposDeDocumento { get; } = ["DNI", "Pasaporte"];

    public IReadOnlyList<string> Medios { get; } = ["Efectivo", "Yape"];

    public ObservableCollection<string> Horarios { get; } = [];

    public ObservableCollection<string> Lugares { get; } = [];

    public string Origen => _cliente.CuentaActual?.Sede ?? string.Empty;

    [ObservableProperty]
    private string numeroTalonario = string.Empty;

    [ObservableProperty]
    private DateTime? fechaDeEmision = DateTime.Today;

    [ObservableProperty]
    private TimeSpan? horaDeEmision = new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, 0);

    [ObservableProperty]
    private string nombre = string.Empty;

    [ObservableProperty]
    private int tipoDeDocumento;

    [ObservableProperty]
    private string numeroDocumento = string.Empty;

    [ObservableProperty]
    private string telefono = string.Empty;

    [ObservableProperty]
    private string destino = string.Empty;

    [ObservableProperty]
    private DateTime? fechaSalida = DateTime.Today;

    [ObservableProperty]
    private int horarioElegido = -1;

    [ObservableProperty]
    private string lugarDeEmbarque = "Agencia";

    [ObservableProperty]
    private int lugarElegido = -1;

    [ObservableProperty]
    private int medioElegido;

    [ObservableProperty]
    private string importe = "S/ 0.00";

    [ObservableProperty]
    private string errorTalonario = string.Empty;

    [ObservableProperty]
    private string errorEmision = string.Empty;

    [ObservableProperty]
    private string errorNombre = string.Empty;

    [ObservableProperty]
    private string errorDocumento = string.Empty;

    [ObservableProperty]
    private string errorTelefono = string.Empty;

    [ObservableProperty]
    private string errorDestino = string.Empty;

    [ObservableProperty]
    private string errorEmbarque = string.Empty;

    [ObservableProperty]
    private string errorAsiento = string.Empty;

    [ObservableProperty]
    private string mensajeDeExito = string.Empty;

    public async Task CargarAsync()
    {
        OnPropertyChanged(nameof(Origen));

        if (_cargado)
        {
            return;
        }

        var tipos = await _cliente.ObtenerTiposDeVehiculoAsync();
        _tipos = tipos.Datos ?? [];

        var parametros = await _cliente.ObtenerParametrosAsync();
        var horas = parametros.Datos?
            .FirstOrDefault(parametro => parametro.Nombre == "horarios_salida")?.Valor
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? HorasPredeterminadas;

        Horarios.Clear();
        foreach (var hora in horas)
        {
            Horarios.Add(hora);
        }

        _cargado = true;
        HorarioElegido = Horarios.Count > 0 ? 0 : -1;
        await ActualizarSalidaAsync();
    }

    partial void OnFechaSalidaChanged(DateTime? value) =>
        _ = ActualizarSalidaAsync();

    partial void OnHorarioElegidoChanged(int value) =>
        _ = ActualizarSalidaAsync();

    private bool LeerSalida(out DateOnly fecha, out TimeOnly hora)
    {
        fecha = DateOnly.FromDateTime(FechaSalida ?? DateTime.Today);
        hora = default;

        return _cargado &&
               HorarioElegido >= 0 &&
               HorarioElegido < Horarios.Count &&
               TimeOnly.TryParse(Horarios[HorarioElegido], Cultura, out hora);
    }

    // El origen es la sede de quien registra; de él sale el sentido de la unidad y de la tarifa.
    private async Task ActualizarSalidaAsync()
    {
        if (!LeerSalida(out var fecha, out var hora) || Ruta.SentidoSegunElOrigen(Origen) is not { } sentido)
        {
            return;
        }

        var tarifa = await _cliente.ObtenerTarifaVigenteAsync(fecha, sentido);
        _importe = tarifa.Exito && tarifa.Datos is not null ? tarifa.Datos.Importe : 0;
        Importe = _importe > 0 ? $"S/ {_importe.ToString("N2", Cultura)}" : "Sin tarifa vigente";

        var unidades = await _cliente.ObtenerUnidadesAsync(fecha, hora, sentido);

        _lugares = [];
        Lugares.Clear();
        foreach (var unidad in unidades.Datos ?? [])
        {
            for (short asiento = 1; asiento <= unidad.NumeroAsientos; asiento++)
            {
                if (!unidad.AsientosOcupados.Contains(asiento))
                {
                    _lugares.Add((unidad.Id, asiento));
                    Lugares.Add($"Unidad {unidad.Numero} · asiento {asiento}");
                }
            }
        }

        LugarElegido = Lugares.Count > 0 ? 0 : -1;
        ErrorAsiento = Lugares.Count == 0
            ? "No hay asientos libres en las unidades de esa salida. Use «Abrir otra unidad»."
            : string.Empty;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AbrirUnidadAsync()
    {
        if (!LeerSalida(out var fecha, out var hora) || Ruta.SentidoSegunElOrigen(Origen) is not { } sentido)
        {
            return;
        }

        if (_tipos.Count == 0)
        {
            await _dialogos.MostrarMensajeAsync("Sin tipos", "No hay tipos de vehículo configurados.");
            return;
        }

        var opciones = _tipos.Select(tipo => $"{tipo.Nombre} ({tipo.NumeroAsientos})").ToArray();
        var elegido = await Shell.Current.CurrentPage.DisplayActionSheetAsync(
            "Tipo de vehículo", "Cancelar", null, opciones);
        var indice = Array.IndexOf(opciones, elegido);

        if (indice < 0)
        {
            return;
        }

        var resultado = await _cliente.AbrirUnidadAsync(new SolicitudDeUnidad
        {
            TipoVehiculoId = _tipos[indice].Id,
            Fecha = fecha,
            Hora = hora,
            Sentido = sentido
        });

        if (!resultado.Exito)
        {
            await _dialogos.MostrarMensajeAsync("No se pudo abrir la unidad", Detalle(resultado));
            return;
        }

        await ActualizarSalidaAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RegistrarAsync()
    {
        MensajeDeExito = string.Empty;
        var emision = (FechaDeEmision ?? DateTime.Today).Date + (HoraDeEmision ?? TimeSpan.Zero);
        var tipo = TipoDeDocumento == 1 ? TipoDocumento.Pasaporte : TipoDocumento.DNI;

        ErrorTalonario = string.IsNullOrWhiteSpace(NumeroTalonario) ? "Escriba el número de boleto del talonario." : string.Empty;
        ErrorEmision = emision > DateTime.Now ? "La fecha de emisión no puede ser posterior a este momento." : string.Empty;
        ErrorNombre = ValidacionDelPasajero.Nombre(Nombre);
        ErrorDocumento = ValidacionDelPasajero.Documento(tipo, NumeroDocumento);
        ErrorTelefono = ValidacionDelPasajero.Celular(Telefono);
        ErrorDestino = string.IsNullOrWhiteSpace(Destino) ? "Escriba el destino." : string.Empty;
        ErrorEmbarque = string.IsNullOrWhiteSpace(LugarDeEmbarque) ? "Escriba el lugar de embarque." : string.Empty;
        ErrorAsiento = LugarElegido < 0 || LugarElegido >= _lugares.Count ? "Elija la unidad y el asiento." : string.Empty;

        if (new[] { ErrorTalonario, ErrorEmision, ErrorNombre, ErrorDocumento, ErrorTelefono, ErrorDestino, ErrorEmbarque, ErrorAsiento }
            .Any(error => error.Length > 0) ||
            !LeerSalida(out var fecha, out var hora))
        {
            return;
        }

        var lugar = _lugares[LugarElegido];
        var resultado = await _cliente.RegistrarDiferidoAsync(new SolicitudDePasaje
        {
            PasajeroNombre = Nombre.Trim(),
            TipoDocumento = tipo,
            NumeroDocumento = NumeroDocumento.Trim(),
            Telefono = Telefono.Replace(" ", string.Empty),
            Origen = Origen,
            Destino = Destino.Trim(),
            LugarDeEmbarque = LugarDeEmbarque.Trim(),
            FechaSalida = fecha,
            HoraSalida = hora,
            UnidadSalidaId = lugar.Unidad,
            NumeroAsiento = lugar.Asiento,
            Monto = _importe,
            Medio = MedioElegido == 1 ? MedioDePago.Yape : MedioDePago.Efectivo,
            NumeroTalonario = NumeroTalonario.Trim(),
            HoraEmisionReal = DateTime.SpecifyKind(emision, DateTimeKind.Local)
        });

        if (!resultado.Exito)
        {
            if (!MostrarErroresDeCampo(resultado))
            {
                await _dialogos.MostrarMensajeAsync("No se registró el pasaje", Detalle(resultado));
            }

            await ActualizarSalidaAsync();
            return;
        }

        MensajeDeExito = "Pasaje diferido registrado. No se imprime boleto: el pasajero tiene el del talonario.";
        NumeroTalonario = string.Empty;
        Nombre = string.Empty;
        NumeroDocumento = string.Empty;
        Telefono = string.Empty;
        await ActualizarSalidaAsync();
    }

    [RelayCommand]
    private Task CancelarAsync() =>
        NavegacionDeModulos.IrAsync("panel");

    private bool MostrarErroresDeCampo(ResultadoDeLaApi resultado)
    {
        var mostrado = false;

        foreach (var error in resultado.Errores)
        {
            mostrado = true;
            switch (error.Campo)
            {
                case "numeroTalonario":
                    ErrorTalonario = error.Mensaje;
                    break;
                case "horaEmisionReal":
                    ErrorEmision = error.Mensaje;
                    break;
                case "pasajeroNombre":
                    ErrorNombre = error.Mensaje;
                    break;
                case "numeroDocumento":
                    ErrorDocumento = error.Mensaje;
                    break;
                case "unidadSalidaId":
                case "numeroAsiento":
                    ErrorAsiento = error.Mensaje;
                    break;
                default:
                    mostrado = false;
                    break;
            }
        }

        return mostrado;
    }

    private static string Detalle(ResultadoDeLaApi resultado) =>
        resultado.SinPermiso
            ? "No tiene permiso para esta operación."
            : resultado.Errores.Count > 0
                ? string.Join(Environment.NewLine, resultado.Errores.Select(error => error.Mensaje))
                : "No se pudo completar la operación.";
}
