namespace Kataleya.Escritorio;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_shell)
        {
            Title = "Kataleya",
            Width = 1366,
            Height = 768,
            MinimumWidth = 1024,
            MinimumHeight = 768
        };
}
