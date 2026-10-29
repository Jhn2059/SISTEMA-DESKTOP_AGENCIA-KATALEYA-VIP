using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.Pasajes;

public partial class ListaDePasajesPage : PaginaDeModulo
{
    private readonly ListaDePasajesViewModel _modelo;

    public ListaDePasajesPage(ListaDePasajesViewModel modelo)
    {
        OpcionActiva = "pasajes";
        InitializeComponent();
        _modelo = modelo;
        BindingContext = modelo;
    }

    protected override async void AlAparecer() =>
        await _modelo.CargarAsync();
}
