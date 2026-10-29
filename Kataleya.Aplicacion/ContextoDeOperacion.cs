using Kataleya.Dominio.Entities;

namespace Kataleya.Aplicacion;

public sealed class ContextoDeOperacion
{
    public Guid? ClaveDeOperacion { get; set; }

    // Lo llena MiddlewareDeSesion con la cuenta de la sesión, o ServicioSesion al iniciarla.
    public Usuario Usuario { get; set; } = null!;
}
