using Kataleya.Dominio.DataObjects;

namespace Kataleya.Dominio.Interfaces;

public interface ICopiaDeLaBase
{
    Task<CopiaCifrada> ExportarAsync(DateTime fechaHora);
}
