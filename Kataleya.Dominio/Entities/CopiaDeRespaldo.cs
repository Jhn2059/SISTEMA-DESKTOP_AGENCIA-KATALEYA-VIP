namespace Kataleya.Dominio.Entities;

public sealed class CopiaDeRespaldo
{
    public int Id { get; set; }

    public DateTime FechaHora { get; set; }

    public bool Correcta { get; set; }

    public string? NombreArchivo { get; set; }
}

public sealed class EstadoDelRespaldo
{
    public DateTime? FechaHoraUltimaCopia { get; set; }

    public bool UltimaCopiaCorrecta { get; set; }

    public int FallosSeguidos { get; set; }
}
