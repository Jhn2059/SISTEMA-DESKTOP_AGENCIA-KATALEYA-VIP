using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record SolicitudDeRegistroPasaje(
    long UnidadSalidaId,
    int NumeroAsiento,
    string NombrePasajero,
    TipoDocumento TipoDocumento,
    string NumeroDocumento,
    string Celular,
    string Origen,
    string Destino,
    string LugarEmbarque,
    DateTime FechaHoraSalida,
    decimal Adelanto,
    bool EsMenor,
    string? NombrePadreMadre,
    string? MotivoMenor);