using System.Globalization;
using System.Text.Json.Serialization;
using Kataleya.Api.Session;
using Kataleya.Aplicacion;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Services;
using Kataleya.Aplicacion.Time;
using Kataleya.Contratos;
using Kataleya.Contratos.Bitacora;
using Kataleya.Contratos.Configuracion;
using Kataleya.Contratos.Cuadre;
using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Session;
using Kataleya.Datos;
using Kataleya.Datos.Repositories;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

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
builder.Services.AddScoped<IRepositorioGrupos, RepositorioGrupos>();
builder.Services.AddScoped<IRepositorioPagos, RepositorioPagos>();
builder.Services.AddScoped<IRepositorioBoletos, RepositorioBoletos>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
builder.Services.AddScoped<ServicioBitacora>();
builder.Services.AddScoped<ServicioSesion>();
builder.Services.AddScoped<ServicioCuentas>();
builder.Services.AddScoped<ServicioTarifas>();
builder.Services.AddScoped<ServicioParametros>();
builder.Services.AddScoped<HidratadorDePasajes>();
builder.Services.AddScoped<ServicioConsultas>();
builder.Services.AddScoped<ServicioAsientos>();
builder.Services.AddScoped<ServicioReservasYVentas>();
builder.Services.AddScoped<ServicioCobros>();
builder.Services.AddScoped<ServicioGestionDePasajes>();
builder.Services.AddScoped<ServicioCuadre>();
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
        else if (excepcion is PostgresException postgres &&
                 postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            estado = StatusCodes.Status409Conflict;
            titulo = "La operación ya fue registrada. Actualice los datos e intente de nuevo.";
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

var soloAdministrador = app.MapGroup("/pasajes")
    .AddEndpointFilter<FiltroDeAdministrador>();

soloAdministrador.MapGet("/cantidad", async (string hora, ServicioConsultas servicio) =>
{
    if (!IntentarLeerHora(hora, out var valor))
    {
        return HoraInvalida();
    }

    var cantidad = await servicio.ContarPasajesDelHorarioAsync(valor);
    return Results.Ok(cantidad);
});

soloAdministrador.MapPut("/{id:long}", async (
    long id,
    SolicitudDeModificacionDePasaje solicitud,
    ServicioGestionDePasajes servicio) =>
{
    var resultado = await servicio.ModificarAsync(id, solicitud);
    var datos = MapearPasaje(resultado.Pasaje);
    datos.RequiereReimpresion = resultado.RequiereReimpresion;
    return Results.Ok(datos);
});

soloAdministrador.MapPost("/{id:long}/anulacion", async (
    long id,
    SolicitudDeAnulacionDePasaje solicitud,
    ServicioGestionDePasajes servicio) =>
{
    var pasaje = await servicio.AnularAsync(id, solicitud);
    return Results.Ok(MapearPasaje(pasaje));
});

soloAdministrador.MapPost("/{id:long}/no-se-presento", async (
    long id,
    ServicioGestionDePasajes servicio) =>
{
    var pasaje = await servicio.MarcarNoSePresentoAsync(id);
    return Results.Ok(MapearPasaje(pasaje));
});

var pasajesDiferidos = app.MapGroup("/pasajes-diferidos")
    .AddEndpointFilter<FiltroDeAdministrador>();

pasajesDiferidos.MapPost("", async (
    SolicitudDePasaje solicitud,
    ServicioReservasYVentas servicio) =>
{
    var pasaje = await servicio.RegistrarDiferidoAsync(solicitud);
    return Results.Ok(MapearPasaje(pasaje));
});

var ventas = app.MapGroup("")
    .AddEndpointFilter<FiltroDeVentas>();

ventas.MapGet("/salidas/{sentido}/{fechaHora}/unidades", async (
    Sentido sentido,
    string fechaHora,
    ServicioAsientos servicio) =>
{
    if (!IntentarLeerFechaHora(fechaHora, out var valor))
    {
        return FechaHoraInvalida();
    }

    var unidades = await servicio.ObtenerAsientosAsync(sentido, valor);
    return Results.Ok(unidades.Select(MapearUnidad));
});

ventas.MapPost("/salidas/{sentido}/{fechaHora}/unidades", async (
    Sentido sentido,
    string fechaHora,
    SolicitudDeUnidadDeSalida solicitud,
    ServicioAsientos servicio) =>
{
    if (!IntentarLeerFechaHora(fechaHora, out var valor))
    {
        return FechaHoraInvalida();
    }

    var unidad = await servicio.AbrirUnidadAsync(sentido, valor, solicitud);
    return Results.Ok(MapearUnidad(unidad));
});

ventas.MapPost("/reservas", async (SolicitudDePasaje solicitud, ServicioReservasYVentas servicio) =>
{
    var pasaje = await servicio.RegistrarReservaAsync(solicitud);
    return Results.Ok(MapearPasaje(pasaje));
});

ventas.MapPost("/ventas", async (SolicitudDePasaje solicitud, ServicioReservasYVentas servicio) =>
{
    var pasaje = await servicio.RegistrarVentaAsync(solicitud);
    return Results.Ok(MapearPasaje(pasaje));
});

ventas.MapPost("/grupos", async (
    SolicitudDeGrupo solicitud,
    ServicioReservasYVentas servicio,
    IRepositorioParametros parametros) =>
{
    var grupo = await servicio.RegistrarGrupoAsync(solicitud);
    var tipos = await parametros.ListarTiposDeVehiculoAsync();
    return Results.Ok(MapearGrupo(grupo, tipos));
});

ventas.MapGet("/pasajes", async (
    string? fecha,
    string? hora,
    Sentido? sentido,
    EstadoPasaje? estado,
    string? documento,
    ServicioConsultas servicio,
    IReloj reloj) =>
{
    var fechaFiltro = ZonaHorariaLima.FechaLocal(reloj.Ahora);

    if (!string.IsNullOrWhiteSpace(fecha) && !DateOnly.TryParse(fecha, out fechaFiltro))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["fecha"] = ["Escriba la fecha en formato aaaa-MM-dd."]
        });
    }

    TimeOnly? horaFiltro = null;
    if (!string.IsNullOrWhiteSpace(hora))
    {
        if (!IntentarLeerHora(hora, out var valor))
        {
            return HoraInvalida();
        }

        horaFiltro = valor;
    }

    var pasajes = await servicio.ConsultarAsync(new FiltroDePasajes(
        fechaFiltro,
        horaFiltro,
        sentido,
        estado,
        documento));

    return Results.Ok(pasajes.Select(MapearPasaje).ToList());
});

