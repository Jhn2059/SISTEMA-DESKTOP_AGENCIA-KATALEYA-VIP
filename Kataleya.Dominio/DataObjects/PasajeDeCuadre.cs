using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record PasajeDeCuadre(
    long Id,
    long? GrupoId,
    int? NumeroAsiento,
    string NombrePasajero,
    string NumeroDocumento,
    string Origen,
    string Destino,
    DateTime FechaHoraSalida,
    decimal Importe,
    decimal Pagado,
    decimal Saldo,
    EstadoPasaje Estado,
    int EmisorId,
    string Emisor,
    string? CodigoDeGrupo,
    string? MotivoDeAnulacion,
    DateTime? FechaDeAnulacion);
