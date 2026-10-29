using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioCopias
{
    Task<CopiaDeRespaldo?> BuscarUltimaAsync();

    Task<int> ContarFallosSeguidosAsync();

    Task AgregarAsync(CopiaDeRespaldo copia);
}
