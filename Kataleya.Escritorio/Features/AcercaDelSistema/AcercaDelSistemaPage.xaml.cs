namespace Kataleya.Escritorio.Features.AcercaDelSistema;

public partial class AcercaDelSistemaPage : ContentPage
{
    public AcercaDelSistemaPage(AcercaDelSistemaViewModel modelo)
    {
        InitializeComponent();
        BindingContext = modelo;
    }
}
