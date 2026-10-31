using System.Globalization;
using System.Text.Json.Serialization;
using Kataleya.Api.Session;
using Kataleya.Aplicacion;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Services;
using Kataleya.Aplicacion.Time;
using Kataleya.Contratos;
using Kataleya.Contratos.Configuracion;
using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Session;
using Kataleya.Datos;
using Kataleya.Datos.Repositories;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
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
builder.Services.AddScoped<IRepositorioTarifas, RepositorioTarifas>();
builder.Services.AddScoped<IRepositorioParametros, RepositorioParametros>();
builder.Services.AddScoped<IRepositorioPasajes, RepositorioPasajes>();
builder.Services.AddScoped<IRepositorioUnidadesSalida, RepositorioUnidadesSalida>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
builder.Services.AddScoped<ServicioBitacora>();
builder.Services.AddScoped<ServicioSesion>();
builder.Services.AddScoped<ServicioCuentas>();
builder.Services.AddScoped<ServicioTarifas>();
builder.Services.AddScoped<ServicioParametros>();
builder.Services.AddScoped<ServicioConsultas>();
builder.Services.AddScoped<ServicioPasajes>();
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

app.MapGet("/tarifas/vigente", async (
    DateOnly fecha,
    Sentido sentido,
    ServicioTarifas servicio) =>
{
    var tarifa = await servicio.ObtenerTarifaVigenteAsync(fecha, sentido);
    return Results.Ok(MapearTarifa(tarifa));
});

app.MapGet("/tipos-de-vehiculo", async (ServicioParametros servicio) =>
{
    var tipos = await servicio.ObtenerTiposDeVehiculoAsync();
    return Results.Ok(tipos.Select(MapearTipoDeVehiculo).ToList());
});

var tarifas = app.MapGroup("/tarifas")
    .AddEndpointFilter<FiltroDeAdministrador>();

tarifas.MapGet("", async (ServicioTarifas servicio) =>
{
    var lista = await servicio.ListarTarifasAsync();
    return Results.Ok(lista.Select(MapearTarifa).ToList());
});

tarifas.MapPut("/regular/{sentido}", async (
    Sentido sentido,
    SolicitudDeTarifaRegular solicitud,
    ServicioTarifas servicio) =>
{
    await servicio.FijarTarifaRegularAsync(sentido, solicitud.Importe);
    return Results.NoContent();
});

tarifas.MapPost("", async (SolicitudDeTarifa solicitud, ServicioTarifas servicio) =>
{
    var tarifa = await servicio.GuardarTarifaAsync(solicitud);
    return Results.Ok(MapearTarifa(tarifa));
});

tarifas.MapPut("/{id:int}", async (
    int id,
    SolicitudDeTarifa solicitud,
    ServicioTarifas servicio) =>
{
    var tarifa = await servicio.ActualizarTarifaAsync(id, solicitud);
    return Results.Ok(MapearTarifa(tarifa));
});

tarifas.MapDelete("/{id:int}", async (int id, ServicioTarifas servicio) =>
{
    await servicio.QuitarTarifaAsync(id);
    return Results.NoContent();
});

var parametros = app.MapGroup("/parametros")
    .AddEndpointFilter<FiltroDeAdministrador>();

parametros.MapGet("", async (ServicioParametros servicio) =>
{
    var lista = await servicio.ListarParametrosAsync();
    return Results.Ok(lista.Select(MapearParametro).ToList());
});

parametros.MapPut("/{nombre}", async (
    string nombre,
    SolicitudDeParametro solicitud,
    ServicioParametros servicio) =>
{
    await servicio.GuardarParametroAsync(nombre, solicitud.Valor);
    return Results.NoContent();
});

parametros.MapPost("/horarios", async (
    SolicitudDeHorario solicitud,
    ServicioParametros servicio) =>
{
    if (!IntentarLeerHora(solicitud.Hora, out var hora))
    {
        return HoraInvalida();
    }

    await servicio.AgregarHorarioAsync(hora);
    return Results.NoContent();
});

parametros.MapDelete("/horarios/{hora}", async (
    string hora,
    ServicioParametros servicio) =>
{
    if (!IntentarLeerHora(hora, out var valor))
    {
        return HoraInvalida();
    }

    await servicio.QuitarHorarioAsync(valor);
    return Results.NoContent();
});

var pasajes = app.MapGroup("/pasajes")
    .AddEndpointFilter<FiltroDeAdministrador>();

