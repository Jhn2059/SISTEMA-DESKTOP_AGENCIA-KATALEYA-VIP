namespace Kataleya.Escritorio.Features.PanelPrincipal;

public partial class PanelPrincipalPage : ContentPage
{
    public PanelPrincipalPage(PanelPrincipalViewModel modelo)
    {
        InitializeComponent();
        BindingContext = modelo;
    }
}
