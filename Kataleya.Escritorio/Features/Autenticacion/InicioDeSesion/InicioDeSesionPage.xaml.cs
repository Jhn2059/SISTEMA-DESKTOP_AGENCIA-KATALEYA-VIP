using Kataleya.Escritorio.UIHelpers;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.Features.Autenticacion.InicioDeSesion;

public partial class InicioDeSesionPage : ContentPage
{
    private static readonly Color FondoBoton = Color.FromArgb("#F1F2F4");
    private static readonly Color FondoBotonHover = Color.FromArgb("#FFFFFF");

    public InicioDeSesionPage(InicioDeSesionViewModel modelo)
    {
        InitializeComponent();
        BindingContext = modelo;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BarraDeTitulo.ModoInicioDeSesion();
        CampoContrasena.RestablecerContrasena();
        Dispatcher.Dispatch(() => CampoUsuario.Enfocar());
    }

    private void AlEntrarBoton(object? remitente, PointerEventArgs argumentos)
    {
        if (BotonIngresar.IsEnabled)
        {
            BotonIngresar.BackgroundColor = FondoBotonHover;
        }
    }

    private void AlSalirBoton(object? remitente, PointerEventArgs argumentos)
    {
        BotonIngresar.BackgroundColor = FondoBoton;
    }
}

