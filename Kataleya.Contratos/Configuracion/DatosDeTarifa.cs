using Kataleya.Dominio.DataObjects;

namespace Kataleya.Contratos.Configuracion;

public sealed class DatosDeTarifa
{
    public int Id { get; set; }

    public string? Nombre { get; set; }

    public decimal Importe { get; set; }

    public Periodo? Vigencia { get; set; }

    public string FijadaPor { get; set; } = string.Empty;

    public string? Sentido { get; set; }
}
