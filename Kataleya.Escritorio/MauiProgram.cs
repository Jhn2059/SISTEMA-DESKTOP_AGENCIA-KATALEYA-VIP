using Kataleya.Cliente;
using Kataleya.Dominio.Interfaces;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.UIHelpers.Extensions;
using Microsoft.Extensions.DependencyInjection;
#if WINDOWS
using Microsoft.Maui.Handlers;
#endif

namespace Kataleya.Escritorio;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();

        builder.ConfigureFonts(fonts =>
        {
            fonts.AddFont("Inter-Regular.ttf", "InterRegular");
            fonts.AddFont("Inter-Medium.ttf", "InterMedium");
            fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
            fonts.AddFont("Inter-Bold.ttf", "InterBold");
        });

#if WINDOWS
        EntryHandler.Mapper.AppendToMapping("Kataleya.SinBorde", (handler, _) =>
        {
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Transparent);
            handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
        });

        PickerHandler.Mapper.AppendToMapping("Kataleya.SinBorde", (handler, _) =>
        {
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Transparent);
            handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
        });
#endif

        builder.Services.AddSingleton(_ => new HttpClient
        {
            BaseAddress = Configuracion.DireccionApi,
            Timeout = TimeSpan.FromSeconds(10)
        });
        builder.Services.AddSingleton<IRegistroTecnico, RegistroTecnico>();
        builder.Services.AddSingleton(sp => new ClienteApi(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<IRegistroTecnico>(),
            Configuracion.Reintentos,
            Configuracion.PermitirHttpSinTls));
        builder.Services.AddSingleton<INavegacion, Navegacion>();
        builder.Services.AddSingleton<IDialogos, DialogosDeMaui>();

        builder.Services.AgregarVistasDeKataleya();

        return builder.Build();
    }
}
