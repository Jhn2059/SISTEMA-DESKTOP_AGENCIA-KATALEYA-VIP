using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record UnidadSalidaGuardada(
    long Id,
    Sentido Sentido,
    short TipoVehiculoId,
    DateTime FechaHoraSalida,
    string Estado,
    int UsuarioId,
    string VendidaPor);