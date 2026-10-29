using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioUnidades : IRepositorioUnidades
{
    private const string SeleccionarUnidad = """
        SELECT u.id,
               u.fecha_hora_salida,
               u.numero,
               u.tipo_vehiculo_id,
               t.nombre,
               t.numero_asientos,
               u.grupo_id,
               u.sentido
        FROM unidad_salida u
        INNER JOIN tipo_vehiculo t ON t.id = u.tipo_vehiculo_id
        """;

    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioUnidades(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<IReadOnlyList<UnidadDeSalida>> ListarDeLaSalidaAsync(
        DateTime fechaHora,
        SentidoDelViaje sentido)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            {SeleccionarUnidad}
            WHERE date_trunc('minute', u.fecha_hora_salida) = date_trunc('minute', @fecha)
              AND u.sentido = @sentido
              AND u.grupo_id IS NULL
            ORDER BY u.numero
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("fecha", NpgsqlDbType.TimestampTz).Value = fechaHora;
        comando.Parameters.Add("sentido", NpgsqlDbType.Varchar).Value = sentido.ToString();

        return await LeerUnidadesAsync(comando);
    }

    public async Task<UnidadDeSalida?> BuscarAsync(long id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"{SeleccionarUnidad} WHERE u.id = @id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = id;

        var unidades = await LeerUnidadesAsync(comando);
        return unidades.Count == 0 ? null : unidades[0];
    }

    public async Task<long> AgregarAsync(UnidadDeSalida unidad)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO unidad_salida (fecha_hora_salida, sentido, numero, tipo_vehiculo_id, grupo_id)
            VALUES (@fecha_hora_salida, @sentido, @numero, @tipo_vehiculo_id, @grupo_id)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("fecha_hora_salida", NpgsqlDbType.TimestampTz).Value = unidad.FechaHoraSalida;
        comando.Parameters.Add("sentido", NpgsqlDbType.Varchar).Value =
            unidad.Sentido is null ? DBNull.Value : unidad.Sentido.Value.ToString();
        comando.Parameters.Add("numero", NpgsqlDbType.Varchar).Value = unidad.Numero;
        comando.Parameters.Add("tipo_vehiculo_id", NpgsqlDbType.Smallint).Value = unidad.TipoVehiculoId;
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value =
            unidad.GrupoId is null ? DBNull.Value : unidad.GrupoId.Value;

        return (long)(await comando.ExecuteScalarAsync())!;
    }

    public async Task<IReadOnlyList<UnidadDeSalida>> ListarDelGrupoAsync(long grupoId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"{SeleccionarUnidad} WHERE u.grupo_id = @grupo_id ORDER BY u.id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value = grupoId;

        return await LeerUnidadesAsync(comando);
    }

    public async Task<IReadOnlyList<short>> AsientosOcupadosAsync(long unidadId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT numero_asiento
            FROM pasaje
            WHERE unidad_salida_id = @unidad_id
              AND estado <> 'Anulado'
            ORDER BY numero_asiento
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("unidad_id", NpgsqlDbType.Bigint).Value = unidadId;

        var asientos = new List<short>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            asientos.Add(lector.GetInt16(0));
        }

        return asientos;
    }

    private async Task<IReadOnlyList<UnidadDeSalida>> LeerUnidadesAsync(NpgsqlCommand comando)
    {
        var unidades = new List<UnidadDeSalida>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            unidades.Add(new UnidadDeSalida
            {
                Id = lector.GetInt64(0),
                FechaHoraSalida = lector.GetDateTime(1),
                Numero = lector.GetString(2),
                TipoVehiculoId = lector.GetInt16(3),
                TipoVehiculoNombre = lector.GetString(4),
                NumeroAsientos = lector.GetInt16(5),
                GrupoId = lector.IsDBNull(6) ? null : lector.GetInt64(6),
                Sentido = lector.IsDBNull(7) ? null : Enum.Parse<SentidoDelViaje>(lector.GetString(7))
            });
        }

        lector.Close();

        foreach (var unidad in unidades)
        {
            unidad.AsientosOcupados = (await AsientosOcupadosAsync(unidad.Id)).ToList();
        }

        return unidades;
    }
}
