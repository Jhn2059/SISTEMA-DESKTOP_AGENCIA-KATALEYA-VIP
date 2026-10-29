using Kataleya.Cliente;
using Kataleya.Escritorio.Components.Layout;
using Kataleya.Escritorio.Servicios.Impresion;
using Kataleya.Escritorio.UIHelpers;
using Kataleya.Escritorio.UIHelpers.Paginas;
using Microsoft.Maui.Controls.Shapes;
using Ruta = Microsoft.Maui.Controls.Shapes.Path;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.Features.GestionDeUsuarios;

public partial class GestionDeUsuariosPage : ContentPage
{
    private readonly ClienteApi _cliente;
    private readonly ServicioImpresion _impresion;
    private bool _lateralVisible = true;
    private string _pestanaActiva = "cuentas";

    public GestionDeUsuariosPage(
        GestionDeUsuariosViewModel modelo,
        ClienteApi cliente,
        ServicioImpresion impresion)
    {
        InitializeComponent();
        _cliente = cliente;
        _impresion = impresion;
        BindingContext = modelo;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var cuenta = _cliente.CuentaActual;

        Superior.Nombre = cuenta?.NombreUsuario ?? string.Empty;
        Superior.Rol = cuenta?.Rol ?? string.Empty;
        Superior.Sede = cuenta?.Sede ?? string.Empty;
        BarraDeTitulo.ModoAplicacion(
            cuenta?.NombreUsuario ?? string.Empty,
            cuenta?.Rol ?? string.Empty,
            cuenta?.Sede ?? string.Empty,
            AlternarLateral,
            ConfirmarCierreDeSesionAsync);

        MostrarPestana(_pestanaActiva);
        await CargarConfiguracionAsync();
    }

    private void AlSolicitarMenu(object? remitente, EventArgs argumentos) =>
        AlternarLateral();

    private void AlternarLateral()
    {
        _lateralVisible = !_lateralVisible;
        Raiz.ColumnDefinitions[0].Width = _lateralVisible ? new GridLength(232) : new GridLength(0);
        Lateral.IsVisible = _lateralVisible;
    }

    private async void AlTocarOpcion(object? remitente, OpcionSeleccionadaEventArgs argumentos) =>
        await NavegacionDeModulos.IrAsync(argumentos.Opcion);

    private void AlElegirTarifas(object? remitente, TappedEventArgs argumentos) =>
        MostrarPestana("tarifas");

    private void AlElegirCuentas(object? remitente, TappedEventArgs argumentos) =>
        MostrarPestana("cuentas");

    private async void AlElegirEquipo(object? remitente, TappedEventArgs argumentos)
    {
        MostrarPestana("equipo");
        await CargarEquipoAsync();
    }

    private void MostrarPestana(string pestana)
    {
        _pestanaActiva = pestana;

        var tarifas = pestana == "tarifas";
        var cuentas = pestana == "cuentas";
        var equipo = pestana == "equipo";

        PanelTarifas.IsVisible = tarifas;
        PanelCuentas.IsVisible = cuentas;
        PanelEquipo.IsVisible = equipo;

        LineaTabTarifas.IsVisible = tarifas;
        LineaTabCuentas.IsVisible = cuentas;
        LineaTabEquipo.IsVisible = equipo;

        PintarPestana(LblTabTarifas, IconoTabTarifas, tarifas);
        PintarPestana(LblTabCuentas, IconoTabCuentas, cuentas);
        PintarPestana(LblTabEquipo, IconoTabEquipo, equipo);
    }

    private static void PintarPestana(Label etiqueta, Ruta icono, bool activa)
    {
        var color = activa ? ServicioDeTema.Color("Texto") : ServicioDeTema.Color("TextoSuave");
        etiqueta.TextColor = color;
        icono.Stroke = color;
        etiqueta.FontFamily = activa ? "InterSemiBold" : "InterMedium";
    }

    private void AlEntrarBotonClaro(object? remitente, PointerEventArgs argumentos) =>
        BotonCrear.BackgroundColor = ServicioDeTema.Color("BotonClaroHover");

    private void AlSalirBotonClaro(object? remitente, PointerEventArgs argumentos) =>
        BotonCrear.BackgroundColor = ServicioDeTema.Color("BotonClaro");

    private void AlEntrarBotonOscuro(object? remitente, PointerEventArgs argumentos)
    {
        if (remitente is not Border borde)
        {
            return;
        }

        borde.BackgroundColor = ServicioDeTema.Color("BotonOscuroHover");
        borde.Stroke = new SolidColorBrush(ServicioDeTema.Color("BordeControlHover"));
    }

    private void AlSalirBotonOscuro(object? remitente, PointerEventArgs argumentos)
    {
        if (remitente is not Border borde)
        {
            return;
        }

        borde.BackgroundColor = ServicioDeTema.Color("BotonOscuro");
        borde.Stroke = new SolidColorBrush(ServicioDeTema.Color("BordeControl"));
    }

    private void AlEntrarBotonPeligro(object? remitente, PointerEventArgs argumentos)
    {
        BotonDesactivar.BackgroundColor = ServicioDeTema.Color("PeligroHover");
        BotonDesactivar.Stroke = new SolidColorBrush(ServicioDeTema.Color("PeligroBordeHover"));
    }

    private void AlSalirBotonPeligro(object? remitente, PointerEventArgs argumentos)
    {
        BotonDesactivar.BackgroundColor = ServicioDeTema.Color("BotonOscuro");
        BotonDesactivar.Stroke = new SolidColorBrush(ServicioDeTema.Color("Peligro"));
    }

    private void AlSolicitarCierreDeSesion(object? remitente, EventArgs argumentos) =>
        _ = ConfirmarCierreDeSesionAsync();

    private async Task ConfirmarCierreDeSesionAsync()
    {
        var confirmar = await DisplayAlertAsync(
            "Cerrar sesión",
            "¿Desea cerrar la sesión actual?",
            "Cerrar sesión",
            "Cancelar");

        if (confirmar && BindingContext is GestionDeUsuariosViewModel modelo)
        {
            await modelo.CerrarSesionCommand.ExecuteAsync(null);
        }
    }
}
