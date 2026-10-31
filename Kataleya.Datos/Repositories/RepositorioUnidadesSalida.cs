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

    public async Task<IReadOnlyList<UnidadSalidaDisponible>> ListarDisponiblesAsync(DateOnly fecha)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT u.id,
                   u.sentido,
                   u.tipo_vehiculo_id,
                   tv.nombre,
                   tv.cantidad_asientos,
                   COALESCE(ocupados.cantidad, 0),
                   u.fecha_hora_salida,
                   u.estado,
                   usr.nombre
            FROM unidad_salida u
            INNER JOIN usuario usr ON usr.id = u.usuario_id
            INNER JOIN tipo_vehiculo tv ON tv.id = u.tipo_vehiculo_id
            LEFT JOIN (
                SELECT unidad_salida_id, count(*)::int AS cantidad
                FROM pasaje
                WHERE estado <> 'Anulado'
                GROUP BY unidad_salida_id
            ) ocupados ON ocupados.unidad_salida_id = u.id
            WHERE u.fecha_hora_salida::date = @fecha
              AND u.estado = 'Programada'
              AND (tv.cantidad_asientos - COALESCE(ocupados.cantidad, 0)) > 0
            ORDER BY u.fecha_hora_salida
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("fecha", NpgsqlDbType.Date).Value = fecha;

        var unidades = new List<UnidadSalidaDisponible>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            unidades.Add(new UnidadSalidaDisponible(
                lector.GetInt64(0),
                Enum.Parse<Sentido>(lector.GetString(1)),
                lector.GetInt16(2),
                lector.GetString(3),
                lector.GetInt32(4),
                lector.GetInt32(5),
                lector.GetFieldValue<DateTime>(6),
                lector.GetString(7),
                lector.GetString(8)));
        }

        return unidades;
    }

    public async Task<UnidadSalida?> ObtenerAsync(long id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, sentido, tipo_vehiculo_id, fecha_hora_salida, estado, usuario_id, usr.nombre
            FROM unidad_salida u
            INNER JOIN usuario usr ON usr.id = u.usuario_id
            WHERE u.id = @id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = id;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync()
            ? UnidadSalida.Reconstruir(new UnidadSalidaGuardada(
                lector.GetInt64(0),
                Enum.Parse<Sentido>(lector.GetString(1)),
                lector.GetInt16(2),
                lector.GetFieldValue<DateTime>(3),
                lector.GetString(4),
                lector.GetInt32(5),
                lector.GetString(6)))
            : null;
    }

    public async Task<int> ContarAsientosOcupadosAsync(long unidadSalidaId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM pasaje
            WHERE unidad_salida_id = @unidadSalidaId
              AND estado <> 'Anulado'
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("unidadSalidaId", NpgsqlDbType.Bigint).Value = unidadSalidaId;

        var resultado = await comando.ExecuteScalarAsync();
        return resultado is int cantidad ? cantidad : 0;
    }
}