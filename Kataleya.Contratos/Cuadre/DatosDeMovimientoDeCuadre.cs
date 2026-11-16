using Kataleya.Dominio.Enums;

namespace Kataleya.Contratos.Cuadre;

public sealed class DatosDeMovimientoDeCuadre
{
    public long PagoId { get; set; }

    public long PasajeId { get; set; }

    public long? GrupoId { get; set; }

    public string? CodigoDeGrupo { get; set; }

    public DateTime FechaPago { get; set; }

    public decimal Monto { get; set; }

    public MedioPago MedioDePago { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string Cobrador { get; set; } = string.Empty;

    public string Emisor { get; set; } = string.Empty;

    public string NombrePasajero { get; set; } = string.Empty;

    public string NumeroDocumento { get; set; } = string.Empty;

    public int? NumeroAsiento { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string Destino { get; set; } = string.Empty;

    public DateTime FechaHoraSalida { get; set; }

    public decimal Importe { get; set; }

    public string Estado { get; set; } = string.Empty;

    public string? MotivoAnulacion { get; set; }

    public DateTime? FechaAnulacion { get; set; }
}
