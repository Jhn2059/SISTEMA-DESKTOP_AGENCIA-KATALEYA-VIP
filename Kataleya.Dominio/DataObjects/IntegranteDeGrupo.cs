using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record IntegranteDeGrupo(
    string Nombre,
    TipoDocumento TipoDocumento,
    string NumeroDocumento,
    bool EsResponsable);
