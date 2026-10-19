namespace Kataleya.Dominio.Entities;

public sealed class Sede
{
    public Sede(short id, string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Id = id;
        Nombre = nombre;
    }

    public short Id { get; }

    public string Nombre { get; }
}
