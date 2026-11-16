using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioGrupos : IRepositorioGrupos
{
    private const string Columnas =
        """
        id, codigo, telefono_contacto, precio_acordado, origen, destino,
        lugar_recojo, fecha_hora_salida, fecha_registro, creado_por,
        creado_por_nombre, clave_de_operacion
        """;

    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioGrupos(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<string> SiguienteCodigoAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT 'G' || lpad(nextval('grupo_codigo_seq')::text, 4, '0')
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        var resultado = await comando.ExecuteScalarAsync();
        return resultado as string
            ?? throw new InvalidOperationException("No se pudo generar el código del grupo.");
    }

    public async Task<long> RegistrarAsync(Grupo grupo)
    {
        ArgumentNullException.ThrowIfNull(grupo);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO grupo
                (codigo, telefono_contacto, precio_acordado, origen, destino,
                 lugar_recojo, fecha_hora_salida, fecha_registro, creado_por,
                 creado_por_nombre, clave_de_operacion)
            VALUES
                (@codigo, @telefono_contacto, @precio_acordado, @origen, @destino,
                 @lugar_recojo, @fecha_hora_salida, @fecha_registro, @creado_por,
                 @creado_por_nombre, @clave_de_operacion)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("codigo", NpgsqlDbType.Varchar).Value = grupo.Codigo;
        comando.Parameters.Add("telefono_contacto", NpgsqlDbType.Varchar).Value =
            grupo.TelefonoContacto;
        comando.Parameters.Add("precio_acordado", NpgsqlDbType.Numeric).Value =
            grupo.PrecioAcordado;
        comando.Parameters.Add("origen", NpgsqlDbType.Varchar).Value = grupo.Origen;
        comando.Parameters.Add("destino", NpgsqlDbType.Varchar).Value = grupo.Destino;
        comando.Parameters.Add("lugar_recojo", NpgsqlDbType.Varchar).Value = grupo.LugarDeRecojo;
        comando.Parameters.Add("fecha_hora_salida", NpgsqlDbType.TimestampTz).Value =
            grupo.FechaHoraSalida;
        comando.Parameters.Add("fecha_registro", NpgsqlDbType.TimestampTz).Value =
            grupo.FechaRegistro;
        comando.Parameters.Add("creado_por", NpgsqlDbType.Integer).Value = grupo.CreadoPorId;
        comando.Parameters.Add("creado_por_nombre", NpgsqlDbType.Varchar).Value = grupo.CreadoPor;
        comando.Parameters.Add("clave_de_operacion", NpgsqlDbType.Uuid).Value =
            (object?)grupo.ClaveDeOperacion ?? DBNull.Value;

        var idResultado = await comando.ExecuteScalarAsync();
        return idResultado is long id ? id : 0;
    }

    public async Task<Grupo?> BuscarPorClaveDeOperacionAsync(Guid clave)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"SELECT {Columnas} FROM grupo WHERE clave_de_operacion = @clave",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("clave", NpgsqlDbType.Uuid).Value = clave;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? Mapear(lector) : null;
    }

    private static Grupo Mapear(NpgsqlDataReader lector) =>
        Grupo.Reconstruir(new GrupoGuardado(
            lector.GetInt64(0),
            lector.GetString(1),
            lector.GetString(2),
            lector.GetDecimal(3),
            lector.GetString(4),
            lector.GetString(5),
            lector.GetString(6),
            lector.GetFieldValue<DateTime>(7),
            lector.GetFieldValue<DateTime>(8),
            lector.GetInt32(9),
            lector.GetString(10),
            lector.IsDBNull(11) ? null : lector.GetGuid(11)));
}
