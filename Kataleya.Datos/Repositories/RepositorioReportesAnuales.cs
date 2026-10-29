using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioReportesAnuales : IRepositorioReportesAnuales
{
    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioReportesAnuales(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<bool> ExisteDelAnioAsync(int anio)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM reporte_anual WHERE anio = @anio)",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("anio", NpgsqlDbType.Smallint).Value = (short)anio;

        return (bool)(await comando.ExecuteScalarAsync())!;
    }

    public async Task AgregarAsync(ReporteAnual reporte)
    {
        ArgumentNullException.ThrowIfNull(reporte);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO reporte_anual (anio, usuario_id, numero_pasajes, ruta_pdf)
            VALUES (@anio, @usuario_id, @numero_pasajes, @ruta_pdf)
            RETURNING fecha_hora
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("anio", NpgsqlDbType.Smallint).Value = (short)reporte.Anio;
        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = reporte.UsuarioId;
        comando.Parameters.Add("numero_pasajes", NpgsqlDbType.Integer).Value = reporte.CantidadDePasajesDepurados;
        comando.Parameters.Add("ruta_pdf", NpgsqlDbType.Text).Value = reporte.RutaPdf;

        reporte.FechaHoraGeneracion = ((DateTime)(await comando.ExecuteScalarAsync())!).ToUniversalTime();
    }
}
