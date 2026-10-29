namespace Kataleya.Dominio.DataObjects;

public sealed class CopiaCifrada
{
    public string NombreArchivo { get; init; } = string.Empty;

    public byte[] Contenido { get; init; } = [];
}
