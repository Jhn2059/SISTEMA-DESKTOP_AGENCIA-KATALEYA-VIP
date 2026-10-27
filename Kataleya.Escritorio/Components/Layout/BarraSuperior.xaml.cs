using Microsoft.Maui.Graphics;
#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
#endif

namespace Kataleya.Escritorio.Components.Layout;

public partial class BarraSuperior : ContentView
{
    private static readonly Color SuperficieHover = Color.FromArgb("#1C2028");
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
#if WINDOWS
        ZonaArrastre.HandlerChanged += AlCambiarHandler;
        Loaded += (_, _) => ActualizarZonasDeEntrada();
        SizeChanged += (_, _) =>
        {
            ActualizarZonasDeEntrada();
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

    private void AlTocarMenu(object? remitente, TappedEventArgs argumentos) =>
        MenuSolicitado?.Invoke(this, EventArgs.Empty);

    private void AlTocarUsuario(object? remitente, TappedEventArgs argumentos) =>
        CierreDeSesionSolicitado?.Invoke(this, EventArgs.Empty);

    private void AlEntrarMenu(object? remitente, PointerEventArgs argumentos) =>
        BotonMenu.BackgroundColor = SuperficieHover;

    private void AlSalirMenu(object? remitente, PointerEventArgs argumentos) =>
        BotonMenu.BackgroundColor = Colors.Transparent;

    private void AlEntrarUsuario(object? remitente, PointerEventArgs argumentos) =>
        BloqueUsuario.BackgroundColor = SuperficieHover;

    private void AlSalirUsuario(object? remitente, PointerEventArgs argumentos) =>
        BloqueUsuario.BackgroundColor = Colors.Transparent;

#if WINDOWS
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr ventana, uint mensaje, IntPtr wParam, IntPtr lParam);

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
        var usuario = ConvertirZona(BloqueUsuario.Bounds, escala);
        var arrastre = ConvertirZona(ZonaArrastre.Bounds, escala);

        var fuente = Microsoft.UI.Input.InputNonClientPointerSource.GetForWindowId(ventana.AppWindow.Id);
        fuente.SetRegionRects(Microsoft.UI.Input.NonClientRegionKind.Caption, new[] { arrastre });
        fuente.SetRegionRects(Microsoft.UI.Input.NonClientRegionKind.Passthrough, new[] { menu, usuario });
    }

    private Windows.Graphics.RectInt32 ConvertirZona(Rect area, double escala) =>
        new(
            (int)Math.Round((X + area.X) * escala),
            (int)Math.Round(area.Y * escala),
            (int)Math.Round(area.Width * escala),
            (int)Math.Round(area.Height * escala));
#endif
}
