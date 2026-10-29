using Kataleya.Dominio.DataObjects;

namespace Kataleya.Dominio.Entities;

public sealed class Rol
{
    public Rol(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Nombre = nombre.Trim();
    }

    private Rol()
    {
    }

    public short Id { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public static Rol Reconstruir(RolGuardado guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return new Rol
        {
            Id = guardado.Id,
            Nombre = guardado.Nombre
        };
    }
}
