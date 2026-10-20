namespace Kataleya.Escritorio.Features.GestionDeUsuarios.CrearCuenta;

public partial class DialogoDeCuentaPage : ContentPage
{
    public DialogoDeCuentaPage(DialogoDeCuentaViewModel modelo)
    {
        InitializeComponent();
        Modelo = modelo;
        BindingContext = modelo;
    }

    public DialogoDeCuentaViewModel Modelo { get; }
}
