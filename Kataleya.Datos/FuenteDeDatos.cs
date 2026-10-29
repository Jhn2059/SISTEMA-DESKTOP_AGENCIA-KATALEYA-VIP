using Npgsql;

namespace Kataleya.Datos;

public sealed class FuenteDeDatos : IAsyncDisposable
{
    static FuenteDeDatos() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    private readonly NpgsqlDataSource _fuente;

    public FuenteDeDatos(string cadenaDeConexion)
    {
        if (string.IsNullOrWhiteSpace(cadenaDeConexion))
        {
            throw new ArgumentException("La cadena de conexión no puede estar vacía.", nameof(cadenaDeConexion));
        }

        _fuente = NpgsqlDataSource.Create(cadenaDeConexion);
    }

    public ValueTask<NpgsqlConnection> AbrirConexionAsync() => _fuente.OpenConnectionAsync();

    public ValueTask DisposeAsync() => _fuente.DisposeAsync();
}
