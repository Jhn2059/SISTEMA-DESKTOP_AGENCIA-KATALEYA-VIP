using Kataleya.Dominio.Enums;

namespace Kataleya.Contratos.Cuentas;

public sealed class DatosDeCuenta
{
    public int Id { get; set; }

    public string NombrePersona { get; set; } = string.Empty;

    public string NombreUsuario { get; set; } = string.Empty;

    public string Rol { get; set; } = string.Empty;

    public string Sede { get; set; } = string.Empty;

    public EstadoCuenta Estado { get; set; }

    public DateTime? FinBloqueo { get; set; }

    public bool PuedeDesactivarse { get; set; }

    // Sin tema elegido, la aplicación sigue el de Windows.
    public Tema? Tema { get; set; }
}
