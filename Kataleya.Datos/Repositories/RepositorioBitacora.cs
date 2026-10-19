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
                (usuario_id, fecha_hora, operacion, valor_anterior, valor_nuevo, clave_de_operacion)
            VALUES
                (@usuario_id, @fecha_hora, @operacion, @valor_anterior, @valor_nuevo, @clave_de_operacion)
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = registro.Usuario.Id;
        comando.Parameters.Add("fecha_hora", NpgsqlDbType.TimestampTz).Value = registro.FechaHora;
        comando.Parameters.Add("operacion", NpgsqlDbType.Varchar).Value = registro.Operacion;
        comando.Parameters.Add("valor_anterior", NpgsqlDbType.Jsonb).Value =
            registro.ValorAnterior is null ? DBNull.Value : registro.ValorAnterior;
        comando.Parameters.Add("valor_nuevo", NpgsqlDbType.Jsonb).Value =
            registro.ValorNuevo is null ? DBNull.Value : registro.ValorNuevo;
        comando.Parameters.Add("clave_de_operacion", NpgsqlDbType.Uuid).Value =
            registro.ClaveDeOperacion is null ? DBNull.Value : registro.ClaveDeOperacion.Value;

        await comando.ExecuteNonQueryAsync();
    }
}
