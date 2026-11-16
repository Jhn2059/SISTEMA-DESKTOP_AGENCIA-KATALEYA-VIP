using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record SolicitudDeGrupo(
    string Origen,
    string Destino,
    string LugarDeRecojo,
    DateTime FechaHoraSalida,
    decimal PrecioAcordado,
    string TelefonoContacto,
    IReadOnlyList<short> TiposDeVehiculo,
    IReadOnlyList<IntegranteDeGrupo> Integrantes,
    decimal? MontoDelPago,
    MedioPago? MedioDePago);
