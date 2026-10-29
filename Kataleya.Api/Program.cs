using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Kataleya.Api.Copia;
using Kataleya.Api.Session;
using Kataleya.Aplicacion;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Services;
using Kataleya.Aplicacion.Time;
using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Session;
using Kataleya.Datos;
using Kataleya.Datos.Repositories;
using Kataleya.Dominio.DataObjects;
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

var opcionesDeCopia = builder.Configuration
    .GetSection(OpcionesDeCopia.Seccion)
    .Get<OpcionesDeCopia>() ?? new OpcionesDeCopia();

var opcionesDeGoogleDrive = builder.Configuration
    .GetSection(OpcionesDeGoogleDrive.Seccion)
    .Get<OpcionesDeGoogleDrive>() ?? new OpcionesDeGoogleDrive();

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
builder.Services.AddScoped<IRepositorioPasajes, RepositorioPasajes>();
builder.Services.AddScoped<IRepositorioUnidades, RepositorioUnidades>();
builder.Services.AddScoped<IRepositorioConfiguracion, RepositorioConfiguracion>();
builder.Services.AddScoped<IRepositorioCuadre, RepositorioCuadre>();
builder.Services.AddScoped<IRepositorioReportesAnuales, RepositorioReportesAnuales>();
builder.Services.AddScoped<IRepositorioCopias, RepositorioCopias>();
builder.Services.AddScoped<IRepositorioPreferencias, RepositorioPreferencias>();
builder.Services.AddScoped<ICopiaDeLaBase>(servicios => new CopiaDeLaBase(
    servicios.GetRequiredService<FuenteDeDatos>(),
    opcionesDeCopia.ClaveDeCifrado));
builder.Services.AddHttpClient<IAlmacenDeCopias, AlmacenEnGoogleDrive>((http, _) =>
    new AlmacenEnGoogleDrive(http, opcionesDeGoogleDrive));
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
builder.Services.AddScoped<ServicioBitacora>();
builder.Services.AddScoped<RegistradorDeBitacora>();
builder.Services.AddScoped<ServicioSesion>();
builder.Services.AddScoped<ServicioCuentas>();
builder.Services.AddScoped<ServicioCatalogos>();
builder.Services.AddScoped<ServicioConfiguracion>();
builder.Services.AddScoped<ServicioInventario>();
builder.Services.AddScoped<ServicioPasajes>();
builder.Services.AddScoped<ServicioReportes>();
builder.Services.AddScoped<ServicioReporteAnual>();
builder.Services.AddScoped<ServicioCopiaDeDatos>();
builder.Services.AddSingleton<EjecutorDeCopias>();
builder.Services.AddHostedService(servicios => servicios.GetRequiredService<EjecutorDeCopias>());
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

// El tema lo elige cada cuenta para sí misma, también el Encargado de ventas (CDU-20).
app.MapPut("/cuentas/{id:int}/tema", async (
    int id,
    SolicitudDeTema solicitud,
    ContextoDeOperacion contexto,
    ServicioCuentas servicio) =>
{
    if (id != contexto.Usuario.Id)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "No tiene permiso para usar esta función.");
    }

    await servicio.ElegirTemaAsync(solicitud.Tema);
    return Results.NoContent();
});

var roles = app.MapGroup("/roles")
    .AddEndpointFilter<FiltroDeAdministrador>();

roles.MapGet("", async (ServicioCatalogos catalogos) =>
{
    var lista = await catalogos.ListarRolesAsync();
    return Results.Ok(lista.Select(MapearRol).ToList());
});

var sedes = app.MapGroup("/sedes")
    .AddEndpointFilter<FiltroDeAdministrador>();

sedes.MapGet("", async (ServicioCatalogos catalogos) =>
{
    var lista = await catalogos.ListarSedesAsync();
    return Results.Ok(lista.Select(MapearSede).ToList());
});

var pasajes = app.MapGroup("/pasajes");

pasajes.MapPost("/reservas", async (
    Kataleya.Contratos.Pasajes.SolicitudDePasaje solicitud,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.RegistrarReservaAsync(solicitud)));

