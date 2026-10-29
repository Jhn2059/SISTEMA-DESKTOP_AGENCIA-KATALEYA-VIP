namespace Kataleya.Dominio.Entities;

public sealed class ReporteAnual
{
    public int Anio { get; set; }

    public DateTime FechaHoraGeneracion { get; set; }

    public int UsuarioId { get; set; }

    public string UsuarioNombre { get; set; } = string.Empty;

    public int CantidadDePasajesDepurados { get; set; }

    public string RutaPdf { get; set; } = string.Empty;
}
