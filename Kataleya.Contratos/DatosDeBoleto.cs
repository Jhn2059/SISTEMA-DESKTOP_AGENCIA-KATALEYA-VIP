namespace Kataleya.Contratos;

public sealed class DatosDeBoleto
{
    public string Serie { get; set; } = string.Empty;

    public int Correlativo { get; set; }

    public string Numero { get; set; } = string.Empty;

    public DateTime FechaEmision { get; set; }

    public string EmitidoPor { get; set; } = string.Empty;

    public string? Grupo { get; set; }
}
