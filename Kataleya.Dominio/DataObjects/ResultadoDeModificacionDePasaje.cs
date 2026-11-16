using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.DataObjects;

public sealed record ResultadoDeModificacionDePasaje(
    Pasaje Pasaje,
    bool RequiereReimpresion);
