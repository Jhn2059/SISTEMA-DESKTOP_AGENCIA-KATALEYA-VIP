using Kataleya.Contratos.Pasajes;
using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.RegistroDePasajes;

public partial class RegistroDePasajesPage : PaginaDeModulo
{
    private readonly RegistroDePasajesViewModel _modelo;

    public RegistroDePasajesPage(RegistroDePasajesViewModel modelo)
    {
        OpcionActiva = "registrar-pasaje";
        InitializeComponent();
        _modelo = modelo;
        BindingContext = modelo;
    }

    public void CopiarDatosDe(DatosDePasaje pasaje) =>
        _modelo.CopiarDatosDe(pasaje);

    protected override async void AlAparecer() =>
        await _modelo.CargarAsync();
}
