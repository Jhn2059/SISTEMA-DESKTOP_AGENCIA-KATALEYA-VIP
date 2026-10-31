using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioParametros : IRepositorioParametros
{
    private const string SeleccionarParametro = """
        SELECT nombre,
               valor,
               usuario_id,
               fecha_hora
        FROM parametro_negocio
        """;

    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioParametros(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<ParametroDelNegocio?> BuscarAsync(string nombre)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"{SeleccionarParametro} WHERE nombre = @nombre",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("nombre", NpgsqlDbType.Varchar).Value = nombre;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? LeerParametro(lector) : null;
    }

    public async Task<IReadOnlyList<ParametroDelNegocio>> ListarAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"{SeleccionarParametro} ORDER BY nombre",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        var parametros = new List<ParametroDelNegocio>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            parametros.Add(LeerParametro(lector));
        }

        return parametros;
    }

    public async Task GuardarAsync(ParametroDelNegocio parametro)
    {
        ArgumentNullException.ThrowIfNull(parametro);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO parametro_negocio
                (nombre, valor, usuario_id, fecha_hora)
            VALUES
                (@nombre, @valor, @usuario_id, now())
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
        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = parametro.IdUsuario;

        await comando.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<TipoDeVehiculo>> ListarTiposDeVehiculoAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, nombre, cantidad_asientos, distribucion
            FROM tipo_vehiculo
            ORDER BY id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        var tipos = new List<TipoDeVehiculo>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            tipos.Add(TipoDeVehiculo.Reconstruir(new TipoDeVehiculoGuardado(
                lector.GetInt16(0),
                lector.GetString(1),
                lector.GetInt16(2),
                lector.IsDBNull(3) ? null : lector.GetString(3))));
        }

        return tipos;
    }

    private static ParametroDelNegocio LeerParametro(NpgsqlDataReader lector) =>
        ParametroDelNegocio.Reconstruir(new ParametroDelNegocioGuardado(
            lector.GetString(0),
            lector.GetString(1),
            lector.GetInt32(2),
            lector.GetDateTime(3)));
}
