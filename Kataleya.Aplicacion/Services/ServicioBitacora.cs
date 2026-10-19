using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioBitacora
{
    private readonly IRepositorioBitacora _repositorio;

    public ServicioBitacora(IRepositorioBitacora repositorio) =>
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));

    public Task RegistrarAsync(RegistroDeBitacora registro) =>
        _repositorio.AgregarAsync(registro ?? throw new ArgumentNullException(nameof(registro)));
}
