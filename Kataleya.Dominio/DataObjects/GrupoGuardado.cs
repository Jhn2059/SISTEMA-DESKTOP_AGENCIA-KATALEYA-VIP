namespace Kataleya.Dominio.DataObjects;

public sealed record GrupoGuardado(
    long Id,
    string Codigo,
    string TelefonoContacto,
    decimal PrecioAcordado,
    string Origen,
    string Destino,
    string LugarDeRecojo,
    DateTime FechaHoraSalida,
    DateTime FechaRegistro,
    int CreadoPorId,
    string CreadoPor,
    Guid? ClaveDeOperacion);
