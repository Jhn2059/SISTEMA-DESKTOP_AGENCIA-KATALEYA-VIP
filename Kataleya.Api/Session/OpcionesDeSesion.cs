namespace Kataleya.Api.Session;

public sealed class OpcionesDeSesion
{
    public const string Seccion = "Sesion";

    public string ClaveSecreta { get; set; } = string.Empty;

    public int HorasDeValidez { get; set; } = 12;
}
