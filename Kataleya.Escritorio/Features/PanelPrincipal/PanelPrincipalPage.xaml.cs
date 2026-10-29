using System.Globalization;
using Kataleya.Cliente;
using Kataleya.Escritorio.Components.Layout;
using Kataleya.Escritorio.UIHelpers;
using Kataleya.Escritorio.UIHelpers.Paginas;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Shapes;

namespace Kataleya.Escritorio.Features.PanelPrincipal;

public partial class PanelPrincipalPage : ContentPage
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private readonly ClienteApi _cliente;
    private bool _lateralVisible = true;

    public PanelPrincipalPage(PanelPrincipalViewModel modelo, ClienteApi cliente)
    {
        InitializeComponent();
        _cliente = cliente;
        BindingContext = modelo;
    }

    protected override async void OnAppearing()
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

        await modelo.CargarAsync();
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

    private async void AlTocarOpcion(object? remitente, OpcionSeleccionadaEventArgs argumentos) =>
        await NavegacionDeModulos.IrAsync(argumentos.Opcion);

    private void AlEntrarBotonClaro(object? remitente, PointerEventArgs argumentos) =>
        BotonRegistrarPasaje.BackgroundColor = ServicioDeTema.Color("BotonClaroHover");

    private void AlSalirBotonClaro(object? remitente, PointerEventArgs argumentos) =>
        BotonRegistrarPasaje.BackgroundColor = ServicioDeTema.Color("BotonClaro");

    private void AlEntrarBotonOscuro(object? remitente, PointerEventArgs argumentos)
    {
        BotonVerPasajes.BackgroundColor = ServicioDeTema.Color("BotonOscuroHover");
        BotonVerPasajes.Stroke = new SolidColorBrush(ServicioDeTema.Color("BordeControlHover"));
    }

    private void AlSalirBotonOscuro(object? remitente, PointerEventArgs argumentos)
    {
        BotonVerPasajes.BackgroundColor = ServicioDeTema.Color("BotonOscuro");
        BotonVerPasajes.Stroke = new SolidColorBrush(ServicioDeTema.Color("BordeControl"));
    }

    private async void AlTocarRegistrarPasaje(object? remitente, TappedEventArgs argumentos) =>
        await NavegacionDeModulos.IrAsync("registrar-pasaje");

    private async void AlTocarVerPasajes(object? remitente, TappedEventArgs argumentos) =>
        await NavegacionDeModulos.IrAsync("pasajes");

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