pasajes.MapPost("/ventas", async (
    Kataleya.Contratos.Pasajes.SolicitudDePasaje solicitud,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.RegistrarVentaAsync(solicitud)));

pasajes.MapPost("/diferidos", async (
    Kataleya.Contratos.Pasajes.SolicitudDePasaje solicitud,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.RegistrarDiferidoAsync(solicitud)))
    .AddEndpointFilter<FiltroDeAdministrador>();

pasajes.MapPost("/grupos/reservas", async (
    Kataleya.Contratos.Pasajes.SolicitudDeGrupo solicitud,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.RegistrarGrupoAsync(solicitud, esVenta: false)));

pasajes.MapPost("/grupos/ventas", async (
    Kataleya.Contratos.Pasajes.SolicitudDeGrupo solicitud,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.RegistrarGrupoAsync(solicitud, esVenta: true)));

pasajes.MapGet("", async (
    [AsParameters] Kataleya.Contratos.Pasajes.FiltroDePasajes filtro,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.ConsultarAsync(filtro)));

pasajes.MapGet("/cantidad", async (TimeOnly hora, ServicioPasajes servicio) =>
    Results.Ok(await servicio.ContarPasajesDelHorarioAsync(hora)))
    .AddEndpointFilter<FiltroDeAdministrador>();

pasajes.MapGet("/grupos/{id:long}", async (long id, ServicioPasajes servicio) =>
    Results.Ok(await servicio.ObtenerGrupoAsync(id)));

pasajes.MapGet("/{id:long}", async (long id, ServicioPasajes servicio) =>
{
    var pasaje = await servicio.BuscarAsync(id);
    return pasaje is null ? NoEncontrado("El pasaje no existe.") : Results.Ok(pasaje);
});

pasajes.MapGet("/{id:long}/revision-de-anulacion", async (
    long id,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.RevisarAnulacionAsync(id)));

pasajes.MapPost("/{id:long}/anulacion", async (
    long id,
    Kataleya.Contratos.Pasajes.SolicitudDeAnulacion solicitud,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.AnularAsync(id, solicitud)))
    .AddEndpointFilter<FiltroDeAdministrador>();

pasajes.MapPost("/{id:long}/cobros", async (
    long id,
    Kataleya.Contratos.Pasajes.SolicitudDeCobro solicitud,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.RegistrarCobroAsync(id, solicitud)));

pasajes.MapPost("/{id:long}/modificacion", async (
    long id,
    Kataleya.Contratos.Pasajes.CambiosDelPasaje cambios,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.ModificarAsync(id, cambios)))
    .AddEndpointFilter<FiltroDeAdministrador>();

pasajes.MapPost("/{id:long}/no-se-presento", async (
    long id,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.MarcarNoSePresentoAsync(id)))
    .AddEndpointFilter<FiltroDeAdministrador>();

app.MapPost("/boletos/{serie}/{numero:long}/reimpresiones", async (
    string serie,
    long numero,
    ServicioPasajes servicio) =>
    Results.Ok(await servicio.RegistrarReimpresionAsync(serie, numero)));

app.MapPost("/boletos/{serie}/{numero:long}/fallas", async (
    string serie,
    long numero,
    Kataleya.Contratos.Pasajes.SolicitudDeFallaDeBoleto solicitud,
    ServicioPasajes servicio) =>
{
    await servicio.RegistrarFallaDeBoletoAsync(serie, numero, solicitud.Descripcion);
    return Results.NoContent();
});

var inventario = app.MapGroup("/inventario");

inventario.MapGet("/unidades", async (
    DateOnly fecha,
    TimeOnly hora,
    Kataleya.Dominio.Enums.SentidoDelViaje sentido,
    ServicioInventario servicio) =>
    Results.Ok(await servicio.ListarAsync(fecha, hora, sentido)));

inventario.MapPost("/unidades", async (
    Kataleya.Contratos.Pasajes.SolicitudDeUnidad solicitud,
    ServicioInventario servicio) =>
    Results.Ok(await servicio.AbrirAsync(solicitud)));

