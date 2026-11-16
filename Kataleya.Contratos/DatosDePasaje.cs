using Kataleya.Dominio.Enums;

namespace Kataleya.Contratos;

public sealed class DatosDePasaje
{
    public long Id { get; set; }

    public long? UnidadDeSalidaId { get; set; }

    public long? GrupoId { get; set; }

    public string? CodigoDeGrupo { get; set; }

    public int? NumeroAsiento { get; set; }

    public string NombrePasajero { get; set; } = string.Empty;

    public TipoDocumento TipoDocumento { get; set; }

    public string NumeroDocumento { get; set; } = string.Empty;

    public string? Celular { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string Destino { get; set; } = string.Empty;

    public string LugarEmbarque { get; set; } = string.Empty;

    public DateTime FechaHoraSalida { get; set; }

    public DateTime FechaVenta { get; set; }

    public decimal Importe { get; set; }

    public decimal Adelanto { get; set; }

    public decimal Pagado { get; set; }

    public decimal Saldo { get; set; }

    public string Estado { get; set; } = string.Empty;

    public string TipoRegistro { get; set; } = string.Empty;

    public bool EsDiferido { get; set; }

    public bool EsResponsable { get; set; }

    public string Emisor { get; set; } = string.Empty;

    public bool EsMenor { get; set; }

    public string? NombrePadreMadre { get; set; }

    public string? MotivoMenor { get; set; }

    public string? MotivoAnulacion { get; set; }

    public DateTime? FechaAnulacion { get; set; }

    public bool RequiereReimpresion { get; set; }

    public DatosDeBoleto? Boleto { get; set; }

    public List<DatosDePago> Pagos { get; set; } = [];
}
