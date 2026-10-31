using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioUnidadesSalida
{
    Task<IReadOnlyList<UnidadSalidaDisponible>> ListarDisponiblesAsync(DateOnly fecha);
    Task<UnidadSalida?> ObtenerAsync(long id);
    Task<int> ContarAsientosOcupadosAsync(long unidadSalidaId);
}