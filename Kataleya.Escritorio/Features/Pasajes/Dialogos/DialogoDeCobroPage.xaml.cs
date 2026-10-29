namespace Kataleya.Escritorio.Features.Pasajes.Dialogos;

public partial class DialogoDeCobroPage : ContentPage
{
    public DialogoDeCobroPage(DialogoDeCobroViewModel modelo)
    {
        InitializeComponent();
        Modelo = modelo;
        BindingContext = modelo;
    }

    public DialogoDeCobroViewModel Modelo { get; }
}
