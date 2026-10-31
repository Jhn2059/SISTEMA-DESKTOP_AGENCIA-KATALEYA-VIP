using Kataleya.Dominio.DataObjects;

namespace Kataleya.Dominio.Entities;

public sealed class TipoDeVehiculo
{
    public TipoDeVehiculo(string nombre, int cantidadDeAsientos)
        : this(0, nombre, cantidadDeAsientos)
    {
    }

    internal TipoDeVehiculo(
        short id,
        string nombre,
        int cantidadDeAsientos,
        string? distribucion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        Id = id;
        Nombre = nombre;
        CantidadDeAsientos = cantidadDeAsientos;
        Distribucion = distribucion;
    }

    public short Id { get; }

    public string Nombre { get; }

    public int CantidadDeAsientos { get; }

    public string? Distribucion { get; }

    public static TipoDeVehiculo Reconstruir(TipoDeVehiculoGuardado guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return new TipoDeVehiculo(
            guardado.Id,
            guardado.Nombre,
            guardado.CantidadDeAsientos,
            guardado.Distribucion);
    }
}
