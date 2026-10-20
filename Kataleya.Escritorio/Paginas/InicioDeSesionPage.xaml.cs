using Kataleya.Escritorio.ModelosDeVista;
#if WINDOWS
using Microsoft.Maui.Dispatching;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;
#endif

namespace Kataleya.Escritorio.Paginas;

public partial class InicioDeSesionPage : ContentPage
{
    private readonly InicioDeSesionViewModel _modelo;
#if WINDOWS
    private IDispatcherTimer? _temporizadorDeBloqMayus;
#endif

    public InicioDeSesionPage(InicioDeSesionViewModel modelo)
    {
        InitializeComponent();
        BindingContext = _modelo = modelo;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _modelo.Limpiar();
        EtiquetaBloqMayus.IsVisible = false;

#if WINDOWS
        _temporizadorDeBloqMayus ??= CrearTemporizadorDeBloqMayus();
        _temporizadorDeBloqMayus.Start();
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if WINDOWS
        _temporizadorDeBloqMayus?.Stop();
#endif
    }

    private void Contrasena_TextChanged(object? sender, TextChangedEventArgs e) =>
        ActualizarBloqMayus();

    private void Contrasena_Focused(object? sender, FocusEventArgs e) =>
        ActualizarBloqMayus();

    private void ActualizarBloqMayus()
    {
#if WINDOWS
        var estado = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.CapitalLock);
        EtiquetaBloqMayus.IsVisible = estado.HasFlag(CoreVirtualKeyStates.Locked);
#endif
    }

#if WINDOWS
    private IDispatcherTimer CrearTemporizadorDeBloqMayus()
    {
        var temporizador = Dispatcher.CreateTimer();
        temporizador.Interval = TimeSpan.FromMilliseconds(250);
        temporizador.Tick += (_, _) => ActualizarBloqMayus();
        return temporizador;
    }
#endif
}
