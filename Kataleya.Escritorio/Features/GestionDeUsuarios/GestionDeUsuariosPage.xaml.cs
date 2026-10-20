namespace Kataleya.Escritorio.Features.GestionDeUsuarios;

public partial class GestionDeUsuariosPage : ContentPage
{
    public GestionDeUsuariosPage(GestionDeUsuariosViewModel modelo)
    {
        InitializeComponent();
        BindingContext = modelo;
    }
}
