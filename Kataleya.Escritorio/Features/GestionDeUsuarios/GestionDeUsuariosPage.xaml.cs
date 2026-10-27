using Kataleya.Cliente;
using Kataleya.Escritorio.Components.Layout;
using Kataleya.Escritorio.UIHelpers;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.Features.GestionDeUsuarios;

public partial class GestionDeUsuariosPage : ContentPage
{
    private static readonly Color FondoBotonClaro = Color.FromArgb("#F1F2F4");
    private static readonly Color FondoBotonClaroHover = Color.FromArgb("#FFFFFF");
    private static readonly Color FondoBotonOscuro = Color.FromArgb("#1A1E25");
    private static readonly Color FondoBotonOscuroHover = Color.FromArgb("#232935");
    private static readonly Color BordeBotonOscuro = Color.FromArgb("#646B78");
    private static readonly Color BordeBotonOscuroHover = Color.FromArgb("#8A909A");
    private static readonly Color FondoBotonPeligroHover = Color.FromArgb("#251014");
    private static readonly Color BordeBotonPeligroHover = Color.FromArgb("#F87171");

    private readonly ClienteApi _cliente;
    private bool _lateralVisible = true;

    public GestionDeUsuariosPage(GestionDeUsuariosViewModel modelo, ClienteApi cliente)
    {
        InitializeComponent();
        _cliente = cliente;
        BindingContext = modelo;
    }

    protected override void OnAppearing()
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
    }

    private void AlSolicitarMenu(object? remitente, EventArgs argumentos) =>
        AlternarLateral();

    private void AlternarLateral()
    {
        _lateralVisible = !_lateralVisible;
        Raiz.ColumnDefinitions[0].Width = _lateralVisible ? new GridLength(232) : new GridLength(0);
        Lateral.IsVisible = _lateralVisible;
    }

    private async void AlTocarOpcion(object? remitente, OpcionSeleccionadaEventArgs argumentos)
    {
        if (BindingContext is not GestionDeUsuariosViewModel modelo)
        {
            return;
        }

        if (argumentos.Opcion == "panel")
        {
            await modelo.VolverCommand.ExecuteAsync(null);
            return;
        }

        if (argumentos.Opcion == "usuarios")
        {
            return;
        }

        await DisplayAlertAsync(
            "Próximamente",
            $"La pantalla «{NombreDeOpcion(argumentos.Opcion)}» se implementará en la siguiente entrega.",
            "Entendido");
    }

    private static string NombreDeOpcion(string opcion) => opcion switch
    {
        "registrar-pasaje" => "Registrar pasaje",
        "pasajes" => "Pasajes",
        "cuadre-ventas" => "Cuadre de ventas",
        "registro-diferido" => "Registro diferido",
        "tarifas" => "Tarifas y parámetros",
        "bitacora" => "Bitácora",
        _ => opcion
    };

    private void AlEntrarBotonClaro(object? remitente, PointerEventArgs argumentos) =>
        BotonCrear.BackgroundColor = FondoBotonClaroHover;

    private void AlSalirBotonClaro(object? remitente, PointerEventArgs argumentos) =>
        BotonCrear.BackgroundColor = FondoBotonClaro;

    private void AlEntrarBotonOscuro(object? remitente, PointerEventArgs argumentos)
    {
        if (remitente is not Border borde)
        {
            return;
        }

        borde.BackgroundColor = FondoBotonOscuroHover;
        borde.Stroke = new SolidColorBrush(BordeBotonOscuroHover);
    }

    private void AlSalirBotonOscuro(object? remitente, PointerEventArgs argumentos)
    {
        if (remitente is not Border borde)
        {
            return;
        }

        borde.BackgroundColor = FondoBotonOscuro;
        borde.Stroke = new SolidColorBrush(BordeBotonOscuro);
    }

    private void AlEntrarBotonPeligro(object? remitente, PointerEventArgs argumentos)
    {
        BotonDesactivar.BackgroundColor = FondoBotonPeligroHover;
        BotonDesactivar.Stroke = new SolidColorBrush(BordeBotonPeligroHover);
    }

    private void AlSalirBotonPeligro(object? remitente, PointerEventArgs argumentos)
    {
        BotonDesactivar.BackgroundColor = FondoBotonOscuro;
        BotonDesactivar.Stroke = new SolidColorBrush(Color.FromArgb("#EF4444"));
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
