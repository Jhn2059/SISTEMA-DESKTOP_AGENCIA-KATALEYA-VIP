namespace Kataleya.Dominio.DataObjects;

// Lo que cada servicio anota en la bitácora; el autor, la clave y la fecha los pone ServicioBitacora.
public sealed record MovimientoDeBitacora(
    string Operacion,
    long? IdPasaje,
    string? ValorAnterior,
    string? ValorNuevo);
