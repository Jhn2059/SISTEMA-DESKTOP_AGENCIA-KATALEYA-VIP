using Kataleya.Contratos.Cuentas;
using Kataleya.Dominio.Enums;

namespace Kataleya.Escritorio.Features.GestionDeUsuarios;

public sealed class CuentaEnLista
{
    public CuentaEnLista(DatosDeCuenta cuenta) =>
        Cuenta = cuenta ?? throw new ArgumentNullException(nameof(cuenta));

    public DatosDeCuenta Cuenta { get; }

    public int Id => Cuenta.Id;

    public string Nombre => Cuenta.Nombre;

    public string NombreUsuario => Cuenta.NombreUsuario;

    public string Rol => Cuenta.Rol;

    public string Sede => Cuenta.Sede;

    public string Estado => Cuenta.Estado switch
    {
        EstadoCuenta.Activa => "Activa",
        EstadoCuenta.Bloqueada => "Bloqueada",
        EstadoCuenta.Desactivada => "Desactivada",
        _ => Cuenta.Estado.ToString()
    };

    public bool EsAdministradora =>
        Cuenta.NombreUsuario.Equals("admin", StringComparison.OrdinalIgnoreCase);

    public bool MostrarDesactivar => Cuenta.Estado != EstadoCuenta.Desactivada;

    public bool PuedeDesactivarse => MostrarDesactivar && !EsAdministradora;

    public bool MostrarReactivar => Cuenta.Estado == EstadoCuenta.Desactivada;

    public bool MostrarDesbloquear => Cuenta.Estado == EstadoCuenta.Bloqueada;

    public bool TieneAdvertencia => EsAdministradora && Cuenta.Estado != EstadoCuenta.Desactivada;

    public string Advertencia =>
        TieneAdvertencia ? "La cuenta de la administradora no puede desactivarse." : string.Empty;
}
