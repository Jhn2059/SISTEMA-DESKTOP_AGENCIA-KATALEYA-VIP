using Kataleya.Escritorio.Paginas;
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

    public async Task IrAGestionDeUsuariosAsync()
    {
        var pagina = _servicios.GetRequiredService<GestionDeUsuariosPage>();
        await Shell.Current.Navigation.PushAsync(pagina);
    }

    public Task VolverAsync() =>
        Shell.Current.Navigation.PopAsync();
}
