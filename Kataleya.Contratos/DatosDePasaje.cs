using Kataleya.Dominio.Enums;

namespace Kataleya.Contratos;

public sealed class DatosDePasaje
{
    public long Id { get; set; }
    public long UnidadSalidaId { get; set; }
    public int NumeroAsiento { get; set; }
    public string NombrePasajero { get; set; } = string.Empty;
    public TipoDocumento TipoDocumento { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public string Origen { get; set; } = string.Empty;
    public string Destino { get; set; } = string.Empty;
    public string LugarEmbarque { get; set; } = string.Empty;
    public DateTime FechaHoraSalida { get; set; }
    public DateTime FechaVenta { get; set; }
    public decimal Adelanto { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string VendidaPor { get; set; } = string.Empty;
    public bool EsMenor { get; set; }
    public string? NombrePadreMadre { get; set; }
    public string? MotivoMenor { get; set; }
}