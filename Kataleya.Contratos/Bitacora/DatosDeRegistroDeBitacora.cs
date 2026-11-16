namespace Kataleya.Contratos.Bitacora;

public sealed class DatosDeRegistroDeBitacora
{
    public long Id { get; set; }

    public DateTime FechaHora { get; set; }

    public int UsuarioId { get; set; }

    public string Usuario { get; set; } = string.Empty;

    public string Operacion { get; set; } = string.Empty;

    public long? PasajeId { get; set; }

    public string? ValorAnterior { get; set; }

    public string? ValorNuevo { get; set; }

    public Guid? ClaveDeOperacion { get; set; }
}
