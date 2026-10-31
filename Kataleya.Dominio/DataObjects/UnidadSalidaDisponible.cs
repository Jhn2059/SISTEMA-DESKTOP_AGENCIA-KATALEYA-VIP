using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record UnidadSalidaDisponible(
    long Id,
    Sentido Sentido,
    short TipoVehiculoId,
    string TipoVehiculoNombre,
    int CantidadAsientos,
    int AsientosOcupados,
    DateTime FechaHoraSalida,
    string Estado,
    string VendidaPor);