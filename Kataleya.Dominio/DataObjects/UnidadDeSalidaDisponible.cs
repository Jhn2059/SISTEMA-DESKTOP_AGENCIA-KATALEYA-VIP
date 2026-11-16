using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record UnidadDeSalidaDisponible(
    long Id,
    int Numero,
    long? GrupoId,
    Sentido Sentido,
    short TipoVehiculoId,
    string TipoVehiculoNombre,
    int CantidadAsientos,
    IReadOnlyList<int> AsientosOcupados,
    DateTime FechaHoraSalida,
    EstadoUnidadDeSalida Estado,
    string AbiertaPor);
