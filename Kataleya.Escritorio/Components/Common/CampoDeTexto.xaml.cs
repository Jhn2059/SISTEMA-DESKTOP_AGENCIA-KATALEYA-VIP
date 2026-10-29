using System.Windows.Input;
using Kataleya.Escritorio.UIHelpers;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.Components.Common;

public partial class CampoDeTexto : ContentView
{
    private bool _enfocado;
    private bool _punteroEncima;
    private bool _hayError;

    public static readonly BindableProperty EtiquetaProperty =
        BindableProperty.Create(nameof(Etiqueta), typeof(string), typeof(CampoDeTexto), string.Empty);

    public static readonly BindableProperty TextoProperty =
        BindableProperty.Create(
            nameof(Texto),
            typeof(string),
            typeof(CampoDeTexto),
            string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(CampoDeTexto), string.Empty);

    public static readonly BindableProperty EsContrasenaProperty =
        BindableProperty.Create(
            nameof(EsContrasena),
            typeof(bool),
            typeof(CampoDeTexto),
            false,
            propertyChanged: AlCambiarEsContrasena);

    public static readonly BindableProperty MuestraAvisoBloqMayusProperty =
        BindableProperty.Create(
            nameof(MuestraAvisoBloqMayus),
            typeof(bool),
            typeof(CampoDeTexto),
            false);

    public static readonly BindableProperty MensajeDeErrorProperty =
        BindableProperty.Create(
            nameof(MensajeDeError),
            typeof(string),
            typeof(CampoDeTexto),
            string.Empty,
            propertyChanged: AlCambiarMensajeDeError);

    public static readonly BindableProperty IconoProperty =
        BindableProperty.Create(nameof(Icono), typeof(Geometry), typeof(CampoDeTexto));

    public static readonly BindableProperty LongitudMaximaProperty =
        BindableProperty.Create(nameof(LongitudMaxima), typeof(int), typeof(CampoDeTexto), int.MaxValue);

    public static readonly BindableProperty TecladoProperty =
        BindableProperty.Create(nameof(Teclado), typeof(Keyboard), typeof(CampoDeTexto), Keyboard.Default);

    public static readonly BindableProperty TipoDeRetornoProperty =
        BindableProperty.Create(
            nameof(TipoDeRetorno),
            typeof(ReturnType),
            typeof(CampoDeTexto),
            ReturnType.Default);

    public static readonly BindableProperty SiguienteProperty =
        BindableProperty.Create(nameof(Siguiente), typeof(CampoDeTexto), typeof(CampoDeTexto));

    public static readonly BindableProperty ComandoAlCompletarProperty =
        BindableProperty.Create(nameof(ComandoAlCompletar), typeof(ICommand), typeof(CampoDeTexto));

    public static readonly BindableProperty EstaOcupadoProperty =
        BindableProperty.Create(nameof(EstaOcupado), typeof(bool), typeof(CampoDeTexto), false);

    public CampoDeTexto()
    {
        InitializeComponent();
        ServicioDeTema.Cambio += (_, _) => ActualizarBorde();
    }

    public string Etiqueta
    {
        get => (string)GetValue(EtiquetaProperty);
        set => SetValue(EtiquetaProperty, value);
    }

