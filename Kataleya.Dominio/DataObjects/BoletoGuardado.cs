namespace Kataleya.Dominio.DataObjects;

public sealed record BoletoGuardado(
    long Id,
    long PasajeId,
    string Serie,
    int Correlativo,
    DateTime FechaEmision,
    int EmitidoPorId,
    string EmitidoPor);
