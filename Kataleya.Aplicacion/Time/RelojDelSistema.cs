using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Time;

public sealed class RelojDelSistema : IReloj
{
    public DateTime Ahora => DateTime.UtcNow;
}
