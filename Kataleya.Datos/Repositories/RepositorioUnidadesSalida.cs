using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioUnidadesSalida : IRepositorioUnidadesSalida
{
    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioUnidadesSalida(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<IReadOnlyList<UnidadDeSalidaDisponible>> ListarPorSalidaAsync(
        Sentido sentido,
        DateTime fechaHoraSalida)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT u.id,
                   u.numero,
                   u.grupo_id,
                   u.sentido,
                   u.tipo_vehiculo_id,
                   tv.nombre,
                   tv.cantidad_asientos,
                   COALESCE(
                       array_agg(p.numero_asiento::int)
                           FILTER (WHERE p.numero_asiento IS NOT NULL),
                       ARRAY[]::int[]) AS asientos_ocupados,
                   u.fecha_hora_salida,
                   u.estado,
                   usr.nombre
            FROM unidad_salida u
            INNER JOIN usuario usr ON usr.id = u.abierta_por
            INNER JOIN tipo_vehiculo tv ON tv.id = u.tipo_vehiculo_id
            LEFT JOIN pasaje p
                ON p.unidad_salida_id = u.id
               AND p.estado <> 'Anulado'
            WHERE u.sentido = @sentido
              AND u.fecha_hora_salida = @fecha_hora_salida
            GROUP BY u.id, u.numero, u.grupo_id, u.sentido, u.tipo_vehiculo_id,
                     tv.nombre, tv.cantidad_asientos, u.fecha_hora_salida,
                     u.estado, usr.nombre
            ORDER BY u.numero
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("sentido", NpgsqlDbType.Varchar).Value = sentido.ToString();
        comando.Parameters.Add("fecha_hora_salida", NpgsqlDbType.TimestampTz).Value = fechaHoraSalida;

        var unidades = new List<UnidadDeSalidaDisponible>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            unidades.Add(new UnidadDeSalidaDisponible(
                lector.GetInt64(0),
                lector.GetInt16(1),
                lector.IsDBNull(2) ? null : lector.GetInt64(2),
                Enum.Parse<Sentido>(lector.GetString(3)),
                lector.GetInt16(4),
                lector.GetString(5),
                lector.GetInt32(6),
                lector.GetFieldValue<int[]>(7),
                lector.GetFieldValue<DateTime>(8),
                Enum.Parse<EstadoUnidadDeSalida>(lector.GetString(9)),
                lector.GetString(10)));
        }

        return unidades;
    }

    public async Task<UnidadDeSalida?> BuscarAsync(long id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT u.id, u.numero, u.grupo_id, u.sentido, u.tipo_vehiculo_id,
                   u.fecha_hora_salida, u.estado, u.abierta_por, usr.nombre
            FROM unidad_salida u
            INNER JOIN usuario usr ON usr.id = u.abierta_por
            WHERE u.id = @id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = id;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync()
            ? UnidadDeSalida.Reconstruir(new UnidadDeSalidaGuardada(
                lector.GetInt64(0),
                lector.GetInt16(1),
                lector.IsDBNull(2) ? null : lector.GetInt64(2),
                lector.IsDBNull(3) ? null : Enum.Parse<Sentido>(lector.GetString(3)),
                lector.GetInt16(4),
                lector.GetFieldValue<DateTime>(5),
                Enum.Parse<EstadoUnidadDeSalida>(lector.GetString(6)),
                lector.GetInt32(7),
                lector.GetString(8)))
            : null;
    }

    public async Task<IReadOnlyList<UnidadDeSalida>> ListarPorGrupoAsync(long grupoId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT u.id, u.numero, u.grupo_id, u.sentido, u.tipo_vehiculo_id,
                   u.fecha_hora_salida, u.estado, u.abierta_por, usr.nombre
            FROM unidad_salida u
            INNER JOIN usuario usr ON usr.id = u.abierta_por
            WHERE u.grupo_id = @grupo_id
            ORDER BY u.numero
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value = grupoId;

        var unidades = new List<UnidadDeSalida>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            unidades.Add(UnidadDeSalida.Reconstruir(new UnidadDeSalidaGuardada(
                lector.GetInt64(0),
                lector.GetInt16(1),
                lector.IsDBNull(2) ? null : lector.GetInt64(2),
                lector.IsDBNull(3) ? null : Enum.Parse<Sentido>(lector.GetString(3)),
                lector.GetInt16(4),
                lector.GetFieldValue<DateTime>(5),
                Enum.Parse<EstadoUnidadDeSalida>(lector.GetString(6)),
                lector.GetInt32(7),
                lector.GetString(8))));
        }

        return unidades;
    }

    public async Task<int> SiguienteNumeroAsync(Sentido sentido, DateTime fechaHoraSalida)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT COALESCE(MAX(numero), 0) + 1
            FROM unidad_salida
            WHERE sentido = @sentido
              AND fecha_hora_salida = @fecha_hora_salida
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("sentido", NpgsqlDbType.Varchar).Value = sentido.ToString();
        comando.Parameters.Add("fecha_hora_salida", NpgsqlDbType.TimestampTz).Value = fechaHoraSalida;

        var resultado = await comando.ExecuteScalarAsync();
        return resultado is int numero ? numero : 1;
    }

    public async Task<long> RegistrarAsync(UnidadDeSalida unidad)
    {
        ArgumentNullException.ThrowIfNull(unidad);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO unidad_salida
                (numero, grupo_id, sentido, tipo_vehiculo_id, fecha_hora_salida,
                 estado, abierta_por)
            VALUES
                (@numero, @grupo_id, @sentido, @tipo_vehiculo_id, @fecha_hora_salida,
                 @estado, @abierta_por)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("numero", NpgsqlDbType.Smallint).Value = unidad.Numero;
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value =
            (object?)unidad.GrupoId ?? DBNull.Value;
        comando.Parameters.Add("sentido", NpgsqlDbType.Varchar).Value =
            (object?)unidad.Sentido?.ToString() ?? DBNull.Value;
        comando.Parameters.Add("tipo_vehiculo_id", NpgsqlDbType.Smallint).Value = unidad.TipoVehiculoId;
        comando.Parameters.Add("fecha_hora_salida", NpgsqlDbType.TimestampTz).Value = unidad.FechaHoraSalida;
        comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value = unidad.Estado.ToString();
        comando.Parameters.Add("abierta_por", NpgsqlDbType.Integer).Value = unidad.AbiertaPorId;

        var idResultado = await comando.ExecuteScalarAsync();
        return idResultado is long id ? id : 0;
    }

    public async Task<int> ContarAsientosOcupadosAsync(long unidadDeSalidaId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM pasaje
            WHERE unidad_salida_id = @unidad_salida_id
              AND estado <> 'Anulado'
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("unidad_salida_id", NpgsqlDbType.Bigint).Value = unidadDeSalidaId;

        var resultado = await comando.ExecuteScalarAsync();
        return resultado is int cantidad ? cantidad : 0;
    }
}
