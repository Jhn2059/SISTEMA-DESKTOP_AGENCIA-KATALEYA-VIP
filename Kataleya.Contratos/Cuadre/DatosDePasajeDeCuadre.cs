namespace Kataleya.Contratos.Cuadre;

public sealed class DatosDePasajeAnuladoCuadre
{
    public long PasajeId { get; set; }

    public long? GrupoId { get; set; }

    public string? CodigoDeGrupo { get; set; }

    public int? NumeroAsiento { get; set; }

    public string NombrePasajero { get; set; } = string.Empty;

    public string NumeroDocumento { get; set; } = string.Empty;

    public string Origen { get; set; } = string.Empty;

    public string Destino { get; set; } = string.Empty;

    public DateTime FechaHoraSalida { get; set; }

    public decimal Importe { get; set; }

    public decimal Pagado { get; set; }

    public decimal AdelantoRetenido { get; set; }

    public DateTime? FechaAnulacion { get; set; }

    public string? MotivoAnulacion { get; set; }
}

public sealed class DatosDeAdelantoPendienteCuadre
{
    public long PasajeId { get; set; }

    public long? GrupoId { get; set; }

    public string? CodigoDeGrupo { get; set; }

    public int? NumeroAsiento { get; set; }

    public string NombrePasajero { get; set; } = string.Empty;

    public string NumeroDocumento { get; set; } = string.Empty;

    public string Origen { get; set; } = string.Empty;

    public string Destino { get; set; } = string.Empty;

    public DateTime FechaHoraSalida { get; set; }

    public decimal Importe { get; set; }

    public decimal Pagado { get; set; }

    public decimal Saldo { get; set; }

    public string Estado { get; set; } = string.Empty;
}
