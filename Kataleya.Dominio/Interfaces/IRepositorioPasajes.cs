using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioPasajes
{
    Task<int> ContarPasajesDelHorarioAsync(TimeOnly hora);
    Task<long> RegistrarPasajeAsync(Pasaje pasaje);
    Task<Pasaje?> ObtenerPasajeAsync(long id);
    Task<bool> AsientoEstaOcupadoAsync(long unidadSalidaId, int numeroAsiento);
}
