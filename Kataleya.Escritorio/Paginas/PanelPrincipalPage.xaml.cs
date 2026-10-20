using Kataleya.Escritorio.ModelosDeVista;

namespace Kataleya.Escritorio.Paginas;

public partial class PanelPrincipalPage : ContentPage
{
    private readonly PanelPrincipalViewModel _modelo;

    public PanelPrincipalPage(PanelPrincipalViewModel modelo)
    {
        InitializeComponent();
        BindingContext = _modelo = modelo;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _modelo.Actualizar();
    }
}
