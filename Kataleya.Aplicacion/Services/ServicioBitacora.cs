using Kataleya.Aplicacion;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioBitacora
{
    private readonly IRepositorioBitacora _repositorio;
    private readonly ContextoDeOperacion _contexto;

    public ServicioBitacora(
        IRepositorioBitacora repositorio,
        ContextoDeOperacion contexto)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public Task RegistrarAsync(RegistroDeBitacora registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        if (registro.ClaveDeOperacion is null && _contexto.ClaveDeOperacion is not null)
        {
            registro.FijarClaveDeOperacion(_contexto.ClaveDeOperacion);
        }

        return _repositorio.AgregarAsync(registro);
    }

    public Task<bool> ExisteAsync(Guid clave) =>
        _repositorio.ExisteClaveAsync(clave);

    public Task<EntradaDeBitacora?> BuscarPorClaveAsync(Guid clave) =>
        _repositorio.BuscarPorClaveAsync(clave);

    public Task<IReadOnlyList<EntradaDeBitacora>> ConsultarAsync(FiltroDeBitacora filtro) =>
        _repositorio.ConsultarAsync(filtro);
}
