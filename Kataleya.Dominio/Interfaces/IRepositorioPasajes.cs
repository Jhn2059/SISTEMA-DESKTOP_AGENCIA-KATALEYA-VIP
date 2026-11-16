using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioPasajes
{
    Task<long> RegistrarAsync(Pasaje pasaje);

    Task<Pasaje?> BuscarAsync(long id);

    Task<Pasaje?> BuscarPorAsientoAsync(long unidadDeSalidaId, int numeroAsiento);

    Task<IReadOnlyList<Pasaje>> ListarAsync(FiltroDePasajes filtro);

    Task<IReadOnlyList<Pasaje>> ListarPorGrupoAsync(long grupoId);

    Task ActualizarEstadoAsync(long id, EstadoPasaje estado);

    Task ActualizarAsync(Pasaje pasaje);

    Task<int> ContarPasajesDelHorarioAsync(TimeOnly hora);

    Task<bool> AsientoEstaOcupadoAsync(
        long unidadDeSalidaId,
        int numeroAsiento,
        long? excluirPasajeId = null);

    Task<IReadOnlyList<PasajeDeCuadre>> ListarAnuladosEnPeriodoAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? emisorId);

    Task<IReadOnlyList<PasajeDeCuadre>> ListarPendientesDeSaldoAsync(int? emisorId);
}