inventario.MapGet("/unidades/{id:long}/asientos", async (
    long id,
    ServicioInventario servicio) =>
    Results.Ok(await servicio.AsientosOcupadosAsync(id)));

var configuracion = app.MapGroup("/configuracion");
var configuracionAdministrativa = configuracion.MapGroup("")
    .AddEndpointFilter<FiltroDeAdministrador>();

configuracion.MapGet("/tarifas", async (ServicioConfiguracion servicio) =>
    Results.Ok((await servicio.ListarTarifasAsync())
        .Select(t => Kataleya.Aplicacion.Mapeo.Mapeador.ADatos(t))
        .ToList()));

configuracion.MapGet("/tarifas/vigente", async (
    DateOnly? fecha,
    Kataleya.Dominio.Enums.SentidoDelViaje? sentido,
    IReloj reloj,
    ServicioConfiguracion servicio) =>
{
    var dia = fecha ?? FechasLocales.Hoy(reloj.Ahora);
    var tarifa = await servicio.ObtenerTarifaVigenteAsync(
        dia,
        sentido ?? Kataleya.Dominio.Enums.SentidoDelViaje.Ida);
    return Results.Ok(Kataleya.Aplicacion.Mapeo.Mapeador.ADatos(tarifa));
});

configuracionAdministrativa.MapPost("/tarifas", async (
    Kataleya.Contratos.Configuracion.SolicitudDeTarifa solicitud,
    ServicioConfiguracion servicio) =>
    Results.Ok(Kataleya.Aplicacion.Mapeo.Mapeador.ADatos(
        await servicio.GuardarTarifaAsync(null, solicitud))));

configuracionAdministrativa.MapPut("/tarifas/{id:int}", async (
    int id,
    Kataleya.Contratos.Configuracion.SolicitudDeTarifa solicitud,
    ServicioConfiguracion servicio) =>
    Results.Ok(Kataleya.Aplicacion.Mapeo.Mapeador.ADatos(
        await servicio.GuardarTarifaAsync(id, solicitud))));

configuracionAdministrativa.MapPut("/tarifas/regular/{sentido}", async (
    Kataleya.Dominio.Enums.SentidoDelViaje sentido,
    Kataleya.Contratos.Configuracion.SolicitudDeTarifaRegular solicitud,
    ServicioConfiguracion servicio) =>
    Results.Ok(Kataleya.Aplicacion.Mapeo.Mapeador.ADatos(
        await servicio.FijarTarifaRegularAsync(sentido, solicitud.Importe))));

configuracionAdministrativa.MapDelete("/tarifas/{id:int}", async (
    int id,
    ServicioConfiguracion servicio) =>
{
    await servicio.EliminarTarifaAsync(id);
    return Results.NoContent();
});

configuracion.MapGet("/tipos-de-vehiculo", async (ServicioConfiguracion servicio) =>
    Results.Ok((await servicio.ListarTiposDeVehiculoAsync())
        .Select(t => Kataleya.Aplicacion.Mapeo.Mapeador.ADatos(t))
        .ToList()));

configuracion.MapGet("/parametros", async (ServicioConfiguracion servicio) =>
    Results.Ok((await servicio.ListarParametrosAsync())
        .Select(p => Kataleya.Aplicacion.Mapeo.Mapeador.ADatos(p))
        .ToList()));

configuracionAdministrativa.MapPut("/parametros/{nombre}", async (
    string nombre,
    Kataleya.Contratos.Configuracion.SolicitudDeParametro solicitud,
    ServicioConfiguracion servicio) =>
    Results.Ok(Kataleya.Aplicacion.Mapeo.Mapeador.ADatos(
        await servicio.GuardarParametroAsync(nombre, solicitud))));

configuracionAdministrativa.MapPost("/parametros/horarios", async (
    Kataleya.Contratos.Configuracion.SolicitudDeHorario solicitud,
    ServicioConfiguracion servicio) =>
    Results.Ok(Kataleya.Aplicacion.Mapeo.Mapeador.ADatos(
        await servicio.AgregarHorarioAsync(solicitud.Hora))));

