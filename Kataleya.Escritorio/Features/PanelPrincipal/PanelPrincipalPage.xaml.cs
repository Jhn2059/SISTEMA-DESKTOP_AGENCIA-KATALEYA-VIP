using System.Globalization;
using Kataleya.Cliente;
using Kataleya.Escritorio.Components.Layout;
using Kataleya.Escritorio.UIHelpers;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.Features.PanelPrincipal;

public partial class PanelPrincipalPage : ContentPage
{
    private static readonly Color FondoBotonClaro = Color.FromArgb("#F1F2F4");
    private static readonly Color FondoBotonClaroHover = Color.FromArgb("#FFFFFF");
    private static readonly Color FondoBotonOscuro = Color.FromArgb("#1A1E25");
    private static readonly Color FondoBotonOscuroHover = Color.FromArgb("#232935");
    private static readonly Color BordeBotonOscuro = Color.FromArgb("#646B78");
    private static readonly Color BordeBotonOscuroHover = Color.FromArgb("#8A909A");
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private readonly ClienteApi _cliente;
    private bool _lateralVisible = true;

    public PanelPrincipalPage(PanelPrincipalViewModel modelo, ClienteApi cliente)
    {
        InitializeComponent();
        _cliente = cliente;
        BindingContext = modelo;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var modelo = (PanelPrincipalViewModel)BindingContext;
        var nombre = _cliente.CuentaActual?.NombreUsuario;
        nombre = string.IsNullOrWhiteSpace(nombre) ? modelo.Nombre : nombre;

        ActualizarEncabezado(nombre);
        Superior.Nombre = nombre;
        Superior.Rol = modelo.Rol;
        Superior.Sede = modelo.Sede;
        BarraDeTitulo.ModoAplicacion(
            nombre,
            modelo.Rol,
            modelo.Sede,
            AlternarLateral,
            ConfirmarCierreDeSesionAsync);
    }

    private void ActualizarEncabezado(string nombre)
    {
        var hora = DateTime.Now.Hour;
        var saludo = hora < 12
            ? "Buenos días"
            : hora < 19
                ? "Buenas tardes"
                : "Buenas noches";

        LblSaludo.Text = $"¡{saludo}, {nombre}!";

        var ahora = DateTime.Now;
        LblDia.Text = Capitalizar(ahora.ToString("dddd", Cultura));
        LblFecha.Text = ahora.ToString("d 'de' MMMM 'de' yyyy", Cultura);
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
        var opcion = argumentos.Opcion;

        if (opcion == "panel")
        {
            return;
        }

        if (opcion == "usuarios")
        {
            if (BindingContext is PanelPrincipalViewModel modelo)
            {
                await modelo.AbrirGestionDeUsuariosCommand.ExecuteAsync(null);
            }

            return;
        }

        await DisplayAlertAsync(
            "Próximamente",
            $"La pantalla «{NombreDeOpcion(opcion)}» se implementará en la siguiente entrega.",
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
        BotonRegistrarPasaje.BackgroundColor = FondoBotonClaroHover;

    private void AlSalirBotonClaro(object? remitente, PointerEventArgs argumentos) =>
        BotonRegistrarPasaje.BackgroundColor = FondoBotonClaro;

    private void AlEntrarBotonOscuro(object? remitente, PointerEventArgs argumentos)
    {
        BotonVerPasajes.BackgroundColor = FondoBotonOscuroHover;
        BotonVerPasajes.Stroke = new SolidColorBrush(BordeBotonOscuroHover);
    }

    private void AlSalirBotonOscuro(object? remitente, PointerEventArgs argumentos)
    {
        BotonVerPasajes.BackgroundColor = FondoBotonOscuro;
        BotonVerPasajes.Stroke = new SolidColorBrush(BordeBotonOscuro);
    }

    private async void AlTocarRegistrarPasaje(object? remitente, TappedEventArgs argumentos) =>
        await DisplayAlertAsync(
            "Próximamente",
            "El registro de pasajes se implementará en la siguiente entrega.",
            "Entendido");

    private async void AlTocarVerPasajes(object? remitente, TappedEventArgs argumentos) =>
        await DisplayAlertAsync(
            "Próximamente",
            "La consulta de pasajes se implementará en la siguiente entrega.",
            "Entendido");

    private void AlSolicitarCierreDeSesion(object? remitente, EventArgs argumentos) =>
        _ = ConfirmarCierreDeSesionAsync();

    private async Task ConfirmarCierreDeSesionAsync()
    {
        var confirmar = await DisplayAlertAsync(
            "Cerrar sesión",
            "¿Desea cerrar la sesión actual?",
            "Cerrar sesión",
            "Cancelar");

        if (confirmar && BindingContext is PanelPrincipalViewModel modelo)
        {
            await modelo.CerrarSesionCommand.ExecuteAsync(null);
        }
    }

    private static string Capitalizar(string texto) =>
        string.IsNullOrEmpty(texto)
            ? texto
            : char.ToUpper(texto[0], Cultura) + texto[1..];
}
