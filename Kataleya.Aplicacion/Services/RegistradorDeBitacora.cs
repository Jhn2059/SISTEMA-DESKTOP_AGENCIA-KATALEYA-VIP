using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class RegistradorDeBitacora
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly ServicioBitacora _bitacora;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public RegistradorDeBitacora(
        IRepositorioUsuarios usuarios,
        ServicioBitacora bitacora,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public async Task RegistrarAsync(
        string operacion,
        string? anterior = null,
        string? nuevo = null,
        long? pasajeId = null)
    {
        var actor = await _usuarios.BuscarPorIdAsync(_contexto.IdUsuario)
            ?? throw new InvalidOperationException("No se encontró el usuario de la sesión.");

        await _bitacora.RegistrarAsync(new RegistroDeBitacora(
            0,
            _reloj.Ahora,
            operacion,
            actor,
            anterior,
            nuevo,
            pasajeId: pasajeId));
    }
}
