using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.Bitacora;

public partial class BitacoraPage : PaginaDeModulo
{
    private readonly BitacoraViewModel _modelo;

    public BitacoraPage(BitacoraViewModel modelo)
    {
        OpcionActiva = "bitacora";
        InitializeComponent();
        _modelo = modelo;
        BindingContext = modelo;
    }

    protected override async void AlAparecer() =>
        await _modelo.CargarAsync();
}
