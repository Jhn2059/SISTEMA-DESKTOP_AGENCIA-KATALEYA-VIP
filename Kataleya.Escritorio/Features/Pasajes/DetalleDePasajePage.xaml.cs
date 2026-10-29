using System.Globalization;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.Servicios.Impresion;
using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.Pasajes;

public partial class DetalleDePasajePage : PaginaDeModulo
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private DatosDePasaje? _pasaje;

    public DetalleDePasajePage()
    {
        OpcionActiva = "pasajes";
        InitializeComponent();
    }

    public long IdPasaje { get; set; }

    protected override async void AlAparecer() => await CargarAsync();

    private async Task CargarAsync()
    {
        var resultado = await Cliente.ObtenerPasajeAsync(IdPasaje);

        if (!resultado.Exito || resultado.Datos is null)
        {
            await DisplayAlertAsync(
                "No se pudo cargar",
                resultado.SinPermiso
                    ? "No tiene permiso para consultar este pasaje."
                    : "No se encontró el pasaje solicitado.",
                "Aceptar");
            return;
        }

        _pasaje = resultado.Datos;
        Mostrar(_pasaje);
    }

    private void Mostrar(DatosDePasaje pasaje)
    {
        LblEstado.Text = pasaje.Estado.ToString();
        LblPasajero.Text = pasaje.PasajeroNombre;
        LblDocumento.Text = $"{pasaje.TipoDocumento} {pasaje.NumeroDocumento}";
        LblTelefono.Text = pasaje.Telefono ?? "—";
        LblBoleto.Text = pasaje.Boleto?.NumeroCompleto ?? "—";
        LblSalida.Text = $"{pasaje.FechaSalida:dd/MM/yyyy} {pasaje.HoraSalida:HH:mm}";
        LblUnidad.Text = pasaje.Unidad is null
            ? $"Asiento {pasaje.NumeroAsiento?.ToString() ?? "—"}"
            : $"{pasaje.Unidad} · Asiento {pasaje.NumeroAsiento?.ToString() ?? "—"}";
        LblEmbarque.Text = string.IsNullOrWhiteSpace(pasaje.LugarDeEmbarque)
            ? "—"
            : pasaje.LugarDeEmbarque;
        LblGrupo.Text = pasaje.GrupoCodigo ?? "—";
        LblEmisor.Text = pasaje.Emisor;
        LblImporte.Text = $"S/ {pasaje.Importe:N2}";
        LblPagado.Text = $"S/ {pasaje.Pagado:N2}";
        LblSaldo.Text = $"S/ {pasaje.Saldo:N2}";

        ListaPagos.ItemsSource = pasaje.Pagos;

        var anulado = pasaje.Estado == EstadoPasaje.Anulado;
        BotonCobrar.IsEnabled = !anulado && pasaje.Saldo > 0;
        BotonModificar.IsEnabled = !anulado;
        BotonAnular.IsEnabled = !anulado;
        BotonImprimir.IsEnabled = pasaje.Estado == EstadoPasaje.Pagado && pasaje.Boleto is not null;
        BotonReimprimir.IsEnabled = BotonImprimir.IsEnabled;
        BotonNoPresentado.IsEnabled =
            !anulado &&
            pasaje.Estado != EstadoPasaje.NoSePresento &&
            pasaje.FechaSalida.ToDateTime(pasaje.HoraSalida) <= DateTime.Now;

        BotonModificar.IsVisible = Cliente.EsAdministrador;
        FilaAnulacion.IsVisible = Cliente.EsAdministrador;

        AjustarOpacidad(BotonCobrar);
        AjustarOpacidad(BotonModificar);
        AjustarOpacidad(BotonAnular);
        AjustarOpacidad(BotonImprimir);
        AjustarOpacidad(BotonReimprimir);
        AjustarOpacidad(BotonNoPresentado);

        LblAviso.Text = anulado
            ? $"Pasaje anulado. Motivo: {pasaje.MotivoAnulacion ?? "no indicado"}."
            : pasaje.Saldo > 0
                ? $"Saldo pendiente: S/ {pasaje.Saldo:N2}. Puede cobrarse en efectivo o por Yape."
                : "El pasaje está pagado. El boleto puede imprimirse o reimprimirse.";
    }

    private static void AjustarOpacidad(Button boton) =>
        boton.Opacity = boton.IsEnabled ? 1 : 0.45;

    private async void AlCobrar(object? remitente, EventArgs argumentos)
    {
        if (_pasaje is null)
        {
            return;
        }

        var texto = await DisplayPromptAsync(
            "Cobrar saldo",
            $"Saldo pendiente: S/ {_pasaje.Saldo:N2}. Importe a cobrar:",
            "Cobrar",
            "Cancelar",
            "0.00",
            keyboard: Keyboard.Numeric,
            initialValue: _pasaje.Saldo.ToString("0.00", Cultura));

        if (texto is null)
        {
            return;
        }

        if (!decimal.TryParse(texto, NumberStyles.Number, Cultura, out var importe) || importe <= 0)
        {
            await DisplayAlertAsync("Importe inválido", "Escriba un importe mayor que cero.", "Aceptar");
            return;
        }

        var medio = await DisplayActionSheetAsync(
            "Medio de pago",
            "Cancelar",
            null,
            nameof(MedioDePago.Efectivo),
            nameof(MedioDePago.Yape));

        if (medio is not "Efectivo" and not "Yape")
        {
            return;
        }

        var resultado = await Cliente.RegistrarCobroAsync(
            _pasaje.Id,
            new SolicitudDeCobro
            {
                Importe = importe,
                Medio = Enum.Parse<MedioDePago>(medio)
            });

        if (!resultado.Exito)
        {
            await MostrarErrorAsync(resultado, "No se pudo registrar el cobro.");
            return;
        }

        if (resultado.Datos is not null)
        {
            _pasaje = resultado.Datos;
            Mostrar(_pasaje);
        }
        else
        {
            await CargarAsync();
        }

        if (_pasaje?.Estado == EstadoPasaje.Pagado && _pasaje.Boleto is not null)
        {
            var estado = await ImprimirLosBoletosDelPagoAsync(_pasaje);
            await AvisarImpresionAsync(
                estado,
                "Pago registrado",
                "Pago registrado. El pasaje quedó Pagado; boleto enviado a la impresora.");
        }
    }

    // Si el pago completa un grupo, se emite un boleto por integrante (CDU-11 A4).
    private async Task<EstadoImpresora> ImprimirLosBoletosDelPagoAsync(DatosDePasaje pasaje)
    {
        if (pasaje.GrupoId is long idGrupo)
        {
            var grupo = await Cliente.ObtenerGrupoAsync(idGrupo);
            if (grupo.Exito && grupo.Datos is not null)
            {
                return await Impresion.ImprimirBoletosAsync(grupo.Datos.Pasajes);
            }
        }

        return await Impresion.ImprimirBoletoAsync(pasaje);
    }

    private Task AvisarImpresionAsync(EstadoImpresora estado, string titulo, string exito) =>
        estado switch
        {
            EstadoImpresora.Lista => DisplayAlertAsync(titulo, exito, "Aceptar"),
            EstadoImpresora.SinPapel => DisplayAlertAsync(
                "No se pudo imprimir",
                "La impresora no tiene papel. Revísela y vuelva a pulsar «Imprimir».",
                "Aceptar"),
            _ => DisplayAlertAsync(
                "No se pudo imprimir",
                "La impresora no responde. Revísela y vuelva a pulsar «Imprimir».",
                "Aceptar")
        };

    private async void AlAnular(object? remitente, EventArgs argumentos)
    {
        if (_pasaje is null)
        {
            return;
        }

        var revision = await Cliente.RevisarAnulacionAsync(_pasaje.Id);
        var detalle = revision.Exito && revision.Datos is not null
            ? revision.Datos.SeDevuelveElAdelanto
                ? $"Se devolverán S/ {revision.Datos.ImporteADevolver:N2}."
                : $"No corresponde devolución. Faltan {revision.Datos.HorasParaLaSalida} horas para la salida."
            : "No se pudo revisar la anulación.";

        var confirmar = await DisplayAlertAsync(
            "Anular pasaje",
            $"{detalle}\n\n¿Desea continuar con la anulación?",
            "Anular",
            "Cancelar");

        if (!confirmar)
        {
            return;
        }

        var motivo = await DisplayPromptAsync(
            "Motivo de anulación",
            "Escriba el motivo:",
            "Anular",
            "Cancelar",
            "Motivo");

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return;
        }

        var resultado = await Cliente.AnularAsync(_pasaje.Id, motivo.Trim());

        if (!resultado.Exito)
        {
            await MostrarErrorAsync(resultado, "No se pudo anular el pasaje.");
            return;
        }

        await CargarAsync();
    }

    private async void AlModificar(object? remitente, EventArgs argumentos)
    {
        if (_pasaje is null)
        {
            return;
        }

        var fecha = await DisplayPromptAsync(
            "Modificar pasaje",
            "Nueva fecha (dd/MM/yyyy). Deje vacío para no cambiarla:",
            "Continuar",
            "Cancelar",
            initialValue: _pasaje.FechaSalida.ToString("dd/MM/yyyy", Cultura));

        if (fecha is null)
        {
            return;
        }

        var hora = await DisplayPromptAsync(
            "Modificar pasaje",
            "Nueva hora (HH:mm). Deje vacío para no cambiarla:",
            "Continuar",
            "Cancelar",
            initialValue: _pasaje.HoraSalida.ToString("HH:mm", Cultura));

        if (hora is null)
        {
            return;
        }

        var asiento = await DisplayPromptAsync(
            "Modificar pasaje",
            "Nuevo asiento. Deje vacío para no cambiarlo:",
            "Continuar",
            "Cancelar",
            keyboard: Keyboard.Numeric,
            initialValue: _pasaje.NumeroAsiento?.ToString() ?? string.Empty);

        if (asiento is null)
        {
            return;
        }

        var cambios = new CambiosDelPasaje();

        if (!string.IsNullOrWhiteSpace(fecha) &&
            DateOnly.TryParseExact(fecha, "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var nuevaFecha))
        {
            cambios.FechaSalida = nuevaFecha;
        }

        if (!string.IsNullOrWhiteSpace(hora) &&
            TimeOnly.TryParse(hora, Cultura, out var nuevaHora))
        {
            cambios.HoraSalida = nuevaHora;
        }

        if (short.TryParse(asiento, out var nuevoAsiento) && nuevoAsiento > 0)
        {
            cambios.NumeroAsiento = nuevoAsiento;
        }

        if (cambios.FechaSalida is null && cambios.HoraSalida is null && cambios.NumeroAsiento is null)
        {
            await DisplayAlertAsync("Sin cambios", "No se indicó ningún cambio válido.", "Aceptar");
            return;
        }

        var resultado = await Cliente.ModificarAsync(
            _pasaje.Id,
            cambios);

        if (!resultado.Exito)
        {
            await MostrarErrorAsync(resultado, "No se pudo modificar el pasaje.");
            return;
        }

        var advertencias = resultado.Datos?.Advertencias ?? [];
        if (advertencias.Count > 0)
        {
            await DisplayAlertAsync(
                "Pasaje modificado",
                string.Join(Environment.NewLine, advertencias),
                "Aceptar");
        }

        await CargarAsync();
    }

    private async void AlImprimir(object? remitente, EventArgs argumentos)
    {
        if (_pasaje?.Boleto is null)
        {
            return;
        }

        BotonImprimir.IsEnabled = false;

        try
        {
            var estado = await Impresion.ImprimirBoletoAsync(_pasaje);
            await AvisarImpresionAsync(
                estado,
                "Boleto enviado",
                $"Boleto N.º {_pasaje.Boleto.Numero:D6} enviado a la impresora.");
        }
        finally
        {
            BotonImprimir.IsEnabled = true;
        }
    }

    private async void AlReimprimir(object? remitente, EventArgs argumentos)
    {
        if (_pasaje?.Boleto is null)
        {
            return;
        }

        BotonReimprimir.IsEnabled = false;

        try
        {
            var estado = await Impresion.ReimprimirAsync(_pasaje);
            await AvisarImpresionAsync(
                estado,
                "Boleto enviado",
                $"Boleto N.º {_pasaje.Boleto.Numero:D6} enviado a la impresora.");
        }
        finally
        {
            BotonReimprimir.IsEnabled = true;
        }

        await CargarAsync();
    }

    private async void AlNoPresentado(object? remitente, EventArgs argumentos)
    {
        if (_pasaje is null)
        {
            return;
        }

        var confirmar = await DisplayAlertAsync(
            "No se presentó",
            "Se marcará el pasaje como no presentado, sin devolución. ¿Continuar?",
            "Marcar",
            "Cancelar");

        if (!confirmar)
        {
            return;
        }

        var resultado = await Cliente.MarcarNoPresentadoAsync(_pasaje.Id);

        if (!resultado.Exito)
        {
            await MostrarErrorAsync(resultado, "No se pudo marcar el pasaje.");
            return;
        }

        await CargarAsync();
    }

    private Task MostrarErrorAsync(Kataleya.Dominio.DataObjects.ResultadoDeLaApi resultado, string mensaje)
    {
        var detalle = resultado.SinPermiso
            ? "No tiene permiso para esta operación."
            : resultado.Errores.Count > 0
                ? string.Join(Environment.NewLine, resultado.Errores.Select(error => error.Mensaje))
                : mensaje;

        return DisplayAlertAsync("Operación no completada", detalle, "Aceptar");
    }

    private async void AlVolver(object? remitente, EventArgs argumentos)
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
}
