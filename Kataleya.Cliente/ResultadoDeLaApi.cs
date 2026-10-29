using Kataleya.Dominio.DataObjects;

namespace Kataleya.Cliente;

// La respuesta del cliente de la Web API; el servidor no la usa.
public class ResultadoDeLaApi
{
    public string? CodigoDeError { get; init; }

    public List<ErrorDeCampo> Errores { get; init; } = [];

    public bool SesionExpirada { get; init; }

    public bool SinPermiso { get; init; }

    public bool SolicitudExitosa { get; init; }

    public bool Exito =>
        SolicitudExitosa &&
        !SesionExpirada &&
        !SinPermiso &&
        string.IsNullOrWhiteSpace(CodigoDeError) &&
        Errores.Count == 0;
}

public sealed class ResultadoDeLaApi<T> : ResultadoDeLaApi
{
    public T? Datos { get; init; }
}
