using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public sealed class Usuario
{
    private const int IntentosParaBloquear = 5;
    private const int MinutosDeBloqueo = 3;
    public const int LongitudMinimaDeContrasena = 8;
    public const int LongitudMaximaDelNombre = 100;
    public const int LongitudMaximaDelNombreUsuario = 30;

    // Una cuenta nueva nace activa, sin intentos fallidos y con su contraseña (CDU-09).
    public Usuario(SolicitudDeCuenta solicitud, Rol rol, Sede sede)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitud.NombrePersona);
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitud.NombreUsuario);

        NombrePersona = solicitud.NombrePersona.Trim();
        NombreUsuario = solicitud.NombreUsuario.Trim();
        Rol = rol ?? throw new ArgumentNullException(nameof(rol));
        Sede = sede ?? throw new ArgumentNullException(nameof(sede));
        Estado = EstadoCuenta.Activa;
        FijarContrasena(solicitud.ContrasenaInicial);
    }

    private Usuario()
    {
    }

    public int Id { get; private set; }

    public string NombrePersona { get; private set; } = string.Empty;

    public string NombreUsuario { get; private set; } = string.Empty;

    public string ResumenContrasena { get; private set; } = string.Empty;

    public string Sal { get; private set; } = string.Empty;

    public bool EsCuentaPrincipal { get; private set; }

    public EstadoCuenta Estado { get; private set; }

    public int CantidadDeIntentosFallidos { get; private set; }

    public DateTime? FinBloqueo { get; private set; }

    public Rol Rol { get; private set; } = null!;

    public Sede Sede { get; private set; } = null!;

    // Rehace la cuenta leída de la base, sin repetir las reglas de creación.
    public static Usuario Reconstruir(UsuarioGuardado guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return new Usuario
        {
            Id = guardado.Id,
            NombrePersona = guardado.NombrePersona,
            NombreUsuario = guardado.NombreUsuario,
            ResumenContrasena = guardado.ResumenContrasena,
            Sal = guardado.Sal,
            EsCuentaPrincipal = guardado.EsCuentaPrincipal,
            Estado = guardado.Estado,
            CantidadDeIntentosFallidos = guardado.CantidadDeIntentosFallidos,
            FinBloqueo = guardado.FinBloqueo,
            Rol = Rol.Reconstruir(guardado.Rol),
            Sede = Sede.Reconstruir(guardado.Sede)
        };
    }

    public bool VerificarContrasena(string contrasena) =>
        HuellaDeContrasena.Verificar(contrasena, ResumenContrasena, Sal);

    // Solo pregunta: si el bloqueo ya venció, la cuenta se desbloquea al registrar el próximo intento.
    public bool EstaBloqueada(DateTime ahora) =>
        Estado == EstadoCuenta.Bloqueada && FinBloqueo > ahora;

    public void RegistrarIntentoFallido(DateTime ahora)
    {
        if (Estado == EstadoCuenta.Bloqueada && !EstaBloqueada(ahora))
        {
            Desbloquear();
        }

        CantidadDeIntentosFallidos = Math.Min(CantidadDeIntentosFallidos + 1, IntentosParaBloquear);

        if (Estado != EstadoCuenta.Desactivada && CantidadDeIntentosFallidos >= IntentosParaBloquear)
        {
            Bloquear(ahora.AddMinutes(MinutosDeBloqueo));
        }
    }

    public void Bloquear(DateTime finBloqueo)
    {
        Estado = EstadoCuenta.Bloqueada;
        FinBloqueo = finBloqueo;
    }

    public void RegistrarAccesoExitoso()
    {
        Estado = EstadoCuenta.Activa;
        CantidadDeIntentosFallidos = 0;
        FinBloqueo = null;
    }

    // La cuenta principal es la de la administradora (RF-28).
    public bool PuedeDesactivarse() =>
        !EsCuentaPrincipal;

    public void Desactivar()
    {
        if (!PuedeDesactivarse())
        {
            throw new InvalidOperationException("La cuenta principal no se desactiva.");
        }

        Estado = EstadoCuenta.Desactivada;
        CantidadDeIntentosFallidos = 0;
        FinBloqueo = null;
    }

    public void Reactivar()
    {
        Estado = EstadoCuenta.Activa;
        CantidadDeIntentosFallidos = 0;
        FinBloqueo = null;
    }

    public void Desbloquear()
    {
        if (Estado == EstadoCuenta.Desactivada)
        {
            return;
        }

        Estado = EstadoCuenta.Activa;
        CantidadDeIntentosFallidos = 0;
        FinBloqueo = null;
    }

    public void FijarContrasena(string contrasena)
    {
        if (string.IsNullOrWhiteSpace(contrasena) || contrasena.Length < LongitudMinimaDeContrasena)
        {
            throw new ArgumentException(
                $"La contraseña debe tener al menos {LongitudMinimaDeContrasena} caracteres.",
                nameof(contrasena));
        }

        var (resumen, sal) = HuellaDeContrasena.Crear(contrasena);
        ResumenContrasena = resumen;
        Sal = sal;
    }
}
