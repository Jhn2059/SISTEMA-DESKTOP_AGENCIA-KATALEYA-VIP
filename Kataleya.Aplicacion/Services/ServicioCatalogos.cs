using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioCatalogos
{
    private readonly IRepositorioCatalogos _catalogos;

    public ServicioCatalogos(IRepositorioCatalogos catalogos) =>
        _catalogos = catalogos ?? throw new ArgumentNullException(nameof(catalogos));

    public Task<IReadOnlyList<Rol>> ListarRolesAsync() =>
        _catalogos.ListarRolesAsync();

    public Task<IReadOnlyList<Sede>> ListarSedesAsync() =>
        _catalogos.ListarSedesAsync();
}
