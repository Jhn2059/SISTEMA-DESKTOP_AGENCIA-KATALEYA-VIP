namespace Kataleya.Dominio.DataObjects;

public sealed class ErrorDeCampo
{
    public string Campo { get; set; } = string.Empty;

    public string Mensaje { get; set; } = string.Empty;
}
