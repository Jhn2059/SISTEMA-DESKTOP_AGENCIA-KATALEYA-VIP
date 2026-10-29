using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.Servicios.Impresion;
using Kataleya.Escritorio.UIHelpers.Paginas;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.Features.Pasajes;

public partial class DetalleDePasajeViewModel : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private readonly ClienteApi _cliente;
    private readonly IDialogos _dialogos;
    private readonly ServicioImpresion _impresion;
    private DatosDePasaje? _pasaje;

    public DetalleDePasajeViewModel(ClienteApi cliente, IDialogos dialogos, ServicioImpresion impresion)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
        _impresion = impresion ?? throw new ArgumentNullException(nameof(impresion));
    }

    public long IdPasaje { get; set; }

    public bool EsAdministrador => _cliente.EsAdministrador;

    public string Ayuda => EsAdministrador
        ? "Imprimir y reimprimir se habilitan cuando el pasaje está pagado. «No se presentó» se habilita " +
          "después de la hora de salida, sin devolución."
        : "Imprimir y reimprimir se habilitan cuando el pasaje está pagado.";

    public ObservableCollection<FilaDePago> Pagos { get; } = [];

    [ObservableProperty]
    private string estado = string.Empty;

    [ObservableProperty]
    private Color fondoDelEstado = Colors.Transparent;

    [ObservableProperty]
    private Color textoDelEstado = Colors.Transparent;

    [ObservableProperty]
    private string pasajero = "—";

    [ObservableProperty]
    private string documento = "—";

    [ObservableProperty]
    private string telefono = "—";

    [ObservableProperty]
    private string boleto = "—";

    [ObservableProperty]
    private string salida = "—";

    [ObservableProperty]
    private string unidadYAsiento = "—";

    [ObservableProperty]
    private string embarque = "—";

    [ObservableProperty]
    private string grupo = "—";

    [ObservableProperty]
    private string emisor = "—";

    [ObservableProperty]
    private string importe = "S/ 0.00";

    [ObservableProperty]
    private string pagado = "S/ 0.00";

    [ObservableProperty]
    private string saldo = "S/ 0.00";

    [ObservableProperty]
    private string infoDeLaSalida = string.Empty;

    public async Task CargarAsync()
    {
        OnPropertyChanged(nameof(EsAdministrador));
        OnPropertyChanged(nameof(Ayuda));

        var resultado = await _cliente.ObtenerPasajeAsync(IdPasaje);

        if (!resultado.Exito || resultado.Datos is null)
        {
            await _dialogos.MostrarMensajeAsync(
                "No se pudo cargar",
                resultado.SinPermiso
                    ? "No tiene permiso para consultar este pasaje."
                    : "No se encontró el pasaje solicitado.");
            return;
        }

        Mostrar(resultado.Datos);

        if (EsAdministrador && _pasaje?.Estado is not (EstadoPasaje.Anulado or EstadoPasaje.NoSePresento))
        {
            var revision = await _cliente.RevisarAnulacionAsync(IdPasaje);
            InfoDeLaSalida = revision.Exito && revision.Datos is not null
                ? DescribirLaSalida(revision.Datos)
                : string.Empty;
        }
        else
        {
            InfoDeLaSalida = string.Empty;
        }
    }

    private void Mostrar(DatosDePasaje pasaje)
    {
        _pasaje = pasaje;

        Estado = EstadoVisible.Nombre(pasaje.Estado);
        FondoDelEstado = EstadoVisible.Fondo(pasaje.Estado);
        TextoDelEstado = EstadoVisible.Texto(pasaje.Estado);
        Pasajero = pasaje.PasajeroNombre;
        Documento = $"{(pasaje.TipoDocumento == TipoDocumento.Pasaporte ? "Pasaporte" : "DNI")} {pasaje.NumeroDocumento}";
        Telefono = string.IsNullOrWhiteSpace(pasaje.Telefono) ? "—" : pasaje.Telefono;
        Boleto = pasaje.Boleto?.Numero.ToString("D6", Cultura) ?? "— (se asigna al quedar pagado)";
        Salida =
            $"{pasaje.FechaSalida.ToString("dd/MM/yyyy", Cultura)} {pasaje.HoraSalida.ToString("HH:mm", Cultura)} · " +
            $"{pasaje.Origen} → {pasaje.Destino}";
        UnidadYAsiento = pasaje.GrupoId is not null
            ? "Grupo"
            : $"Unidad {pasaje.Unidad ?? "—"} · asiento {pasaje.NumeroAsiento?.ToString(Cultura) ?? "—"}";
        Embarque = string.IsNullOrWhiteSpace(pasaje.LugarDeEmbarque) ? "—" : pasaje.LugarDeEmbarque;
        Grupo = pasaje.GrupoCodigo ?? "—";
        Emisor = pasaje.Emisor;
        Importe = Soles(pasaje.Importe);
        Pagado = Soles(pasaje.Pagado);
        Saldo = Soles(pasaje.Saldo);

        Pagos.Clear();
        var acumulado = 0m;
        foreach (var pago in pasaje.Pagos.OrderBy(pago => pago.FechaHora))
        {
            var esDevolucion = pago.TipoMovimiento == TipoDeMovimiento.Devolucion;
            acumulado += esDevolucion ? -pago.Importe : pago.Importe;

            Pagos.Add(new FilaDePago(
                pago.FechaHora.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura),
                esDevolucion ? "Devolución" : acumulado < pasaje.Importe ? "Adelanto" : "Pago",
                pago.Medio == MedioDePago.Yape ? "Yape" : "Efectivo",
                Soles(pago.Importe),
                pago.CobradoPor));
        }

        CobrarCommand.NotifyCanExecuteChanged();
        ModificarCommand.NotifyCanExecuteChanged();
        CambiarAsientoCommand.NotifyCanExecuteChanged();
        ImprimirCommand.NotifyCanExecuteChanged();
        ReimprimirCommand.NotifyCanExecuteChanged();
        AnularCommand.NotifyCanExecuteChanged();
        MarcarNoPresentadoCommand.NotifyCanExecuteChanged();
    }

    private static string DescribirLaSalida(RevisionDeAnulacion revision)
    {
        var horas = revision.HorasParaLaSalida;
        var aviso = revision.HorasDeAviso;

        if (horas < 0)
        {
            return "La hora de salida ya pasó: puede marcar «No se presentó», sin devolución.";
        }

        return horas >= aviso
            ? $"Faltan {horas} horas para la salida: anular devuelve el adelanto y cambiar la fecha o el horario " +
              $"no tiene costo. Con menos de {aviso} horas, anular retiene el adelanto y el cambio solo se permite " +
              "a una salida con asiento libre, con un cobro adicional opcional."
            : $"Faltan {horas} horas para la salida: anular retiene el adelanto y el cambio de fecha u horario " +
              "solo se permite a una salida con asiento libre, con un cobro adicional opcional.";
    }

    private bool EstaVigente =>
        _pasaje is not null && _pasaje.Estado is not (EstadoPasaje.Anulado or EstadoPasaje.NoSePresento);

    private bool PuedeCobrar() =>
        EstaVigente && _pasaje!.Saldo > 0;

    private bool PuedeModificar() =>
        EsAdministrador && EstaVigente && _pasaje!.GrupoId is null;

    private bool PuedeImprimir() =>
        _pasaje is { Estado: EstadoPasaje.Pagado, Boleto: not null };

    private bool PuedeAnular() =>
        EsAdministrador && _pasaje is not null && _pasaje.Estado != EstadoPasaje.Anulado;

    private bool PuedeMarcarNoPresentado() =>
        EsAdministrador &&
        EstaVigente &&
        _pasaje!.FechaSalida.ToDateTime(_pasaje.HoraSalida) <= DateTime.Now;

    [RelayCommand(CanExecute = nameof(PuedeCobrar), AllowConcurrentExecutions = false)]
    private async Task CobrarAsync()
    {
        var solicitud = await _dialogos.PedirCobroAsync(_pasaje!);
        if (solicitud is null)
        {
            return;
        }

        var resultado = await _cliente.RegistrarCobroAsync(_pasaje!.Id, solicitud);
        if (!resultado.Exito || resultado.Datos is null)
        {
            await MostrarErrorAsync(resultado, "No se pudo registrar el cobro.");
            return;
        }

        Mostrar(resultado.Datos);

        if (resultado.Datos is not { Estado: EstadoPasaje.Pagado, Boleto: not null })
        {
            await _dialogos.MostrarMensajeAsync("Pago registrado", "Pago registrado.");
            return;
        }

        var impresion = await ImprimirLosBoletosDelPagoAsync(resultado.Datos);
        await AvisarImpresionAsync(
            impresion,
            "Pago registrado",
            "Pago registrado. El pasaje quedó Pagado; boleto enviado a la impresora.");
    }

    [RelayCommand(CanExecute = nameof(PuedeModificar), AllowConcurrentExecutions = false)]
    private Task ModificarAsync() =>
        CambiarAsync(soloAsiento: false);

    [RelayCommand(CanExecute = nameof(PuedeModificar), AllowConcurrentExecutions = false)]
    private Task CambiarAsientoAsync() =>
        CambiarAsync(soloAsiento: true);

    private async Task CambiarAsync(bool soloAsiento)
    {
        var cambios = await _dialogos.PedirCambiosAsync(_pasaje!, soloAsiento);
        if (cambios is null ||
            !await _dialogos.ConfirmarAsync(new Confirmacion(
                "Guardar cambios",
                "¿Guardar los cambios del pasaje?",
                "Sí, guardar",
                "No")))
        {
            return;
        }

        var resultado = await _cliente.ModificarAsync(_pasaje!.Id, cambios);
        if (!resultado.Exito)
        {
            await MostrarErrorAsync(resultado, "No se pudo modificar el pasaje.");
            return;
        }

        await CargarAsync();
    }

    [RelayCommand(CanExecute = nameof(PuedeImprimir), AllowConcurrentExecutions = false)]
    private async Task ImprimirAsync()
    {
        var impresion = await _impresion.ImprimirBoletoAsync(_pasaje!);
        await AvisarImpresionAsync(
            impresion,
            "Boleto enviado",
            $"Boleto N.º {_pasaje!.Boleto!.Numero:D6} enviado a la impresora.");
    }

    [RelayCommand(CanExecute = nameof(PuedeImprimir), AllowConcurrentExecutions = false)]
    private async Task ReimprimirAsync()
    {
        var impresion = await _impresion.ReimprimirAsync(_pasaje!);
        await AvisarImpresionAsync(
            impresion,
            "Boleto enviado",
            $"Boleto N.º {_pasaje!.Boleto!.Numero:D6} enviado a la impresora.");
        await CargarAsync();
    }

    [RelayCommand(CanExecute = nameof(PuedeAnular), AllowConcurrentExecutions = false)]
    private async Task AnularAsync()
    {
        var revision = await _cliente.RevisarAnulacionAsync(_pasaje!.Id);
        if (!revision.Exito || revision.Datos is null)
        {
            await MostrarErrorAsync(revision, "No se pudo revisar la anulación.");
            return;
        }

        var solicitud = await _dialogos.PedirAnulacionAsync(_pasaje, revision.Datos);
        if (solicitud is null)
        {
            return;
        }

        var resultado = await _cliente.AnularAsync(_pasaje.Id, solicitud);
        if (!resultado.Exito)
        {
            await MostrarErrorAsync(resultado, "No se pudo anular el pasaje.");
            return;
        }

        await CargarAsync();
    }

    [RelayCommand(CanExecute = nameof(PuedeMarcarNoPresentado), AllowConcurrentExecutions = false)]
    private async Task MarcarNoPresentadoAsync()
    {
        if (!await _dialogos.ConfirmarAsync(new Confirmacion(
                "No se presentó",
                "¿Marcar que el pasajero no se presentó? El pasaje no tendrá devolución.",
                "Sí, marcar",
                "No")))
        {
            return;
        }

        var resultado = await _cliente.MarcarNoPresentadoAsync(_pasaje!.Id);
        if (!resultado.Exito)
        {
            await MostrarErrorAsync(resultado, "No se pudo marcar el pasaje.");
            return;
        }

        await CargarAsync();
    }

    [RelayCommand]
    private async Task VolverAsync()
    {
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
        {
            await Shell.Current.Navigation.PopAsync();
        }
        else
        {
            await NavegacionDeModulos.IrAsync("pasajes");
        }
    }

    // Si el pago completa un grupo, se emite un boleto por integrante (CDU-11 A4).
    private async Task<EstadoImpresora> ImprimirLosBoletosDelPagoAsync(DatosDePasaje pasaje)
    {
        if (pasaje.GrupoId is long idGrupo)
        {
            var delGrupo = await _cliente.ObtenerGrupoAsync(idGrupo);
            if (delGrupo.Exito && delGrupo.Datos is not null)
            {
                return await _impresion.ImprimirBoletosAsync(delGrupo.Datos.Pasajes);
            }
        }

        return await _impresion.ImprimirBoletoAsync(pasaje);
    }

    private Task AvisarImpresionAsync(EstadoImpresora impresion, string titulo, string exito) =>
        impresion switch
        {
            EstadoImpresora.Lista => _dialogos.MostrarMensajeAsync(titulo, exito),
            EstadoImpresora.SinPapel => _dialogos.MostrarMensajeAsync(
                "No se pudo imprimir",
                "La impresora no tiene papel. Revísela y vuelva a pulsar «Imprimir»."),
            _ => _dialogos.MostrarMensajeAsync(
                "No se pudo imprimir",
                "La impresora no responde. Revísela y vuelva a pulsar «Imprimir».")
        };

    private Task MostrarErrorAsync(ResultadoDeLaApi resultado, string mensaje) =>
        _dialogos.MostrarMensajeAsync(
            "Operación no completada",
            resultado.SinPermiso
                ? "No tiene permiso para esta operación."
                : resultado.Errores.Count > 0
                    ? string.Join(Environment.NewLine, resultado.Errores.Select(error => error.Mensaje))
                    : mensaje);

    private static string Soles(decimal importe) =>
        $"S/ {importe.ToString("N2", Cultura)}";
}

public sealed record FilaDePago(string FechaHora, string Tipo, string Medio, string Monto, string Cobro);
