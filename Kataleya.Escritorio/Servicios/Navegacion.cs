using Kataleya.Escritorio.Features.Configuracion;
using Kataleya.Escritorio.Features.PanelPrincipal;
using Microsoft.Extensions.DependencyInjection;

namespace Kataleya.Escritorio.Servicios;

public sealed class Navegacion : INavegacion
{
    private readonly IServiceProvider _servicios;

    public Navegacion(IServiceProvider servicios) =>
        _servicios = servicios ?? throw new ArgumentNullException(nameof(servicios));

    public async Task IrAlPanelAsync()
    {
        var pagina = _servicios.GetRequiredService<PanelPrincipalPage>();
        Shell.SetBackButtonBehavior(pagina, new BackButtonBehavior
        {
            IsVisible = false,
            IsEnabled = false
        });

        await Shell.Current.Navigation.PushAsync(pagina);
    }

    public Task IrAlInicioDeSesionAsync() =>
        Shell.Current.Navigation.PopToRootAsync();

    public async Task IrAConfiguracionAsync(PestanaDeConfiguracion pestana)
    {
        var pagina = Shell.Current.Navigation.NavigationStack.OfType<ConfiguracionPage>().LastOrDefault();

        if (pagina is not null)
        {
            pagina.Pestana = pestana;

            while (Shell.Current.Navigation.NavigationStack[^1] != pagina)
            {
                await Shell.Current.Navigation.PopAsync();
            }

            return;
        }

        pagina = _servicios.GetRequiredService<ConfiguracionPage>();
        pagina.Pestana = pestana;
        await Shell.Current.Navigation.PushAsync(pagina);
    }

    public Task VolverAsync() =>
        Shell.Current.Navigation.PopAsync();

    public Task CerrarModalAsync() =>
        Shell.Current.Navigation.PopModalAsync();
}
