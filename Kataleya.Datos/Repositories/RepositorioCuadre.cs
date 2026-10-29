using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioCuadre : IRepositorioCuadre
{
    private const string PagosAtribuidos = """
        WITH pago_atribuido AS (
            SELECT COALESCE(
                       pg.pasaje_id,
                       (SELECT p2.id FROM pasaje p2
                        WHERE p2.grupo_id = pg.grupo_id AND p2.es_responsable
                        LIMIT 1)) AS pasaje_id,
                   pg.importe,
                   pg.medio,
                   pg.tipo_movimiento
            FROM pago pg
            WHERE (pg.fecha_hora AT TIME ZONE 'America/Lima')::date BETWEEN @desde AND @hasta
        )
        """;

    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioCuadre(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<DetalleDelCuadre> GenerarAsync(DateOnly desde, DateOnly hasta, int? emisorId, short? sedeId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            {PagosAtribuidos}
            SELECT u.id,
                   u.nombre_usuario,
                   (SELECT COUNT(*) FROM pasaje p
                    WHERE p.emisor_id = u.id
                      AND p.fecha_salida BETWEEN @desde AND @hasta
                      AND p.estado <> 'Anulado') AS pasajes,
                   COALESCE((SELECT SUM(CASE WHEN pa.tipo_movimiento = 'Pago' THEN pa.importe ELSE -pa.importe END)
                             FROM pago_atribuido pa
                             INNER JOIN pasaje p ON p.id = pa.pasaje_id
                             WHERE p.emisor_id = u.id AND pa.medio = 'Efectivo'), 0) AS efectivo,
                   COALESCE((SELECT SUM(CASE WHEN pa.tipo_movimiento = 'Pago' THEN pa.importe ELSE -pa.importe END)
                             FROM pago_atribuido pa
                             INNER JOIN pasaje p ON p.id = pa.pasaje_id
                             WHERE p.emisor_id = u.id AND pa.medio = 'Yape'), 0) AS yape,
                   COALESCE((SELECT SUM(GREATEST(
                                 p.importe - COALESCE(
                                     (SELECT SUM(CASE WHEN x.tipo_movimiento = 'Pago' THEN x.importe ELSE -x.importe END)
                                      FROM pago x
                                      WHERE x.pasaje_id = p.id
                                         OR (p.grupo_id IS NOT NULL AND x.grupo_id = p.grupo_id AND p.es_responsable)), 0), 0))
                             FROM pasaje p
                             WHERE p.emisor_id = u.id
                               AND p.fecha_salida BETWEEN @desde AND @hasta
                               AND p.estado <> 'Anulado'), 0) AS adelantos,
                   (SELECT COUNT(*) FROM pasaje p
                    WHERE p.emisor_id = u.id
                      AND p.fecha_salida BETWEEN @desde AND @hasta
                      AND p.estado = 'Anulado') AS anulados,
                   COALESCE((SELECT SUM(p.importe) FROM pasaje p
                             WHERE p.emisor_id = u.id
                               AND p.fecha_salida BETWEEN @desde AND @hasta
                               AND p.estado = 'Anulado'), 0) AS importe_anulado
            FROM usuario u
            WHERE (@emisor IS NULL OR u.id = @emisor)
              AND (@sede IS NULL OR u.sede_id = @sede)
              AND (
                  EXISTS (SELECT 1 FROM pasaje p
                          WHERE p.emisor_id = u.id
                            AND p.fecha_salida BETWEEN @desde AND @hasta)
                  OR EXISTS (SELECT 1 FROM pago_atribuido pa
                             INNER JOIN pasaje p ON p.id = pa.pasaje_id
                             WHERE p.emisor_id = u.id)
              )
            ORDER BY u.nombre_usuario
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        AgregarPeriodo(comando, desde, hasta, emisorId);
        AgregarSede(comando, sedeId);

        var filas = new List<FilaDeCuadre>();
        await using (var lector = await comando.ExecuteReaderAsync())
        {
            while (await lector.ReadAsync())
            {
                filas.Add(new FilaDeCuadre
                {
                    EmisorId = lector.GetInt32(0),
                    EmisorNombre = lector.GetString(1),
                    Pasajes = (int)lector.GetInt64(2),
                    Efectivo = lector.GetDecimal(3),
                    Yape = lector.GetDecimal(4),
                    AdelantosPorCobrar = lector.GetDecimal(5),
                    Anulados = (int)lector.GetInt64(6),
                    ImporteAnulado = lector.GetDecimal(7)
                });
            }
        }

        var pasajes = new List<PasajeDelCuadre>();
        await using (var comandoPasajes = new NpgsqlCommand(
            """
            SELECT p.id,
                   p.pasajero_nombre,
                   p.numero_documento,
                   u.nombre_usuario,
                   p.estado,
                   p.importe,
                   CASE
                       WHEN p.grupo_id IS NULL THEN COALESCE((
                           SELECT SUM(CASE WHEN pg.tipo_movimiento = 'Pago' THEN pg.importe ELSE -pg.importe END)
                           FROM pago pg WHERE pg.pasaje_id = p.id), 0)
                       WHEN p.es_responsable THEN COALESCE((
                           SELECT SUM(CASE WHEN pg.tipo_movimiento = 'Pago' THEN pg.importe ELSE -pg.importe END)
                           FROM pago pg WHERE pg.grupo_id = p.grupo_id), 0)
                       ELSE 0
                   END,
                   b.serie || '-' || lpad(b.numero::text, 6, '0')
            FROM pasaje p
            INNER JOIN usuario u ON u.id = p.emisor_id
            LEFT JOIN boleto_viaje b ON b.pasaje_id = p.id
            WHERE p.fecha_salida BETWEEN @desde AND @hasta
              AND (@emisor IS NULL OR p.emisor_id = @emisor)
              AND (@sede IS NULL OR u.sede_id = @sede)
            ORDER BY p.fecha_salida, p.hora_salida, p.id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        })
        {
            AgregarPeriodo(comandoPasajes, desde, hasta, emisorId);
            AgregarSede(comandoPasajes, sedeId);

            await using var lector = await comandoPasajes.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                pasajes.Add(new PasajeDelCuadre
                {
                    Id = lector.GetInt64(0),
                    PasajeroNombre = lector.GetString(1),
                    NumeroDocumento = lector.GetString(2),
                    EmisorNombre = lector.GetString(3),
                    Estado = lector.GetString(4),
                    Importe = lector.GetDecimal(5),
                    Pagado = lector.GetDecimal(6),
                    Boleto = lector.IsDBNull(7) ? null : lector.GetString(7)
                });
            }
        }

        return new DetalleDelCuadre
        {
            Filas = filas,
            Pasajes = pasajes,
            GeneradoEl = DateTime.Now,
            GeneradoPor = string.Empty,
            Sede = string.Empty
        };
    }

    public async Task<ResumenDelPanel> ObtenerPanelAsync(DateOnly fecha, int? emisorId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();

        var resumen = new ResumenDelPanel();

        await using (var comando = new NpgsqlCommand(
            """
            SELECT
                (SELECT COUNT(*) FROM pasaje p
                 WHERE (p.fecha_hora_registro AT TIME ZONE 'America/Lima')::date = @fecha
                   AND p.estado IN ('Reservado', 'ReservadoConAdelanto')
                   AND (@emisor IS NULL OR p.emisor_id = @emisor)),
                (SELECT COUNT(*) FROM pasaje p
                 WHERE (p.fecha_hora_registro AT TIME ZONE 'America/Lima')::date = @fecha
                   AND p.estado = 'Pagado'
                   AND (@emisor IS NULL OR p.emisor_id = @emisor)),
                COALESCE((SELECT SUM(CASE WHEN pg.tipo_movimiento = 'Pago' THEN pg.importe ELSE -pg.importe END)
                          FROM pago pg
                          INNER JOIN usuario u ON u.id = pg.usuario_id
                          WHERE (pg.fecha_hora AT TIME ZONE 'America/Lima')::date = @fecha
                            AND pg.medio = 'Efectivo'
                            AND (@emisor IS NULL OR u.id = @emisor)), 0),
                COALESCE((SELECT SUM(CASE WHEN pg.tipo_movimiento = 'Pago' THEN pg.importe ELSE -pg.importe END)
                          FROM pago pg
                          INNER JOIN usuario u ON u.id = pg.usuario_id
                          WHERE (pg.fecha_hora AT TIME ZONE 'America/Lima')::date = @fecha
                            AND pg.medio = 'Yape'
                            AND (@emisor IS NULL OR u.id = @emisor)), 0),
                COALESCE((SELECT SUM(GREATEST(
                              p.importe - COALESCE(
                                  (SELECT SUM(CASE WHEN x.tipo_movimiento = 'Pago' THEN x.importe ELSE -x.importe END)
                                   FROM pago x
                                   WHERE x.pasaje_id = p.id
                                      OR (p.grupo_id IS NOT NULL AND x.grupo_id = p.grupo_id AND p.es_responsable)), 0), 0))
                          FROM pasaje p
                          WHERE p.fecha_salida = @fecha
                            AND p.estado <> 'Anulado'
                            AND (@emisor IS NULL OR p.emisor_id = @emisor)), 0)
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        })
        {
            comando.Parameters.Add("fecha", NpgsqlDbType.Date).Value = fecha;
            comando.Parameters.Add("emisor", NpgsqlDbType.Integer).Value =
                emisorId is null ? DBNull.Value : emisorId.Value;

            await using var lector = await comando.ExecuteReaderAsync();
            await lector.ReadAsync();
            resumen.ReservasRegistradas = (int)lector.GetInt64(0);
            resumen.Ventas = (int)lector.GetInt64(1);
            resumen.CobradoEnEfectivo = lector.GetDecimal(2);
            resumen.CobradoPorYape = lector.GetDecimal(3);
            resumen.AdelantosPorCobrar = lector.GetDecimal(4);
        }

        await using (var comando = new NpgsqlCommand(
            """
            SELECT p.hora_salida,
                   COUNT(*) AS pasajes,
                   COUNT(*) FILTER (WHERE p.estado = 'Pagado') AS pagados,
                   COUNT(*) FILTER (WHERE p.estado = 'Reservado') AS reservados,
                   COUNT(*) FILTER (WHERE p.estado = 'ReservadoConAdelanto') AS con_adelanto
            FROM pasaje p
            WHERE p.fecha_salida = @fecha
              AND p.estado <> 'Anulado'
              AND (@emisor IS NULL OR p.emisor_id = @emisor)
            GROUP BY p.hora_salida
            ORDER BY p.hora_salida
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        })
        {
            comando.Parameters.Add("fecha", NpgsqlDbType.Date).Value = fecha;
            comando.Parameters.Add("emisor", NpgsqlDbType.Integer).Value =
                emisorId is null ? DBNull.Value : emisorId.Value;

            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                resumen.Horarios.Add(new FilaDeHorario
                {
                    Hora = lector.GetFieldValue<TimeOnly>(0),
                    Pasajes = (int)lector.GetInt64(1),
                    Pagados = (int)lector.GetInt64(2),
                    Reservados = (int)lector.GetInt64(3),
                    ConAdelanto = (int)lector.GetInt64(4)
                });
            }
        }

        return resumen;
    }

    private static void AgregarSede(NpgsqlCommand comando, short? sedeId) =>
        comando.Parameters.Add("sede", NpgsqlDbType.Smallint).Value =
            sedeId is null ? DBNull.Value : sedeId.Value;

    private static void AgregarPeriodo(NpgsqlCommand comando, DateOnly desde, DateOnly hasta, int? emisorId)
    {
        comando.Parameters.Add("desde", NpgsqlDbType.Date).Value = desde;
        comando.Parameters.Add("hasta", NpgsqlDbType.Date).Value = hasta;
        comando.Parameters.Add("emisor", NpgsqlDbType.Integer).Value =
            emisorId is null ? DBNull.Value : emisorId.Value;
    }
}
