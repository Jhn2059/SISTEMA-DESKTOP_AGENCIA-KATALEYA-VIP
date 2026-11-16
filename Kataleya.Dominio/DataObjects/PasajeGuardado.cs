using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record PasajeGuardado(
    long Id,
    long? UnidadDeSalidaId,
    long? GrupoId,
    int? NumeroAsiento,
    string NombrePasajero,
    TipoDocumento TipoDocumento,
    string NumeroDocumento,
    string? Celular,
    string Origen,
    string Destino,
    string LugarEmbarque,
    DateTime FechaHoraSalida,
    DateTime FechaVenta,
    decimal Importe,
    EstadoPasaje Estado,
    TipoDeRegistroDePasaje TipoRegistro,
    int EmisorId,
    string Emisor,
    bool EsMenor,
    string? NombrePadreMadre,
    string? MotivoMenor,
    bool EsResponsable,
    string? CodigoDeGrupo,
    string? MotivoDeAnulacion = null,
    DateTime? FechaDeAnulacion = null,
    bool EsDiferido = false);
