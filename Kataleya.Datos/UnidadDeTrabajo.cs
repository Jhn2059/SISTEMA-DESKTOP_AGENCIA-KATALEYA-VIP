using Kataleya.Dominio.Interfaces;

namespace Kataleya.Datos;

public sealed class UnidadDeTrabajo : IUnidadDeTrabajo
{
    private readonly ContextoDeBaseDeDatos _contexto;

    public UnidadDeTrabajo(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public Task EjecutarEnTransaccionAsync(Func<Task> trabajo) =>
        _contexto.EjecutarEnTransaccionAsync(trabajo);
}
