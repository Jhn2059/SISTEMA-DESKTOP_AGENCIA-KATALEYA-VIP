using Kataleya.Escritorio.UIHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Kataleya.Escritorio;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(IServiceProvider servicios)
    {
        InitializeComponent();
        ServicioDeTema.Inicializar();
        _shell = servicios.GetRequiredService<AppShell>();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_shell)
        {
            Title = "Kataleya",
            Width = 1366,
            Height = 768,
            MinimumWidth = 1024,
            MinimumHeight = 768,
            TitleBar = BarraDeTitulo.Crear()
        };
}
