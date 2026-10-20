using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public sealed class Usuario
{
    private const int IntentosParaBloquear = 5;
    private const int MinutosDeBloqueo = 3;
    public const int LongitudMinimaDeContrasena = 8;
    public const int LongitudMaximaDelNombre = 100;
    public const int LongitudMaximaDelNombreUsuario = 30;

    public Usuario(
        int id,
        string nombre,
        string nombreUsuario,
        string resumenContrasena,
        string sal,
        EstadoCuenta estado,
        int intentosFallidos,
        DateTime? finBloqueo,
        Rol rol,
        Sede sede)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreUsuario);
        ArgumentNullException.ThrowIfNull(rol);
        ArgumentNullException.ThrowIfNull(sede);

        Id = id;
        Nombre = nombre;
        NombreUsuario = nombreUsuario;
        ResumenContrasena = resumenContrasena;
        Sal = sal;
        Estado = estado;
        IntentosFallidos = intentosFallidos;
        FinBloqueo = finBloqueo;
        Rol = rol;
        Sede = sede;
    }

    public int Id { get; }

    public string Nombre { get; }

    public string NombreUsuario { get; }

    public string ResumenContrasena { get; private set; }

    public string Sal { get; private set; }

    public EstadoCuenta Estado { get; private set; }

    public int IntentosFallidos { get; private set; }

    public DateTime? FinBloqueo { get; private set; }

    public Rol Rol { get; }

    public Sede Sede { get; }

    public bool VerificarContrasena(string contrasena) =>
        HuellaDeContrasena.Verificar(contrasena, ResumenContrasena, Sal);

    public bool EstaBloqueada(DateTime ahora)
    {
        if (Estado != EstadoCuenta.Bloqueada)
        {
            return false;
        }

        if (FinBloqueo is null || FinBloqueo <= ahora)
        {
            Estado = EstadoCuenta.Activa;
            FinBloqueo = null;
            IntentosFallidos = 0;
            return false;
        }

        return true;
    }

    public void RegistrarIntentoFallido(DateTime ahora)
    {
        IntentosFallidos = Math.Min(IntentosFallidos + 1, IntentosParaBloquear);

        if (Estado != EstadoCuenta.Desactivada && IntentosFallidos >= IntentosParaBloquear)
        {
            Estado = EstadoCuenta.Bloqueada;
            FinBloqueo = ahora.AddMinutes(MinutosDeBloqueo);
        }
    }

    public void RegistrarAccesoExitoso()
    {
        Estado = EstadoCuenta.Activa;
        IntentosFallidos = 0;
        FinBloqueo = null;
    }

    public void Desactivar()
    {
        Estado = EstadoCuenta.Desactivada;
        IntentosFallidos = 0;
        FinBloqueo = null;
    }

    public void Reactivar()
    {
        Estado = EstadoCuenta.Activa;
        IntentosFallidos = 0;
        FinBloqueo = null;
    }

    public void Desbloquear()
    {
        if (Estado == EstadoCuenta.Desactivada)
        {
            return;
        }

        Estado = EstadoCuenta.Activa;
        IntentosFallidos = 0;
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
