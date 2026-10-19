namespace Kataleya.Contratos.Session;

public sealed class SolicitudDeInicioDeSesion
{
    public string NombreUsuario { get; set; } = string.Empty;

    public string Contrasena { get; set; } = string.Empty;
}
