using Kataleya.Escritorio.UIHelpers;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.Features.Autenticacion.InicioDeSesion;

public partial class InicioDeSesionPage : ContentPage
{
    public InicioDeSesionPage(InicioDeSesionViewModel modelo)
    {
        InitializeComponent();
        BindingContext = modelo;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ServicioDeTema.SeguirAlSistema();
        BarraDeTitulo.ModoInicioDeSesion();
        CampoContrasena.RestablecerContrasena();
        Dispatcher.Dispatch(() => CampoUsuario.Enfocar());
    }

    private void AlEntrarBoton(object? remitente, PointerEventArgs argumentos)
    {
        if (BotonIngresar.IsEnabled)
        {
            BotonIngresar.BackgroundColor = ServicioDeTema.Color("BotonClaroHover");
        }
    }

    private void AlSalirBoton(object? remitente, PointerEventArgs argumentos)
    {
        BotonIngresar.BackgroundColor = ServicioDeTema.Color("BotonClaro");
    }
}

