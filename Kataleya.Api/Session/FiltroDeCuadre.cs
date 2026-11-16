namespace Kataleya.Api.Session;

public sealed class FiltroDeCuadre : IEndpointFilter
{
    private static readonly string[] RolesPermitidos =
    [
        "Administrador",
        "Encargado de ventas"
    ];

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext contexto,
        EndpointFilterDelegate next)
    {
        if (contexto.HttpContext.Items[MiddlewareDeSesion.ClaveEnItems] is not CargaDeLlave carga ||
            !RolesPermitidos.Contains(carga.Rol, StringComparer.OrdinalIgnoreCase))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "No tiene permiso para usar esta función.");
        }

        return await next(contexto);
    }
}
