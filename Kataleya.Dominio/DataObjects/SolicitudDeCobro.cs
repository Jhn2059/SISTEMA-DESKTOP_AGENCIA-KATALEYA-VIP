using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record SolicitudDeCobro(decimal Monto, MedioPago MedioDePago);
