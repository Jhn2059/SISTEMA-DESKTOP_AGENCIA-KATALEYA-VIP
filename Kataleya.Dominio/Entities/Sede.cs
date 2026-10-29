using Kataleya.Dominio.DataObjects;

namespace Kataleya.Dominio.Entities;

public sealed class Sede
{
    public Sede(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Nombre = nombre.Trim();
    }

    private Sede()
    {
    }

    public short Id { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public static Sede Reconstruir(SedeGuardada guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return new Sede
        {
            Id = guardado.Id,
            Nombre = guardado.Nombre
        };
    }
}
