using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioBitacora
{
    Task AgregarAsync(RegistroDeBitacora registro);

    Task<bool> ExisteClaveAsync(Guid clave);

    Task<EntradaDeBitacora?> BuscarPorClaveAsync(Guid clave);

    Task<IReadOnlyList<EntradaDeBitacora>> ConsultarAsync(FiltroDeBitacora filtro);

    Task<IReadOnlyList<EntradaDeBitacora>> BuscarHistoriaDeAsync(IReadOnlyCollection<long> idsDePasajes);

    Task EliminarHistoriaDeAsync(IReadOnlyCollection<long> idsDePasajes);
}
