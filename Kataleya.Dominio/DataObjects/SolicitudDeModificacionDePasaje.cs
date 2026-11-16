using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record SolicitudDeModificacionDePasaje(
    long? UnidadDeSalidaId = null,
    int? NumeroAsiento = null,
    string? LugarEmbarque = null,
    string? NombrePasajero = null,
    TipoDocumento? TipoDocumento = null,
    string? NumeroDocumento = null,
    string? Celular = null,
    bool? EsMenor = null,
    string? NombrePadreMadre = null,
    string? MotivoMenor = null,
    decimal? MontoAdicional = null,
    MedioPago? MedioDePago = null);
