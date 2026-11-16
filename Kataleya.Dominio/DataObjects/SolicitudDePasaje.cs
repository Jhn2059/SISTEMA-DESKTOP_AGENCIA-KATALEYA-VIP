using Kataleya.Dominio.Enums;
using Kataleya.Dominio.ValueObjects;

namespace Kataleya.Dominio.DataObjects;

public sealed record SolicitudDePasaje(
    long UnidadDeSalidaId,
    int NumeroAsiento,
    string NombrePasajero,
    TipoDocumento TipoDocumento,
    string NumeroDocumento,
    string Celular,
    string Origen,
    string Destino,
    string LugarEmbarque,
    bool EsMenor,
    string? NombrePadreMadre,
    string? MotivoMenor,
    decimal? MontoDelPago,
    MedioPago? MedioDePago,
    NumeroDeBoleto? Talonario = null,
    DateTime? FechaHoraEmisionTalonario = null);
