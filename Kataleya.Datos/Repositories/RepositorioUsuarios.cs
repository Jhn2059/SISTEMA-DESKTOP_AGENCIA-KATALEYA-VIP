using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioUsuarios : IRepositorioUsuarios
{
    private const string SeleccionarUsuario = """
        SELECT u.id,
               u.nombre_persona,
               u.nombre_usuario,
               u.resumen_contrasena,
               u.sal,
               u.estado,
               u.cantidad_intentos_fallidos,
               u.fin_bloqueo,
               r.id,
               r.nombre,
               s.id,
               s.nombre,
               u.es_cuenta_principal
        FROM usuario u
        INNER JOIN rol r ON r.id = u.rol_id
        INNER JOIN sede s ON s.id = u.sede_id
        """;

    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioUsuarios(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<Usuario?> BuscarPorNombreUsuarioAsync(string nombreUsuario)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"{SeleccionarUsuario} WHERE u.nombre_usuario = @nombre_usuario",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("nombre_usuario", NpgsqlDbType.Varchar).Value = nombreUsuario;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? LeerUsuario(lector) : null;
    }

    public async Task<Usuario?> BuscarPorIdAsync(int id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"{SeleccionarUsuario} WHERE u.id = @id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Integer).Value = id;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? LeerUsuario(lector) : null;
    }

    public async Task<IReadOnlyList<Usuario>> ListarAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"{SeleccionarUsuario} ORDER BY u.nombre_usuario",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        var usuarios = new List<Usuario>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            usuarios.Add(LeerUsuario(lector));
        }

        return usuarios;
    }

    public async Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM usuario WHERE nombre_usuario = @nombre_usuario)",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("nombre_usuario", NpgsqlDbType.Varchar).Value = nombreUsuario;

        var resultado = await comando.ExecuteScalarAsync();
        return resultado is true;
    }

    public async Task AgregarAsync(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO usuario
                (nombre_persona, nombre_usuario, resumen_contrasena, sal, rol_id, sede_id,
                 estado, cantidad_intentos_fallidos, fin_bloqueo)
            VALUES
                (@nombre_persona, @nombre_usuario, @resumen_contrasena, @sal, @rol_id, @sede_id,
                 @estado, @cantidad_intentos_fallidos, @fin_bloqueo)
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        comando.Parameters.Add("nombre_persona", NpgsqlDbType.Varchar).Value = usuario.NombrePersona;
        comando.Parameters.Add("nombre_usuario", NpgsqlDbType.Varchar).Value = usuario.NombreUsuario;
        comando.Parameters.Add("resumen_contrasena", NpgsqlDbType.Text).Value = usuario.ResumenContrasena;
        comando.Parameters.Add("sal", NpgsqlDbType.Text).Value = usuario.Sal;
        comando.Parameters.Add("rol_id", NpgsqlDbType.Smallint).Value = usuario.Rol.Id;
        comando.Parameters.Add("sede_id", NpgsqlDbType.Smallint).Value = usuario.Sede.Id;
        comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value = usuario.Estado.ToString();
        comando.Parameters.Add("cantidad_intentos_fallidos", NpgsqlDbType.Smallint).Value =
            (short)usuario.CantidadDeIntentosFallidos;
        comando.Parameters.Add("fin_bloqueo", NpgsqlDbType.TimestampTz).Value =
            usuario.FinBloqueo is null ? DBNull.Value : usuario.FinBloqueo.Value;

        await comando.ExecuteNonQueryAsync();
    }

    public async Task ActualizarAsync(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            UPDATE usuario
            SET nombre_persona = @nombre_persona,
                nombre_usuario = @nombre_usuario,
                resumen_contrasena = @resumen_contrasena,
                sal = @sal,
                rol_id = @rol_id,
                sede_id = @sede_id,
                estado = @estado,
                cantidad_intentos_fallidos = @cantidad_intentos_fallidos,
                fin_bloqueo = @fin_bloqueo
            WHERE id = @id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        comando.Parameters.Add("nombre_persona", NpgsqlDbType.Varchar).Value = usuario.NombrePersona;
        comando.Parameters.Add("nombre_usuario", NpgsqlDbType.Varchar).Value = usuario.NombreUsuario;
        comando.Parameters.Add("resumen_contrasena", NpgsqlDbType.Text).Value = usuario.ResumenContrasena;
        comando.Parameters.Add("sal", NpgsqlDbType.Text).Value = usuario.Sal;
        comando.Parameters.Add("rol_id", NpgsqlDbType.Smallint).Value = usuario.Rol.Id;
        comando.Parameters.Add("sede_id", NpgsqlDbType.Smallint).Value = usuario.Sede.Id;
        comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value = usuario.Estado.ToString();
        comando.Parameters.Add("cantidad_intentos_fallidos", NpgsqlDbType.Smallint).Value =
            (short)usuario.CantidadDeIntentosFallidos;
        comando.Parameters.Add("fin_bloqueo", NpgsqlDbType.TimestampTz).Value =
            usuario.FinBloqueo is null ? DBNull.Value : usuario.FinBloqueo.Value;
        comando.Parameters.Add("id", NpgsqlDbType.Integer).Value = usuario.Id;

        var filas = await comando.ExecuteNonQueryAsync();
        if (filas != 1)
        {
            throw new InvalidOperationException("No se encontró la cuenta que se quería actualizar.");
        }
    }

    private static Usuario LeerUsuario(NpgsqlDataReader lector) =>
        Usuario.Reconstruir(new UsuarioGuardado(
            Id: lector.GetInt32(0),
            NombrePersona: lector.GetString(1),
            NombreUsuario: lector.GetString(2),
            ResumenContrasena: lector.GetString(3),
            Sal: lector.GetString(4),
            EsCuentaPrincipal: lector.GetBoolean(12),
            Rol: new RolGuardado(lector.GetInt16(8), lector.GetString(9)),
            Sede: new SedeGuardada(lector.GetInt16(10), lector.GetString(11)),
            Estado: Enum.Parse<EstadoCuenta>(lector.GetString(5)),
            CantidadDeIntentosFallidos: lector.GetInt16(6),
            FinBloqueo: lector.IsDBNull(7) ? null : lector.GetDateTime(7).ToUniversalTime()));
}
