using Npgsql;

namespace Kataleya.Datos;

public sealed class ContextoDeBaseDeDatos : IAsyncDisposable
{
    private readonly FuenteDeDatos _fuente;
    private NpgsqlConnection? _conexion;
    private NpgsqlTransaction? _transaccion;

    public ContextoDeBaseDeDatos(FuenteDeDatos fuente) =>
        _fuente = fuente ?? throw new ArgumentNullException(nameof(fuente));

    public NpgsqlTransaction? TransaccionActual => _transaccion;

    public async Task<NpgsqlConnection> ObtenerConexionAsync()
    {
        _conexion ??= await _fuente.AbrirConexionAsync();
        return _conexion;
    }

    public async Task EjecutarEnTransaccionAsync(Func<Task> trabajo)
    {
        ArgumentNullException.ThrowIfNull(trabajo);

        if (_transaccion is not null)
        {
            await trabajo();
            return;
        }

        var conexion = await ObtenerConexionAsync();
        var transaccion = await conexion.BeginTransactionAsync();
        _transaccion = transaccion;

        try
        {
            await trabajo();
            await transaccion.CommitAsync();
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
        finally
        {
            await transaccion.DisposeAsync();
            _transaccion = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaccion is not null)
        {
            await _transaccion.DisposeAsync();
            _transaccion = null;
        }

        if (_conexion is not null)
        {
            await _conexion.DisposeAsync();
            _conexion = null;
        }
    }
}
