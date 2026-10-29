using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Configuracion;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.Servicios;

namespace Kataleya.Escritorio.Features.Configuracion.TarifasYParametros;

// Pestaña «Tarifas y parámetros» de Configuración (IU-P10; CDU-06 y CDU-16).
public partial class TarifasYParametrosViewModel : ObservableObject
{
    private const string ParametroDeHorarios = "horarios_salida";
    private const string ParametroDePlazo = "plazo_maximo_reserva_dias";
    private const string ParametroDeAviso = "aviso_previo_horas";
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string[] FormatosDeHora = ["H:mm", "HH:mm"];

    private readonly ClienteApi _cliente;
    private readonly IDialogos _dialogos;
    private readonly INavegacion _navegacion;
    private decimal? _importeIda;
    private decimal? _importeVuelta;
    private string _plazoGuardado = string.Empty;
    private string _avisoGuardado = string.Empty;

    public TarifasYParametrosViewModel(
        ClienteApi cliente,
        IDialogos dialogos,
        INavegacion navegacion)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));
    }

    public ObservableCollection<FilaDeTarifa> TarifasProgramadas { get; } = [];

    public ObservableCollection<HorarioVisible> HorariosDeSalida { get; } = [];

    public ObservableCollection<string> TiposDeVehiculo { get; } = [];

    public bool NoHayTarifasProgramadas => TarifasProgramadas.Count == 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarTarifaIdaCommand))]
    private string tarifaIda = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarTarifaVueltaCommand))]
    private string tarifaVuelta = string.Empty;

    [ObservableProperty]
    private string errorTarifaIda = string.Empty;

    [ObservableProperty]
    private string errorTarifaVuelta = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditarTarifaCommand))]
    [NotifyCanExecuteChangedFor(nameof(QuitarTarifaCommand))]
    private FilaDeTarifa? tarifaSeleccionada;

    [ObservableProperty]
    private string plazoDeReserva = string.Empty;

    [ObservableProperty]
    private string avisoPrevioEnHoras = string.Empty;

    [ObservableProperty]
    private string errorPlazo = string.Empty;

    [ObservableProperty]
    private string errorAviso = string.Empty;

    [ObservableProperty]
    private string nuevoHorario = string.Empty;

    [ObservableProperty]
    private string errorHorario = string.Empty;

    [ObservableProperty]
    private string mensajeDeExito = string.Empty;

    [ObservableProperty]
    private string mensajeDeError = string.Empty;

    public async Task CargarAsync()
    {
        MensajeDeError = string.Empty;

        var tarifas = await _cliente.ObtenerTarifasAsync();
        if (!tarifas.Exito || tarifas.Datos is null)
        {
            await MostrarErrorAsync(tarifas);
            return;
        }

        MostrarTarifas(tarifas.Datos);

        var parametros = await _cliente.ObtenerParametrosAsync();
        if (!parametros.Exito || parametros.Datos is null)
        {
            await MostrarErrorAsync(parametros);
            return;
        }

        MostrarParametros(parametros.Datos);

        var tipos = await _cliente.ObtenerTiposDeVehiculoAsync();
        if (!tipos.Exito || tipos.Datos is null)
        {
            await MostrarErrorAsync(tipos);
            return;
        }

        TiposDeVehiculo.Clear();
        foreach (var tipo in tipos.Datos.OrderBy(tipo => tipo.NumeroAsientos))
        {
            TiposDeVehiculo.Add($"{tipo.Nombre} · {tipo.NumeroAsientos}");
        }
    }

    // «Guardar» se activa solo si el importe cambió.
    private bool PuedeGuardarTarifaIda() =>
        CambioElImporte(TarifaIda, _importeIda);

    private bool PuedeGuardarTarifaVuelta() =>
        CambioElImporte(TarifaVuelta, _importeVuelta);

    [RelayCommand(CanExecute = nameof(PuedeGuardarTarifaIda), AllowConcurrentExecutions = false)]
    private Task GuardarTarifaIdaAsync() =>
        GuardarTarifaRegularAsync(SentidoDelViaje.Ida);

    [RelayCommand(CanExecute = nameof(PuedeGuardarTarifaVuelta), AllowConcurrentExecutions = false)]
    private Task GuardarTarifaVueltaAsync() =>
        GuardarTarifaRegularAsync(SentidoDelViaje.Vuelta);

    private async Task GuardarTarifaRegularAsync(SentidoDelViaje sentido)
    {
        var esIda = sentido == SentidoDelViaje.Ida;
        var importe = LeerImporte(esIda ? TarifaIda : TarifaVuelta);
        var error = importe is > 0 ? string.Empty : "Escriba un importe mayor que cero.";

        MensajeDeExito = string.Empty;
        AsignarErrorDeTarifa(esIda, error);

        if (error.Length > 0)
        {
            return;
        }

        var resultado = await _cliente.FijarTarifaRegularAsync(sentido, importe!.Value);

        if (!resultado.Exito)
        {
            var errorDelCampo = resultado.Errores.FirstOrDefault(e => e.Campo == "importe")?.Mensaje;

            if (errorDelCampo is not null)
            {
                AsignarErrorDeTarifa(esIda, errorDelCampo);
                return;
            }

            await MostrarErrorAsync(resultado);
            return;
        }

        MensajeDeExito =
            $"Tarifa regular {FilaDeTarifa.TextoDelSentido(sentido)}: S/ {importe.Value.ToString("N2", Cultura)}. " +
            "Se aplica a los pasajes nuevos.";
        await RecargarTarifasAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task AgregarTarifaAsync() =>
        EditarTarifaProgramadaAsync(null);

    private bool HayTarifaSeleccionada() =>
        TarifaSeleccionada is not null;

    [RelayCommand(CanExecute = nameof(HayTarifaSeleccionada), AllowConcurrentExecutions = false)]
    private Task EditarTarifaAsync() =>
        EditarTarifaProgramadaAsync(TarifaSeleccionada?.Tarifa);

    private async Task EditarTarifaProgramadaAsync(DatosDeTarifa? tarifa)
    {
        MensajeDeExito = string.Empty;
        MensajeDeError = string.Empty;

        var id = tarifa?.Id;
        var anterior = tarifa;
        IReadOnlyDictionary<string, string>? errores = null;

        while (true)
        {
            var solicitud = await _dialogos.PedirTarifaProgramadaAsync(anterior, errores);
            if (solicitud is null)
            {
                return;
            }

            var resultado = await _cliente.GuardarTarifaAsync(id, solicitud);

            if (resultado.Exito && resultado.Datos is not null)
            {
                await RecargarTarifasAsync();
                TarifaSeleccionada = TarifasProgramadas.FirstOrDefault(fila => fila.Id == resultado.Datos.Id);
                return;
            }

            // Los errores de los campos vuelven al diálogo, con lo que se escribió (CDU-06 A2).
            if (!resultado.SesionExpirada &&
                !resultado.SinPermiso &&
                string.IsNullOrWhiteSpace(resultado.CodigoDeError) &&
                resultado.Errores.Count > 0)
            {
                errores = resultado.Errores
                    .GroupBy(error => error.Campo)
                    .ToDictionary(
                        grupo => grupo.Key,
                        grupo => string.Join(Environment.NewLine, grupo.Select(error => error.Mensaje)));
                anterior = new DatosDeTarifa
                {
                    Id = id ?? 0,
                    Tipo = TipoDeTarifa.Programada,
                    Nombre = solicitud.Nombre,
                    Importe = solicitud.Importe,
                    VigenciaDesde = solicitud.VigenciaDesde,
                    VigenciaHasta = solicitud.VigenciaHasta,
                    Sentido = solicitud.Sentido
                };
                continue;
            }

            await MostrarErrorAsync(resultado);
            return;
        }
    }

    [RelayCommand(CanExecute = nameof(HayTarifaSeleccionada), AllowConcurrentExecutions = false)]
    private async Task QuitarTarifaAsync()
    {
        if (TarifaSeleccionada is not { } fila)
        {
            return;
        }

        MensajeDeExito = string.Empty;

        var confirmado = await _dialogos.ConfirmarAsync(new Confirmacion(
            $"¿Quitar la tarifa programada «{fila.Nombre}»?",
            "En esas fechas se aplicará la tarifa regular.",
            "Sí, quitar",
            "No, volver"));

        if (!confirmado)
        {
            return;
        }

        var resultado = await _cliente.EliminarTarifaAsync(fila.Id);

        if (!resultado.Exito)
        {
            await MostrarErrorAsync(resultado);
            return;
        }

        await RecargarTarifasAsync();
    }

    [RelayCommand]
    private void SeleccionarTarifa(FilaDeTarifa? fila) =>
        TarifaSeleccionada = fila;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task GuardarParametrosAsync()
    {
        MensajeDeExito = string.Empty;
        ErrorPlazo = EsEnteroEntre(PlazoDeReserva, 1, 30) ? string.Empty : "Escriba un plazo de 1 a 30 días.";
        ErrorAviso = EsEnteroEntre(AvisoPrevioEnHoras, 0, 24) ? string.Empty : "Escriba un aviso de 0 a 24 horas.";

        if (ErrorPlazo.Length > 0 || ErrorAviso.Length > 0)
        {
            return;
        }

        var plazo = PlazoDeReserva.Trim();
        var aviso = AvisoPrevioEnHoras.Trim();

        if (plazo != _plazoGuardado && !await GuardarParametroAsync(ParametroDePlazo, plazo))
        {
            return;
        }

        _plazoGuardado = plazo;

        if (aviso != _avisoGuardado && !await GuardarParametroAsync(ParametroDeAviso, aviso))
        {
            return;
        }

        _avisoGuardado = aviso;
        MensajeDeExito = "Cambios guardados; quedan en la bitácora.";
    }

    private async Task<bool> GuardarParametroAsync(string nombre, string valor)
    {
        var resultado = await _cliente.GuardarParametroAsync(nombre, valor);

        if (resultado.Exito)
        {
            return true;
        }

        var errorDelCampo = resultado.Errores.FirstOrDefault(e => e.Campo == nombre)?.Mensaje;

        if (errorDelCampo is null)
        {
            await MostrarErrorAsync(resultado);
        }
        else if (nombre == ParametroDePlazo)
        {
            ErrorPlazo = errorDelCampo;
        }
        else
        {
            ErrorAviso = errorDelCampo;
        }

        return false;
    }

    // CDU-16 A1: el horario aparece entre los demás, en orden, y el campo queda vacío.
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AgregarHorarioAsync()
    {
        MensajeDeExito = string.Empty;
        ErrorHorario = string.Empty;

        if (!TimeOnly.TryParseExact(
                NuevoHorario.Trim(),
                FormatosDeHora,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var hora))
        {
            ErrorHorario = "Escriba la hora en formato de 24 horas, por ejemplo 07:30.";
            return;
        }

        var texto = hora.ToString("HH:mm", CultureInfo.InvariantCulture);

        if (HorariosDeSalida.Any(horario => horario.Hora == texto))
        {
            ErrorHorario = "Ese horario ya existe.";
            return;
        }

        var resultado = await _cliente.AgregarHorarioAsync(texto);

        if (!resultado.Exito || resultado.Datos is null)
        {
            var errorDelCampo = resultado.Errores.FirstOrDefault(e => e.Campo == "hora")?.Mensaje;

            if (errorDelCampo is null)
            {
                await MostrarErrorAsync(resultado);
            }
            else
            {
                ErrorHorario = errorDelCampo;
            }

            return;
        }

        MostrarHorarios(resultado.Datos.Valor);
        NuevoHorario = string.Empty;
    }

    // CDU-16 A3: la confirmación dice cuántos pasajes hay en ese horario en los próximos días.
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task QuitarHorarioAsync(HorarioVisible? horario)
    {
        if (horario is null || !horario.SePuedeQuitar)
        {
            return;
        }

        MensajeDeExito = string.Empty;

        var cantidad = await _cliente.ContarPasajesDelHorarioAsync(horario.Hora);

        if (!cantidad.Exito)
        {
            await MostrarErrorAsync(cantidad);
            return;
        }

        var mensaje = "Ya no podrá elegirse en los pasajes nuevos, en ninguno de los dos sentidos.";

        if (cantidad.Datos == 1)
        {
            mensaje += $" Hay 1 pasaje registrado para las {horario.Hora} en los próximos días: " +
                       "conserva su horario; si hace falta, modifíquelo desde su detalle.";
        }
        else if (cantidad.Datos > 1)
        {
            mensaje += $" Hay {cantidad.Datos} pasajes registrados para las {horario.Hora} en los próximos días: " +
                       "conservan su horario; si hace falta, modifíquelos desde su detalle.";
        }

        var confirmado = await _dialogos.ConfirmarAsync(new Confirmacion(
            $"¿Quitar el horario de las {horario.Hora}?",
            mensaje,
            "Sí, quitar",
            "No, volver"));

        if (!confirmado)
        {
            return;
        }

        var resultado = await _cliente.QuitarHorarioAsync(horario.Hora);

        if (!resultado.Exito || resultado.Datos is null)
        {
            await MostrarErrorAsync(resultado);
            return;
        }

        MostrarHorarios(resultado.Datos.Valor);
    }

    [RelayCommand]
    private void CerrarAviso() =>
        MensajeDeExito = string.Empty;

    partial void OnTarifaSeleccionadaChanged(FilaDeTarifa? oldValue, FilaDeTarifa? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.EsSeleccionada = false;
        }

        if (newValue is not null)
        {
            newValue.EsSeleccionada = true;
        }
    }

    private async Task RecargarTarifasAsync()
    {
        var tarifas = await _cliente.ObtenerTarifasAsync();

        if (!tarifas.Exito || tarifas.Datos is null)
        {
            await MostrarErrorAsync(tarifas);
            return;
        }

        MostrarTarifas(tarifas.Datos);
    }

    private void MostrarTarifas(IReadOnlyList<DatosDeTarifa> tarifas)
    {
        _importeIda = tarifas.FirstOrDefault(t => t.Tipo == TipoDeTarifa.Regular && t.Sentido == SentidoDelViaje.Ida)?.Importe;
        _importeVuelta = tarifas.FirstOrDefault(t => t.Tipo == TipoDeTarifa.Regular && t.Sentido == SentidoDelViaje.Vuelta)?.Importe;
        TarifaIda = TextoDelImporte(_importeIda);
        TarifaVuelta = TextoDelImporte(_importeVuelta);
        ErrorTarifaIda = string.Empty;
        ErrorTarifaVuelta = string.Empty;
        GuardarTarifaIdaCommand.NotifyCanExecuteChanged();
        GuardarTarifaVueltaCommand.NotifyCanExecuteChanged();

        var idSeleccionado = TarifaSeleccionada?.Id;
        TarifaSeleccionada = null;
        TarifasProgramadas.Clear();

        var indice = 0;
        foreach (var tarifa in tarifas
                     .Where(t => t.Tipo == TipoDeTarifa.Programada)
                     .OrderBy(t => t.VigenciaDesde)
                     .ThenBy(t => t.Nombre))
        {
            TarifasProgramadas.Add(new FilaDeTarifa(tarifa, indice % 2 == 1));
            indice++;
        }

        TarifaSeleccionada = TarifasProgramadas.FirstOrDefault(fila => fila.Id == idSeleccionado);
        OnPropertyChanged(nameof(NoHayTarifasProgramadas));
    }

    private void MostrarParametros(IReadOnlyList<DatosDeParametro> parametros)
    {
        string? Valor(string nombre) =>
            parametros.FirstOrDefault(parametro => parametro.Nombre == nombre)?.Valor;

        _plazoGuardado = Valor(ParametroDePlazo) ?? string.Empty;
        _avisoGuardado = Valor(ParametroDeAviso) ?? string.Empty;
        PlazoDeReserva = _plazoGuardado;
        AvisoPrevioEnHoras = _avisoGuardado;
        ErrorPlazo = string.Empty;
        ErrorAviso = string.Empty;
        MostrarHorarios(Valor(ParametroDeHorarios) ?? string.Empty);
    }

    private void MostrarHorarios(string valor)
    {
        var horas = valor
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order(StringComparer.Ordinal)
            .ToList();

        HorariosDeSalida.Clear();
        foreach (var hora in horas)
        {
            HorariosDeSalida.Add(new HorarioVisible(hora, horas.Count > 1));
        }
    }

    private void AsignarErrorDeTarifa(bool esIda, string error)
    {
        if (esIda)
        {
            ErrorTarifaIda = error;
        }
        else
        {
            ErrorTarifaVuelta = error;
        }
    }

    private async Task MostrarErrorAsync(ResultadoDeLaApi resultado)
    {
        if (resultado.SesionExpirada)
        {
            _cliente.CerrarSesion();
            await _navegacion.IrAlInicioDeSesionAsync();
            return;
        }

        MensajeDeError = resultado.SinPermiso
            ? "No tiene permiso para usar esta función."
            : resultado.Errores.Count > 0
                ? string.Join(Environment.NewLine, resultado.Errores.Select(error => error.Mensaje))
                : "No se pudo completar la operación. Vuelva a intentarlo.";
    }

    private static bool CambioElImporte(string texto, decimal? guardado) =>
        LeerImporte(texto) is decimal importe
            ? importe != guardado
            : guardado is not null || !string.IsNullOrWhiteSpace(texto);

    private static decimal? LeerImporte(string? texto)
    {
        var limpio = (texto ?? string.Empty)
            .Replace("S/", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out var importe)
            ? importe
            : null;
    }

    private static string TextoDelImporte(decimal? importe) =>
        importe is decimal valor ? $"S/ {valor.ToString("N2", CultureInfo.InvariantCulture)}" : string.Empty;

    private static bool EsEnteroEntre(string? texto, int minimo, int maximo) =>
        int.TryParse((texto ?? string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var numero) &&
        numero >= minimo &&
        numero <= maximo;
}
