namespace Kataleya.Escritorio.Features.Pasajes.Dialogos;

public partial class DialogoDeAnulacionPage : ContentPage
{
    public DialogoDeAnulacionPage(DialogoDeAnulacionViewModel modelo)
    {
        InitializeComponent();
        Modelo = modelo;
        BindingContext = modelo;
    }

    public DialogoDeAnulacionViewModel Modelo { get; }
}
