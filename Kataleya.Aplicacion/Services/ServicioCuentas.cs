using System.Text.Json;
using Kataleya.Aplicacion;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioCuentas
{
    private const string OperacionCrear = "Crear cuenta";
    private const string OperacionDesactivar = "Desactivar cuenta";
    private const string OperacionReactivar = "Reactivar cuenta";
    private const string OperacionDesbloquear = "Desbloquear cuenta";
    private const string OperacionRestablecer = "Restablecer contraseña";

    private readonly IRepositorioUsuarios _usuarios;
    private readonly IRepositorioCatalogos _catalogos;
    private readonly IRepositorioPreferencias _preferencias;
    private readonly ServicioBitacora _bitacora;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly ContextoDeOperacion _contexto;

    public ServicioCuentas(
        IRepositorioUsuarios usuarios,
        IRepositorioCatalogos catalogos,
        IRepositorioPreferencias preferencias,
        ServicioBitacora bitacora,
        IUnidadDeTrabajo unidadDeTrabajo,
        ContextoDeOperacion contexto)
    {
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _catalogos = catalogos ?? throw new ArgumentNullException(nameof(catalogos));
        _preferencias = preferencias ?? throw new ArgumentNullException(nameof(preferencias));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public Task<IReadOnlyList<Usuario>> ListarAsync() =>
        _usuarios.ListarAsync();

    public Task<Usuario?> ObtenerAsync(int id) =>
        _usuarios.BuscarPorIdAsync(id);

    public async Task<Usuario> CrearCuentaAsync(SolicitudDeCuenta solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var nombrePersona = solicitud.NombrePersona.Trim();
        var nombreUsuario = solicitud.NombreUsuario.Trim();

        if (string.IsNullOrWhiteSpace(nombrePersona))
        {
            throw new ReglaDeNegocioException("Escriba el nombre.", "nombrePersona");
        }

        if (string.IsNullOrWhiteSpace(nombreUsuario))
        {
            throw new ReglaDeNegocioException("Escriba el nombre de usuario.", "nombreUsuario");
        }

        if (nombrePersona.Length > Usuario.LongitudMaximaDelNombre)
        {
            throw new ReglaDeNegocioException(
                $"El nombre no puede superar los {Usuario.LongitudMaximaDelNombre} caracteres.",
                "nombrePersona");
        }

        if (nombreUsuario.Length > Usuario.LongitudMaximaDelNombreUsuario)
        {
            throw new ReglaDeNegocioException(
                $"El nombre de usuario no puede superar los {Usuario.LongitudMaximaDelNombreUsuario} caracteres.",
                "nombreUsuario");
        }

        if (string.IsNullOrWhiteSpace(solicitud.ContrasenaInicial) ||
            solicitud.ContrasenaInicial.Length < Usuario.LongitudMinimaDeContrasena)
        {
            throw new ReglaDeNegocioException(
                $"La contraseña debe tener al menos {Usuario.LongitudMinimaDeContrasena} caracteres.",
                "contrasenaInicial");
        }

        if (await YaSeRegistroAsync())
        {
            return await _usuarios.BuscarPorNombreUsuarioAsync(nombreUsuario)
                ?? throw new InvalidOperationException(
                    "La operación ya se registró, pero no se encontró la cuenta creada.");
        }

        if (await _usuarios.ExisteNombreUsuarioAsync(nombreUsuario))
        {
            throw new ReglaDeNegocioException(
                "Ese nombre de usuario ya existe.",
                "nombreUsuario");
        }

        var rol = await _catalogos.BuscarRolAsync(solicitud.IdRol)
            ?? throw new ReglaDeNegocioException("Elija un rol válido.", "idRol");
        var sede = await _catalogos.BuscarSedeAsync(solicitud.IdSede)
            ?? throw new ReglaDeNegocioException("Elija una sede válida.", "idSede");

        var usuario = new Usuario(solicitud, rol, sede);

        Usuario? creada = null;

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _usuarios.AgregarAsync(usuario);
            creada = await _usuarios.BuscarPorNombreUsuarioAsync(nombreUsuario);

            await RegistrarBitacoraAsync(
                OperacionCrear,
                null,
                Serializar(creada ?? usuario));
        });

        return creada
            ?? await _usuarios.BuscarPorNombreUsuarioAsync(nombreUsuario)
            ?? throw new InvalidOperationException("No se encontró la cuenta recién creada.");
    }

    public async Task DesactivarAsync(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        if (!usuario.PuedeDesactivarse())
        {
            throw new ReglaDeNegocioException(
                "La cuenta de la administradora no puede desactivarse.",
                "cuenta");
        }

        if (await YaSeRegistroAsync() || usuario.Estado == EstadoCuenta.Desactivada)
        {
            return;
        }

        var anterior = Serializar(usuario);
        usuario.Desactivar();

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _usuarios.ActualizarAsync(usuario);
            await RegistrarBitacoraAsync(OperacionDesactivar, anterior, Serializar(usuario));
        });
    }

    public async Task ReactivarAsync(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        if (await YaSeRegistroAsync())
        {
            return;
        }

        if (usuario.Estado != EstadoCuenta.Desactivada)
        {
            throw new ReglaDeNegocioException("La cuenta no está desactivada.", "cuenta");
        }

        var anterior = Serializar(usuario);
        usuario.Reactivar();

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _usuarios.ActualizarAsync(usuario);
            await RegistrarBitacoraAsync(OperacionReactivar, anterior, Serializar(usuario));
        });
    }

    public async Task DesbloquearAsync(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        if (await YaSeRegistroAsync())
        {
            return;
        }

        if (usuario.Estado == EstadoCuenta.Desactivada)
        {
            throw new ReglaDeNegocioException(
                "La cuenta está desactivada; primero reactívela.",
                "cuenta");
        }

        if (usuario.Estado != EstadoCuenta.Bloqueada)
        {
            return;
        }

        var anterior = Serializar(usuario);
        usuario.Desbloquear();

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _usuarios.ActualizarAsync(usuario);
            await RegistrarBitacoraAsync(OperacionDesbloquear, anterior, Serializar(usuario));
        });
    }

    public async Task RestablecerContrasenaAsync(Usuario usuario, string contrasenaNueva)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        if (string.IsNullOrWhiteSpace(contrasenaNueva) ||
            contrasenaNueva.Length < Usuario.LongitudMinimaDeContrasena)
        {
            throw new ReglaDeNegocioException(
                $"La contraseña debe tener al menos {Usuario.LongitudMinimaDeContrasena} caracteres.",
                "contrasenaNueva");
        }

        if (await YaSeRegistroAsync())
        {
            return;
        }

        usuario.FijarContrasena(contrasenaNueva);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _usuarios.ActualizarAsync(usuario);
            await RegistrarBitacoraAsync(
                OperacionRestablecer,
                null,
                Serializar(usuario));
        });
    }

    // CDU-20: cada cuenta guarda su tema; lo usa al iniciar sesión en cualquier laptop.
    public async Task ElegirTemaAsync(Tema tema)
    {
        var preferencias = await _preferencias.BuscarAsync(_contexto.Usuario.Id)
            ?? new Preferencias(_contexto.Usuario.Id, tema);

        preferencias.ElegirTema(tema);
        await _preferencias.GuardarAsync(preferencias);
    }

    private async Task<bool> YaSeRegistroAsync() =>
        _contexto.ClaveDeOperacion is Guid clave &&
        await _bitacora.ExisteAsync(clave);

    private Task RegistrarBitacoraAsync(
        string operacion,
        string? anterior,
        string? nuevo) =>
        _bitacora.RegistrarAsync(new MovimientoDeBitacora(operacion, null, anterior, nuevo));

    private static string Serializar(Usuario usuario) =>
        JsonSerializer.Serialize(new
        {
            usuario.Id,
            usuario.NombrePersona,
            usuario.NombreUsuario,
            Rol = usuario.Rol.Nombre,
            Sede = usuario.Sede.Nombre,
            Estado = usuario.Estado.ToString(),
            usuario.FinBloqueo
        });
}
