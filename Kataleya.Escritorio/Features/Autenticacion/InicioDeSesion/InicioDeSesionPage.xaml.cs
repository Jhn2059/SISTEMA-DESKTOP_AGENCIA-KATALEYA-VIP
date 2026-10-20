namespace Kataleya.Escritorio.Features.Autenticacion.InicioDeSesion;

public partial class InicioDeSesionPage : ContentPage
{
    public InicioDeSesionPage(InicioDeSesionViewModel modelo)
    {
        InitializeComponent();
        BindingContext = modelo;
    }
}
