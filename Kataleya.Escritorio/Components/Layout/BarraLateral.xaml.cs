using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.Components.Layout;

public sealed partial class OpcionSeleccionadaEventArgs : EventArgs
{
    public OpcionSeleccionadaEventArgs(string opcion) => Opcion = opcion;

    public string Opcion { get; }
}

public partial class BarraLateral : ContentView
{
    public static readonly BindableProperty OpcionActivaProperty = BindableProperty.Create(
        nameof(OpcionActiva),
        typeof(string),
        typeof(BarraLateral),
        "panel",
        propertyChanged: (bindable, _, _) => ((BarraLateral)bindable).ActualizarOpcionActiva());

    public static readonly BindableProperty EsAdministradorProperty = BindableProperty.Create(
        nameof(EsAdministrador),
        typeof(bool),
        typeof(BarraLateral),
        true);

    private static readonly Color FondoOpcionHover = Color.FromArgb("#1C2028");
    private readonly List<(Border Opcion, BoxView Acento)> _opciones;

    public BarraLateral()
    {
        InitializeComponent();

        _opciones =
        [
            (OpcionPanel, AcentoPanel),
            (OpcionRegistrar, AcentoRegistrar),
            (OpcionPasajes, AcentoPasajes),
            (OpcionCuadre, AcentoCuadre),
            (OpcionDiferido, AcentoDiferido),
            (OpcionTarifas, AcentoTarifas),
            (OpcionUsuarios, AcentoUsuarios),
            (OpcionBitacora, AcentoBitacora)
        ];

        ActualizarOpcionActiva();
    }

    public event EventHandler<OpcionSeleccionadaEventArgs>? OpcionSeleccionada;

    public event EventHandler? CierreDeSesionSolicitado;

    public string OpcionActiva
    {
        get => (string)GetValue(OpcionActivaProperty);
        set => SetValue(OpcionActivaProperty, value);
    }

    public bool EsAdministrador
    {
        get => (bool)GetValue(EsAdministradorProperty);
        set => SetValue(EsAdministradorProperty, value);
    }

    private void ActualizarOpcionActiva()
    {
        foreach (var (opcion, acento) in _opciones)
        {
            var activa = opcion.ClassId == OpcionActiva;
            opcion.BackgroundColor = activa ? Colores.SuperficieActiva : Colors.Transparent;
            acento.IsVisible = activa;
        }
    }

    private void AlTocarOpcion(object? remitente, TappedEventArgs argumentos)
    {
        var opcion = argumentos.Parameter?.ToString();

        if (string.IsNullOrWhiteSpace(opcion))
        {
            return;
        }

        OpcionSeleccionada?.Invoke(this, new OpcionSeleccionadaEventArgs(opcion));
    }

    private void AlTocarCerrarSesion(object? remitente, TappedEventArgs argumentos) =>
        CierreDeSesionSolicitado?.Invoke(this, EventArgs.Empty);

    private void AlEntrarOpcion(object? remitente, PointerEventArgs argumentos)
    {
        if (remitente is Border borde && borde.ClassId != OpcionActiva)
        {
            borde.BackgroundColor = FondoOpcionHover;
        }
    }

    private void AlSalirOpcion(object? remitente, PointerEventArgs argumentos)
    {
        if (remitente is Border borde)
        {
            borde.BackgroundColor = borde.ClassId == OpcionActiva
                ? Colores.SuperficieActiva
                : Colors.Transparent;
        }
    }

    private static class Colores
    {
        public static readonly Color SuperficieActiva = Color.FromArgb("#22262E");
    }
}
