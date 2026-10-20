using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioCatalogos
{
    Task<IReadOnlyList<Rol>> ListarRolesAsync();

    Task<IReadOnlyList<Sede>> ListarSedesAsync();

    Task<Rol?> BuscarRolAsync(short id);

    Task<Sede?> BuscarSedeAsync(short id);
}
