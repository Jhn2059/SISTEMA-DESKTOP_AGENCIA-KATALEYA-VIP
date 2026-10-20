using System.Text.Json.Serialization;
using Kataleya.Api.Session;
using Kataleya.Aplicacion;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Services;
using Kataleya.Aplicacion.Time;
using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Session;
using Kataleya.Datos;
using Kataleya.Datos.Repositories;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;
using Microsoft.AspNetCore.Diagnostics;

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
builder.Services.AddSingleton<IReloj, RelojDelSistema>();
builder.Services.AddSingleton(new FuenteDeDatos(cadenaDeConexion));
builder.Services.AddScoped<ContextoDeBaseDeDatos>();
builder.Services.AddScoped<ContextoDeOperacion>();
builder.Services.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
builder.Services.AddScoped<IRepositorioCatalogos, RepositorioCatalogos>();
builder.Services.AddScoped<IRepositorioBitacora, RepositorioBitacora>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
builder.Services.AddScoped<ServicioBitacora>();
builder.Services.AddScoped<ServicioSesion>();
builder.Services.AddScoped<ServicioCuentas>();
builder.Services.AddOptions<OpcionesDeSesion>()
    .Bind(builder.Configuration.GetSection(OpcionesDeSesion.Seccion));
builder.Services.AddSingleton<ServicioDeLlaves>();

var app = builder.Build();

app.UseExceptionHandler(manejador =>
{
    manejador.Run(async contexto =>
    {
        var excepcion = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;
        var estado = StatusCodes.Status500InternalServerError;
        var titulo = "Ocurrió un error inesperado y no se guardó nada.";
        var errores = default(Dictionary<string, string[]>);
        var codigo = default(string);

        if (excepcion is ReglaDeNegocioException regla)
        {
            estado = StatusCodes.Status400BadRequest;
            titulo = regla.Message;
            codigo = regla.Codigo;

            if (!string.IsNullOrWhiteSpace(regla.Campo))
            {
                errores = new Dictionary<string, string[]>
                {
                    [regla.Campo] = [regla.Message]
                };
            }
        }
        else if (excepcion is BadHttpRequestException solicitud)
        {
            estado = solicitud.StatusCode;
            titulo = "La solicitud no es válida.";
        }

        contexto.Response.StatusCode = estado;

        var extensiones = new Dictionary<string, object?>();
        if (codigo is not null)
        {
            extensiones["code"] = codigo;
        }

        if (errores is not null)
        {
            extensiones["errors"] = errores;
        }

        await Results.Problem(statusCode: estado, title: titulo, extensions: extensiones)
            .ExecuteAsync(contexto);
    });
});

app.UseMiddleware<MiddlewareDeSesion>();

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

var cuentas = app.MapGroup("/cuentas")
    .AddEndpointFilter<FiltroDeAdministrador>();

cuentas.MapGet("", async (ServicioCuentas servicio) =>
{
    var lista = await servicio.ListarAsync();
    return Results.Ok(lista.Select(MapearCuenta).ToList());
});

cuentas.MapPost("", async (SolicitudDeCuenta solicitud, ServicioCuentas servicio) =>
{
    var errores = ValidarCuenta(solicitud);
    if (errores.Count > 0)
    {
        return Results.ValidationProblem(errores);
    }

    var usuario = await servicio.CrearCuentaAsync(solicitud);
    return Results.Ok(MapearCuenta(usuario));
});

cuentas.MapPost("/{id:int}/desactivacion", async (int id, ServicioCuentas servicio) =>
{
    var usuario = await servicio.ObtenerAsync(id);
    if (usuario is null)
    {
        return NoEncontrada();
    }

    await servicio.DesactivarAsync(usuario);
    return Results.NoContent();
});

cuentas.MapPost("/{id:int}/reactivacion", async (int id, ServicioCuentas servicio) =>
{
    var usuario = await servicio.ObtenerAsync(id);
    if (usuario is null)
    {
        return NoEncontrada();
    }

    await servicio.ReactivarAsync(usuario);
    return Results.NoContent();
});