ventas.MapGet("/pasajes/{id:long}", async (long id, ServicioConsultas servicio) =>
{
    var pasaje = await servicio.ObtenerDetalleAsync(id);
    if (pasaje is null)
    {
        return NoEncontradaPasaje();
    }

    return Results.Ok(MapearPasaje(pasaje));
});

ventas.MapPost("/pasajes/{id:long}/cobros", async (
    long id,
    SolicitudDeCobro solicitud,
    ServicioCobros servicio) =>
{
    var pasaje = await servicio.RegistrarCobroDelSaldoAsync(id, solicitud);
    return Results.Ok(MapearPasaje(pasaje));
});

var cuadre = app.MapGroup("/cuadre")
    .AddEndpointFilter<FiltroDeCuadre>();

cuadre.MapGet("", async (
    string? desde,
    string? hasta,
    ServicioCuadre servicio) =>
{
    var errores = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(desde))
    {
        errores["desde"] = ["Elija la fecha 'Desde'."];
    }

    if (string.IsNullOrWhiteSpace(hasta))
    {
        errores["hasta"] = ["Elija la fecha 'Hasta'."];
    }

    if (errores.Count > 0)
    {
        return Results.ValidationProblem(errores);
    }

    if (!DateOnly.TryParseExact(
            desde!.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var fechaDesde))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["desde"] = ["Escriba la fecha 'Desde' en formato aaaa-MM-dd."]
        });
    }

    if (!DateOnly.TryParseExact(
            hasta!.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var fechaHasta))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["hasta"] = ["Escriba la fecha 'Hasta' en formato aaaa-MM-dd."]
        });
    }

    var resultado = await servicio.ObtenerAsync(new ConsultaDeCuadre(fechaDesde, fechaHasta));
    return Results.Ok(MapearCuadre(resultado));
});

