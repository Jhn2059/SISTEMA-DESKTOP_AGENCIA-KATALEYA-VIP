using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Session;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioSesion
{
    private const string OperacionDeAcceso = "Iniciar sesión";

    private readonly IRepositorioUsuarios _repositorioUsuarios;
    private readonly IRepositorioPreferencias _preferencias;
    private readonly ServicioBitacora _servicioBitacora;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public ServicioSesion(
        IRepositorioUsuarios repositorioUsuarios,
        IRepositorioPreferencias preferencias,
        ServicioBitacora servicioBitacora,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _repositorioUsuarios = repositorioUsuarios ?? throw new ArgumentNullException(nameof(repositorioUsuarios));
        _preferencias = preferencias ?? throw new ArgumentNullException(nameof(preferencias));
        _servicioBitacora = servicioBitacora ?? throw new ArgumentNullException(nameof(servicioBitacora));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public async Task<ResultadoDeInicioDeSesion> IniciarSesionAsync(string nombreUsuario, string contrasena)
    {
        var ahora = _reloj.Ahora;
        var usuario = await _repositorioUsuarios.BuscarPorNombreUsuarioAsync(nombreUsuario);

        if (usuario is null)
        {
            return Rechazo(MotivoDeRechazo.DatosIncorrectos);
        }

        if (usuario.EstaBloqueada(ahora))
        {
            return Rechazo(MotivoDeRechazo.CuentaBloqueada, usuario.FinBloqueo);
        }

        if (!usuario.VerificarContrasena(contrasena))
        {
            usuario.RegistrarIntentoFallido(ahora);
            await _repositorioUsuarios.ActualizarAsync(usuario);

            return usuario.EstaBloqueada(ahora)
                ? Rechazo(MotivoDeRechazo.CuentaBloqueada, usuario.FinBloqueo)
                : Rechazo(MotivoDeRechazo.DatosIncorrectos);
        }

        if (usuario.Estado == EstadoCuenta.Desactivada)
        {
            return Rechazo(MotivoDeRechazo.CuentaDesactivada);
        }

        usuario.RegistrarAccesoExitoso();
        _contexto.Usuario = usuario;

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _repositorioUsuarios.ActualizarAsync(usuario);
            await _servicioBitacora.RegistrarAsync(
                new MovimientoDeBitacora(OperacionDeAcceso, null, null, null));
        });

        return new ResultadoDeInicioDeSesion
        {
            Cuenta = new DatosDeCuenta
            {
                Id = usuario.Id,
                NombrePersona = usuario.NombrePersona,
                NombreUsuario = usuario.NombreUsuario,
                Rol = usuario.Rol.Nombre,
                Sede = usuario.Sede.Nombre,
                Estado = usuario.Estado,
                FinBloqueo = usuario.FinBloqueo,
                PuedeDesactivarse = usuario.PuedeDesactivarse(),
                Tema = (await _preferencias.BuscarAsync(usuario.Id))?.Tema
            }
        };
    }

    private static ResultadoDeInicioDeSesion Rechazo(MotivoDeRechazo motivo, DateTime? finBloqueo = null) =>
        new()
        {
            Rechazo = motivo,
            FinBloqueo = finBloqueo
        };
}
