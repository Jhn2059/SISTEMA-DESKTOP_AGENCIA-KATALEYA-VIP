using Kataleya.Cliente;
using Kataleya.Escritorio.Components.Layout;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.UIHelpers.Paginas;

public class PaginaDeModulo : ContentPage
{
    private bool _marcoConstruido;
    private bool _lateralVisible = true;
    private Grid? _raiz;
    private BarraLateral? _lateral;
    private BarraSuperior? _superior;
    private Image? _fondo;

    public PaginaDeModulo()
    {
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            IsVisible = false,
            IsEnabled = false
        });
    }

    public string OpcionActiva { get; protected set; } = "panel";

    protected ClienteApi Cliente =>
        (Application.Current?.Handler?.MauiContext?.Services.GetService(typeof(ClienteApi))
            as ClienteApi)!;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_marcoConstruido)
        {
            ConstruirMarco();
        }

        ActualizarEncabezado();
        AlAparecer();
    }

    protected virtual void AlAparecer()
    {
    }

    protected void AlternarLateral()
    {
        if (_raiz is null || _lateral is null)
        {
            return;
        }

        _lateralVisible = !_lateralVisible;
        _raiz.ColumnDefinitions[0].Width =
            _lateralVisible ? new GridLength(232) : new GridLength(0);
        _lateral.IsVisible = _lateralVisible;
    }

    private void ConstruirMarco()
    {
        var contenido = Content;
        Content = null;

        _raiz = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(232)),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(new GridLength(58)),
                new RowDefinition(GridLength.Star),
                new RowDefinition(new GridLength(32))
            }
        };
        _raiz.SetDynamicResource(BackgroundColorProperty, "Fondo");

        _lateral = new BarraLateral
        {
            OpcionActiva = OpcionActiva
        };
        Grid.SetRowSpan(_lateral, 3);
        _lateral.OpcionSeleccionada += AlSeleccionarOpcion;
        _lateral.CierreDeSesionSolicitado += AlSolicitarCierreDeSesion;
        _raiz.Children.Add(_lateral);

        _superior = new BarraSuperior();
        Grid.SetColumn(_superior, 1);
        _superior.MenuSolicitado += (_, _) => AlternarLateral();
        _superior.CierreDeSesionSolicitado += AlSolicitarCierreDeSesion;
        _raiz.Children.Add(_superior);

        _fondo = new Image
        {
            Source = "fondo_oscuro.png",
            Aspect = Aspect.AspectFill,
            InputTransparent = true,
            IsVisible = ServicioDeTema.EsOscuro
        };
        Grid.SetColumn(_fondo, 1);
        Grid.SetRow(_fondo, 1);
        Grid.SetRowSpan(_fondo, 2);
        _raiz.Children.Add(_fondo);

        var camioneta = new Grid
        {
            WidthRequest = 430,
            HeightRequest = 291,
            Margin = new Thickness(0, 0, 12, 32),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            IsClippedToBounds = true,
            Opacity = 0.35,
            InputTransparent = true
        };
        Grid.SetColumn(camioneta, 1);
        Grid.SetRow(camioneta, 1);
        Grid.SetRowSpan(camioneta, 2);
        camioneta.Children.Add(new Image
        {
            Source = "camioneta.png",
            WidthRequest = 430,
            HeightRequest = 322.5,
            Aspect = Aspect.Fill,
            TranslationY = -24.4,
            VerticalOptions = LayoutOptions.Start,
            InputTransparent = true
        });
        _raiz.Children.Add(camioneta);

        var host = new Grid();
        Grid.SetColumn(host, 1);
        Grid.SetRow(host, 1);
        if (contenido is not null)
        {
            host.Children.Add(contenido);
        }
        _raiz.Children.Add(host);

        var pie = CrearPieDePagina();
        Grid.SetColumn(pie, 1);
        Grid.SetRow(pie, 2);
        _raiz.Children.Add(pie);

        Content = _raiz;
        _marcoConstruido = true;
        ServicioDeTema.Cambio += AlCambiarTema;
    }

    private static Grid CrearPieDePagina()
    {
        var pie = new Grid();
        var fila = new HorizontalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Spacing = 14
        };

        fila.Children.Add(CrearTextoDelPie("Empresa Turismo VIP Kataleya SV E.I.R.L.", "TextoSuave"));
        fila.Children.Add(CrearTextoDelPie("|", "TextoTenue"));
        fila.Children.Add(CrearTextoDelPie("Uniendo a las familias peruanas", "TextoSuave"));
        fila.Children.Add(CrearTextoDelPie("|", "TextoTenue"));
        fila.Children.Add(CrearTextoDelPie("Ica - Hyo. - viceversa", "TextoSuave"));

        pie.Children.Add(fila);
        return pie;
    }

    private static Label CrearTextoDelPie(string texto, string claveDeColor)
    {
        var etiqueta = new Label
        {
            Text = texto,
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        };
        etiqueta.SetDynamicResource(Label.TextColorProperty, claveDeColor);
        return etiqueta;
    }

    private void AlCambiarTema(object? remitente, EventArgs argumentos)
    {
        if (_fondo is not null)
        {
            _fondo.IsVisible = ServicioDeTema.EsOscuro;
        }
    }

    private void ActualizarEncabezado()
    {
        var cuenta = Cliente?.CuentaActual;
        var nombre = cuenta?.NombreUsuario ?? string.Empty;
        var rol = cuenta?.Rol ?? string.Empty;
        var sede = cuenta?.Sede ?? string.Empty;

        if (_superior is not null)
        {
            _superior.Nombre = nombre;
            _superior.Rol = rol;
            _superior.Sede = sede;
        }

        if (_lateral is not null)
        {
            _lateral.EsAdministrador = Cliente?.EsAdministrador == true;
        }

        BarraDeTitulo.ModoAplicacion(
            nombre,
            rol,
            sede,
            AlternarLateral,
            ConfirmarCierreDeSesionAsync);
    }

    private async void AlSeleccionarOpcion(object? remitente, OpcionSeleccionadaEventArgs argumentos) =>
        await NavegacionDeModulos.IrAsync(argumentos.Opcion);

    private void AlSolicitarCierreDeSesion(object? remitente, EventArgs argumentos) =>
        _ = ConfirmarCierreDeSesionAsync();

    private async Task ConfirmarCierreDeSesionAsync()
    {
        var confirmar = await DisplayAlertAsync(
            "Cerrar sesión",
            "¿Desea cerrar la sesión actual?",
            "Cerrar sesión",
            "Cancelar");

        if (!confirmar)
        {
            return;
        }

        Cliente?.CerrarSesion();
        await Shell.Current.Navigation.PopToRootAsync();
    }
}
