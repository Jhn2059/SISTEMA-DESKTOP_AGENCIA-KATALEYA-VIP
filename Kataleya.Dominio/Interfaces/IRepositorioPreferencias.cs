using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioPreferencias
{
    Task<Preferencias?> BuscarAsync(int idUsuario);

    Task GuardarAsync(Preferencias preferencias);
}
