using Kataleya.Dominio.DataObjects;

namespace Kataleya.Aplicacion.Services;

// Atajo para anotar un movimiento con la operación y sus valores.
public sealed class RegistradorDeBitacora
{
    private readonly ServicioBitacora _bitacora;

    public RegistradorDeBitacora(ServicioBitacora bitacora) =>
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));

    public Task RegistrarAsync(
        string operacion,
        string? anterior = null,
        string? nuevo = null,
        long? pasajeId = null) =>
        _bitacora.RegistrarAsync(new MovimientoDeBitacora(operacion, pasajeId, anterior, nuevo));
}
