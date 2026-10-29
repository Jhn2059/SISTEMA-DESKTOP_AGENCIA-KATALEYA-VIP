using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioConfiguracion
{
    Task<IReadOnlyList<Tarifa>> ListarTarifasAsync();

    Task<Tarifa?> BuscarTarifaAsync(int id);

    Task<int> AgregarTarifaAsync(Tarifa tarifa);

    Task ActualizarTarifaAsync(Tarifa tarifa);

    Task EliminarTarifaAsync(int id);

    Task<IReadOnlyList<TipoDeVehiculo>> ListarTiposDeVehiculoAsync();

    Task<TipoDeVehiculo?> BuscarTipoDeVehiculoAsync(short id);

    Task<IReadOnlyList<ParametroDelNegocio>> ListarParametrosAsync();

    Task GuardarParametroAsync(ParametroDelNegocio parametro);
}

public interface IRepositorioCuadre
{
    Task<DetalleDelCuadre> GenerarAsync(DateOnly desde, DateOnly hasta, int? emisorId);

    Task<ResumenDelPanel> ObtenerPanelAsync(DateOnly fecha, int? emisorId);
}
