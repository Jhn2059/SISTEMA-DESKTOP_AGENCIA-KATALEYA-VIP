using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record SolicitudDeTarifa(
    string Nombre,
    decimal Importe,
    Periodo Vigencia,
    Sentido? Sentido);
