using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioPasajes : IRepositorioPasajes
{
    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioPasajes(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<int> ContarPasajesDelHorarioAsync(TimeOnly hora)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM pasaje
            WHERE estado <> 'Anulado'
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

    public async Task<long> RegistrarPasajeAsync(Pasaje pasaje)
    {
        ArgumentNullException.ThrowIfNull(pasaje);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO pasaje
                (unidad_salida_id, numero_asiento, nombre_pasajero, tipo_documento,
                 numero_documento, celular, origen, destino, lugar_embarque,
                 fecha_hora_salida, fecha_venta, adelanto, estado, usuario_id,
                 es_menor, nombre_padre_madre, motivo_menor, vendida_por)
            VALUES
                (@unidad_salida_id, @numero_asiento, @nombre_pasajero, @tipo_documento,
                 @numero_documento, @celular, @origen, @destino, @lugar_embarque,
                 @fecha_hora_salida, @fecha_venta, @adelanto, @estado, @usuario_id,
                 @es_menor, @nombre_padre_madre, @motivo_menor, @vendida_por)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("unidad_salida_id", NpgsqlDbType.Bigint).Value = pasaje.UnidadSalidaId;
        comando.Parameters.Add("numero_asiento", NpgsqlDbType.Smallint).Value = pasaje.NumeroAsiento;
        comando.Parameters.Add("nombre_pasajero", NpgsqlDbType.Varchar).Value = pasaje.NombrePasajero;
        comando.Parameters.Add("tipo_documento", NpgsqlDbType.Varchar).Value = pasaje.TipoDocumento.ToString();
        comando.Parameters.Add("numero_documento", NpgsqlDbType.Varchar).Value = pasaje.NumeroDocumento;
        comando.Parameters.Add("celular", NpgsqlDbType.Varchar).Value = pasaje.Celular;
        comando.Parameters.Add("origen", NpgsqlDbType.Varchar).Value = pasaje.Origen;
        comando.Parameters.Add("destino", NpgsqlDbType.Varchar).Value = pasaje.Destino;
        comando.Parameters.Add("lugar_embarque", NpgsqlDbType.Varchar).Value = pasaje.LugarEmbarque;
        comando.Parameters.Add("fecha_hora_salida", NpgsqlDbType.TimestampTz).Value = pasaje.FechaHoraSalida;
        comando.Parameters.Add("fecha_venta", NpgsqlDbType.TimestampTz).Value = pasaje.FechaVenta;
        comando.Parameters.Add("adelanto", NpgsqlDbType.Numeric).Value = pasaje.Adelanto;
        comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value = pasaje.Estado;
        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = pasaje.IdUsuario;
        comando.Parameters.Add("es_menor", NpgsqlDbType.Boolean).Value = pasaje.EsMenor;
        comando.Parameters.Add("nombre_padre_madre", NpgsqlDbType.Varchar).Value =
            (object?)pasaje.NombrePadreMadre ?? DBNull.Value;
        comando.Parameters.Add("motivo_menor", NpgsqlDbType.Varchar).Value =
            (object?)pasaje.MotivoMenor ?? DBNull.Value;
        comando.Parameters.Add("vendida_por", NpgsqlDbType.Varchar).Value = pasaje.VendidaPor;

        var idResultado = await comando.ExecuteScalarAsync();
        return idResultado is long id ? id : 0;
    }

    public async Task<Pasaje?> ObtenerPasajeAsync(long id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, unidad_salida_id, numero_asiento, nombre_pasajero, tipo_documento,
                   numero_documento, celular, origen, destino, lugar_embarque,
                   fecha_hora_salida, fecha_venta, adelanto, estado, usuario_id,
                   es_menor, nombre_padre_madre, motivo_menor, vendida_por
            FROM pasaje
            WHERE id = @id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = id;

        await using var lector = await comando.ExecuteReaderAsync();
        if (!await lector.ReadAsync())
        {
            return null;
        }

        return Pasaje.Reconstruir(new PasajeGuardado(
            lector.GetInt64(0),
            lector.GetInt64(1),
            lector.GetInt32(2),
            lector.GetString(3),
            Enum.Parse<TipoDocumento>(lector.GetString(4)),
            lector.GetString(5),
            lector.GetString(6),
            lector.GetString(7),
            lector.GetString(8),
            lector.GetString(9),
            lector.GetFieldValue<DateTime>(10),
            lector.GetFieldValue<DateTime>(11),
            lector.GetDecimal(12),
            lector.GetString(13),
            lector.GetInt32(14),
            lector.GetString(18),
            lector.GetBoolean(15),
            lector.IsDBNull(16) ? null : lector.GetString(16),
            lector.IsDBNull(17) ? null : lector.GetString(17)));
    }

    public async Task<bool> AsientoEstaOcupadoAsync(long unidadSalidaId, int numeroAsiento)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM pasaje
            WHERE unidad_salida_id = @unidadSalidaId
              AND numero_asiento = @numeroAsiento
              AND estado <> 'Anulado'
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("unidadSalidaId", NpgsqlDbType.Bigint).Value = unidadSalidaId;
        comando.Parameters.Add("numeroAsiento", NpgsqlDbType.Smallint).Value = numeroAsiento;

        var resultado = await comando.ExecuteScalarAsync();
        return resultado is int cantidad && cantidad > 0;
    }
}
