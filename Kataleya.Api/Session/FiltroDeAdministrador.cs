namespace Kataleya.Api.Session;

public sealed class FiltroDeAdministrador : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext contexto,
        EndpointFilterDelegate next)
    {
        if (contexto.HttpContext.Items[MiddlewareDeSesion.ClaveEnItems] is not CargaDeLlave carga ||
            !carga.Rol.Equals("Administrador", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "No tiene permiso para usar esta función.");
        }

        return await next(contexto);
    }
}
