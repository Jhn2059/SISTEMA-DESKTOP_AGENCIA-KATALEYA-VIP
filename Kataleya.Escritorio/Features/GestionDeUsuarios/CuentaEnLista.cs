using CommunityToolkit.Mvvm.ComponentModel;
using Kataleya.Contratos.Cuentas;
using Kataleya.Dominio.Enums;

namespace Kataleya.Escritorio.Features.GestionDeUsuarios;

public partial class CuentaEnLista : ObservableObject
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

    public bool EsActiva => Cuenta.Estado == EstadoCuenta.Activa;

    public bool EsBloqueada => Cuenta.Estado == EstadoCuenta.Bloqueada;

    public bool EsDesactivada => Cuenta.Estado == EstadoCuenta.Desactivada;

    public bool TieneDetalleEstado => EsBloqueada && Cuenta.FinBloqueo is not null;

    public string DetalleEstado
    {
        get
        {
            if (!TieneDetalleEstado)
            {
                return string.Empty;
            }

            var hora = Cuenta.FinBloqueo!.Value.ToLocalTime();
            return $"Se desbloquea sola a las {hora:HH:mm}";
        }
    }

    public bool EsAdministradora =>
        Cuenta.NombreUsuario.Equals("admin", StringComparison.OrdinalIgnoreCase);

    public bool MostrarDesactivar => Cuenta.Estado != EstadoCuenta.Desactivada;

    public bool PuedeDesactivarse => MostrarDesactivar && !EsAdministradora;

    public bool MostrarReactivar => Cuenta.Estado == EstadoCuenta.Desactivada;

    public bool MostrarDesbloquear => Cuenta.Estado == EstadoCuenta.Bloqueada;

    public bool TieneAdvertencia => EsAdministradora && Cuenta.Estado != EstadoCuenta.Desactivada;

    public string Advertencia =>
        TieneAdvertencia ? "La cuenta de la administradora no puede desactivarse." : string.Empty;

    public bool EsFilaAlterna { get; set; }

    [ObservableProperty]
    private bool esSeleccionada;
}
