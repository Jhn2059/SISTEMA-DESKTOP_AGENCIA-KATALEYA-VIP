namespace Kataleya.Dominio.DataObjects;

public sealed record ParametroDelNegocioGuardado(
    string Nombre,
    string Valor,
    int IdUsuario,
    DateTime FechaHora);
