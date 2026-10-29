namespace Kataleya.Dominio.Entities;

public sealed class ResumenDelPanel
{
    public int ReservasRegistradas { get; set; }

    public int Ventas { get; set; }

    public decimal CobradoEnEfectivo { get; set; }

    public decimal CobradoPorYape { get; set; }

    public decimal AdelantosPorCobrar { get; set; }

    public List<FilaDeHorario> Horarios { get; set; } = [];
}

public sealed class FilaDeHorario
{
    public TimeOnly Hora { get; set; }

    public int Pasajes { get; set; }

    public int Pagados { get; set; }

    public int Reservados { get; set; }

    public int ConAdelanto { get; set; }
}

public sealed class FilaDeCuadre
{
    public int EmisorId { get; set; }

    public string EmisorNombre { get; set; } = string.Empty;

    public int Pasajes { get; set; }

    public decimal Efectivo { get; set; }

    public decimal Yape { get; set; }

    public decimal AdelantosPorCobrar { get; set; }

    public int Anulados { get; set; }

    public decimal ImporteAnulado { get; set; }

    public decimal TotalNeto => Efectivo + Yape;
}

public sealed class DetalleDelCuadre
{
    public List<FilaDeCuadre> Filas { get; set; } = [];

    public List<PasajeDelCuadre> Pasajes { get; set; } = [];

    public DateTime GeneradoEl { get; set; }

    public string GeneradoPor { get; set; } = string.Empty;

    public string Sede { get; set; } = string.Empty;
}

public sealed class PasajeDelCuadre
{
    public long Id { get; set; }

    public string PasajeroNombre { get; set; } = string.Empty;

    public string NumeroDocumento { get; set; } = string.Empty;

    public string EmisorNombre { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public decimal Importe { get; set; }

    public decimal Pagado { get; set; }

    public string? Boleto { get; set; }
}
