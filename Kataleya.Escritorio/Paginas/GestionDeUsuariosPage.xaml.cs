using Kataleya.Escritorio.ModelosDeVista;

namespace Kataleya.Escritorio.Paginas;

public partial class GestionDeUsuariosPage : ContentPage
{
    private readonly GestionDeUsuariosViewModel _modelo;

    public GestionDeUsuariosPage(GestionDeUsuariosViewModel modelo)
    {
        InitializeComponent();
        BindingContext = _modelo = modelo;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _modelo.CargarAsync();
    }
}
