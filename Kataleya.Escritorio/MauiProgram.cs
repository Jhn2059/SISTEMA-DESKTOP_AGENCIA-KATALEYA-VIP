using Kataleya.Cliente;
using Kataleya.Dominio.Interfaces;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.Servicios.Impresion;
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

        // En Windows, el valor inicial llega antes que las opciones y el selector queda en blanco.
        PickerHandler.Mapper.AppendToMapping("Kataleya.SeleccionInicial", (handler, picker) =>
        {
            handler.PlatformView.Loaded += (_, _) =>
            {
                if (picker.SelectedIndex >= 0 &&
                    picker.SelectedIndex < handler.PlatformView.Items.Count &&
                    handler.PlatformView.SelectedIndex != picker.SelectedIndex)
                {
                    handler.PlatformView.SelectedIndex = picker.SelectedIndex;
                }
            };
        });

        // La casilla y el selector de hora de WinUI traen un ancho mínimo que no entra en los formularios.
        CheckBoxHandler.Mapper.AppendToMapping("Kataleya.SinAnchoMinimo", (handler, _) =>
            handler.PlatformView.MinWidth = 0);

        TimePickerHandler.Mapper.AppendToMapping("Kataleya.SinAnchoMinimo", (handler, _) =>
        {
            handler.PlatformView.MinWidth = 0;
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Transparent);
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
            Configuracion.PermitirHttpSinTls,
            new CopiaLocal()));
        builder.Services.AddSingleton<INavegacion, Navegacion>();
        builder.Services.AddSingleton<IDialogos, DialogosDeMaui>();
        builder.Services.AddSingleton<IImpresora, AdaptadorImpresora>();
        builder.Services.AddSingleton<IRespaldo>(_ => new ServicioRespaldo(Configuracion.CarpetaDelRespaldo));
        builder.Services.AddSingleton<ServicioImpresion>();

        builder.Services.AgregarVistasDeKataleya();

        return builder.Build();
    }
}
