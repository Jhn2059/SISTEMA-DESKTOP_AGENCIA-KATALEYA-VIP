using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public sealed class TipoDeVehiculo
{
    public short Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public short NumeroAsientos { get; set; }
}

public sealed class ParametroDelNegocio
{
    public string Nombre { get; set; } = string.Empty;

    public string Valor { get; set; } = string.Empty;

    public int? UsuarioId { get; set; }

    public DateTime FechaHora { get; set; }
}

public sealed class Tarifa
{
    public int Id { get; set; }

    public TipoDeTarifa Tipo { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public decimal Importe { get; set; }

    public DateOnly? VigenciaDesde { get; set; }

    public DateOnly? VigenciaHasta { get; set; }

    public SentidoDelViaje Sentido { get; set; }

    public int UsuarioId { get; set; }

    public string UsuarioNombre { get; set; } = string.Empty;

    public DateTime FechaHora { get; set; }

    public bool EstaVigente(DateOnly fecha) =>
        Tipo == TipoDeTarifa.Programada &&
        VigenciaDesde is not null &&
        VigenciaHasta is not null &&
        fecha >= VigenciaDesde.Value &&
        fecha <= VigenciaHasta.Value;
}
