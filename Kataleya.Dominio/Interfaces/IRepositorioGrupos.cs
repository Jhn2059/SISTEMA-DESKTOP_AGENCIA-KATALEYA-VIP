using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioGrupos
{
    Task<string> SiguienteCodigoAsync();

    Task<long> RegistrarAsync(Grupo grupo);

    Task<Grupo?> BuscarPorClaveDeOperacionAsync(Guid clave);
}