cuentas.MapPost("/{id:int}/desbloqueo", async (int id, ServicioCuentas servicio) =>
{
    var usuario = await servicio.ObtenerAsync(id);
    if (usuario is null)
    {
        return NoEncontrada();
    }

    await servicio.DesbloquearAsync(usuario);
    return Results.NoContent();
});

cuentas.MapPut("/{id:int}/contrasena", async (
    int id,
    SolicitudDeRestablecimientoDeContrasena solicitud,
    ServicioCuentas servicio) =>
{
    if (string.IsNullOrWhiteSpace(solicitud.ContrasenaNueva) ||
        solicitud.ContrasenaNueva.Length < Usuario.LongitudMinimaDeContrasena)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["contrasenaNueva"] =
                [$"La contraseña debe tener al menos {Usuario.LongitudMinimaDeContrasena} caracteres."]
        });
    }

    var usuario = await servicio.ObtenerAsync(id);
    if (usuario is null)
    {
        return NoEncontrada();
    }

    await servicio.RestablecerContrasenaAsync(usuario, solicitud.ContrasenaNueva);
    return Results.NoContent();
});

var roles = app.MapGroup("/roles")
    .AddEndpointFilter<FiltroDeAdministrador>();

roles.MapGet("", async (IRepositorioCatalogos catalogos) =>
{
    var lista = await catalogos.ListarRolesAsync();
    return Results.Ok(lista.Select(MapearRol).ToList());
});

var sedes = app.MapGroup("/sedes")
    .AddEndpointFilter<FiltroDeAdministrador>();

sedes.MapGet("", async (IRepositorioCatalogos catalogos) =>
{
    var lista = await catalogos.ListarSedesAsync();
    return Results.Ok(lista.Select(MapearSede).ToList());
});

app.Run();

static Dictionary<string, string[]> ValidarCuenta(SolicitudDeCuenta solicitud)
{
    var errores = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(solicitud.Nombre))
    {
        errores["nombre"] = ["Escriba el nombre."];
    }
    else if (solicitud.Nombre.Trim().Length > Usuario.LongitudMaximaDelNombre)
    {
        errores["nombre"] =
            [$"El nombre no puede superar los {Usuario.LongitudMaximaDelNombre} caracteres."];
    }

    if (string.IsNullOrWhiteSpace(solicitud.NombreUsuario))
    {
        errores["nombreUsuario"] = ["Escriba el nombre de usuario."];
    }
    else if (solicitud.NombreUsuario.Trim().Length > Usuario.LongitudMaximaDelNombreUsuario)
    {
        errores["nombreUsuario"] =
            [$"El nombre de usuario no puede superar los {Usuario.LongitudMaximaDelNombreUsuario} caracteres."];
    }

    if (string.IsNullOrWhiteSpace(solicitud.ContrasenaInicial) ||
        solicitud.ContrasenaInicial.Length < Usuario.LongitudMinimaDeContrasena)
    {
        errores["contrasenaInicial"] =
            [$"La contraseña debe tener al menos {Usuario.LongitudMinimaDeContrasena} caracteres."];
    }

    if (solicitud.IdRol <= 0)
    {
        errores["idRol"] = ["Elija un rol."];
    }

    if (solicitud.IdSede <= 0)
    {
        errores["idSede"] = ["Elija una sede."];
    }

    return errores;
}

static IResult NoEncontrada() =>
    Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "No se encontró la cuenta.");

static DatosDeCuenta MapearCuenta(Usuario usuario) =>
    new()
    {
        Id = usuario.Id,
        Nombre = usuario.Nombre,
        NombreUsuario = usuario.NombreUsuario,
        Rol = usuario.Rol.Nombre,
        Sede = usuario.Sede.Nombre,
        Estado = usuario.Estado,
        FinBloqueo = usuario.FinBloqueo
    };

static DatosDeRol MapearRol(Rol rol) =>
    new()
    {
        Id = rol.Id,
        Nombre = rol.Nombre
    };

static DatosDeSede MapearSede(Sede sede) =>
    new()
    {
        Id = sede.Id,
        Nombre = sede.Nombre
    };
