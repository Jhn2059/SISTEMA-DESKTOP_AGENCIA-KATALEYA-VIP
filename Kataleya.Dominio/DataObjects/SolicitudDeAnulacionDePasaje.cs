namespace Kataleya.Dominio.DataObjects;

public sealed record SolicitudDeAnulacionDePasaje(
    string Motivo,
    bool DevolverComoExcepcion = false,
    bool EsCancelacionDeLaAgencia = false);
