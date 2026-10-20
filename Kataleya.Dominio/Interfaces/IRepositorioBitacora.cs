using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioBitacora
{
    Task AgregarAsync(RegistroDeBitacora registro);

    Task<bool> ExisteClaveAsync(Guid clave);
}
