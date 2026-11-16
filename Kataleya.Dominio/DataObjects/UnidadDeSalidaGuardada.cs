using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record UnidadDeSalidaGuardada(
    long Id,
    int Numero,
    long? GrupoId,
    Sentido? Sentido,
    short TipoVehiculoId,
    DateTime FechaHoraSalida,
    EstadoUnidadDeSalida Estado,
    int AbiertaPorId,
    string AbiertaPor);
