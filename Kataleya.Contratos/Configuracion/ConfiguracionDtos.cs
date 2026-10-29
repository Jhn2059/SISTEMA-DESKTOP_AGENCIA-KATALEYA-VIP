using Kataleya.Dominio.Enums;

namespace Kataleya.Contratos.Configuracion;

public sealed class DatosDeTarifa
{
    public int Id { get; set; }

    public TipoDeTarifa Tipo { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public decimal Importe { get; set; }

    public DateOnly? VigenciaDesde { get; set; }

    public DateOnly? VigenciaHasta { get; set; }

    public SentidoDelViaje Sentido { get; set; }

    public string FijadaPor { get; set; } = string.Empty;

    public DateTime FechaHora { get; set; }
}

public sealed class SolicitudDeTarifa
{
    public TipoDeTarifa Tipo { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public decimal Importe { get; set; }

    public DateOnly? VigenciaDesde { get; set; }

    public DateOnly? VigenciaHasta { get; set; }

    public SentidoDelViaje Sentido { get; set; }
}

public sealed class SolicitudDeTarifaRegular
{
    public decimal Importe { get; set; }
}

public sealed class DatosDeTipoDeVehiculo
{
    public short Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public short NumeroAsientos { get; set; }
}

public sealed class DatosDeParametro
{
    public string Nombre { get; set; } = string.Empty;

    public string Valor { get; set; } = string.Empty;

    public string? CambiadoPor { get; set; }

    public DateTime? FechaHora { get; set; }
}

public sealed class SolicitudDeParametro
{
    public string Valor { get; set; } = string.Empty;
}

public sealed class SolicitudDeHorario
{
    public string Hora { get; set; } = string.Empty;
}
