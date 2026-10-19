using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioUsuarios
{
    Task<Usuario?> BuscarPorNombreUsuarioAsync(string nombreUsuario);

    Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario);

    Task AgregarAsync(Usuario usuario);

    Task ActualizarAsync(Usuario usuario);
}
