using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record TarifaGuardada(
    int Id,
    TipoDeTarifa Tipo,
    string? Nombre,
    decimal Importe,
    Periodo? Vigencia,
    int IdUsuario,
    string FijadaPor,
    Sentido? Sentido);