pasajes.MapGet("/cantidad", async (string hora, ServicioConsultas servicio) =>
{
    if (!IntentarLeerHora(hora, out var valor))
    {
        return HoraInvalida();
    }

    var cantidad = await servicio.ContarPasajesDelHorarioAsync(valor);
    return Results.Ok(cantidad);
});

pasajes.MapGet("/disponibles", async (string fecha, ServicioPasajes servicio) =>
{
    if (!DateOnly.TryParse(fecha, out var valor))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["fecha"] = ["Escriba la fecha en formato aaaa-MM-dd."]
        });
    }

    var unidades = await servicio.ListarUnidadesDisponiblesAsync(valor);
    return Results.Ok(unidades.Select(MapearUnidadSalida));
});

pasajes.MapPost("", async (SolicitudDeRegistroPasaje solicitud, ServicioPasajes servicio) =>
{
    var id = await servicio.RegistrarPasajeAsync(solicitud);
    return Results.Ok(new { id });
});

pasajes.MapGet("/{id:long}", async (long id, ServicioPasajes servicio) =>
{
    var pasaje = await servicio.ObtenerPasajeAsync(id);
    if (pasaje is null)
    {
        return NoEncontradaPasaje();
    }

    return Results.Ok(MapearPasaje(pasaje));
});

app.Run();

static DatosDeUnidadSalida MapearUnidadSalida(UnidadSalidaDisponible unidad) => new()
{
    Id = unidad.Id,
    Sentido = unidad.Sentido,
    TipoVehiculoId = unidad.TipoVehiculoId,
    TipoVehiculoNombre = unidad.TipoVehiculoNombre,
    CantidadAsientos = unidad.CantidadAsientos,
    AsientosOcupados = unidad.AsientosOcupados,
    FechaHoraSalida = unidad.FechaHoraSalida,
    Estado = unidad.Estado,
    VendidaPor = unidad.VendidaPor
};

static DatosDePasaje MapearPasaje(Pasaje pasaje) => new()
{
    Id = pasaje.Id,
    UnidadSalidaId = pasaje.UnidadSalidaId,
    NumeroAsiento = pasaje.NumeroAsiento,
    NombrePasajero = pasaje.NombrePasajero,
    TipoDocumento = pasaje.TipoDocumento,
    NumeroDocumento = pasaje.NumeroDocumento,
    Celular = pasaje.Celular,
    Origen = pasaje.Origen,
    Destino = pasaje.Destino,
    LugarEmbarque = pasaje.LugarEmbarque,
    FechaHoraSalida = pasaje.FechaHoraSalida,
    FechaVenta = pasaje.FechaVenta,
    Adelanto = pasaje.Adelanto,
    Estado = pasaje.Estado,
    VendidaPor = pasaje.VendidaPor,
    EsMenor = pasaje.EsMenor,
    NombrePadreMadre = pasaje.NombrePadreMadre,
    MotivoMenor = pasaje.MotivoMenor
};

static IResult NoEncontradaPasaje() =>
    Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "No se encontró el pasaje.");

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

static DatosDeTarifa MapearTarifa(Tarifa tarifa) => tarifa switch
{
    TarifaRegular regular => new DatosDeTarifa
    {
        Id = regular.Id,
        Nombre = null,
        Importe = regular.Importe,
        Vigencia = null,
        FijadaPor = regular.FijadaPor,
        Sentido = regular.Sentido.ToString()
    },
    TarifaProgramada programada => new DatosDeTarifa
    {
        Id = programada.Id,
        Nombre = programada.Nombre,
        Importe = programada.Importe,
        Vigencia = programada.Vigencia,
        FijadaPor = programada.FijadaPor,
        Sentido = programada.Sentido?.ToString()
    },
    _ => throw new ArgumentOutOfRangeException(nameof(tarifa), "El tipo de tarifa no es válido.")
};

static DatosDeParametro MapearParametro(ParametroDelNegocio parametro) =>
    new()
    {
        Nombre = parametro.Nombre,
        Valor = parametro.Valor
    };

static DatosDeTipoDeVehiculo MapearTipoDeVehiculo(TipoDeVehiculo tipo) =>
    new()
    {
        Nombre = tipo.Nombre,
        CantidadDeAsientos = tipo.CantidadDeAsientos
    };

static bool IntentarLeerHora(string? texto, out TimeOnly hora) =>
    TimeOnly.TryParseExact(
        texto?.Trim(),
        ["H:mm", "HH:mm"],
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out hora);

static IResult HoraInvalida() =>
    Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["hora"] = ["Escriba la hora en formato de 24 horas, por ejemplo 07:30."]
    });
