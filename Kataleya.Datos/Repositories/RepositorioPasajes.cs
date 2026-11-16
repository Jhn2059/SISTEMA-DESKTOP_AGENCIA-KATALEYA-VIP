using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioPasajes : IRepositorioPasajes
{
    private const string Columnas =
        """
        p.id, p.unidad_salida_id, p.grupo_id, p.numero_asiento,
        p.nombre_pasajero, p.tipo_documento, p.numero_documento, p.celular,
        p.origen, p.destino, p.lugar_embarque, p.fecha_hora_salida,
        p.fecha_venta, p.importe, p.estado, p.tipo_registro, p.emisor_id,
        p.emisor, p.es_menor, p.nombre_padre_madre, p.motivo_menor,
        p.es_responsable, p.motivo_anulacion, p.fecha_anulacion,
        (SELECT g.codigo FROM grupo g WHERE g.id = p.grupo_id),
        p.es_diferido
        """;

    private const string ColumnasDeCuadre =
        """
        p.id, p.grupo_id, p.numero_asiento, p.nombre_pasajero, p.numero_documento,
        p.origen, p.destino, p.fecha_hora_salida, p.importe, p.estado,
        p.emisor_id, p.emisor, p.motivo_anulacion, p.fecha_anulacion,
        (SELECT g.codigo FROM grupo g WHERE g.id = p.grupo_id),
        COALESCE(SUM(CASE WHEN pg.tipo = 'Devolucion' THEN -pg.monto ELSE pg.monto END), 0)
        """;

    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioPasajes(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<long> RegistrarAsync(Pasaje pasaje)
    {
        ArgumentNullException.ThrowIfNull(pasaje);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO pasaje
                (unidad_salida_id, grupo_id, numero_asiento, nombre_pasajero,
                 tipo_documento, numero_documento, celular, origen, destino,
                 lugar_embarque, fecha_hora_salida, fecha_venta, importe, estado,
                 tipo_registro, emisor_id, emisor, es_menor, nombre_padre_madre,
                 motivo_menor, es_responsable, es_diferido)
            VALUES
                (@unidad_salida_id, @grupo_id, @numero_asiento, @nombre_pasajero,
                 @tipo_documento, @numero_documento, @celular, @origen, @destino,
                 @lugar_embarque, @fecha_hora_salida, @fecha_venta, @importe, @estado,
                 @tipo_registro, @emisor_id, @emisor, @es_menor, @nombre_padre_madre,
                 @motivo_menor, @es_responsable, @es_diferido)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("unidad_salida_id", NpgsqlDbType.Bigint).Value =
            (object?)pasaje.UnidadDeSalidaId ?? DBNull.Value;
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value =
            (object?)pasaje.GrupoId ?? DBNull.Value;
        comando.Parameters.Add("numero_asiento", NpgsqlDbType.Smallint).Value =
            (object?)pasaje.NumeroAsiento ?? DBNull.Value;
        comando.Parameters.Add("nombre_pasajero", NpgsqlDbType.Varchar).Value = pasaje.NombrePasajero;
        comando.Parameters.Add("tipo_documento", NpgsqlDbType.Varchar).Value = pasaje.TipoDocumento.ToString();
        comando.Parameters.Add("numero_documento", NpgsqlDbType.Varchar).Value = pasaje.NumeroDocumento;
        comando.Parameters.Add("celular", NpgsqlDbType.Varchar).Value =
            (object?)pasaje.Celular ?? DBNull.Value;
        comando.Parameters.Add("origen", NpgsqlDbType.Varchar).Value = pasaje.Origen;
        comando.Parameters.Add("destino", NpgsqlDbType.Varchar).Value = pasaje.Destino;
        comando.Parameters.Add("lugar_embarque", NpgsqlDbType.Varchar).Value = pasaje.LugarEmbarque;
        comando.Parameters.Add("fecha_hora_salida", NpgsqlDbType.TimestampTz).Value = pasaje.FechaHoraSalida;
        comando.Parameters.Add("fecha_venta", NpgsqlDbType.TimestampTz).Value = pasaje.FechaVenta;
        comando.Parameters.Add("importe", NpgsqlDbType.Numeric).Value = pasaje.Importe;
        comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value = pasaje.Estado.ToString();
        comando.Parameters.Add("tipo_registro", NpgsqlDbType.Varchar).Value = pasaje.TipoRegistro.ToString();
        comando.Parameters.Add("emisor_id", NpgsqlDbType.Integer).Value = pasaje.EmisorId;
        comando.Parameters.Add("emisor", NpgsqlDbType.Varchar).Value = pasaje.Emisor;
        comando.Parameters.Add("es_menor", NpgsqlDbType.Boolean).Value = pasaje.EsMenor;
        comando.Parameters.Add("nombre_padre_madre", NpgsqlDbType.Varchar).Value =
            (object?)pasaje.NombrePadreMadre ?? DBNull.Value;
        comando.Parameters.Add("motivo_menor", NpgsqlDbType.Varchar).Value =
            (object?)pasaje.MotivoMenor ?? DBNull.Value;
        comando.Parameters.Add("es_responsable", NpgsqlDbType.Boolean).Value =
            pasaje.EsResponsable;
        comando.Parameters.Add("es_diferido", NpgsqlDbType.Boolean).Value =
            pasaje.EsDiferido;

        var idResultado = await comando.ExecuteScalarAsync();
        return idResultado is long id ? id : 0;
    }

    public async Task<Pasaje?> BuscarAsync(long id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"SELECT {Columnas} FROM pasaje p WHERE p.id = @id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = id;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? Mapear(lector) : null;
    }

    public async Task<Pasaje?> BuscarPorAsientoAsync(long unidadDeSalidaId, int numeroAsiento)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
             SELECT {Columnas}
             FROM pasaje p
             WHERE p.unidad_salida_id = @unidad_salida_id
               AND p.numero_asiento = @numero_asiento
               AND p.estado <> 'Anulado'
             """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("unidad_salida_id", NpgsqlDbType.Bigint).Value = unidadDeSalidaId;
        comando.Parameters.Add("numero_asiento", NpgsqlDbType.Smallint).Value = numeroAsiento;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? Mapear(lector) : null;
    }

    public async Task<IReadOnlyList<Pasaje>> ListarAsync(FiltroDePasajes filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
             SELECT {Columnas}
             FROM pasaje p
             LEFT JOIN unidad_salida u ON u.id = p.unidad_salida_id
             WHERE p.fecha_hora_salida >= (@fecha::date AT TIME ZONE 'America/Lima')
               AND p.fecha_hora_salida < ((@fecha::date + 1) AT TIME ZONE 'America/Lima')
               AND (@hora::time IS NULL
                    OR (p.fecha_hora_salida AT TIME ZONE 'America/Lima')::time = @hora)
               AND (@sentido::varchar IS NULL OR u.sentido = @sentido)
               AND (@estado::varchar IS NULL OR p.estado = @estado)
               AND (@documento::varchar IS NULL OR p.numero_documento = @documento)
             ORDER BY p.fecha_hora_salida, p.numero_asiento NULLS LAST, p.id
             """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("fecha", NpgsqlDbType.Date).Value = filtro.Fecha;
        comando.Parameters.Add("hora", NpgsqlDbType.Time).Value =
            filtro.Hora is null ? DBNull.Value : filtro.Hora.Value;
        comando.Parameters.Add("sentido", NpgsqlDbType.Varchar).Value =
            filtro.Sentido is null ? DBNull.Value : filtro.Sentido.Value.ToString();
        comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value =
            filtro.Estado is null ? DBNull.Value : filtro.Estado.Value.ToString();
        comando.Parameters.Add("documento", NpgsqlDbType.Varchar).Value =
            string.IsNullOrWhiteSpace(filtro.NumeroDocumento)
                ? DBNull.Value
                : filtro.NumeroDocumento.Trim();

        var pasajes = new List<Pasaje>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            pasajes.Add(Mapear(lector));
        }

        return pasajes;
    }

    public async Task<IReadOnlyList<Pasaje>> ListarPorGrupoAsync(long grupoId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
             SELECT {Columnas}
             FROM pasaje p
             WHERE p.grupo_id = @grupo_id
             ORDER BY p.id
             """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value = grupoId;

        var pasajes = new List<Pasaje>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            pasajes.Add(Mapear(lector));
        }

        return pasajes;
    }

    public async Task ActualizarEstadoAsync(long id, EstadoPasaje estado)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            UPDATE pasaje
            SET estado = @estado
            WHERE id = @id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value = estado.ToString();
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = id;

        await comando.ExecuteNonQueryAsync();
    }

    public async Task ActualizarAsync(Pasaje pasaje)
    {
        ArgumentNullException.ThrowIfNull(pasaje);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            UPDATE pasaje
            SET unidad_salida_id = @unidad_salida_id,
                numero_asiento = @numero_asiento,
                nombre_pasajero = @nombre_pasajero,
                tipo_documento = @tipo_documento,
                numero_documento = @numero_documento,
                celular = @celular,
                origen = @origen,
                destino = @destino,
                lugar_embarque = @lugar_embarque,
                fecha_hora_salida = @fecha_hora_salida,
                importe = @importe,
                estado = @estado,
                es_menor = @es_menor,
                nombre_padre_madre = @nombre_padre_madre,
                motivo_menor = @motivo_menor,
                motivo_anulacion = @motivo_anulacion,
                fecha_anulacion = @fecha_anulacion,
                es_diferido = @es_diferido
            WHERE id = @id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("unidad_salida_id", NpgsqlDbType.Bigint).Value =
            (object?)pasaje.UnidadDeSalidaId ?? DBNull.Value;
        comando.Parameters.Add("numero_asiento", NpgsqlDbType.Smallint).Value =
            (object?)pasaje.NumeroAsiento ?? DBNull.Value;
        comando.Parameters.Add("nombre_pasajero", NpgsqlDbType.Varchar).Value = pasaje.NombrePasajero;
        comando.Parameters.Add("tipo_documento", NpgsqlDbType.Varchar).Value = pasaje.TipoDocumento.ToString();
        comando.Parameters.Add("numero_documento", NpgsqlDbType.Varchar).Value = pasaje.NumeroDocumento;
        comando.Parameters.Add("celular", NpgsqlDbType.Varchar).Value =
            (object?)pasaje.Celular ?? DBNull.Value;
        comando.Parameters.Add("origen", NpgsqlDbType.Varchar).Value = pasaje.Origen;
        comando.Parameters.Add("destino", NpgsqlDbType.Varchar).Value = pasaje.Destino;
        comando.Parameters.Add("lugar_embarque", NpgsqlDbType.Varchar).Value = pasaje.LugarEmbarque;
        comando.Parameters.Add("fecha_hora_salida", NpgsqlDbType.TimestampTz).Value = pasaje.FechaHoraSalida;
        comando.Parameters.Add("importe", NpgsqlDbType.Numeric).Value = pasaje.Importe;
        comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value = pasaje.Estado.ToString();
        comando.Parameters.Add("es_menor", NpgsqlDbType.Boolean).Value = pasaje.EsMenor;
        comando.Parameters.Add("nombre_padre_madre", NpgsqlDbType.Varchar).Value =
            (object?)pasaje.NombrePadreMadre ?? DBNull.Value;
        comando.Parameters.Add("motivo_menor", NpgsqlDbType.Varchar).Value =
            (object?)pasaje.MotivoMenor ?? DBNull.Value;
        comando.Parameters.Add("motivo_anulacion", NpgsqlDbType.Varchar).Value =
            (object?)pasaje.MotivoDeAnulacion ?? DBNull.Value;
        comando.Parameters.Add("fecha_anulacion", NpgsqlDbType.TimestampTz).Value =
            (object?)pasaje.FechaDeAnulacion ?? DBNull.Value;
        comando.Parameters.Add("es_diferido", NpgsqlDbType.Boolean).Value =
            pasaje.EsDiferido;
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = pasaje.Id;

        await comando.ExecuteNonQueryAsync();
    }

    public async Task<int> ContarPasajesDelHorarioAsync(TimeOnly hora)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM pasaje
            WHERE estado <> 'Anulado'
              AND tipo_registro = 'Individual'
              AND fecha_hora_salida >= now()
              AND (fecha_hora_salida AT TIME ZONE 'America/Lima')::time = @hora
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("hora", NpgsqlDbType.Time).Value = hora;

        var resultado = await comando.ExecuteScalarAsync();
        return resultado is int cantidad ? cantidad : 0;
    }

    public async Task<bool> AsientoEstaOcupadoAsync(
        long unidadDeSalidaId,
        int numeroAsiento,
        long? excluirPasajeId = null)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM pasaje
            WHERE unidad_salida_id = @unidad_salida_id
              AND numero_asiento = @numero_asiento
              AND estado <> 'Anulado'
              AND (@excluir_pasaje_id IS NULL OR id <> @excluir_pasaje_id)
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("unidad_salida_id", NpgsqlDbType.Bigint).Value = unidadDeSalidaId;
        comando.Parameters.Add("numero_asiento", NpgsqlDbType.Smallint).Value = numeroAsiento;
        comando.Parameters.Add("excluir_pasaje_id", NpgsqlDbType.Bigint).Value =
            (object?)excluirPasajeId ?? DBNull.Value;

        var resultado = await comando.ExecuteScalarAsync();
        return resultado is int cantidad && cantidad > 0;
    }

    public async Task<IReadOnlyList<PasajeDeCuadre>> ListarAnuladosEnPeriodoAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? emisorId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
             SELECT {ColumnasDeCuadre}
             FROM pasaje p
             LEFT JOIN pago pg ON pg.pasaje_id = p.id
             WHERE p.estado = 'Anulado'
               AND p.tipo_registro = 'Individual'
               AND p.fecha_anulacion >= @desde
               AND p.fecha_anulacion < @hasta
               AND (@emisor_id IS NULL OR p.emisor_id = @emisor_id)
             GROUP BY p.id
             ORDER BY p.fecha_anulacion, p.id
             """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("desde", NpgsqlDbType.TimestampTz).Value = desdeUtc;
        comando.Parameters.Add("hasta", NpgsqlDbType.TimestampTz).Value = hastaUtcExclusivo;
        comando.Parameters.Add("emisor_id", NpgsqlDbType.Integer).Value =
            (object?)emisorId ?? DBNull.Value;

        var pasajes = new List<PasajeDeCuadre>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            pasajes.Add(MapearDeCuadre(lector));
        }

        return pasajes;
    }

    public async Task<IReadOnlyList<PasajeDeCuadre>> ListarPendientesDeSaldoAsync(int? emisorId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
             SELECT {ColumnasDeCuadre}
             FROM pasaje p
             LEFT JOIN pago pg ON pg.pasaje_id = p.id
             WHERE p.estado IN ('Reservado', 'ReservadoConAdelanto')
               AND p.tipo_registro = 'Individual'
               AND (@emisor_id IS NULL OR p.emisor_id = @emisor_id)
             GROUP BY p.id
             HAVING p.importe -
                    COALESCE(SUM(CASE WHEN pg.tipo = 'Devolucion' THEN -pg.monto ELSE pg.monto END), 0) > 0
             ORDER BY p.fecha_hora_salida, p.id
             """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("emisor_id", NpgsqlDbType.Integer).Value =
            (object?)emisorId ?? DBNull.Value;

        var pasajes = new List<PasajeDeCuadre>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            pasajes.Add(MapearDeCuadre(lector));
        }

        return pasajes;
    }

    private static PasajeDeCuadre MapearDeCuadre(NpgsqlDataReader lector)
    {
        var estado = Enum.Parse<EstadoPasaje>(lector.GetString(9));
        var importe = lector.GetDecimal(8);
        var pagado = lector.GetDecimal(15);
        var saldo = estado is EstadoPasaje.Anulado or EstadoPasaje.NoSePresento
            ? 0
            : Math.Max(0, importe - pagado);

        return new PasajeDeCuadre(
            lector.GetInt64(0),
            lector.IsDBNull(1) ? null : lector.GetInt64(1),
            lector.IsDBNull(2) ? null : lector.GetInt32(2),
            lector.GetString(3),
            lector.GetString(4),
            lector.GetString(5),
            lector.GetString(6),
            lector.GetFieldValue<DateTime>(7),
            importe,
            pagado,
            saldo,
            estado,
            lector.GetInt32(10),
            lector.GetString(11),
            lector.IsDBNull(14) ? null : lector.GetString(14),
            lector.IsDBNull(12) ? null : lector.GetString(12),
            lector.IsDBNull(13) ? null : lector.GetFieldValue<DateTime>(13));
    }

    private static Pasaje Mapear(NpgsqlDataReader lector) =>
        Pasaje.Reconstruir(new PasajeGuardado(
            lector.GetInt64(0),
            lector.IsDBNull(1) ? null : lector.GetInt64(1),
            lector.IsDBNull(2) ? null : lector.GetInt64(2),
            lector.IsDBNull(3) ? null : lector.GetInt32(3),
            lector.GetString(4),
            Enum.Parse<TipoDocumento>(lector.GetString(5)),
            lector.GetString(6),
            lector.IsDBNull(7) ? null : lector.GetString(7),
            lector.GetString(8),
            lector.GetString(9),
            lector.GetString(10),
            lector.GetFieldValue<DateTime>(11),
            lector.GetFieldValue<DateTime>(12),
            lector.GetDecimal(13),
            Enum.Parse<EstadoPasaje>(lector.GetString(14)),
            Enum.Parse<TipoDeRegistroDePasaje>(lector.GetString(15)),
            lector.GetInt32(16),
            lector.GetString(17),
            lector.GetBoolean(18),
            lector.IsDBNull(19) ? null : lector.GetString(19),
            lector.IsDBNull(20) ? null : lector.GetString(20),
            lector.GetBoolean(21),
            lector.IsDBNull(24) ? null : lector.GetString(24),
            lector.IsDBNull(22) ? null : lector.GetString(22),
            lector.IsDBNull(23) ? null : lector.GetFieldValue<DateTime>(23),
            lector.GetBoolean(25)));
}
