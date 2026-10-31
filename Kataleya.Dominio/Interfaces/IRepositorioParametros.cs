using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioParametros
{
    Task<ParametroDelNegocio?> BuscarAsync(string nombre);

    Task<IReadOnlyList<ParametroDelNegocio>> ListarAsync();

    Task GuardarAsync(ParametroDelNegocio parametro);

    Task<IReadOnlyList<TipoDeVehiculo>> ListarTiposDeVehiculoAsync();
}
