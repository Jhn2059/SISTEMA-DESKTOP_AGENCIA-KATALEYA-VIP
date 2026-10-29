using Kataleya.Aplicacion;
using Kataleya.Aplicacion.Services;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Pruebas;

internal sealed class RepositorioUsuariosFalso : IRepositorioUsuarios
{
    private readonly List<Usuario> _usuarios = [];

    public IReadOnlyList<Usuario> Todos => _usuarios;

    public void Sembrar(Usuario usuario) =>
        _usuarios.Add(usuario);

    public Task<Usuario?> BuscarPorNombreUsuarioAsync(string nombreUsuario) =>
        Task.FromResult(_usuarios.FirstOrDefault(usuario =>
            usuario.NombreUsuario.Equals(nombreUsuario, StringComparison.OrdinalIgnoreCase)));

    public Task<Usuario?> BuscarPorIdAsync(int id) =>
        Task.FromResult(_usuarios.FirstOrDefault(usuario => usuario.Id == id));

    public Task<IReadOnlyList<Usuario>> ListarAsync() =>
        Task.FromResult<IReadOnlyList<Usuario>>(_usuarios.ToList());

    public Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario) =>
        Task.FromResult(_usuarios.Any(usuario =>
            usuario.NombreUsuario.Equals(nombreUsuario, StringComparison.OrdinalIgnoreCase)));

    public Task AgregarAsync(Usuario usuario)
    {
        var siguienteId = _usuarios.Count == 0 ? 1 : _usuarios.Max(item => item.Id) + 1;

        _usuarios.Add(new Usuario(
            siguienteId,
            usuario.Nombre,
            usuario.NombreUsuario,
            usuario.ResumenContrasena,
            usuario.Sal,
            usuario.Estado,
            usuario.IntentosFallidos,
            usuario.FinBloqueo,
            usuario.Rol,
            usuario.Sede));

        return Task.CompletedTask;
    }

    public Task ActualizarAsync(Usuario usuario) =>
        Task.CompletedTask;
}

internal sealed class RepositorioCatalogosFalso : IRepositorioCatalogos
{
    public List<Rol> Roles { get; } =
    [
        new(1, "Administrador"),
        new(2, "Vendedor")
    ];

    public List<Sede> Sedes { get; } =
    [
        new(1, "Central")
    ];

    public Task<IReadOnlyList<Rol>> ListarRolesAsync() =>
        Task.FromResult<IReadOnlyList<Rol>>(Roles);

    public Task<IReadOnlyList<Sede>> ListarSedesAsync() =>
        Task.FromResult<IReadOnlyList<Sede>>(Sedes);

    public Task<Rol?> BuscarRolAsync(short id) =>
        Task.FromResult(Roles.FirstOrDefault(rol => rol.Id == id));

    public Task<Sede?> BuscarSedeAsync(short id) =>
        Task.FromResult(Sedes.FirstOrDefault(sede => sede.Id == id));
}

internal sealed class RepositorioBitacoraFalso : IRepositorioBitacora
{
    public List<RegistroDeBitacora> Registros { get; } = [];

    public Task AgregarAsync(RegistroDeBitacora registro)
    {
        Registros.Add(registro);
        return Task.CompletedTask;
    }

    public Task<bool> ExisteClaveAsync(Guid clave) =>
        Task.FromResult(Registros.Any(registro => registro.ClaveDeOperacion == clave));

    public Task<EntradaDeBitacora?> BuscarPorClaveAsync(Guid clave) =>
        Task.FromResult(Registros
            .Where(registro => registro.ClaveDeOperacion == clave)
            .Select(CrearEntrada)
            .FirstOrDefault());

    public Task<IReadOnlyList<EntradaDeBitacora>> ConsultarAsync(FiltroDeBitacora filtro) =>
        Task.FromResult<IReadOnlyList<EntradaDeBitacora>>(Registros
            .Where(registro => filtro.UsuarioId is null || registro.Usuario.Id == filtro.UsuarioId)
            .Where(registro => filtro.PasajeId is null || registro.PasajeId == filtro.PasajeId)
            .Select(CrearEntrada)
            .ToList());

