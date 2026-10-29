using Kataleya.Aplicacion;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Api.Session;

public sealed class MiddlewareDeSesion
{
    public const string ClaveEnItems = "Sesion";

    private readonly RequestDelegate _next;

    public MiddlewareDeSesion(RequestDelegate next) =>
        _next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(
        HttpContext contexto,
        ServicioDeLlaves llaves,
        IRepositorioUsuarios usuarios,
        ContextoDeOperacion operacion)
    {
        if (HttpMethods.IsPost(contexto.Request.Method) &&
            (contexto.Request.Path.Equals("/sesiones", StringComparison.OrdinalIgnoreCase) ||
             contexto.Request.Path.Equals("/copia-de-datos/semanal", StringComparison.OrdinalIgnoreCase)))
        {
            await _next(contexto);
            return;
        }

        var encabezado = contexto.Request.Headers.Authorization.ToString();

        if (!encabezado.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await RechazarAsync(contexto, "La sesión no está iniciada. Inicie sesión de nuevo.");
            return;
        }

        var llave = encabezado["Bearer ".Length..].Trim();

        if (!llaves.TryValidar(llave, out var carga) || carga is null)
        {
            await RechazarAsync(contexto, "La sesión venció. Inicie sesión de nuevo.");
            return;
        }

        var usuario = await usuarios.BuscarPorIdAsync(carga.Sub);

        if (usuario is null || usuario.Estado != EstadoCuenta.Activa)
        {
            await RechazarAsync(contexto, "La cuenta ya no está activa. Inicie sesión de nuevo.");
            return;
        }

        operacion.IdUsuario = usuario.Id;

        if (contexto.Request.Headers.TryGetValue("Idempotency-Key", out var valor) &&
            Guid.TryParse(valor.ToString(), out var clave))
        {
            operacion.ClaveDeOperacion = clave;
        }

        contexto.Items[ClaveEnItems] = carga;

        await _next(contexto);
    }

    private static Task RechazarAsync(HttpContext contexto, string mensaje) =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: mensaje)
            .ExecuteAsync(contexto);
}