    public string Texto
    {
        get => (string)GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool EsContrasena
    {
        get => (bool)GetValue(EsContrasenaProperty);
        set => SetValue(EsContrasenaProperty, value);
    }

    public bool MuestraAvisoBloqMayus
    {
        get => (bool)GetValue(MuestraAvisoBloqMayusProperty);
        set => SetValue(MuestraAvisoBloqMayusProperty, value);
    }

    public string MensajeDeError
    {
        get => (string)GetValue(MensajeDeErrorProperty);
        set => SetValue(MensajeDeErrorProperty, value);
    }

    public Geometry? Icono
    {
        get => (Geometry?)GetValue(IconoProperty);
        set => SetValue(IconoProperty, value);
    }

    public int LongitudMaxima
    {
        get => (int)GetValue(LongitudMaximaProperty);
        set => SetValue(LongitudMaximaProperty, value);
    }

    public Keyboard Teclado
    {
        get => (Keyboard)GetValue(TecladoProperty);
        set => SetValue(TecladoProperty, value);
    }

    public ReturnType TipoDeRetorno
    {
        get => (ReturnType)GetValue(TipoDeRetornoProperty);
        set => SetValue(TipoDeRetornoProperty, value);
    }

    public CampoDeTexto? Siguiente
    {
        get => (CampoDeTexto?)GetValue(SiguienteProperty);
        set => SetValue(SiguienteProperty, value);
    }

    public ICommand? ComandoAlCompletar
    {
        get => (ICommand?)GetValue(ComandoAlCompletarProperty);
        set => SetValue(ComandoAlCompletarProperty, value);
    }

    public bool EstaOcupado
    {
        get => (bool)GetValue(EstaOcupadoProperty);
        set => SetValue(EstaOcupadoProperty, value);
    }

    public void Enfocar()
    {
        Entrada.Focus();
    }

    public void RestablecerContrasena()
    {
        if (!EsContrasena)
        {
            return;
        }

        Entrada.IsPassword = true;
        ActualizarIconoDeOjo();
    }

    private static void AlCambiarEsContrasena(BindableObject objetivo, object? valorAnterior, object? valorNuevo)
    {
        if (objetivo is not CampoDeTexto campo)
        {
            return;
        }

        campo.Entrada.IsPassword = (bool)(valorNuevo ?? false);
        campo.ActualizarIconoDeOjo();
    }

    private static void AlCambiarMensajeDeError(BindableObject objetivo, object? valorAnterior, object? valorNuevo)
    {
        if (objetivo is CampoDeTexto campo)
        {
            campo.ActualizarError();
        }
    }

    private void AlEntrarPuntero(object? remitente, PointerEventArgs argumentos)
    {
        _punteroEncima = true;
        ActualizarBorde();
    }

    private void AlSalirPuntero(object? remitente, PointerEventArgs argumentos)
    {
        _punteroEncima = false;
        ActualizarBorde();
    }

    private void AlEnfocarEntrada(object? remitente, FocusEventArgs argumentos)
    {
        _enfocado = true;
        ActualizarBorde();
    }

    private void AlDesenfocarEntrada(object? remitente, FocusEventArgs argumentos)
    {
        _enfocado = false;
        ActualizarBorde();
    }

    private void AlCompletarEntrada(object? remitente, EventArgs argumentos)
    {
        if (Siguiente is not null)
        {
            Siguiente.Enfocar();
            return;
        }

        if (!EstaOcupado && ComandoAlCompletar?.CanExecute(null) == true)
        {
            ComandoAlCompletar.Execute(null);
        }
    }

    private void AlAlternarContrasena(object? remitente, TappedEventArgs argumentos)
    {
        Entrada.IsPassword = !Entrada.IsPassword;
        ActualizarIconoDeOjo();
    }

    private void ActualizarIconoDeOjo()
    {
        OjoAbierto.IsVisible = Entrada.IsPassword;
        OjoCerrado.IsVisible = !Entrada.IsPassword;
    }

    private void ActualizarError()
    {
        var mensaje = MensajeDeError ?? string.Empty;
        _hayError = !string.IsNullOrWhiteSpace(mensaje);
        EtiquetaDeError.Text = mensaje;
        FilaDeError.IsVisible = _hayError;
        ActualizarBorde();
    }

    private void ActualizarBorde()
    {
        var color = _hayError
            ? ServicioDeTema.Color("Peligro")
            : _enfocado
                ? ServicioDeTema.Color("Acento")
                : _punteroEncima
                    ? ServicioDeTema.Color("BordeControlHover")
                    : ServicioDeTema.Color("BordeControl");

        Caja.Stroke = new SolidColorBrush(color);

        if (_hayError)
        {
            Caja.Shadow = CrearSombra(ServicioDeTema.Color("Peligro"), 0.3f);
        }
        else if (_enfocado)
        {
            Caja.Shadow = CrearSombra(ServicioDeTema.Color("Acento"), 0.45f);
        }
        else
        {
            Caja.Shadow = null!;
        }
    }

    private static Shadow CrearSombra(Color color, float opacidad) =>
        new()
        {
            Brush = new SolidColorBrush(color.WithAlpha(0.55f)),
            Offset = Point.Zero,
            Radius = 8,
            Opacity = opacidad
        };
}
