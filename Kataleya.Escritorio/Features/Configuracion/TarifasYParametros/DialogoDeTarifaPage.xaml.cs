namespace Kataleya.Escritorio.Features.Configuracion.TarifasYParametros;

public partial class DialogoDeTarifaPage : ContentPage
{
    public DialogoDeTarifaPage(DialogoDeTarifaViewModel modelo)
    {
        InitializeComponent();
        Modelo = modelo;
        BindingContext = modelo;
    }

    public DialogoDeTarifaViewModel Modelo { get; }
}
