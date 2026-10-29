namespace Kataleya.Escritorio.Features.Configuracion.CuentasDeUsuario.RestablecerContrasena;

public partial class DialogoDeContrasenaPage : ContentPage
{
    public DialogoDeContrasenaPage(DialogoDeContrasenaViewModel modelo)
    {
        InitializeComponent();
        Modelo = modelo;
        BindingContext = modelo;
    }

    public DialogoDeContrasenaViewModel Modelo { get; }
}
