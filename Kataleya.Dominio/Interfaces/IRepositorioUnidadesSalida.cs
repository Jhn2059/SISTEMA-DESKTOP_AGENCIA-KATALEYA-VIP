using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioUnidadesSalida
{
    Task<IReadOnlyList<UnidadDeSalidaDisponible>> ListarPorSalidaAsync(
        Sentido sentido,
        DateTime fechaHoraSalida);

    Task<UnidadDeSalida?> BuscarAsync(long id);

    Task<IReadOnlyList<UnidadDeSalida>> ListarPorGrupoAsync(long grupoId);

    Task<int> SiguienteNumeroAsync(Sentido sentido, DateTime fechaHoraSalida);

    Task<long> RegistrarAsync(UnidadDeSalida unidad);

    Task<int> ContarAsientosOcupadosAsync(long unidadDeSalidaId);
}
