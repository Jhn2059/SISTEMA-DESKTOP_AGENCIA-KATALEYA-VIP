using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioBitacora : IRepositorioBitacora
{
    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioBitacora(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task AgregarAsync(RegistroDeBitacora registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO registro_bitacora
                (usuario_id, fecha_hora, operacion, pasaje_id, valor_anterior, valor_nuevo, clave_de_operacion)
            VALUES
                (@usuario_id, @fecha_hora, @operacion, @pasaje_id, @valor_anterior, @valor_nuevo, @clave_de_operacion)
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = registro.Usuario.Id;
        comando.Parameters.Add("fecha_hora", NpgsqlDbType.TimestampTz).Value = registro.FechaHora;
        comando.Parameters.Add("operacion", NpgsqlDbType.Varchar).Value = registro.Operacion;
        comando.Parameters.Add("pasaje_id", NpgsqlDbType.Bigint).Value =
            registro.PasajeId is null ? DBNull.Value : registro.PasajeId.Value;
        comando.Parameters.Add("valor_anterior", NpgsqlDbType.Jsonb).Value =
            registro.ValorAnterior is null ? DBNull.Value : registro.ValorAnterior;
        comando.Parameters.Add("valor_nuevo", NpgsqlDbType.Jsonb).Value =
            registro.ValorNuevo is null ? DBNull.Value : registro.ValorNuevo;
        comando.Parameters.Add("clave_de_operacion", NpgsqlDbType.Uuid).Value =
            registro.ClaveDeOperacion is null ? DBNull.Value : registro.ClaveDeOperacion.Value;

        await comando.ExecuteNonQueryAsync();
    }

    public async Task<bool> ExisteClaveAsync(Guid clave)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM registro_bitacora WHERE clave_de_operacion = @clave)",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("clave", NpgsqlDbType.Uuid).Value = clave;

        var resultado = await comando.ExecuteScalarAsync();
        return resultado is true;
    }

    public async Task<EntradaDeBitacora?> BuscarPorClaveAsync(Guid clave)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand
        {
            Connection = conexion,
            Transaction = _contexto.TransaccionActual,
            CommandText = """
                SELECT b.id,
                       b.fecha_hora,
                       u.nombre_usuario,
                       b.operacion,
                       b.pasaje_id,
                       b.valor_anterior::text,
                       b.valor_nuevo::text
                FROM registro_bitacora b
                INNER JOIN usuario u ON u.id = b.usuario_id
                WHERE b.clave_de_operacion = @clave
                ORDER BY b.id
                LIMIT 1
                """
        };
        comando.Parameters.Add("clave", NpgsqlDbType.Uuid).Value = clave;

        await using var lector = await comando.ExecuteReaderAsync();
        if (!await lector.ReadAsync())
        {
            return null;
        }

        return new EntradaDeBitacora
        {
            Id = lector.GetInt64(0),
            FechaHora = lector.GetDateTime(1),
            UsuarioNombre = lector.GetString(2),
            Operacion = lector.GetString(3),
            PasajeId = lector.IsDBNull(4) ? null : lector.GetInt64(4),
            ValorAnterior = lector.IsDBNull(5) ? null : lector.GetString(5),
            ValorNuevo = lector.IsDBNull(6) ? null : lector.GetString(6)
        };
    }

    public async Task<IReadOnlyList<EntradaDeBitacora>> ConsultarAsync(FiltroDeBitacora filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var conexion = await _contexto.ObtenerConexionAsync();
        var condiciones = new List<string>();

        await using var comando = new NpgsqlCommand
        {
            Connection = conexion,
            Transaction = _contexto.TransaccionActual
        };

        if (filtro.UsuarioId is not null)
        {
            condiciones.Add("b.usuario_id = @usuario");
            comando.Parameters.Add("usuario", NpgsqlDbType.Integer).Value = filtro.UsuarioId.Value;
        }

        if (filtro.Desde is not null)
        {
            condiciones.Add("b.fecha_hora::date >= @desde");
            comando.Parameters.Add("desde", NpgsqlDbType.Date).Value = filtro.Desde.Value;
        }

        if (filtro.Hasta is not null)
        {
            condiciones.Add("b.fecha_hora::date <= @hasta");
            comando.Parameters.Add("hasta", NpgsqlDbType.Date).Value = filtro.Hasta.Value;
        }

        if (filtro.PasajeId is not null)
        {
            condiciones.Add("b.pasaje_id = @pasaje");
            comando.Parameters.Add("pasaje", NpgsqlDbType.Bigint).Value = filtro.PasajeId.Value;
        }

        var sql = """
            SELECT b.id,
                   b.fecha_hora,
                   u.nombre_usuario,
                   b.operacion,
                   b.pasaje_id,
                   b.valor_anterior::text,
                   b.valor_nuevo::text
            FROM registro_bitacora b
            INNER JOIN usuario u ON u.id = b.usuario_id
            """;

        if (condiciones.Count > 0)
        {
            sql += " WHERE " + string.Join(" AND ", condiciones);
        }

        sql += " ORDER BY b.fecha_hora DESC, b.id DESC LIMIT 500";
        comando.CommandText = sql;

        var entradas = new List<EntradaDeBitacora>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            entradas.Add(new EntradaDeBitacora
            {
                Id = lector.GetInt64(0),
                FechaHora = lector.GetDateTime(1),
                UsuarioNombre = lector.GetString(2),
                Operacion = lector.GetString(3),
                PasajeId = lector.IsDBNull(4) ? null : lector.GetInt64(4),
                ValorAnterior = lector.IsDBNull(5) ? null : lector.GetString(5),
                ValorNuevo = lector.IsDBNull(6) ? null : lector.GetString(6)
            });
        }

        return entradas;
    }
}
