namespace Kataleya.Contratos.Bitacora;

public sealed class FiltroDeBitacora
{
    public int? UsuarioId { get; set; }

    public DateOnly? Desde { get; set; }

    public DateOnly? Hasta { get; set; }

    public long? PasajeId { get; set; }
}

public sealed class DatosDeBitacora
{
    public long Id { get; set; }

    public DateTime FechaHora { get; set; }

    public string Usuario { get; set; } = string.Empty;

    public string Operacion { get; set; } = string.Empty;

    public long? PasajeId { get; set; }

    public string? ValorAnterior { get; set; }

    public string? ValorNuevo { get; set; }
}
