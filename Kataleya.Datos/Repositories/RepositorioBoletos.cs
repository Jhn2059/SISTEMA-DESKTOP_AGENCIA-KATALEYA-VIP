using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioBoletos : IRepositorioBoletos
{
    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioBoletos(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<BoletoDeViaje> EmitirAsync(
        long pasajeId,
        string serie,
        DateTime fechaEmision,
        int emitidoPorId,
        string emitidoPor)
    {
        var existente = await BuscarPorPasajeAsync(pasajeId);
        if (existente is not null)
        {
            return existente;
        }

        var conexion = await _contexto.ObtenerConexionAsync();

        int correlativo;
        await using (var comandoSerie = new NpgsqlCommand(
            """
            INSERT INTO boleto_correlativo (serie, ultimo)
            VALUES (@serie, 1)
            ON CONFLICT (serie)
            DO UPDATE SET ultimo = boleto_correlativo.ultimo + 1
            RETURNING ultimo
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        })
        {
            comandoSerie.Parameters.Add("serie", NpgsqlDbType.Varchar).Value = serie;
            correlativo = (int)(await comandoSerie.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("No se pudo obtener el correlativo del boleto."));
        }

        return await RegistrarAsync(
            pasajeId,
            serie,
            correlativo,
            fechaEmision,
            emitidoPorId,
            emitidoPor);
    }

    public async Task<BoletoDeViaje> RegistrarAsync(
        long pasajeId,
        string serie,
        int correlativo,
        DateTime fechaEmision,
        int emitidoPorId,
        string emitidoPor)
    {
        var existente = await BuscarPorPasajeAsync(pasajeId);
        if (existente is not null)
        {
            return existente;
        }

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO boleto
                (pasaje_id, serie, correlativo, fecha_emision, emitido_por_id, emitido_por)
            VALUES
                (@pasaje_id, @serie, @correlativo, @fecha_emision, @emitido_por_id, @emitido_por)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("pasaje_id", NpgsqlDbType.Bigint).Value = pasajeId;
        comando.Parameters.Add("serie", NpgsqlDbType.Varchar).Value = serie;
        comando.Parameters.Add("correlativo", NpgsqlDbType.Integer).Value = correlativo;
        comando.Parameters.Add("fecha_emision", NpgsqlDbType.TimestampTz).Value = fechaEmision;
        comando.Parameters.Add("emitido_por_id", NpgsqlDbType.Integer).Value = emitidoPorId;
        comando.Parameters.Add("emitido_por", NpgsqlDbType.Varchar).Value = emitidoPor;

        var idResultado = await comando.ExecuteScalarAsync();
        var id = idResultado is long valor ? valor : 0;

        return BoletoDeViaje.Reconstruir(new BoletoGuardado(
            id,
            pasajeId,
            serie,
            correlativo,
            fechaEmision,
            emitidoPorId,
            emitidoPor));
    }

    public async Task<BoletoDeViaje?> BuscarPorPasajeAsync(long pasajeId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, pasaje_id, serie, correlativo, fecha_emision,
                   emitido_por_id, emitido_por
            FROM boleto
            WHERE pasaje_id = @pasaje_id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("pasaje_id", NpgsqlDbType.Bigint).Value = pasajeId;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? Mapear(lector) : null;
    }

    public async Task<BoletoDeViaje?> BuscarPorNumeroAsync(string serie, int correlativo)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, pasaje_id, serie, correlativo, fecha_emision,
                   emitido_por_id, emitido_por
            FROM boleto
            WHERE serie = @serie AND correlativo = @correlativo
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("serie", NpgsqlDbType.Varchar).Value = serie;
        comando.Parameters.Add("correlativo", NpgsqlDbType.Integer).Value = correlativo;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? Mapear(lector) : null;
    }

    private static BoletoDeViaje Mapear(NpgsqlDataReader lector) =>
        BoletoDeViaje.Reconstruir(new BoletoGuardado(
            lector.GetInt64(0),
            lector.GetInt64(1),
            lector.GetString(2),
            lector.GetInt32(3),
            lector.GetFieldValue<DateTime>(4),
            lector.GetInt32(5),
            lector.GetString(6)));
}
