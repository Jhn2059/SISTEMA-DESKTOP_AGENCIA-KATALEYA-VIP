namespace Kataleya.Dominio.DataObjects;

public sealed class ErrorTecnico
{
    public string Codigo { get; init; } = string.Empty;

    public DateTime FechaHora { get; init; }

    public string Operacion { get; init; } = string.Empty;

    public string Detalle { get; init; } = string.Empty;
}
