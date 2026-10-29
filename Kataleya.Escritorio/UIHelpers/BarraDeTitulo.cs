using Microsoft.Maui.Controls.Shapes;
using Ruta = Microsoft.Maui.Controls.Shapes.Path;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.UIHelpers;

public static class BarraDeTitulo
{
    private static TitleBar? _barra;
    private static Image? _logo;
    private static Border? _botonDeMenu;
    private static Border? _usuario;
    private static Border? _avatar;
    private static Label? _titulo;
    private static Label? _nombre;
    private static Label? _rol;
    private static Label? _sede;
    private static Action? _alternarLateral;
    private static Func<Task>? _cerrarSesion;

    public static TitleBar Crear()
    {
        _logo = new Image
        {
            Source = "logo_kataleya.png",
            WidthRequest = 56,
            HeightRequest = 33,
            Aspect = Aspect.AspectFit,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };

        _botonDeMenu = CrearBotonDeMenu();

        _titulo = new Label
        {
            Text = "Sistema de la Agencia Kataleya VIP",
            FontFamily = "InterSemiBold",
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            Margin = new Thickness(12, 0, 0, 0)
        };

        var izquierda = new HorizontalStackLayout
        {
            Spacing = 0,
            VerticalOptions = LayoutOptions.Center
        };
        izquierda.Children.Add(_logo);
        izquierda.Children.Add(_botonDeMenu);
        izquierda.Children.Add(_titulo);

        _usuario = CrearUsuario();

        _barra = new TitleBar
        {
            HeightRequest = 48,
            LeadingContent = izquierda,
            TrailingContent = _usuario
        };

        ServicioDeTema.Cambio += (_, _) => ActualizarTema();
        ActualizarTema();

        return _barra;
    }

    public static void ModoInicioDeSesion()
    {
        if (_barra is not null)
        {
            _barra.IsVisible = true;
        }

        if (_logo is not null)
        {
            _logo.IsVisible = true;
        }

        if (_botonDeMenu is not null)
        {
            _botonDeMenu.IsVisible = false;
        }

        if (_usuario is not null)
        {
            _usuario.IsVisible = false;
        }
    }

    public static void ModoAplicacion(
        string nombre,
        string rol,
        string sede,
        Action alternarLateral,
        Func<Task>? cerrarSesion = null)
    {
        _alternarLateral = alternarLateral;
        _cerrarSesion = cerrarSesion;

        if (_barra is not null)
        {
            _barra.IsVisible = false;
        }

        if (_logo is not null)
        {
            _logo.IsVisible = false;
        }

        if (_botonDeMenu is not null)
        {
            _botonDeMenu.IsVisible = true;
        }

        if (_usuario is not null)
        {
            _usuario.IsVisible = true;
        }

        if (_nombre is not null)
        {
            _nombre.Text = nombre;
        }

        if (_rol is not null)
        {
            _rol.Text = rol;
        }

        if (_sede is not null)
        {
            _sede.Text = $"Sede: {sede}";
        }

        ActualizarTema();
    }

    private static void ActualizarTema()
    {
        var texto = ServicioDeTema.Color("Texto");
        var textoSuave = ServicioDeTema.Color("TextoSuave");

        if (_barra is not null)
        {
            _barra.BackgroundColor = ServicioDeTema.Color("Superficie");
            _barra.ForegroundColor = texto;
        }

        if (_titulo is not null)
        {
            _titulo.TextColor = texto;
        }

        if (_avatar is not null)
        {
            _avatar.BackgroundColor = ServicioDeTema.Color("SuperficieActiva");
        }

        if (_nombre is not null)
        {
            _nombre.TextColor = texto;
        }

        if (_rol is not null)
        {
            _rol.TextColor = textoSuave;
        }

        if (_sede is not null)
        {
            _sede.TextColor = textoSuave;
        }
    }

    private static Border CrearBotonDeMenu()
    {
        var icono = new Ruta
        {
            Data = (Geometry)Application.Current!.Resources["IconoMenu"],
            WidthRequest = 20,
            HeightRequest = 20,
            Aspect = Stretch.Uniform,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var boton = new Border
        {
            WidthRequest = 40,
            HeightRequest = 40,
            Margin = new Thickness(12, 0, 0, 0),
            BackgroundColor = Colors.Transparent,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Content = icono,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false
        };

        var puntero = new PointerGestureRecognizer();
        puntero.PointerEntered += (_, _) =>
            boton.BackgroundColor = ServicioDeTema.Color("SuperficieHover");
        puntero.PointerExited += (_, _) => boton.BackgroundColor = Colors.Transparent;
        boton.GestureRecognizers.Add(puntero);

        var toque = new TapGestureRecognizer();
        toque.Tapped += (_, _) => _alternarLateral?.Invoke();
        boton.GestureRecognizers.Add(toque);

        return boton;
    }

    private static Border CrearUsuario()
    {
        _avatar = new Border
        {
            WidthRequest = 26,
            HeightRequest = 26,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 13 },
            VerticalOptions = LayoutOptions.Center,
            Content = new Ruta
            {
                Data = (Geometry)Application.Current!.Resources["IconoPersona"],
                WidthRequest = 16,
                HeightRequest = 16,
                Aspect = Stretch.Uniform,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

        _nombre = new Label
        {
            FontFamily = "InterSemiBold",
            FontSize = 13,
            LineHeight = 1
        };

        _rol = new Label
        {
            FontSize = 11,
            LineHeight = 1
        };

        _sede = new Label
        {
            FontSize = 11,
            LineHeight = 1
        };

        var datos = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center
        };
        datos.Children.Add(_nombre);
        datos.Children.Add(_rol);
        datos.Children.Add(_sede);

        var chevron = new Ruta
        {
            Data = (Geometry)Application.Current!.Resources["IconoChevron"],
            WidthRequest = 14,
            HeightRequest = 14,
            Aspect = Stretch.Uniform,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };

        var contenido = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center
        };
        contenido.Children.Add(_avatar);
        contenido.Children.Add(datos);
        contenido.Children.Add(chevron);

        var bloque = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(8, 0),
            BackgroundColor = Colors.Transparent,
            Content = contenido,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 8, 0),
            IsVisible = false
        };

        var puntero = new PointerGestureRecognizer();
        puntero.PointerEntered += (_, _) =>
            bloque.BackgroundColor = ServicioDeTema.Color("SuperficieHover");
        puntero.PointerExited += (_, _) => bloque.BackgroundColor = Colors.Transparent;
        bloque.GestureRecognizers.Add(puntero);

        var toque = new TapGestureRecognizer();
        toque.Tapped += async (_, _) =>
        {
            if (_cerrarSesion is not null)
            {
                await _cerrarSesion();
            }
        };
        bloque.GestureRecognizers.Add(toque);

        return bloque;
    }
}
