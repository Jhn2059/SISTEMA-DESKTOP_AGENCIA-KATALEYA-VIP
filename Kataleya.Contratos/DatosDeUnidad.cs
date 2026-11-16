using Kataleya.Dominio.Enums;

namespace Kataleya.Contratos;

public sealed class DatosDeUnidad
{
    public long Id { get; set; }

    public int Numero { get; set; }

    public Sentido Sentido { get; set; }

    public short TipoVehiculoId { get; set; }

    public string TipoVehiculoNombre { get; set; } = string.Empty;

    public int CantidadAsientos { get; set; }

    public DateTime FechaHoraSalida { get; set; }

    public string Estado { get; set; } = string.Empty;

    public string AbiertaPor { get; set; } = string.Empty;

    public List<DatosDeAsiento> Asientos { get; set; } = [];
}
