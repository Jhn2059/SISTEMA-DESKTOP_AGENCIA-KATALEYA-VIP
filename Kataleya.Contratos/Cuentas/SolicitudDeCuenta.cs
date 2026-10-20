namespace Kataleya.Contratos.Cuentas;

public sealed class SolicitudDeCuenta
{
    public string Nombre { get; set; } = string.Empty;

    public string NombreUsuario { get; set; } = string.Empty;

    public string ContrasenaInicial { get; set; } = string.Empty;

    public short IdRol { get; set; }

    public short IdSede { get; set; }
}
