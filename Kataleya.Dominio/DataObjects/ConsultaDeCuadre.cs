namespace Kataleya.Dominio.DataObjects;

public sealed record ConsultaDeCuadre(
    DateOnly Desde,
    DateOnly Hasta,
    int? EmisorId = null);
