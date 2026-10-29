namespace Kataleya.Escritorio.Features.Pasajes.Dialogos;

public partial class DialogoDeModificacionPage : ContentPage
{
    public DialogoDeModificacionPage(DialogoDeModificacionViewModel modelo)
    {
        InitializeComponent();
        Modelo = modelo;
        BindingContext = modelo;
    }

    public DialogoDeModificacionViewModel Modelo { get; }
}
