using Kataleya.Dominio.DataObjects;

namespace Kataleya.Dominio.Interfaces;

public interface IRegistroTecnico
{
    Task RegistrarAsync(ErrorTecnico error);
}
