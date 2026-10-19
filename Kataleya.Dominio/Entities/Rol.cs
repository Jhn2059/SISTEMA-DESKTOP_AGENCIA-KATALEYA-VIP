namespace Kataleya.Dominio.Entities;

public sealed class Rol
{
    public Rol(short id, string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Id = id;
        Nombre = nombre;
    }

    public short Id { get; }

    public string Nombre { get; }
}
