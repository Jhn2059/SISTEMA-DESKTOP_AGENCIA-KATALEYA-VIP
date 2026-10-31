using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioTarifas : IRepositorioTarifas
{
    private const string SeleccionarTarifa = """
        SELECT t.id,
               t.tipo,
               t.nombre,
               t.importe,
               t.desde,
               t.hasta,
               t.usuario_id,
               u.nombre,
               t.sentido
        FROM tarifa t
        INNER JOIN usuario u ON u.id = t.usuario_id
        """;

    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioTarifas(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<Tarifa?> BuscarVigenteAsync(DateOnly fecha, Sentido sentido)
    {
        var programada = await BuscarProgramadaVigenteAsync(fecha, sentido);
        return programada ?? await BuscarRegularAsync(sentido);
    }

    public async Task<IReadOnlyList<Tarifa>> ListarAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            {SeleccionarTarifa}
            ORDER BY CASE WHEN t.tipo = 'Regular' THEN 0 ELSE 1 END,
                     t.sentido,
                     t.desde,
                     t.id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        var tarifas = new List<Tarifa>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            tarifas.Add(LeerTarifa(lector));
        }

        return tarifas;
    }

    public async Task<int> GuardarAsync(Tarifa tarifa)
    {
        ArgumentNullException.ThrowIfNull(tarifa);

        var conexion = await _contexto.ObtenerConexionAsync();
        var programada = tarifa as TarifaProgramada;
        var esNueva = tarifa.Id == 0;

        await using var comando = new NpgsqlCommand(
            esNueva
                ? """
                  INSERT INTO tarifa
                      (tipo, nombre, importe, desde, hasta, usuario_id, sentido)
                  VALUES
                      (@tipo, @nombre, @importe, @desde, @hasta, @usuario_id, @sentido)
                  RETURNING id
                  """
                : """
                  UPDATE tarifa
                  SET tipo = @tipo,
                      nombre = @nombre,
                      importe = @importe,
                      desde = @desde,
                      hasta = @hasta,
                      usuario_id = @usuario_id,
                      sentido = @sentido
                  WHERE id = @id
                  """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        comando.Parameters.Add("tipo", NpgsqlDbType.Varchar).Value = tarifa.Tipo.ToString();
        comando.Parameters.Add("nombre", NpgsqlDbType.Varchar).Value =
            programada is null ? DBNull.Value : programada.Nombre;
        comando.Parameters.Add("importe", NpgsqlDbType.Numeric).Value = tarifa.Importe;
        comando.Parameters.Add("desde", NpgsqlDbType.Date).Value =
            programada is null ? DBNull.Value : programada.Vigencia.Desde;
        comando.Parameters.Add("hasta", NpgsqlDbType.Date).Value =
            programada is null ? DBNull.Value : programada.Vigencia.Hasta;
        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = tarifa.IdUsuario;
        comando.Parameters.Add("sentido", NpgsqlDbType.Varchar).Value =
            ObtenerSentido(tarifa) is { } sentido ? sentido.ToString() : DBNull.Value;

        if (!esNueva)
        {
            comando.Parameters.Add("id", NpgsqlDbType.Integer).Value = tarifa.Id;

            var filas = await comando.ExecuteNonQueryAsync();
            if (filas != 1)
            {
                throw new InvalidOperationException("No se encontró la tarifa que se quería actualizar.");
            }

            return tarifa.Id;
        }

        var resultado = await comando.ExecuteScalarAsync();
        return Convert.ToInt32(resultado);
    }

    public async Task QuitarAsync(TarifaProgramada tarifa)
    {
        ArgumentNullException.ThrowIfNull(tarifa);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "DELETE FROM tarifa WHERE id = @id AND tipo = 'Programada'",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Integer).Value = tarifa.Id;

        var filas = await comando.ExecuteNonQueryAsync();
        if (filas != 1)
        {
            throw new InvalidOperationException("No se encontró la tarifa programada que se quería quitar.");
        }
    }

    private async Task<Tarifa?> BuscarProgramadaVigenteAsync(DateOnly fecha, Sentido sentido)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            {SeleccionarTarifa}
            WHERE t.tipo = 'Programada'
              AND t.desde <= @fecha
              AND t.hasta >= @fecha
              AND (t.sentido IS NULL OR t.sentido = @sentido)
            ORDER BY (t.sentido = @sentido) DESC, t.id DESC
            LIMIT 1
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("fecha", NpgsqlDbType.Date).Value = fecha;
        comando.Parameters.Add("sentido", NpgsqlDbType.Varchar).Value = sentido.ToString();

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? LeerTarifa(lector) : null;
    }

    private async Task<Tarifa?> BuscarRegularAsync(Sentido sentido)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            {SeleccionarTarifa}
            WHERE t.tipo = 'Regular'
              AND t.sentido = @sentido
            LIMIT 1
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("sentido", NpgsqlDbType.Varchar).Value = sentido.ToString();

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? LeerTarifa(lector) : null;
    }

    private static Sentido? ObtenerSentido(Tarifa tarifa) => tarifa switch
    {
        TarifaRegular regular => regular.Sentido,
        TarifaProgramada programada => programada.Sentido,
        _ => throw new ArgumentOutOfRangeException(nameof(tarifa), "El tipo de tarifa no es válido.")
    };

    private static Tarifa LeerTarifa(NpgsqlDataReader lector)
    {
        var tipo = Enum.Parse<TipoDeTarifa>(lector.GetString(1));
        var nombre = lector.IsDBNull(2) ? null : lector.GetString(2);
        var desde = lector.IsDBNull(4) ? (DateOnly?)null : lector.GetFieldValue<DateOnly>(4);
        var hasta = lector.IsDBNull(5) ? (DateOnly?)null : lector.GetFieldValue<DateOnly>(5);
        var sentido = lector.IsDBNull(8)
            ? (Sentido?)null
            : Enum.Parse<Sentido>(lector.GetString(8));

        var guardado = new TarifaGuardada(
            lector.GetInt32(0),
            tipo,
            nombre,
            lector.GetDecimal(3),
            desde is not null && hasta is not null ? new Periodo(desde.Value, hasta.Value) : null,
            lector.GetInt32(6),
            lector.GetString(7),
            sentido);

        return Tarifa.Reconstruir(guardado);
    }
}
