using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.Pasajes;

public partial class DetalleDePasajePage : PaginaDeModulo
{
    private readonly DetalleDePasajeViewModel _modelo;

    public DetalleDePasajePage(DetalleDePasajeViewModel modelo)
    {
        OpcionActiva = "pasajes";
        InitializeComponent();
        _modelo = modelo;
        BindingContext = modelo;
    }

    public long IdPasaje
    {
        get => _modelo.IdPasaje;
        set => _modelo.IdPasaje = value;
    }

    protected override async void AlAparecer() =>
        await _modelo.CargarAsync();
}
