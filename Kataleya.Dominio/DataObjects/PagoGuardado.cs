using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record PagoGuardado(
    long Id,
    long? PasajeId,
    long? GrupoId,
    decimal Monto,
    MedioPago MedioDePago,
    TipoDePago Tipo,
    DateTime FechaPago,
    int CobradorId,
    string Cobrador);