var bitacora = app.MapGroup("/bitacora")
    .AddEndpointFilter<FiltroDeBitacora>();

bitacora.MapGet("", async (
    string? desde,
    string? hasta,
    ServicioBitacora servicio) =>
{
    var errores = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(desde))
    {
        errores["desde"] = ["Elija la fecha 'Desde'."];
    }

    if (string.IsNullOrWhiteSpace(hasta))
    {
        errores["hasta"] = ["Elija la fecha 'Hasta'."];
    }

    if (errores.Count > 0)
    {
        return Results.ValidationProblem(errores);
    }

    if (!DateOnly.TryParseExact(
            desde!.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var fechaDesde))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["desde"] = ["Escriba la fecha 'Desde' en formato aaaa-MM-dd."]
        });
    }

    if (!DateOnly.TryParseExact(
            hasta!.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var fechaHasta))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["hasta"] = ["Escriba la fecha 'Hasta' en formato aaaa-MM-dd."]
        });
    }

    var resultado = await servicio.ConsultarAsync(
        new ConsultaDeBitacora(fechaDesde, fechaHasta));
    return Results.Ok(resultado.Select(MapearRegistroDeBitacora).ToList());
});

app.Run();

static DatosDeUnidad MapearUnidad(UnidadDeSalidaDisponible unidad)
{
    var ocupados = unidad.AsientosOcupados.ToHashSet();

    return new DatosDeUnidad
    {
        Id = unidad.Id,
        Numero = unidad.Numero,
        Sentido = unidad.Sentido,
        TipoVehiculoId = unidad.TipoVehiculoId,
        TipoVehiculoNombre = unidad.TipoVehiculoNombre,
        CantidadAsientos = unidad.CantidadAsientos,
        FechaHoraSalida = unidad.FechaHoraSalida,
        Estado = unidad.Estado.ToString(),
        AbiertaPor = unidad.AbiertaPor,
        Asientos = Enumerable.Range(1, unidad.CantidadAsientos)
            .Select(numero => new DatosDeAsiento
            {
                Numero = numero,
                Ocupado = ocupados.Contains(numero)
            })
            .ToList()
    };
}

static DatosDePasaje MapearPasaje(Pasaje pasaje) => new()
{
    Id = pasaje.Id,
    UnidadDeSalidaId = pasaje.UnidadDeSalidaId,
    GrupoId = pasaje.GrupoId,
    CodigoDeGrupo = pasaje.CodigoDeGrupo,
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
    Importe = pasaje.Importe,
    Adelanto = pasaje.Pagos
        .Where(pago => pago.Tipo == TipoDePago.Adelanto)
        .Sum(pago => pago.Monto),
    Pagado = pasaje.Pagado,
    Saldo = pasaje.Saldo,
    Estado = pasaje.Estado.ToString(),
    TipoRegistro = pasaje.TipoRegistro.ToString(),
    EsDiferido = pasaje.EsDiferido,
    EsResponsable = pasaje.EsResponsable,
    Emisor = pasaje.Emisor,
    EsMenor = pasaje.EsMenor,
    NombrePadreMadre = pasaje.NombrePadreMadre,
    MotivoMenor = pasaje.MotivoMenor,
    MotivoAnulacion = pasaje.MotivoDeAnulacion,
    FechaAnulacion = pasaje.FechaDeAnulacion,
    Boleto = pasaje.Boleto is null
        ? null
        : new DatosDeBoleto
        {
            Serie = pasaje.Boleto.Numero.Serie,
            Correlativo = pasaje.Boleto.Numero.Correlativo,
            Numero = pasaje.Boleto.Numero.Texto,
            FechaEmision = pasaje.Boleto.FechaEmision,
            EmitidoPor = pasaje.Boleto.EmitidoPor,
            Grupo = pasaje.CodigoDeGrupo
        },
    Pagos = pasaje.Pagos
        .Select(pago => new DatosDePago
        {
            Monto = pago.Monto,
            MedioDePago = pago.MedioDePago,
            Tipo = pago.Tipo.ToString(),
            FechaPago = pago.FechaPago,
            Cobrador = pago.Cobrador
        })
        .ToList()
};

