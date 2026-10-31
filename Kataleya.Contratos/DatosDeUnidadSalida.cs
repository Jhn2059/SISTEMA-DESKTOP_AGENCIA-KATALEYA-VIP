using Kataleya.Dominio.Enums;

namespace Kataleya.Contratos;

public sealed class DatosDeUnidadSalida
{
    public long Id { get; set; }
    public Sentido Sentido { get; set; }
    public short TipoVehiculoId { get; set; }
    public string TipoVehiculoNombre { get; set; } = string.Empty;
    public int CantidadAsientos { get; set; }
    public int AsientosOcupados { get; set; }
    public int AsientosDisponibles => CantidadAsientos - AsientosOcupados;
    public DateTime FechaHoraSalida { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string VendidaPor { get; set; } = string.Empty;
}