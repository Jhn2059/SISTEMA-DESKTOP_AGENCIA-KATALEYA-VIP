using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record MovimientoDeCuadre(
    long PagoId,
    long PasajeId,
    long? GrupoId,
    DateTime FechaPago,
    decimal Monto,
    MedioPago MedioDePago,
    TipoDePago Tipo,
    int CobradorId,
    string Cobrador,
    int EmisorId,
    string Emisor,
    string NombrePasajero,
    string NumeroDocumento,
    int? NumeroAsiento,
    string Origen,
    string Destino,
    DateTime FechaHoraSalida,
    decimal Importe,
    EstadoPasaje Estado,
    string? CodigoDeGrupo,
    string? MotivoDeAnulacion,
    DateTime? FechaDeAnulacion);
