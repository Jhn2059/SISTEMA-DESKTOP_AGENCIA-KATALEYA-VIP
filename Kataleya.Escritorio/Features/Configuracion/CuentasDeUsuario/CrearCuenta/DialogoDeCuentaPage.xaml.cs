namespace Kataleya.Escritorio.Features.Configuracion.CuentasDeUsuario.CrearCuenta;

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
