using Kataleya.Aplicacion;
using Kataleya.Aplicacion.Services;
using Kataleya.Dominio.DataObjects;
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

        _usuarios.Add(Usuario.Reconstruir(new UsuarioGuardado(
            siguienteId,
            usuario.NombrePersona,
            usuario.NombreUsuario,
            usuario.ResumenContrasena,
            usuario.Sal,
            usuario.EsCuentaPrincipal,
            new RolGuardado(usuario.Rol.Id, usuario.Rol.Nombre),
            new SedeGuardada(usuario.Sede.Id, usuario.Sede.Nombre),
            usuario.Estado,
            usuario.CantidadDeIntentosFallidos,
            usuario.FinBloqueo)));

        return Task.CompletedTask;
    }

    public Task ActualizarAsync(Usuario usuario) =>
        Task.CompletedTask;
}

internal sealed class RepositorioCatalogosFalso : IRepositorioCatalogos
{
    public List<Rol> Roles { get; } =
    [
        Rol.Reconstruir(new RolGuardado(1, "Administrador")),
        Rol.Reconstruir(new RolGuardado(2, "Vendedor"))
    ];

    public List<Sede> Sedes { get; } =
    [
        Sede.Reconstruir(new SedeGuardada(1, "Central"))
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

    private EntradaDeBitacora CrearEntrada(RegistroDeBitacora registro) =>
        new()
        {
            Id = Registros.IndexOf(registro) + 1,
            FechaHora = DateTime.UtcNow,
            UsuarioNombre = registro.Usuario.NombreUsuario,
            Operacion = registro.Operacion,
            PasajeId = registro.PasajeId,
            ValorAnterior = registro.ValorAnterior,
            ValorNuevo = registro.ValorNuevo
        };
}

internal sealed class RepositorioPreferenciasFalso : IRepositorioPreferencias
{
    private readonly Dictionary<int, Preferencias> _preferencias = [];

    public Task<Preferencias?> BuscarAsync(int idUsuario) =>
        Task.FromResult(_preferencias.GetValueOrDefault(idUsuario));

    public Task GuardarAsync(Preferencias preferencias)
    {
        _preferencias[preferencias.IdUsuario] = preferencias;
        return Task.CompletedTask;
    }
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
        Preferencias = new RepositorioPreferenciasFalso();

        Administradora = Sembrar(1, "admin", "Admin1234", EstadoCuenta.Activa, esCuentaPrincipal: true, rol: 0);
        Contexto.Usuario = Administradora;

        var servicioBitacora = new ServicioBitacora(Bitacora, Contexto);
        var unidad = new UnidadDeTrabajoFalsa();

        Cuentas = new ServicioCuentas(
            Usuarios,
            Catalogos,
            Preferencias,
            servicioBitacora,
            unidad,
            Contexto);
        Sesion = new ServicioSesion(Usuarios, Preferencias, servicioBitacora, unidad, Reloj, new ContextoDeOperacion());
    }

    public RelojFalso Reloj { get; }

    public ContextoDeOperacion Contexto { get; }

    public RepositorioUsuariosFalso Usuarios { get; }

    public RepositorioCatalogosFalso Catalogos { get; }

    public RepositorioBitacoraFalso Bitacora { get; }

    public RepositorioPreferenciasFalso Preferencias { get; }

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

        return Sembrar(siguienteId, nombreUsuario, contrasena, estado, esCuentaPrincipal: false, rol: 1);
    }

    private Usuario Sembrar(
        int id,
        string nombreUsuario,
        string contrasena,
        EstadoCuenta estado,
        bool esCuentaPrincipal,
        int rol)
    {
        var usuario = Usuario.Reconstruir(new UsuarioGuardado(
            id,
            $"Persona {nombreUsuario}",
            nombreUsuario,
            string.Empty,
            string.Empty,
            esCuentaPrincipal,
            new RolGuardado(Catalogos.Roles[rol].Id, Catalogos.Roles[rol].Nombre),
            new SedeGuardada(Catalogos.Sedes[0].Id, Catalogos.Sedes[0].Nombre),
            estado,
            0,
            null));
        usuario.FijarContrasena(contrasena);
        Usuarios.Sembrar(usuario);
        return usuario;
    }
}
