using Kataleya.Escritorio.UIHelpers;
using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.Configuracion;

public partial class ConfiguracionPage : PaginaDeModulo
{
    private readonly ConfiguracionViewModel _modelo;

    public ConfiguracionPage(ConfiguracionViewModel modelo)
    {
        OpcionActiva = "configuracion";
        InitializeComponent();
        _modelo = modelo;
        BindingContext = modelo;
    }

    public PestanaDeConfiguracion Pestana
    {
        get => _modelo.PestanaActiva;
        set => _modelo.PestanaActiva = value;
    }

    protected override async void AlAparecer() =>
        await _modelo.CargarAsync();

    private void AlEntrarBotonClaro(object? remitente, PointerEventArgs argumentos) =>
        BotonCrear.BackgroundColor = ServicioDeTema.Color("BotonClaroHover");

    private void AlSalirBotonClaro(object? remitente, PointerEventArgs argumentos) =>
        BotonCrear.BackgroundColor = ServicioDeTema.Color("BotonClaro");

    private void AlEntrarBotonOscuro(object? remitente, PointerEventArgs argumentos)
    {
        if (remitente is not Border borde)
        {
            return;
        }

        borde.BackgroundColor = ServicioDeTema.Color("BotonOscuroHover");
        borde.Stroke = new SolidColorBrush(ServicioDeTema.Color("BordeControlHover"));
    }

    private void AlSalirBotonOscuro(object? remitente, PointerEventArgs argumentos)
    {
        if (remitente is not Border borde)
        {
            return;
        }

        borde.BackgroundColor = ServicioDeTema.Color("BotonOscuro");
        borde.Stroke = new SolidColorBrush(ServicioDeTema.Color("BordeControl"));
    }

    private void AlEntrarBotonPeligro(object? remitente, PointerEventArgs argumentos)
    {
        BotonDesactivar.BackgroundColor = ServicioDeTema.Color("PeligroHover");
        BotonDesactivar.Stroke = new SolidColorBrush(ServicioDeTema.Color("PeligroBordeHover"));
    }

    private void AlSalirBotonPeligro(object? remitente, PointerEventArgs argumentos)
    {
        BotonDesactivar.BackgroundColor = ServicioDeTema.Color("BotonOscuro");
        BotonDesactivar.Stroke = new SolidColorBrush(ServicioDeTema.Color("Peligro"));
    }
}
