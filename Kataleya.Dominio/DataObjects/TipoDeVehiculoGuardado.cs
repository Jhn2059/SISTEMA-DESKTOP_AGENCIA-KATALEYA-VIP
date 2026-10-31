namespace Kataleya.Dominio.DataObjects;

public sealed record TipoDeVehiculoGuardado(
    short Id,
    string Nombre,
    int CantidadDeAsientos,
    string? Distribucion = null);
