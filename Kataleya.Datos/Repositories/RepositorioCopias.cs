using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioCopias : IRepositorioCopias
{
    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioCopias(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<CopiaDeRespaldo?> BuscarUltimaAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, fecha_hora, correcta, nombre_archivo
            FROM copia_respaldo
            WHERE correcta
            ORDER BY fecha_hora DESC
            LIMIT 1
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        await using var lector = await comando.ExecuteReaderAsync();
        if (!await lector.ReadAsync())
        {
            return null;
        }

        return new CopiaDeRespaldo
        {
            Id = lector.GetInt32(0),
            FechaHora = lector.GetDateTime(1).ToUniversalTime(),
            Correcta = lector.GetBoolean(2),
            NombreArchivo = lector.IsDBNull(3) ? null : lector.GetString(3)
        };
    }

    public async Task<int> ContarFallosSeguidosAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM copia_respaldo
            WHERE NOT correcta
              AND fecha_hora > COALESCE(
                  (SELECT max(fecha_hora) FROM copia_respaldo WHERE correcta),
                  '-infinity'::timestamptz)
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        return (int)(await comando.ExecuteScalarAsync())!;
    }

    public async Task AgregarAsync(CopiaDeRespaldo copia)
    {
        ArgumentNullException.ThrowIfNull(copia);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO copia_respaldo (fecha_hora, correcta, nombre_archivo)
            VALUES (@fecha_hora, @correcta, @nombre_archivo)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("fecha_hora", NpgsqlDbType.TimestampTz).Value = copia.FechaHora;
        comando.Parameters.Add("correcta", NpgsqlDbType.Boolean).Value = copia.Correcta;
        comando.Parameters.Add("nombre_archivo", NpgsqlDbType.Text).Value =
            copia.NombreArchivo is null ? DBNull.Value : copia.NombreArchivo;

        copia.Id = (int)(await comando.ExecuteScalarAsync())!;
    }
}
