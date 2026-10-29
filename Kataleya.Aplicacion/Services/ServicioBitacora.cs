using Kataleya.Aplicacion;
using Kataleya.Dominio.DataObjects;
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

    // El autor y la clave salen del contexto de la operación; la fecha la pone la base.
    public Task RegistrarAsync(MovimientoDeBitacora movimiento)
    {
        ArgumentNullException.ThrowIfNull(movimiento);

        return _repositorio.AgregarAsync(new RegistroDeBitacora(
            movimiento,
            _contexto.Usuario,
            _contexto.ClaveDeOperacion));
    }

    public Task<bool> ExisteAsync(Guid clave) =>
        _repositorio.ExisteClaveAsync(clave);

    public Task<EntradaDeBitacora?> BuscarPorClaveAsync(Guid clave) =>
        _repositorio.BuscarPorClaveAsync(clave);

    public Task<IReadOnlyList<EntradaDeBitacora>> ConsultarAsync(FiltroDeBitacora filtro) =>
        _repositorio.ConsultarAsync(filtro);
}
