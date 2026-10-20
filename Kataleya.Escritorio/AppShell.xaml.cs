using Kataleya.Escritorio.Features.Autenticacion.InicioDeSesion;

namespace Kataleya.Escritorio;

public partial class AppShell : Shell
{
    public AppShell(InicioDeSesionPage inicioDeSesion)
    {
        InitializeComponent();
        ContenidoInicial.Content = inicioDeSesion;
    }
}