configuracionAdministrativa.MapDelete("/parametros/horarios/{hora}", async (
    string hora,
    ServicioConfiguracion servicio) =>
    Results.Ok(Kataleya.Aplicacion.Mapeo.Mapeador.ADatos(
        await servicio.QuitarHorarioAsync(hora))));

app.MapGet("/cuadre", async (
    [AsParameters] Kataleya.Contratos.Cuadre.FiltroDeCuadre filtro,
    ServicioReportes servicio) =>
    Results.Ok(await servicio.GenerarCuadreAsync(filtro)));

app.MapGet("/panel", async (
    DateOnly? fecha,
    ServicioReportes servicio) =>
    Results.Ok(await servicio.ObtenerPanelAsync(fecha)));

app.MapGet("/bitacora", async (
    [AsParameters] Kataleya.Contratos.Bitacora.FiltroDeBitacora filtro,
    ServicioReportes servicio) =>
    Results.Ok(await servicio.ConsultarBitacoraAsync(filtro)))
    .AddEndpointFilter<FiltroDeAdministrador>();

// La llama el programador de tareas de Supabase con su clave, sin sesión (CDU-22).
app.MapPost("/copia-de-datos/semanal", (HttpContext contexto, EjecutorDeCopias ejecutor) =>
{
    var clave = contexto.Request.Headers["X-Clave-Programador"].ToString();

    if (string.IsNullOrEmpty(opcionesDeCopia.ClaveDelProgramador) ||
        !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(clave),
            Encoding.UTF8.GetBytes(opcionesDeCopia.ClaveDelProgramador)))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "La clave del programador de tareas no es válida.");
    }

    ejecutor.Pedir();
    return Results.Accepted();
});

app.MapGet("/copia-de-datos/estado", async (ServicioCopiaDeDatos servicio) =>
    Results.Ok(await servicio.ObtenerEstadoAsync()))
    .AddEndpointFilter<FiltroDeAdministrador>();

var reportesAnuales = app.MapGroup("/reportes-anuales")
    .AddEndpointFilter<FiltroDeAdministrador>();

reportesAnuales.MapGet("/pendiente", async (ServicioReporteAnual servicio) =>
    Results.Ok(await servicio.HayReportePendienteAsync()));

reportesAnuales.MapPost("/{anio:int}", async (
    int anio,
    Kataleya.Contratos.Reportes.SolicitudDeReporteAnual solicitud,
    ServicioReporteAnual servicio) =>
    Results.Ok(await servicio.GenerarAsync(anio, solicitud.ContrasenaPdf)));

reportesAnuales.MapPost("/{anio:int}/depuracion", async (
    int anio,
    Kataleya.Contratos.Reportes.SolicitudDeDepuracion solicitud,
    ServicioReporteAnual servicio) =>
    Results.Ok(await servicio.DepurarAsync(anio, solicitud.RutaPdf)));

app.Run();

static Dictionary<string, string[]> ValidarCuenta(SolicitudDeCuenta solicitud)
{
    var errores = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(solicitud.NombrePersona))
    {
        errores["nombrePersona"] = ["Escriba el nombre."];
    }
    else if (solicitud.NombrePersona.Trim().Length > Usuario.LongitudMaximaDelNombre)
    {
        errores["nombrePersona"] =
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

static IResult NoEncontrado(string mensaje) =>
    Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: mensaje);

static DatosDeCuenta MapearCuenta(Usuario usuario) =>
    new()
    {
        Id = usuario.Id,
        NombrePersona = usuario.NombrePersona,
        NombreUsuario = usuario.NombreUsuario,
        Rol = usuario.Rol.Nombre,
        Sede = usuario.Sede.Nombre,
        Estado = usuario.Estado,
        FinBloqueo = usuario.FinBloqueo,
        PuedeDesactivarse = usuario.PuedeDesactivarse()
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
