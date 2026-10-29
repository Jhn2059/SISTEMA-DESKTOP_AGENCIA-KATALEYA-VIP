namespace Kataleya.Contratos.Reportes;

public sealed class ArchivoProtegido
{
    public string Nombre { get; set; } = string.Empty;

    public byte[] Contenido { get; set; } = [];
}

public sealed class SolicitudDeReporteAnual
{
    public string ContrasenaPdf { get; set; } = string.Empty;
}

public sealed class SolicitudDeDepuracion
{
    public string RutaPdf { get; set; } = string.Empty;
}

public sealed class DatosDelReporteAnual
{
    public int Anio { get; set; }

    public DateTime FechaHoraGeneracion { get; set; }

    public string GeneradoPor { get; set; } = string.Empty;

    public int CantidadDePasajesDepurados { get; set; }

    public string RutaPdf { get; set; } = string.Empty;
}
