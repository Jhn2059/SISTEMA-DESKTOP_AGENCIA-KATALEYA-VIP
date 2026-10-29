using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioPreferencias : IRepositorioPreferencias
{
    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioPreferencias(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<Preferencias?> BuscarAsync(int idUsuario)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT tema FROM preferencias WHERE usuario_id = @usuario_id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = idUsuario;

        return await comando.ExecuteScalarAsync() is string tema
            ? new Preferencias(idUsuario, Enum.Parse<Tema>(tema))
            : null;
    }

    public async Task GuardarAsync(Preferencias preferencias)
    {
        ArgumentNullException.ThrowIfNull(preferencias);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO preferencias (usuario_id, tema)
            VALUES (@usuario_id, @tema)
            ON CONFLICT (usuario_id) DO UPDATE SET tema = EXCLUDED.tema
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = preferencias.IdUsuario;
        comando.Parameters.Add("tema", NpgsqlDbType.Varchar).Value = preferencias.Tema.ToString();

        await comando.ExecuteNonQueryAsync();
    }
}
