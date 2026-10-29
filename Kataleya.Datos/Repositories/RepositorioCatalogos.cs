using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioCatalogos : IRepositorioCatalogos
{
    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioCatalogos(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<IReadOnlyList<Rol>> ListarRolesAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT id, nombre FROM rol ORDER BY nombre",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        var roles = new List<Rol>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            roles.Add(Rol.Reconstruir(new RolGuardado(lector.GetInt16(0), lector.GetString(1))));
        }

        return roles;
    }

    public async Task<IReadOnlyList<Sede>> ListarSedesAsync()
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT id, nombre FROM sede ORDER BY nombre",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        var sedes = new List<Sede>();
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            sedes.Add(Sede.Reconstruir(new SedeGuardada(lector.GetInt16(0), lector.GetString(1))));
        }

        return sedes;
    }

    public async Task<Rol?> BuscarRolAsync(short id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT id, nombre FROM rol WHERE id = @id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Smallint).Value = id;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync()
            ? Rol.Reconstruir(new RolGuardado(lector.GetInt16(0), lector.GetString(1)))
            : null;
    }

    public async Task<Sede?> BuscarSedeAsync(short id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT id, nombre FROM sede WHERE id = @id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Smallint).Value = id;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync()
            ? Sede.Reconstruir(new SedeGuardada(lector.GetInt16(0), lector.GetString(1)))
            : null;
    }
}
