using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.RegistroDiferido;

public partial class RegistroDiferidoPage : PaginaDeModulo
{
    private readonly RegistroDiferidoViewModel _modelo;

    public RegistroDiferidoPage(RegistroDiferidoViewModel modelo)
    {
        OpcionActiva = "registro-diferido";
        InitializeComponent();
        _modelo = modelo;
        BindingContext = modelo;
    }

    protected override async void AlAparecer() =>
        await _modelo.CargarAsync();
}
