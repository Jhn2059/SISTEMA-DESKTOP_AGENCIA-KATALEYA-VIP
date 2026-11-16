using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioPagos
{
    Task<long> RegistrarAsync(Pago pago);

    Task<IReadOnlyList<Pago>> ListarPorPasajeAsync(long pasajeId);

    Task<IReadOnlyList<Pago>> ListarPorGrupoAsync(long grupoId);

    Task<IReadOnlyList<MovimientoDeCuadre>> ListarMovimientosDeCuadreAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? emisorId);
}
