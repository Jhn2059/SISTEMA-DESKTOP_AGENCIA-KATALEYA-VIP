using Microsoft.Maui.Dispatching;
#if WINDOWS
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;
#endif

namespace Kataleya.Escritorio.Components.Common;

public partial class IndicadorBloqMayus : ContentView
{
#if WINDOWS
    private IDispatcherTimer? _temporizador;
#endif

    public IndicadorBloqMayus()
    {
        InitializeComponent();
        Loaded += AlCargarse;
        Unloaded += AlDescargarse;
    }

    private void AlCargarse(object? remitente, EventArgs argumentos)
    {
#if WINDOWS
        _temporizador ??= CrearTemporizador();
        _temporizador.Start();
#endif
    }

    private void AlDescargarse(object? remitente, EventArgs argumentos)
    {
#if WINDOWS
        _temporizador?.Stop();
#endif
    }

#if WINDOWS
    private IDispatcherTimer CrearTemporizador()
    {
        var temporizador = Dispatcher.CreateTimer();
        temporizador.Interval = TimeSpan.FromMilliseconds(250);
        temporizador.Tick += (_, _) => ActualizarEstado();
        return temporizador;
    }

    private void ActualizarEstado()
    {
        var estado = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.CapitalLock);
        Etiqueta.IsVisible = estado.HasFlag(CoreVirtualKeyStates.Locked);
    }
#endif
}
