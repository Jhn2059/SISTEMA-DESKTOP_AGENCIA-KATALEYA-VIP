using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Panel;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.Servicios.Impresion;

namespace Kataleya.Escritorio.Features.PanelPrincipal;

public partial class PanelPrincipalViewModel : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private readonly ClienteApi _cliente;
    private readonly INavegacion _navegacion;
    private readonly ServicioImpresion _impresion;

    public PanelPrincipalViewModel(
        ClienteApi cliente,
        INavegacion navegacion,
        ServicioImpresion impresion)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));
        _impresion = impresion ?? throw new ArgumentNullException(nameof(impresion));
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

    public async Task CargarAsync()
    {
        ImpresoraNoEncontrada =
            await _impresion.ConsultarEstadoAsync() == EstadoImpresora.SinRespuesta;

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