static DatosDeRegistroDeBitacora MapearRegistroDeBitacora(
    RegistroDeBitacora registro) => new()
{
    Id = registro.Id,
    FechaHora = registro.FechaHora,
    UsuarioId = registro.Usuario.Id,
    Usuario = registro.Usuario.Nombre,
    Operacion = registro.Operacion,
    PasajeId = registro.PasajeId,
    ValorAnterior = registro.ValorAnterior,
    ValorNuevo = registro.ValorNuevo,
    ClaveDeOperacion = registro.ClaveDeOperacion
};

static DatosDeCuadre MapearCuadre(Cuadre cuadre) => new()
{
    Desde = cuadre.Desde,
    Hasta = cuadre.Hasta,
    Totales = MapearTotalesDeCuadre(cuadre.Totales),
    Emisores = cuadre.Emisores
        .Select(emisor => new DatosDeCuadrePorEmisor
        {
            EmisorId = emisor.EmisorId,
            Emisor = emisor.Emisor,
            Totales = MapearTotalesDeCuadre(emisor.Totales),
            Movimientos = emisor.Movimientos
                .Select(MapearMovimientoDeCuadre)
                .ToList(),
            PasajesAnulados = emisor.PasajesAnulados
                .Select(MapearPasajeAnuladoDeCuadre)
                .ToList(),
            AdelantosPendientes = emisor.AdelantosPendientes
                .Select(MapearAdelantoPendienteDeCuadre)
                .ToList()
        })
        .ToList()
};

static DatosDeTotalesDeCuadre MapearTotalesDeCuadre(TotalesDeCuadre totales) => new()
{
    CobradoEfectivo = totales.CobradoEfectivo,
    CobradoYape = totales.CobradoYape,
    Devoluciones = totales.Devoluciones,
    TotalNeto = totales.TotalNeto,
    AdelantosRetenidos = totales.AdelantosRetenidos,
    CantidadPasajesAnulados = totales.CantidadPasajesAnulados,
    ImportePasajesAnulados = totales.ImportePasajesAnulados,
    CantidadAdelantosPendientes = totales.CantidadAdelantosPendientes,
    ImporteAdelantosPendientes = totales.ImporteAdelantosPendientes
};

static DatosDeMovimientoDeCuadre MapearMovimientoDeCuadre(
    MovimientoDeCuadre movimiento) => new()
{
    PagoId = movimiento.PagoId,
    PasajeId = movimiento.PasajeId,
    GrupoId = movimiento.GrupoId,
    CodigoDeGrupo = movimiento.CodigoDeGrupo,
    FechaPago = movimiento.FechaPago,
    Monto = movimiento.Monto,
    MedioDePago = movimiento.MedioDePago,
    Tipo = movimiento.Tipo.ToString(),
    Cobrador = movimiento.Cobrador,
    Emisor = movimiento.Emisor,
    NombrePasajero = movimiento.NombrePasajero,
    NumeroDocumento = movimiento.NumeroDocumento,
    NumeroAsiento = movimiento.NumeroAsiento,
    Origen = movimiento.Origen,
    Destino = movimiento.Destino,
    FechaHoraSalida = movimiento.FechaHoraSalida,
    Importe = movimiento.Importe,
    Estado = movimiento.Estado.ToString(),
    MotivoAnulacion = movimiento.MotivoDeAnulacion,
    FechaAnulacion = movimiento.FechaDeAnulacion
};

