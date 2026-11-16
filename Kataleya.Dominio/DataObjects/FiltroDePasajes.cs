using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record FiltroDePasajes(
    DateOnly Fecha,
    TimeOnly? Hora,
    Sentido? Sentido,
    EstadoPasaje? Estado,
    string? NumeroDocumento);
