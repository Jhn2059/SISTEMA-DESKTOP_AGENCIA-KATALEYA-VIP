using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.CuadreDeVentas;

public partial class CuadreDeVentasPage : PaginaDeModulo
{
    private readonly CuadreDeVentasViewModel _modelo;

    public CuadreDeVentasPage(CuadreDeVentasViewModel modelo)
    {
        OpcionActiva = "cuadre-ventas";
        InitializeComponent();
        _modelo = modelo;
        BindingContext = modelo;
    }

    protected override async void AlAparecer() =>
        await _modelo.CargarAsync();
}