static DatosDePasajeAnuladoCuadre MapearPasajeAnuladoDeCuadre(
    PasajeDeCuadre pasaje) => new()
{
    PasajeId = pasaje.Id,
    GrupoId = pasaje.GrupoId,
    CodigoDeGrupo = pasaje.CodigoDeGrupo,
    NumeroAsiento = pasaje.NumeroAsiento,
    NombrePasajero = pasaje.NombrePasajero,
    NumeroDocumento = pasaje.NumeroDocumento,
    Origen = pasaje.Origen,
    Destino = pasaje.Destino,
    FechaHoraSalida = pasaje.FechaHoraSalida,
    Importe = pasaje.Importe,
    Pagado = pasaje.Pagado,
    AdelantoRetenido = Math.Max(0, pasaje.Pagado),
    FechaAnulacion = pasaje.FechaDeAnulacion,
    MotivoAnulacion = pasaje.MotivoDeAnulacion
};

static DatosDeAdelantoPendienteCuadre MapearAdelantoPendienteDeCuadre(
    PasajeDeCuadre pasaje) => new()
{
    PasajeId = pasaje.Id,
    GrupoId = pasaje.GrupoId,
    CodigoDeGrupo = pasaje.CodigoDeGrupo,
    NumeroAsiento = pasaje.NumeroAsiento,
    NombrePasajero = pasaje.NombrePasajero,
    NumeroDocumento = pasaje.NumeroDocumento,
    Origen = pasaje.Origen,
    Destino = pasaje.Destino,
    FechaHoraSalida = pasaje.FechaHoraSalida,
    Importe = pasaje.Importe,
    Pagado = pasaje.Pagado,
    Saldo = pasaje.Saldo,
    Estado = pasaje.Estado.ToString()
};

static DatosDeGrupo MapearGrupo(
    Grupo grupo,
    IReadOnlyList<TipoDeVehiculo> tipos) => new()
{
    Id = grupo.Id,
    Codigo = grupo.Codigo,
    Origen = grupo.Origen,
    Destino = grupo.Destino,
    LugarDeRecojo = grupo.LugarDeRecojo,
    FechaHoraSalida = grupo.FechaHoraSalida,
    PrecioAcordado = grupo.PrecioAcordado,
    Pagado = grupo.Pagado,
    Saldo = grupo.Saldo,
    Estado = grupo.Estado.ToString(),
    TelefonoContacto = grupo.TelefonoContacto,
    Responsable = grupo.Responsable,
    Unidades = grupo.Unidades
        .Select(unidad =>
        {
            var tipo = tipos.FirstOrDefault(item => item.Id == unidad.TipoVehiculoId);
            return new DatosDeUnidadDelGrupo
            {
                Id = unidad.Id,
                Numero = unidad.Numero,
                TipoVehiculoId = unidad.TipoVehiculoId,
                TipoVehiculoNombre = tipo?.Nombre ?? string.Empty,
                CantidadAsientos = tipo?.CantidadDeAsientos ?? 0,
                FechaHoraSalida = unidad.FechaHoraSalida,
                Estado = unidad.Estado.ToString(),
                AbiertaPor = unidad.AbiertaPor
            };
        })
        .ToList(),
    Pasajes = grupo.Pasajes.Select(MapearPasaje).ToList(),
    Pagos = grupo.Pagos
        .Select(pago => new DatosDePago
        {
            Monto = pago.Monto,
            MedioDePago = pago.MedioDePago,
            Tipo = pago.Tipo.ToString(),
            FechaPago = pago.FechaPago,
            Cobrador = pago.Cobrador
        })
        .ToList()
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

static bool IntentarLeerFechaHora(string? texto, out DateTime valor) =>
    DateTime.TryParseExact(
        texto?.Trim(),
        ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"],
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out valor);

static IResult FechaHoraInvalida() =>
    Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["fechaHora"] =
            ["Escriba la fecha y hora en formato aaaa-MM-ddTHH:mm, por ejemplo 2026-11-20T06:00."]
    });
