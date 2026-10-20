using Microsoft.Maui;
using Microsoft.UI.Xaml;

namespace Kataleya.Escritorio.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() =>
        global::Kataleya.Escritorio.MauiProgram.CreateMauiApp();
}
