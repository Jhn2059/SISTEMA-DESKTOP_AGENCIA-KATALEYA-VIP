using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioPagos : IRepositorioPagos
{
    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioPagos(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<long> RegistrarAsync(Pago pago)
    {
        ArgumentNullException.ThrowIfNull(pago);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO pago
                (pasaje_id, grupo_id, monto, medio_pago, tipo, fecha_pago,
                 cobrador_id, cobrador)
            VALUES
                (@pasaje_id, @grupo_id, @monto, @medio_pago, @tipo, @fecha_pago,
                 @cobrador_id, @cobrador)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("pasaje_id", NpgsqlDbType.Bigint).Value =
            (object?)pago.PasajeId ?? DBNull.Value;
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value =
            (object?)pago.GrupoId ?? DBNull.Value;
        comando.Parameters.Add("monto", NpgsqlDbType.Numeric).Value = pago.Monto;
        comando.Parameters.Add("medio_pago", NpgsqlDbType.Varchar).Value = pago.MedioDePago.ToString();
        comando.Parameters.Add("tipo", NpgsqlDbType.Varchar).Value = pago.Tipo.ToString();
        comando.Parameters.Add("fecha_pago", NpgsqlDbType.TimestampTz).Value = pago.FechaPago;
        comando.Parameters.Add("cobrador_id", NpgsqlDbType.Integer).Value = pago.CobradorId;
        comando.Parameters.Add("cobrador", NpgsqlDbType.Varchar).Value = pago.Cobrador;

        var idResultado = await comando.ExecuteScalarAsync();
        return idResultado is long id ? id : 0;
    }

    public async Task<IReadOnlyList<Pago>> ListarPorPasajeAsync(long pasajeId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, pasaje_id, grupo_id, monto, medio_pago, tipo, fecha_pago,
                   cobrador_id, cobrador
            FROM pago
            WHERE pasaje_id = @pasaje_id
            ORDER BY fecha_pago, id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("pasaje_id", NpgsqlDbType.Bigint).Value = pasajeId;

        var pagos = new List<Pago>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            pagos.Add(Pago.Reconstruir(new PagoGuardado(
                lector.GetInt64(0),
                lector.IsDBNull(1) ? null : lector.GetInt64(1),
                lector.IsDBNull(2) ? null : lector.GetInt64(2),
                lector.GetDecimal(3),
                Enum.Parse<MedioPago>(lector.GetString(4)),
                Enum.Parse<TipoDePago>(lector.GetString(5)),
                lector.GetFieldValue<DateTime>(6),
                lector.GetInt32(7),
                lector.GetString(8))));
        }

        return pagos;
    }

    public Task<IReadOnlyList<Pago>> ListarPorGrupoAsync(long grupoId) =>
        ListarAsync("grupo_id", grupoId);

    public async Task<IReadOnlyList<MovimientoDeCuadre>> ListarMovimientosDeCuadreAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? emisorId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT pg.id, pg.pasaje_id, pg.grupo_id, pg.fecha_pago, pg.monto,
                   pg.medio_pago, pg.tipo, pg.cobrador_id, pg.cobrador,
                   p.emisor_id, p.emisor, p.nombre_pasajero, p.numero_documento,
                   p.numero_asiento, p.origen, p.destino, p.fecha_hora_salida,
                   p.importe, p.estado, p.motivo_anulacion, p.fecha_anulacion,
                   (SELECT g.codigo FROM grupo g WHERE g.id = p.grupo_id)
            FROM pago pg
            JOIN pasaje p ON p.id = pg.pasaje_id
            WHERE pg.fecha_pago >= @desde
              AND pg.fecha_pago < @hasta
              AND (@emisor_id IS NULL OR p.emisor_id = @emisor_id)
            ORDER BY pg.fecha_pago, pg.id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("desde", NpgsqlDbType.TimestampTz).Value = desdeUtc;
        comando.Parameters.Add("hasta", NpgsqlDbType.TimestampTz).Value = hastaUtcExclusivo;
        comando.Parameters.Add("emisor_id", NpgsqlDbType.Integer).Value =
            (object?)emisorId ?? DBNull.Value;

        var movimientos = new List<MovimientoDeCuadre>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            movimientos.Add(new MovimientoDeCuadre(
                lector.GetInt64(0),
                lector.GetInt64(1),
                lector.IsDBNull(2) ? null : lector.GetInt64(2),
                lector.GetFieldValue<DateTime>(3),
                lector.GetDecimal(4),
                Enum.Parse<MedioPago>(lector.GetString(5)),
                Enum.Parse<TipoDePago>(lector.GetString(6)),
                lector.GetInt32(7),
                lector.GetString(8),
                lector.GetInt32(9),
                lector.GetString(10),
                lector.GetString(11),
                lector.GetString(12),
                lector.IsDBNull(13) ? null : lector.GetInt32(13),
                lector.GetString(14),
                lector.GetString(15),
                lector.GetFieldValue<DateTime>(16),
                lector.GetDecimal(17),
                Enum.Parse<EstadoPasaje>(lector.GetString(18)),
                lector.IsDBNull(21) ? null : lector.GetString(21),
                lector.IsDBNull(19) ? null : lector.GetString(19),
                lector.IsDBNull(20) ? null : lector.GetFieldValue<DateTime>(20)));
        }

        return movimientos;
    }

    private async Task<IReadOnlyList<Pago>> ListarAsync(string columna, long id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            SELECT id, pasaje_id, grupo_id, monto, medio_pago, tipo, fecha_pago,
                   cobrador_id, cobrador
            FROM pago
            WHERE {columna} = @id
            ORDER BY fecha_pago, id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = id;

        var pagos = new List<Pago>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            pagos.Add(Pago.Reconstruir(new PagoGuardado(
                lector.GetInt64(0),
                lector.IsDBNull(1) ? null : lector.GetInt64(1),
                lector.IsDBNull(2) ? null : lector.GetInt64(2),
                lector.GetDecimal(3),
                Enum.Parse<MedioPago>(lector.GetString(4)),
                Enum.Parse<TipoDePago>(lector.GetString(5)),
                lector.GetFieldValue<DateTime>(6),
                lector.GetInt32(7),
                lector.GetString(8))));
        }

        return pagos;
    }
}
