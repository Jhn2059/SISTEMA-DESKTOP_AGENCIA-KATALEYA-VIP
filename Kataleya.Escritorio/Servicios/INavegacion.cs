using Kataleya.Escritorio.Features.Configuracion;

namespace Kataleya.Escritorio.Servicios;

public interface INavegacion
{
    Task IrAlPanelAsync();

    Task IrAlInicioDeSesionAsync();

    Task IrAConfiguracionAsync(PestanaDeConfiguracion pestana);

    Task VolverAsync();

    Task CerrarModalAsync();
}
