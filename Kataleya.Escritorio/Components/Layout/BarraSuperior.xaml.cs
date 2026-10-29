using Kataleya.Escritorio.UIHelpers;
#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
#endif

namespace Kataleya.Escritorio.Components.Layout;

public partial class BarraSuperior : ContentView
{
#if WINDOWS
    private FrameworkElement? _zonaNativa;
#endif

    public static readonly BindableProperty NombreProperty = BindableProperty.Create(
        nameof(Nombre),
        typeof(string),
        typeof(BarraSuperior),
        string.Empty,
        propertyChanged: (barra, _, _) => ((BarraSuperior)barra).ActualizarDatos());

    public static readonly BindableProperty RolProperty = BindableProperty.Create(
        nameof(Rol),
        typeof(string),
        typeof(BarraSuperior),
        string.Empty,
        propertyChanged: (barra, _, _) => ((BarraSuperior)barra).ActualizarDatos());

    public static readonly BindableProperty SedeProperty = BindableProperty.Create(
        nameof(Sede),
        typeof(string),
        typeof(BarraSuperior),
        string.Empty,
        propertyChanged: (barra, _, _) => ((BarraSuperior)barra).ActualizarDatos());

    public BarraSuperior()
    {
        InitializeComponent();
        ActualizarDatos();
        ActualizarIconoTema();
        ServicioDeTema.Cambio += AlCambiarTema;
#if WINDOWS
        ZonaArrastre.HandlerChanged += AlCambiarHandler;
        Loaded += (_, _) => ActualizarZonasDeEntrada();
        SizeChanged += (_, _) =>
        {
            ActualizarZonasDeEntrada();
            ActualizarIconoDeVentana();
            Dispatcher.DispatchDelayed(
                TimeSpan.FromMilliseconds(75),
                ActualizarZonasDeEntrada);
        };
#endif
    }

    public string Nombre
    {
        get => (string)GetValue(NombreProperty);
        set => SetValue(NombreProperty, value);
    }

    public string Rol
    {
        get => (string)GetValue(RolProperty);
        set => SetValue(RolProperty, value);
    }

    public string Sede
    {
        get => (string)GetValue(SedeProperty);
        set => SetValue(SedeProperty, value);
    }

    public event EventHandler? MenuSolicitado;

    public event EventHandler? CierreDeSesionSolicitado;

    private void ActualizarDatos()
    {
        LblNombre.Text = Nombre;
        LblRol.Text = Rol;
        LblSede.Text = string.IsNullOrWhiteSpace(Sede) ? string.Empty : $"Sede: {Sede}";
    }

    private void ActualizarIconoTema() =>
        IconoTema.Data = (Microsoft.Maui.Controls.Shapes.Geometry)Microsoft.Maui.Controls
            .Application.Current!.Resources[ServicioDeTema.EsOscuro ? "IconoSol" : "IconoLuna"];

    private void AlCambiarTema(object? remitente, EventArgs argumentos) =>
        Dispatcher.Dispatch(() =>
        {
            ActualizarIconoTema();
            RestablecerFondos();
        });

    private void RestablecerFondos()
    {
        BotonTema.BackgroundColor = Colors.Transparent;
        BloqueUsuario.BackgroundColor = Colors.Transparent;
        BotonMenu.BackgroundColor = Colors.Transparent;
        BotonMinimizar.BackgroundColor = Colors.Transparent;
        BotonMaximizar.BackgroundColor = Colors.Transparent;
        BotonCerrar.BackgroundColor = Colors.Transparent;
        IconoCerrar.Stroke = new SolidColorBrush(ServicioDeTema.Color("Texto"));
    }

    private void AlTocarTema(object? remitente, TappedEventArgs argumentos) =>
        ServicioDeTema.Alternar();

    private void AlEntrarTema(object? remitente, PointerEventArgs argumentos) =>
        BotonTema.BackgroundColor = ServicioDeTema.Color("SuperficieHover");

    private void AlSalirTema(object? remitente, PointerEventArgs argumentos) =>
        BotonTema.BackgroundColor = Colors.Transparent;

    private void AlTocarMenu(object? remitente, TappedEventArgs argumentos) =>
        MenuSolicitado?.Invoke(this, EventArgs.Empty);

    private void AlTocarUsuario(object? remitente, TappedEventArgs argumentos) =>
        CierreDeSesionSolicitado?.Invoke(this, EventArgs.Empty);

    private void AlEntrarMenu(object? remitente, PointerEventArgs argumentos) =>
        BotonMenu.BackgroundColor = ServicioDeTema.Color("SuperficieHover");

    private void AlSalirMenu(object? remitente, PointerEventArgs argumentos) =>
        BotonMenu.BackgroundColor = Colors.Transparent;

    private void AlEntrarUsuario(object? remitente, PointerEventArgs argumentos) =>
        BloqueUsuario.BackgroundColor = ServicioDeTema.Color("SuperficieHover");

    private void AlSalirUsuario(object? remitente, PointerEventArgs argumentos) =>
        BloqueUsuario.BackgroundColor = Colors.Transparent;

