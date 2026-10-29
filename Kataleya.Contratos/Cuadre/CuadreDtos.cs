namespace Kataleya.Contratos.Cuadre;

public sealed class FiltroDeCuadre
{
    public DateOnly? Desde { get; set; }

    public DateOnly? Hasta { get; set; }

    public int? EmisorId { get; set; }

    public short? SedeId { get; set; }
}

public sealed class DatosDelCuadre
{
    public List<DatosDeFilaDeCuadre> Filas { get; set; } = [];

    public List<DatosDePasajeDelCuadre> Pasajes { get; set; } = [];

    public DateTime GeneradoEl { get; set; }

    public string GeneradoPor { get; set; } = string.Empty;

    public string Sede { get; set; } = string.Empty;
}

public sealed class DatosDeFilaDeCuadre
{
    public int EmisorId { get; set; }

    public string Emisor { get; set; } = string.Empty;

    public int Pasajes { get; set; }

    public decimal Efectivo { get; set; }

    public decimal Yape { get; set; }

    public decimal AdelantosPorCobrar { get; set; }

    public int Anulados { get; set; }

    public decimal ImporteAnulado { get; set; }

    public decimal TotalNeto { get; set; }
}

public sealed class DatosDePasajeDelCuadre
{
    public long Id { get; set; }

    public string Pasajero { get; set; } = string.Empty;

    public string Documento { get; set; } = string.Empty;

    public string Emisor { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public decimal Importe { get; set; }

    public decimal Pagado { get; set; }

    public string? Boleto { get; set; }
}