    public Task<IReadOnlyList<EntradaDeBitacora>> BuscarHistoriaDeAsync(IReadOnlyCollection<long> idsDePasajes) =>
        Task.FromResult<IReadOnlyList<EntradaDeBitacora>>(Registros
            .Where(registro => registro.PasajeId is long id && idsDePasajes.Contains(id))
            .Select(CrearEntrada)
            .ToList());

    public Task EliminarHistoriaDeAsync(IReadOnlyCollection<long> idsDePasajes)
    {
        Registros.RemoveAll(registro => registro.PasajeId is long id && idsDePasajes.Contains(id));
        return Task.CompletedTask;
    }

    private static EntradaDeBitacora CrearEntrada(RegistroDeBitacora registro) =>
        new()
        {
            Id = registro.Id,
            FechaHora = registro.FechaHora,
            UsuarioNombre = registro.Usuario.NombreUsuario,
            Operacion = registro.Operacion,
            PasajeId = registro.PasajeId,
            ValorAnterior = registro.ValorAnterior,
            ValorNuevo = registro.ValorNuevo
        };
}

internal sealed class UnidadDeTrabajoFalsa : IUnidadDeTrabajo
{
    public Task EjecutarEnTransaccionAsync(Func<Task> trabajo) =>
        trabajo();
}

internal sealed class RelojFalso : IReloj
{
    public RelojFalso(DateTime ahora) =>
        Ahora = ahora;

    public DateTime Ahora { get; set; }
}

internal sealed class Escenario
{
    public Escenario()
    {
        Reloj = new RelojFalso(new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc));
        Contexto = new ContextoDeOperacion();
        Usuarios = new RepositorioUsuariosFalso();
        Catalogos = new RepositorioCatalogosFalso();
        Bitacora = new RepositorioBitacoraFalso();

        Administradora = new Usuario(
            1,
            "Administradora",
            "admin",
            string.Empty,
            string.Empty,
            EstadoCuenta.Activa,
            0,
            null,
            Catalogos.Roles[0],
            Catalogos.Sedes[0]);
        Administradora.FijarContrasena("Admin1234");
        Usuarios.Sembrar(Administradora);
        Contexto.IdUsuario = Administradora.Id;

        var servicioBitacora = new ServicioBitacora(Bitacora, Contexto);
        var unidad = new UnidadDeTrabajoFalsa();

        Cuentas = new ServicioCuentas(
            Usuarios,
            Catalogos,
            servicioBitacora,
            unidad,
            Reloj,
            Contexto);
        Sesion = new ServicioSesion(Usuarios, servicioBitacora, unidad, Reloj);
    }

    public RelojFalso Reloj { get; }

    public ContextoDeOperacion Contexto { get; }

    public RepositorioUsuariosFalso Usuarios { get; }

    public RepositorioCatalogosFalso Catalogos { get; }

    public RepositorioBitacoraFalso Bitacora { get; }

    public Usuario Administradora { get; }

    public ServicioCuentas Cuentas { get; }

    public ServicioSesion Sesion { get; }

    public Usuario AgregarUsuario(
        string nombreUsuario,
        string contrasena,
        EstadoCuenta estado = EstadoCuenta.Activa)
    {
        var siguienteId = Usuarios.Todos.Count == 0
            ? 1
            : Usuarios.Todos.Max(usuario => usuario.Id) + 1;

        var usuario = new Usuario(
            siguienteId,
            $"Persona {nombreUsuario}",
            nombreUsuario,
            string.Empty,
            string.Empty,
            estado,
            0,
            null,
            Catalogos.Roles[1],
            Catalogos.Sedes[0]);
        usuario.FijarContrasena(contrasena);
        Usuarios.Sembrar(usuario);
        return usuario;
    }
}