    private void AlEntrarControl(object? remitente, PointerEventArgs argumentos)
    {
        if (remitente is Border boton)
        {
            boton.BackgroundColor = ServicioDeTema.Color("SuperficieHover");
        }
    }

    private void AlSalirControl(object? remitente, PointerEventArgs argumentos)
    {
        if (remitente is Border boton)
        {
            boton.BackgroundColor = Colors.Transparent;
        }
    }

    private void AlEntrarCerrar(object? remitente, PointerEventArgs argumentos)
    {
        BotonCerrar.BackgroundColor = ServicioDeTema.Color("CerrarHover");
        IconoCerrar.Stroke = new SolidColorBrush(Colors.White);
    }

    private void AlSalirCerrar(object? remitente, PointerEventArgs argumentos)
    {
        BotonCerrar.BackgroundColor = Colors.Transparent;
        IconoCerrar.Stroke = new SolidColorBrush(ServicioDeTema.Color("Texto"));
    }

    private void AlTocarMinimizar(object? remitente, TappedEventArgs argumentos)
    {
#if WINDOWS
        var identificador = IdentificadorDeVentana();

        if (identificador != IntPtr.Zero)
        {
            ShowWindow(identificador, 6);
        }
#endif
    }

    private void AlTocarMaximizar(object? remitente, TappedEventArgs argumentos)
    {
#if WINDOWS
        var identificador = IdentificadorDeVentana();

        if (identificador == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(identificador, IsZoomed(identificador) ? 9 : 3);
        ActualizarIconoDeVentana();
#endif
    }

    private void AlTocarCerrar(object? remitente, TappedEventArgs argumentos)
    {
#if WINDOWS
        var identificador = IdentificadorDeVentana();

        if (identificador != IntPtr.Zero)
        {
            PostMessage(identificador, 0x0010, IntPtr.Zero, IntPtr.Zero);
        }
#endif
    }

#if WINDOWS
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr ventana, uint mensaje, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr ventana, int comando);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr ventana);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool PostMessage(IntPtr ventana, uint mensaje, IntPtr wParam, IntPtr lParam);

    private static IntPtr IdentificadorDeVentana()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            is not Microsoft.UI.Xaml.Window ventana)
        {
            return IntPtr.Zero;
        }

        return WinRT.Interop.WindowNative.GetWindowHandle(ventana);
    }

    private void ActualizarIconoDeVentana()
    {
        var identificador = IdentificadorDeVentana();
        var maximizada = identificador != IntPtr.Zero && IsZoomed(identificador);
        IconoMaximizar.Data = (Microsoft.Maui.Controls.Shapes.Geometry)Microsoft.Maui.Controls
            .Application.Current!.Resources[maximizada ? "IconoRestaurar" : "IconoMaximizar"];
    }

    private void AlCambiarHandler(object? remitente, EventArgs argumentos)
    {
        if (_zonaNativa is not null)
        {
            _zonaNativa.PointerPressed -= AlPresionarZonaDeArrastre;
            _zonaNativa = null;
        }

        if (ZonaArrastre.Handler?.PlatformView is FrameworkElement zona)
        {
            _zonaNativa = zona;
            _zonaNativa.PointerPressed += AlPresionarZonaDeArrastre;
        }
    }

    private void AlPresionarZonaDeArrastre(object remitente, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs argumentos)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            is not Microsoft.UI.Xaml.Window ventana)
        {
            return;
        }

        var identificador = WinRT.Interop.WindowNative.GetWindowHandle(ventana);
        ReleaseCapture();
        SendMessage(identificador, 0x00A1, (IntPtr)2, IntPtr.Zero);
    }

    private void ActualizarZonasDeEntrada()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            is not Microsoft.UI.Xaml.Window ventana)
        {
            return;
        }

        if (ventana.Content is not FrameworkElement contenido ||
            contenido.XamlRoot is not { } raiz)
        {
            return;
        }

        var escala = raiz.RasterizationScale;
        var menu = ConvertirZona(BotonMenu.Bounds, escala);
        var tema = ConvertirZona(BotonTema.Bounds, escala);
        var usuario = ConvertirZona(BloqueUsuario.Bounds, escala);
        var minimizar = ConvertirZona(BotonMinimizar.Bounds, escala);
        var maximizar = ConvertirZona(BotonMaximizar.Bounds, escala);
        var cerrar = ConvertirZona(BotonCerrar.Bounds, escala);
        var arrastre = ConvertirZona(ZonaArrastre.Bounds, escala);

        var fuente = Microsoft.UI.Input.InputNonClientPointerSource.GetForWindowId(ventana.AppWindow.Id);
        fuente.SetRegionRects(Microsoft.UI.Input.NonClientRegionKind.Caption, new[] { arrastre });
        fuente.SetRegionRects(
            Microsoft.UI.Input.NonClientRegionKind.Passthrough,
            new[] { menu, tema, usuario, minimizar, maximizar, cerrar });
    }

    private Windows.Graphics.RectInt32 ConvertirZona(Rect area, double escala) =>
        new(
            (int)Math.Round((X + area.X) * escala),
            (int)Math.Round(area.Y * escala),
            (int)Math.Round(area.Width * escala),
            (int)Math.Round(area.Height * escala));
#endif
}
