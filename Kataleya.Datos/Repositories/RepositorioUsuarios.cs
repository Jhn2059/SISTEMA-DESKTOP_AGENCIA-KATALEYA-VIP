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
               u.nombre,
               u.nombre_usuario,
               u.resumen_contrasena,
               u.sal,
               u.estado,
               u.intentos_fallidos,
               u.fin_bloqueo,
               r.id,
               r.nombre,
               s.id,
               s.nombre
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
        await using var comando = new NpgsqlCommand($"{SeleccionarUsuario} WHERE u.nombre_usuario = @nombre_usuario", conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("nombre_usuario", NpgsqlDbType.Varchar).Value = nombreUsuario;

        await using var lector = await comando.ExecuteReaderAsync();
        if (!await lector.ReadAsync())
        {
            return null;
        }

        var finBloqueo = lector.IsDBNull(7) ? (DateTime?)null : lector.GetDateTime(7);
        var rol = new Rol(lector.GetInt16(8), lector.GetString(9));
        var sede = new Sede(lector.GetInt16(10), lector.GetString(11));

        return new Usuario(
            lector.GetInt32(0),
            lector.GetString(1),
            lector.GetString(2),
            lector.GetString(3),
            lector.GetString(4),
            Enum.Parse<EstadoCuenta>(lector.GetString(5)),
            lector.GetInt16(6),
            finBloqueo,
            rol,
            sede);
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
                (nombre, nombre_usuario, resumen_contrasena, sal, rol_id, sede_id,
                 estado, intentos_fallidos, fin_bloqueo)
            VALUES
                (@nombre, @nombre_usuario, @resumen_contrasena, @sal, @rol_id, @sede_id,
                 @estado, @intentos_fallidos, @fin_bloqueo)
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        comando.Parameters.Add("nombre", NpgsqlDbType.Varchar).Value = usuario.Nombre;
        comando.Parameters.Add("nombre_usuario", NpgsqlDbType.Varchar).Value = usuario.NombreUsuario;
        comando.Parameters.Add("resumen_contrasena", NpgsqlDbType.Text).Value = usuario.ResumenContrasena;
        comando.Parameters.Add("sal", NpgsqlDbType.Text).Value = usuario.Sal;
        comando.Parameters.Add("rol_id", NpgsqlDbType.Smallint).Value = usuario.Rol.Id;
        comando.Parameters.Add("sede_id", NpgsqlDbType.Smallint).Value = usuario.Sede.Id;
        comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value = usuario.Estado.ToString();
        comando.Parameters.Add("intentos_fallidos", NpgsqlDbType.Smallint).Value = usuario.IntentosFallidos;
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
            SET nombre = @nombre,
                nombre_usuario = @nombre_usuario,
                resumen_contrasena = @resumen_contrasena,
                sal = @sal,
                rol_id = @rol_id,
                sede_id = @sede_id,
                estado = @estado,
                intentos_fallidos = @intentos_fallidos,
                fin_bloqueo = @fin_bloqueo
            WHERE id = @id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        comando.Parameters.Add("nombre", NpgsqlDbType.Varchar).Value = usuario.Nombre;
        comando.Parameters.Add("nombre_usuario", NpgsqlDbType.Varchar).Value = usuario.NombreUsuario;
        comando.Parameters.Add("resumen_contrasena", NpgsqlDbType.Text).Value = usuario.ResumenContrasena;
        comando.Parameters.Add("sal", NpgsqlDbType.Text).Value = usuario.Sal;
        comando.Parameters.Add("rol_id", NpgsqlDbType.Smallint).Value = usuario.Rol.Id;
        comando.Parameters.Add("sede_id", NpgsqlDbType.Smallint).Value = usuario.Sede.Id;
        comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value = usuario.Estado.ToString();
        comando.Parameters.Add("intentos_fallidos", NpgsqlDbType.Smallint).Value = usuario.IntentosFallidos;
        comando.Parameters.Add("fin_bloqueo", NpgsqlDbType.TimestampTz).Value =
            usuario.FinBloqueo is null ? DBNull.Value : usuario.FinBloqueo.Value;
        comando.Parameters.Add("id", NpgsqlDbType.Integer).Value = usuario.Id;

        var filas = await comando.ExecuteNonQueryAsync();
        if (filas != 1)
        {
            throw new InvalidOperationException("No se encontró la cuenta que se quería actualizar.");
        }
    }
}
