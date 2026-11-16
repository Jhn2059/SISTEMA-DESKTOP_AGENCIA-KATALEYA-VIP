using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioBitacora : IRepositorioBitacora
{
    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioBitacora(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task AgregarAsync(RegistroDeBitacora registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO registro_bitacora
                (usuario_id, fecha_hora, operacion, valor_anterior, valor_nuevo,
                 clave_de_operacion, pasaje_id)
            VALUES
                (@usuario_id, @fecha_hora, @operacion, @valor_anterior, @valor_nuevo,
                 @clave_de_operacion, @pasaje_id)
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = registro.Usuario.Id;
        comando.Parameters.Add("fecha_hora", NpgsqlDbType.TimestampTz).Value = registro.FechaHora;
        comando.Parameters.Add("operacion", NpgsqlDbType.Varchar).Value = registro.Operacion;
        comando.Parameters.Add("valor_anterior", NpgsqlDbType.Jsonb).Value =
            registro.ValorAnterior is null ? DBNull.Value : registro.ValorAnterior;
        comando.Parameters.Add("valor_nuevo", NpgsqlDbType.Jsonb).Value =
            registro.ValorNuevo is null ? DBNull.Value : registro.ValorNuevo;
        comando.Parameters.Add("clave_de_operacion", NpgsqlDbType.Uuid).Value =
            registro.ClaveDeOperacion is null ? DBNull.Value : registro.ClaveDeOperacion.Value;
        comando.Parameters.Add("pasaje_id", NpgsqlDbType.Bigint).Value =
            registro.PasajeId is null ? DBNull.Value : registro.PasajeId.Value;

        await comando.ExecuteNonQueryAsync();
    }

    public async Task<bool> ExisteClaveAsync(Guid clave)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM registro_bitacora WHERE clave_de_operacion = @clave)",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("clave", NpgsqlDbType.Uuid).Value = clave;

        var resultado = await comando.ExecuteScalarAsync();
        return resultado is true;
    }

    public async Task<IReadOnlyList<RegistroDeBitacora>> ListarAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? usuarioId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT b.id,
                   b.fecha_hora,
                   b.operacion,
                   b.valor_anterior::text,
                   b.valor_nuevo::text,
                   b.clave_de_operacion,
                   b.pasaje_id,
                   u.id,
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
            FROM registro_bitacora b
            INNER JOIN usuario u ON u.id = b.usuario_id
            INNER JOIN rol r ON r.id = u.rol_id
            INNER JOIN sede s ON s.id = u.sede_id
            WHERE b.fecha_hora >= @desde
              AND b.fecha_hora < @hasta
              AND (@usuario_id IS NULL OR b.usuario_id = @usuario_id)
            ORDER BY b.fecha_hora DESC, b.id DESC
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("desde", NpgsqlDbType.TimestampTz).Value = desdeUtc;
        comando.Parameters.Add("hasta", NpgsqlDbType.TimestampTz).Value = hastaUtcExclusivo;
        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value =
            usuarioId is null ? DBNull.Value : usuarioId.Value;

        var registros = new List<RegistroDeBitacora>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            registros.Add(LeerRegistro(lector));
        }

        return registros;
    }

    private static RegistroDeBitacora LeerRegistro(NpgsqlDataReader lector)
    {
        var finBloqueo = lector.IsDBNull(14) ? (DateTime?)null : lector.GetDateTime(14);
        var rol = new Rol(lector.GetInt16(15), lector.GetString(16));
        var sede = new Sede(lector.GetInt16(17), lector.GetString(18));
        var usuario = new Usuario(
            lector.GetInt32(7),
            lector.GetString(8),
            lector.GetString(9),
            lector.GetString(10),
            lector.GetString(11),
            Enum.Parse<EstadoCuenta>(lector.GetString(12)),
            lector.GetInt16(13),
            finBloqueo,
            rol,
            sede);

        return new RegistroDeBitacora(
            lector.GetInt64(0),
            lector.GetDateTime(1),
            lector.GetString(2),
            usuario,
            lector.IsDBNull(3) ? null : lector.GetString(3),
            lector.IsDBNull(4) ? null : lector.GetString(4),
            lector.IsDBNull(5) ? null : lector.GetGuid(5),
            lector.IsDBNull(6) ? null : lector.GetInt64(6));
    }
}
