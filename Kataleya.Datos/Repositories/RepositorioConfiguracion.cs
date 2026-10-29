using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioConfiguracion : IRepositorioConfiguracion
{
    private const string SeleccionarTarifa = """
        SELECT t.id,
               t.tipo,
               t.nombre,
               t.importe,
               t.vigencia_desde,
               t.vigencia_hasta,
               t.sentido,
               t.usuario_id,
               u.nombre_usuario,
               t.fecha_hora
        FROM tarifa t
        INNER JOIN usuario u ON u.id = t.usuario_id
        """;

    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioConfiguracion(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<IReadOnlyList<Tarifa>> ListarTarifasAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"{SeleccionarTarifa} ORDER BY t.tipo, t.vigencia_desde, t.id",
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

    public async Task<Tarifa?> BuscarTarifaAsync(int id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"{SeleccionarTarifa} WHERE t.id = @id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Integer).Value = id;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? LeerTarifa(lector) : null;
    }

    public async Task<int> AgregarTarifaAsync(Tarifa tarifa)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO tarifa
                (tipo, nombre, importe, vigencia_desde, vigencia_hasta, sentido, usuario_id)
            VALUES
                (@tipo, @nombre, @importe, @vigencia_desde, @vigencia_hasta, @sentido, @usuario_id)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        AgregarParametrosDeTarifa(comando, tarifa);
        return (int)(await comando.ExecuteScalarAsync())!;
    }

    public async Task ActualizarTarifaAsync(Tarifa tarifa)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            UPDATE tarifa
            SET nombre = @nombre,
                importe = @importe,
                vigencia_desde = @vigencia_desde,
                vigencia_hasta = @vigencia_hasta,
                sentido = @sentido,
                usuario_id = @usuario_id,
                fecha_hora = now()
            WHERE id = @id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        comando.Parameters.Add("id", NpgsqlDbType.Integer).Value = tarifa.Id;
        AgregarParametrosDeTarifa(comando, tarifa);

        await comando.ExecuteNonQueryAsync();
    }

    public async Task EliminarTarifaAsync(int id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "DELETE FROM tarifa WHERE id = @id AND tipo = 'Programada'",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Integer).Value = id;

        await comando.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<TipoDeVehiculo>> ListarTiposDeVehiculoAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT id, nombre, numero_asientos FROM tipo_vehiculo ORDER BY numero_asientos, nombre",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        var tipos = new List<TipoDeVehiculo>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            tipos.Add(new TipoDeVehiculo
            {
                Id = lector.GetInt16(0),
                Nombre = lector.GetString(1),
                NumeroAsientos = lector.GetInt16(2)
            });
        }

        return tipos;
    }

    public async Task<TipoDeVehiculo?> BuscarTipoDeVehiculoAsync(short id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT id, nombre, numero_asientos FROM tipo_vehiculo WHERE id = @id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Smallint).Value = id;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync()
            ? new TipoDeVehiculo
            {
                Id = lector.GetInt16(0),
                Nombre = lector.GetString(1),
                NumeroAsientos = lector.GetInt16(2)
            }
            : null;
    }

    public async Task<short> AgregarTipoDeVehiculoAsync(TipoDeVehiculo tipo)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO tipo_vehiculo (nombre, numero_asientos)
            VALUES (@nombre, @numero_asientos)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("nombre", NpgsqlDbType.Varchar).Value = tipo.Nombre;
        comando.Parameters.Add("numero_asientos", NpgsqlDbType.Smallint).Value = tipo.NumeroAsientos;

        return (short)(await comando.ExecuteScalarAsync())!;
    }

    public async Task ActualizarTipoDeVehiculoAsync(TipoDeVehiculo tipo)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "UPDATE tipo_vehiculo SET nombre = @nombre, numero_asientos = @numero_asientos WHERE id = @id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Smallint).Value = tipo.Id;
        comando.Parameters.Add("nombre", NpgsqlDbType.Varchar).Value = tipo.Nombre;
        comando.Parameters.Add("numero_asientos", NpgsqlDbType.Smallint).Value = tipo.NumeroAsientos;

        await comando.ExecuteNonQueryAsync();
    }

    public async Task EliminarTipoDeVehiculoAsync(short id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "DELETE FROM tipo_vehiculo WHERE id = @id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Smallint).Value = id;

        await comando.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<ParametroDelNegocio>> ListarParametrosAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT nombre, valor, usuario_id, fecha_hora FROM parametro ORDER BY nombre",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        var parametros = new List<ParametroDelNegocio>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            parametros.Add(new ParametroDelNegocio
            {
                Nombre = lector.GetString(0),
                Valor = lector.GetString(1),
                UsuarioId = lector.IsDBNull(2) ? null : lector.GetInt32(2),
                FechaHora = lector.GetDateTime(3)
            });
        }

        return parametros;
    }

    public async Task GuardarParametroAsync(ParametroDelNegocio parametro)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO parametro (nombre, valor, usuario_id, fecha_hora)
            VALUES (@nombre, @valor, @usuario_id, now())
            ON CONFLICT (nombre) DO UPDATE
            SET valor = EXCLUDED.valor,
                usuario_id = EXCLUDED.usuario_id,
                fecha_hora = now()
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("nombre", NpgsqlDbType.Varchar).Value = parametro.Nombre;
        comando.Parameters.Add("valor", NpgsqlDbType.Text).Value = parametro.Valor;
        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value =
            parametro.UsuarioId is null ? DBNull.Value : parametro.UsuarioId.Value;

        await comando.ExecuteNonQueryAsync();
    }

    private static void AgregarParametrosDeTarifa(NpgsqlCommand comando, Tarifa tarifa)
    {
        comando.Parameters.Add("tipo", NpgsqlDbType.Varchar).Value = tarifa.Tipo.ToString();
        comando.Parameters.Add("nombre", NpgsqlDbType.Varchar).Value = tarifa.Nombre;
        comando.Parameters.Add("importe", NpgsqlDbType.Numeric).Value = tarifa.Importe;
        comando.Parameters.Add("vigencia_desde", NpgsqlDbType.Date).Value =
            tarifa.VigenciaDesde is null ? DBNull.Value : tarifa.VigenciaDesde.Value;
        comando.Parameters.Add("vigencia_hasta", NpgsqlDbType.Date).Value =
            tarifa.VigenciaHasta is null ? DBNull.Value : tarifa.VigenciaHasta.Value;
        comando.Parameters.Add("sentido", NpgsqlDbType.Varchar).Value = tarifa.Sentido.ToString();
        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = tarifa.UsuarioId;
    }

    private static Tarifa LeerTarifa(NpgsqlDataReader lector) =>
        new()
        {
            Id = lector.GetInt32(0),
            Tipo = Enum.Parse<TipoDeTarifa>(lector.GetString(1)),
            Nombre = lector.GetString(2),
            Importe = lector.GetDecimal(3),
            VigenciaDesde = lector.IsDBNull(4) ? null : lector.GetFieldValue<DateOnly>(4),
            VigenciaHasta = lector.IsDBNull(5) ? null : lector.GetFieldValue<DateOnly>(5),
            Sentido = Enum.Parse<SentidoDelViaje>(lector.GetString(6)),
            UsuarioId = lector.GetInt32(7),
            UsuarioNombre = lector.GetString(8),
            FechaHora = lector.GetDateTime(9)
        };
}
