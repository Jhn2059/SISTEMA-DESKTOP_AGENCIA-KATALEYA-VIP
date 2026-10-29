using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Panel;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Interfaces;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.Servicios.Impresion;

namespace Kataleya.Escritorio.Features.PanelPrincipal;

public partial class PanelPrincipalViewModel : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private readonly ClienteApi _cliente;
    private readonly INavegacion _navegacion;
    private readonly ServicioImpresion _impresion;
    private readonly IDialogos _dialogos;
    private readonly IRespaldo _respaldo;
    private readonly IRegistroTecnico _registroTecnico;

    public PanelPrincipalViewModel(
        ClienteApi cliente,
        INavegacion navegacion,
        ServicioImpresion impresion,
        IDialogos dialogos,
        IRespaldo respaldo,
        IRegistroTecnico registroTecnico)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));
        _impresion = impresion ?? throw new ArgumentNullException(nameof(impresion));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
        _respaldo = respaldo ?? throw new ArgumentNullException(nameof(respaldo));
        _registroTecnico = registroTecnico ?? throw new ArgumentNullException(nameof(registroTecnico));
    }

    public string Nombre => _cliente.CuentaActual?.Nombre ?? string.Empty;

    public string Rol => _cliente.CuentaActual?.Rol ?? string.Empty;

    public string Sede => _cliente.CuentaActual?.Sede ?? string.Empty;

    public bool EsAdministrador => _cliente.EsAdministrador;

    public ObservableCollection<DatosDeHorario> Horarios { get; } = [];

    [ObservableProperty]
    private int reservasRegistradas;

    [ObservableProperty]
    private int ventas;

    [ObservableProperty]
    private string efectivo = "S/ 0.00";

    [ObservableProperty]
    private string yape = "S/ 0.00";

    [ObservableProperty]
    private string adelantos = "S/ 0.00";

    [ObservableProperty]
    private string fechaDelPanel = string.Empty;

    [ObservableProperty]
    private string mensajeDeError = string.Empty;

    [ObservableProperty]
    private bool impresoraNoEncontrada;

    [ObservableProperty]
    private bool hayReportePendiente;

    [ObservableProperty]
    private bool copiaConFallos;

    [ObservableProperty]
    private string mensajeDelReporte = string.Empty;

    [ObservableProperty]
    private bool generandoReporte;

    private static int AnioDelReporte => DateTime.Today.Year - 2;

    public string TextoDelReportePendiente => $"Hay un reporte anual pendiente: el de {AnioDelReporte}.";

    public async Task CargarAsync()
    {
        ImpresoraNoEncontrada =
            await _impresion.ConsultarEstadoAsync() == EstadoImpresora.SinRespuesta;

        if (EsAdministrador)
        {
            var pendiente = await _cliente.HayReportePendienteAsync();
            HayReportePendiente = pendiente.Exito && pendiente.Datos;

            // RES-01: solo informa, sin botones, hasta que una copia se guarde bien (CDU-22 A2).
            var copia = await _cliente.ObtenerEstadoDeLaCopiaAsync();
            CopiaConFallos = copia.Exito && copia.Datos?.FallosSeguidos >= 2;
        }

        var resultado = await _cliente.ObtenerPanelAsync(DateOnly.FromDateTime(DateTime.Now));

        if (!resultado.Exito || resultado.Datos is null)
        {
            MensajeDeError = resultado.SinPermiso
                ? "No tiene permiso para consultar el panel."
                : "No se pudo cargar el panel.";
            return;
        }

        var datos = resultado.Datos;
        ReservasRegistradas = datos.ReservasRegistradas;
        Ventas = datos.Ventas;
        Efectivo = $"S/ {datos.CobradoEnEfectivo:N2}";
        Yape = $"S/ {datos.CobradoPorYape:N2}";
        Adelantos = $"S/ {datos.AdelantosPorCobrar:N2}";
        FechaDelPanel = datos.Fecha
            .ToDateTime(TimeOnly.MinValue)
            .ToString("d 'de' MMMM 'de' yyyy", Cultura);

        Horarios.Clear();
        foreach (var horario in datos.Horarios)
        {
            Horarios.Add(horario);
        }

        MensajeDeError = string.Empty;
    }

    [RelayCommand]
    private void Actualizar()
    {
        OnPropertyChanged(nameof(Nombre));
        OnPropertyChanged(nameof(Rol));
        OnPropertyChanged(nameof(Sede));
        OnPropertyChanged(nameof(EsAdministrador));
    }

    // CDU-12: se genera el PDF, se guarda en la carpeta del respaldo y solo entonces se depuran los pasajes.
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task GenerarReporteAnualAsync()
    {
        var anio = AnioDelReporte;
        var contrasena = await _dialogos.PedirContrasenaDelReporteAsync(anio);
        if (contrasena is null)
        {
            return;
        }

        GenerandoReporte = true;
        MensajeDelReporte = string.Empty;

        try
        {
            var pdf = await _cliente.DescargarReporteAnualAsync(anio, contrasena);
            if (!pdf.Exito || pdf.Datos is null)
            {
                await AvisarReporteFallidoAsync(anio, MotivoDelError(pdf, "no se pudo generar el PDF"));
                return;
            }

            string ruta;
            try
            {
                ruta = await _respaldo.GuardarReporteAnualAsync(pdf.Datos);
            }
            catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
            {
                await RegistrarErrorAsync(excepcion.Message);
                await AvisarReporteFallidoAsync(anio, "no se pudo guardar en Google Drive");
                return;
            }

            var depuracion = await _cliente.PedirDepuracionAsync(anio, ruta);
            if (!depuracion.Exito || depuracion.Datos is null)
            {
                await AvisarReporteFallidoAsync(anio, MotivoDelError(depuracion, "no se pudieron depurar los pasajes"));
                return;
            }

            HayReportePendiente = false;
            MensajeDelReporte =
                $"Reporte de {anio} guardado en Google Drive; " +
                $"se depuraron {depuracion.Datos.CantidadDePasajesDepurados.ToString("N0", Cultura)} pasajes.";
        }
        finally
        {
            GenerandoReporte = false;
        }
    }

    private Task AvisarReporteFallidoAsync(int anio, string motivo) =>
        _dialogos.MostrarMensajeAsync(
            "RES-02",
            $"El reporte de {anio} no se generó completo, así que no se eliminó ningún pasaje. " +
            $"Motivo: {motivo}. Vuelva a intentarlo.");

    private static string MotivoDelError(ResultadoDeLaApi resultado, string predeterminado) =>
        resultado.Errores.Count > 0
            ? resultado.Errores[0].Mensaje.TrimEnd('.').ToLowerInvariant()
            : predeterminado;

    private Task RegistrarErrorAsync(string detalle) =>
        _registroTecnico.RegistrarAsync(new ErrorTecnico
        {
            Codigo = "RES-02",
            FechaHora = DateTime.UtcNow,
            Operacion = "Guardar reporte anual",
            Detalle = detalle
        });

    [RelayCommand]
    private Task AbrirGestionDeUsuariosAsync() =>
        _navegacion.IrAGestionDeUsuariosAsync();

    [RelayCommand]
    private async Task CerrarSesionAsync()
    {
        _cliente.CerrarSesion();
        await _navegacion.IrAlInicioDeSesionAsync();
    }
}
