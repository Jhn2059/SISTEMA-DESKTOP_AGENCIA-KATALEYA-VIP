using System.Text.Json.Serialization;
using Kataleya.Api.Session;
using Microsoft.AspNetCore.Diagnostics;
using Kataleya.Aplicacion.Services;
using Kataleya.Aplicacion.Time;
using Kataleya.Contratos.Session;
using Kataleya.Datos;
using Kataleya.Datos.Repositories;
using Kataleya.Dominio.Interfaces;

var builder = WebApplication.CreateBuilder(args);

var cadenaDeConexion = builder.Configuration.GetConnectionString("Kataleya");
if (string.IsNullOrWhiteSpace(cadenaDeConexion))
{
    throw new InvalidOperationException("Falta la cadena de conexión 'Kataleya'.");
}

var opcionesDeSesion = builder.Configuration
    .GetSection(OpcionesDeSesion.Seccion)
    .Get<OpcionesDeSesion>() ?? new OpcionesDeSesion();

if (string.IsNullOrWhiteSpace(opcionesDeSesion.ClaveSecreta) || opcionesDeSesion.ClaveSecreta.Length < 32)
{
    throw new InvalidOperationException(
        "Configure 'Sesion:ClaveSecreta' con al menos 32 caracteres.");
}

builder.Services.ConfigureHttpJsonOptions(opciones =>
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddCors();
builder.Services.AddSingleton<IReloj, RelojDelSistema>();
builder.Services.AddSingleton(new FuenteDeDatos(cadenaDeConexion));
builder.Services.AddScoped<ContextoDeBaseDeDatos>();
builder.Services.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
builder.Services.AddScoped<IRepositorioBitacora, RepositorioBitacora>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
builder.Services.AddScoped<ServicioBitacora>();
builder.Services.AddScoped<ServicioSesion>();
builder.Services.AddOptions<OpcionesDeSesion>()
    .Bind(builder.Configuration.GetSection(OpcionesDeSesion.Seccion));
builder.Services.AddSingleton<ServicioDeLlaves>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors(politica => politica
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
}

app.UseExceptionHandler(manejador =>
{
    manejador.Run(async contexto =>
    {
        var excepcion = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;
        var estado = excepcion is BadHttpRequestException solicitud
            ? solicitud.StatusCode
            : StatusCodes.Status500InternalServerError;

        contexto.Response.StatusCode = estado;
        await Results.Problem(statusCode: estado).ExecuteAsync(contexto);
    });
});

app.MapPost("/sesiones", async (
    HttpContext contexto,
    SolicitudDeInicioDeSesion solicitud,
    ServicioSesion servicio,
    ServicioDeLlaves llaves) =>
{
    contexto.Response.Headers.CacheControl = "no-store";

    var errores = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(solicitud.NombreUsuario))
    {
        errores["nombreUsuario"] = ["Escriba su nombre de usuario."];
    }

    if (string.IsNullOrEmpty(solicitud.Contrasena))
    {
        errores["contrasena"] = ["Escriba su contraseña."];
    }

    if (errores.Count > 0)
    {
        return Results.ValidationProblem(errores);
    }

    var resultado = await servicio.IniciarSesionAsync(solicitud.NombreUsuario.Trim(), solicitud.Contrasena);

    if (resultado.Cuenta is not null)
    {
        var (llave, vence) = llaves.Crear(resultado.Cuenta);
        resultado.Llave = llave;
        resultado.VenceLlave = vence;
    }

    return Results.Ok(resultado);
});

app.Run();
