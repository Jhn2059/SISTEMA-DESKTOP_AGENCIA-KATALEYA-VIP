using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kataleya.Contratos;
using Kataleya.Contratos.Bitacora;
using Kataleya.Contratos.Configuracion;
using Kataleya.Contratos.Cuadre;
using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Session;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Cliente;

public sealed class ClienteApi
{
    private static readonly JsonSerializerOptions Formato = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const int ReintentosPredeterminados = 3;
    private static readonly TimeSpan EsperaEntreReintentos = TimeSpan.FromMilliseconds(250);

    private readonly HttpClient _http;
    private readonly IRegistroTecnico _registro;
    private readonly int _reintentosMaximos;
    private readonly Dictionary<string, Guid> _clavesDeOperacion = [];
    private string? _llave;
    private DateTime? _venceLlave;

    public ClienteApi(
        HttpClient http,
        IRegistroTecnico registro,
        int reintentosMaximos = ReintentosPredeterminados,
        bool permitirHttpSinTls = false)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));

        if (reintentosMaximos < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reintentosMaximos),
                "El número de reintentos no puede ser negativo.");
        }

        _reintentosMaximos = reintentosMaximos;

        var direccion = _http.BaseAddress;
        if (direccion is not null &&
            !direccion.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !direccion.IsLoopback &&
            !permitirHttpSinTls)
        {
            throw new InvalidOperationException(
                "La Web API debe usar HTTPS. Configure KATALEYA_API con https:// o, solo para desarrollo, KATALEYA_PERMITIR_HTTP=true.");
        }
    }

    public DatosDeCuenta? CuentaActual { get; private set; }

    public bool SesionActiva =>
        _llave is not null &&
        _venceLlave is not null &&
        _venceLlave > DateTime.UtcNow &&
        CuentaActual is not null;

    public bool EsAdministrador =>
        CuentaActual?.Rol.Equals("Administrador", StringComparison.OrdinalIgnoreCase) == true;

    public async Task<ResultadoDeInicioDeSesion> IniciarSesionAsync(
        string nombreUsuario,
        string contrasena)
    {
        try
        {
            using var respuesta = await EnviarConReintentosAsync(
                () => CrearSolicitud(
                    HttpMethod.Post,
                    "sesiones",
                    new SolicitudDeInicioDeSesion
                    {
                        NombreUsuario = nombreUsuario,
                        Contrasena = contrasena
                    },
                    null));

            if (!respuesta.IsSuccessStatusCode)
            {
                await RegistrarAsync(
                    "SERVIDOR",
                    "Iniciar sesión",
                    $"El servidor respondió {(int)respuesta.StatusCode}.");
                return Rechazo(MotivoDeRechazo.ServidorNoResponde);
            }

            var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoDeInicioDeSesion>(Formato);

            if (resultado is null)
            {
                await RegistrarAsync(
                    "SERVIDOR",
                    "Iniciar sesión",
                    "La respuesta del servidor llegó vacía.");
                return Rechazo(MotivoDeRechazo.ServidorNoResponde);
            }

            if (resultado.Cuenta is not null && !string.IsNullOrWhiteSpace(resultado.Llave))
            {
                _llave = resultado.Llave;
                _venceLlave = resultado.VenceLlave;
                CuentaActual = resultado.Cuenta;
            }

            return resultado;
        }
        catch (TaskCanceledException)
        {
            await RegistrarAsync(
                "CON-02",
                "Iniciar sesión",
                "El servidor no respondió tras agotar los reintentos.");
            return Rechazo(MotivoDeRechazo.ServidorNoResponde);
        }
        catch (HttpRequestException ex)
            when (ex.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError)
        {
            var sinInternet = !NetworkInterface.GetIsNetworkAvailable();
            await RegistrarAsync(
                sinInternet ? "CON-01" : "CON-02",
                "Iniciar sesión",
                ex.GetType().Name);
            return Rechazo(sinInternet ? MotivoDeRechazo.SinInternet : MotivoDeRechazo.ServidorNoResponde);
        }
        catch (HttpRequestException ex)
        {
            await RegistrarAsync("CON-02", "Iniciar sesión", ex.GetType().Name);
            return Rechazo(MotivoDeRechazo.ServidorNoResponde);
        }
        catch (Exception ex)
            when (ex is JsonException or NotSupportedException)
        {
            await RegistrarAsync("SERVIDOR", "Iniciar sesión", ex.GetType().Name);
            return Rechazo(MotivoDeRechazo.ServidorNoResponde);
        }
    }

    public Task<ResultadoDeLaApi<List<DatosDeCuenta>>> ObtenerCuentasAsync() =>
        EnviarAsync<List<DatosDeCuenta>>(
            HttpMethod.Get,
            "cuentas",
            null,
            "Consultar cuentas");

    public Task<ResultadoDeLaApi<DatosDeCuenta>> CrearCuentaAsync(SolicitudDeCuenta solicitud) =>
        EnviarAsync<DatosDeCuenta>(
            HttpMethod.Post,
            "cuentas",
            solicitud,
            "Crear cuenta",
            idempotente: true);

    public Task<ResultadoDeLaApi> DesactivarAsync(int idCuenta) =>
        EnviarSinDatosAsync(
            HttpMethod.Post,
            $"cuentas/{idCuenta}/desactivacion",
            null,
            "Desactivar cuenta",
            idempotente: true);

    public Task<ResultadoDeLaApi> ReactivarAsync(int idCuenta) =>
        EnviarSinDatosAsync(
            HttpMethod.Post,
            $"cuentas/{idCuenta}/reactivacion",
            null,
            "Reactivar cuenta",
            idempotente: true);

    public Task<ResultadoDeLaApi> DesbloquearAsync(int idCuenta) =>
        EnviarSinDatosAsync(
            HttpMethod.Post,
            $"cuentas/{idCuenta}/desbloqueo",
            null,
            "Desbloquear cuenta",
            idempotente: true);

    public Task<ResultadoDeLaApi> RestablecerContrasenaAsync(
        int idCuenta,
        string contrasenaNueva) =>
        EnviarSinDatosAsync(
            HttpMethod.Put,
            $"cuentas/{idCuenta}/contrasena",
            new SolicitudDeRestablecimientoDeContrasena
            {
                ContrasenaNueva = contrasenaNueva
            },
            "Restablecer contraseña",
            idempotente: true);

    public Task<ResultadoDeLaApi<List<DatosDeRol>>> ObtenerRolesAsync() =>
        EnviarAsync<List<DatosDeRol>>(
            HttpMethod.Get,
            "roles",
            null,
            "Consultar roles");

    public Task<ResultadoDeLaApi<List<DatosDeSede>>> ObtenerSedesAsync() =>
        EnviarAsync<List<DatosDeSede>>(
            HttpMethod.Get,
            "sedes",
            null,
            "Consultar sedes");

    public Task<ResultadoDeLaApi<DatosDeTarifa>> ObtenerTarifaVigenteAsync(
        DateOnly fecha,
        Sentido sentido) =>
        EnviarAsync<DatosDeTarifa>(
            HttpMethod.Get,
            $"tarifas/vigente?fecha={fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&sentido={sentido}",
            null,
            "Consultar tarifa vigente");

    public Task<ResultadoDeLaApi<List<DatosDeTarifa>>> ObtenerTarifasAsync() =>
        EnviarAsync<List<DatosDeTarifa>>(
            HttpMethod.Get,
            "tarifas",
            null,
            "Consultar tarifas");

    public Task<ResultadoDeLaApi> FijarTarifaRegularAsync(
        Sentido sentido,
        decimal importe) =>
        EnviarSinDatosAsync(
            HttpMethod.Put,
            $"tarifas/regular/{sentido}",
            new SolicitudDeTarifaRegular(importe),
            "Fijar tarifa regular",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDeTarifa>> GuardarTarifaAsync(
        SolicitudDeTarifa solicitud) =>
        EnviarAsync<DatosDeTarifa>(
            HttpMethod.Post,
            "tarifas",
            solicitud,
            "Guardar tarifa programada",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDeTarifa>> ActualizarTarifaAsync(
        int idTarifa,
        SolicitudDeTarifa solicitud) =>
        EnviarAsync<DatosDeTarifa>(
            HttpMethod.Put,
            $"tarifas/{idTarifa}",
            solicitud,
            "Editar tarifa programada",
            idempotente: true);

    public Task<ResultadoDeLaApi> QuitarTarifaAsync(int idTarifa) =>
        EnviarSinDatosAsync(
            HttpMethod.Delete,
            $"tarifas/{idTarifa}",
            null,
            "Quitar tarifa programada",
            idempotente: true);

    public Task<ResultadoDeLaApi<List<DatosDeParametro>>> ObtenerParametrosAsync() =>
        EnviarAsync<List<DatosDeParametro>>(
            HttpMethod.Get,
            "parametros",
            null,
            "Consultar parámetros");

    public Task<ResultadoDeLaApi> GuardarParametroAsync(
        string nombre,
        string valor) =>
        EnviarSinDatosAsync(
            HttpMethod.Put,
            $"parametros/{Uri.EscapeDataString(nombre)}",
            new SolicitudDeParametro(valor),
            "Guardar parámetro",
            idempotente: true);

    public Task<ResultadoDeLaApi> AgregarHorarioAsync(TimeOnly hora) =>
        EnviarSinDatosAsync(
            HttpMethod.Post,
            "parametros/horarios",
            new SolicitudDeHorario(hora.ToString("HH:mm", CultureInfo.InvariantCulture)),
            "Agregar horario",
            idempotente: true);

    public Task<ResultadoDeLaApi> QuitarHorarioAsync(TimeOnly hora) =>
        EnviarSinDatosAsync(
            HttpMethod.Delete,
            $"parametros/horarios/{hora.ToString("HH:mm", CultureInfo.InvariantCulture)}",
            null,
            "Quitar horario",
            idempotente: true);

    public Task<ResultadoDeLaApi<int>> ContarPasajesDelHorarioAsync(TimeOnly hora) =>
        EnviarAsync<int>(
            HttpMethod.Get,
            $"pasajes/cantidad?hora={Uri.EscapeDataString(hora.ToString("HH:mm", CultureInfo.InvariantCulture))}",
            null,
            "Consultar cantidad de pasajes");

    public Task<ResultadoDeLaApi<List<DatosDeTipoDeVehiculo>>> ObtenerTiposDeVehiculoAsync() =>
        EnviarAsync<List<DatosDeTipoDeVehiculo>>(
            HttpMethod.Get,
            "tipos-de-vehiculo",
            null,
            "Consultar tipos de vehículo");

    public Task<ResultadoDeLaApi<List<DatosDeUnidad>>> ObtenerAsientosAsync(
        Sentido sentido,
        DateTime fechaHoraSalida) =>
        EnviarAsync<List<DatosDeUnidad>>(
            HttpMethod.Get,
            $"salidas/{sentido}/{FormatearFechaHora(fechaHoraSalida)}/unidades",
            null,
            "Consultar unidades de salida");

    public Task<ResultadoDeLaApi<DatosDeUnidad>> AbrirUnidadAsync(
        Sentido sentido,
        DateTime fechaHoraSalida,
        short tipoVehiculoId) =>
        EnviarAsync<DatosDeUnidad>(
            HttpMethod.Post,
            $"salidas/{sentido}/{FormatearFechaHora(fechaHoraSalida)}/unidades",
            new SolicitudDeUnidadDeSalida(tipoVehiculoId),
            "Abrir unidad de salida",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDePasaje>> RegistrarReservaAsync(
        SolicitudDePasaje solicitud) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            "reservas",
            solicitud,
            "Registrar reserva",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDePasaje>> RegistrarVentaAsync(
        SolicitudDePasaje solicitud) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            "ventas",
            solicitud,
            "Registrar venta",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDeGrupo>> RegistrarGrupoAsync(
        SolicitudDeGrupo solicitud) =>
        EnviarAsync<DatosDeGrupo>(
            HttpMethod.Post,
            "grupos",
            solicitud,
            "Registrar grupo",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDePasaje>> RegistrarDiferidoAsync(
        SolicitudDePasaje solicitud) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            "pasajes-diferidos",
            solicitud,
            "Registrar pasaje diferido",
            idempotente: true);

    public Task<ResultadoDeLaApi<List<DatosDePasaje>>> ConsultarPasajesAsync(
        FiltroDePasajes filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var ruta = new System.Text.StringBuilder("pasajes")
            .Append("?fecha=")
            .Append(filtro.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        if (filtro.Hora is not null)
        {
            ruta.Append("&hora=")
                .Append(filtro.Hora.Value.ToString("HH:mm", CultureInfo.InvariantCulture));
        }

        if (filtro.Sentido is not null)
        {
            ruta.Append("&sentido=").Append(filtro.Sentido.Value);
        }

        if (filtro.Estado is not null)
        {
            ruta.Append("&estado=").Append(filtro.Estado.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.NumeroDocumento))
        {
            ruta.Append("&documento=")
                .Append(Uri.EscapeDataString(filtro.NumeroDocumento.Trim()));
        }

        return EnviarAsync<List<DatosDePasaje>>(
            HttpMethod.Get,
            ruta.ToString(),
            null,
            "Consultar pasajes");
    }

    public Task<ResultadoDeLaApi<DatosDePasaje>> RegistrarCobroDelSaldoAsync(
        long idPasaje,
        SolicitudDeCobro solicitud) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            $"pasajes/{idPasaje}/cobros",
            solicitud,
            "Cobrar saldo del pasaje",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDePasaje>> ObtenerDetalleAsync(long idPasaje) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Get,
            $"pasajes/{idPasaje}",
            null,
            "Consultar pasaje");

    public Task<ResultadoDeLaApi<DatosDePasaje>> ModificarPasajeAsync(
        long idPasaje,
        SolicitudDeModificacionDePasaje solicitud) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Put,
            $"pasajes/{idPasaje}",
            solicitud,
            "Modificar pasaje",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDePasaje>> AnularPasajeAsync(
        long idPasaje,
        SolicitudDeAnulacionDePasaje solicitud) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            $"pasajes/{idPasaje}/anulacion",
            solicitud,
            "Anular pasaje",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDePasaje>> MarcarNoSePresentoAsync(long idPasaje) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            $"pasajes/{idPasaje}/no-se-presento",
            null,
            "Marcar No se presentó",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDeCuadre>> ObtenerCuadreAsync(
        DateOnly desde,
        DateOnly hasta) =>
        EnviarAsync<DatosDeCuadre>(
            HttpMethod.Get,
            $"cuadre?desde={desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&hasta={hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            null,
            "Consultar cuadre de ventas");

    public Task<ResultadoDeLaApi<List<DatosDeRegistroDeBitacora>>> ConsultarBitacoraAsync(
        DateOnly desde,
        DateOnly hasta) =>
        EnviarAsync<List<DatosDeRegistroDeBitacora>>(
            HttpMethod.Get,
            $"bitacora?desde={desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&hasta={hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            null,
            "Consultar bitácora");

    public void CerrarSesion()
    {
        _llave = null;
        _venceLlave = null;
        CuentaActual = null;
        _clavesDeOperacion.Clear();
    }

    private async Task<ResultadoDeLaApi<T>> EnviarAsync<T>(
        HttpMethod metodo,
        string ruta,
        object? contenido,
        string operacion,
        bool idempotente = false)
    {
        var huella = idempotente ? CrearHuella(metodo, ruta, contenido) : null;
        var clave = huella is null ? (Guid?)null : ObtenerClave(huella);

        try
        {
            using var respuesta = await EnviarConReintentosAsync(
                () => CrearSolicitud(metodo, ruta, contenido, clave));

            if (respuesta.IsSuccessStatusCode)
            {
                if (huella is not null)
                {
                    _clavesDeOperacion.Remove(huella);
                }

                var texto = await respuesta.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(texto))
                {
                    return new ResultadoDeLaApi<T> { SolicitudExitosa = true };
                }

                var datos = JsonSerializer.Deserialize<T>(texto, Formato);
                return new ResultadoDeLaApi<T>
                {
                    Datos = datos,
                    SolicitudExitosa = true
                };
            }

            if ((int)respuesta.StatusCode >= 500)
            {
                await RegistrarAsync(
                    "SERVIDOR",
                    operacion,
                    $"El servidor respondió {(int)respuesta.StatusCode}.");
            }

            return await LeerErrorAsync<T>(respuesta);
        }
        catch (TaskCanceledException)
        {
            await RegistrarAsync(
                "CON-02",
                operacion,
                "El servidor no respondió tras agotar los reintentos.");
            return Error<T>("CON-02");
        }
        catch (HttpRequestException ex)
            when (ex.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError)
        {
            var sinInternet = !NetworkInterface.GetIsNetworkAvailable();
            await RegistrarAsync(
                sinInternet ? "CON-01" : "CON-02",
                operacion,
                ex.GetType().Name);
            return Error<T>(sinInternet ? "CON-01" : "CON-02");
        }
        catch (HttpRequestException ex)
        {
            await RegistrarAsync("CON-02", operacion, ex.GetType().Name);
            return Error<T>("CON-02");
        }
        catch (Exception ex)
            when (ex is JsonException or NotSupportedException)
        {
            await RegistrarAsync("SERVIDOR", operacion, ex.GetType().Name);
            return Error<T>("SERVIDOR");
        }
    }

    private async Task<HttpResponseMessage> EnviarConReintentosAsync(
        Func<HttpRequestMessage> crearSolicitud)
    {
        for (var intento = 0; ; intento++)
        {
            try
            {
                using var solicitud = crearSolicitud();
                var respuesta = await _http.SendAsync(solicitud);

                if (intento >= _reintentosMaximos || !EsTransitorio(respuesta.StatusCode))
                {
                    return respuesta;
                }

                respuesta.Dispose();
            }
            catch (Exception ex) when (intento < _reintentosMaximos && EsTransitorio(ex))
            {
            }

            await Task.Delay(EsperaEntreReintentos * (intento + 1));
        }
    }

    private static bool EsTransitorio(HttpStatusCode estado) =>
        estado == HttpStatusCode.RequestTimeout ||
        estado == HttpStatusCode.TooManyRequests ||
        (int)estado >= 500;

    private static bool EsTransitorio(Exception ex) =>
        ex is TaskCanceledException ||
        ex is HttpRequestException
        {
            HttpRequestError:
                HttpRequestError.ConnectionError or
                HttpRequestError.NameResolutionError or
                HttpRequestError.ResponseEnded or
                HttpRequestError.HttpProtocolError
        };

    private async Task<ResultadoDeLaApi> EnviarSinDatosAsync(
        HttpMethod metodo,
        string ruta,
        object? contenido,
        string operacion,
        bool idempotente = false)
    {
        var resultado = await EnviarAsync<object>(
            metodo,
            ruta,
            contenido,
            operacion,
            idempotente);

        return new ResultadoDeLaApi
        {
            CodigoDeError = resultado.CodigoDeError,
            Errores = resultado.Errores,
            SesionExpirada = resultado.SesionExpirada,
            SinPermiso = resultado.SinPermiso,
            SolicitudExitosa = resultado.SolicitudExitosa
        };
    }

    private HttpRequestMessage CrearSolicitud(
        HttpMethod metodo,
        string ruta,
        object? contenido,
        Guid? clave)
    {
        var solicitud = new HttpRequestMessage(metodo, ruta);

        if (_llave is not null)
        {
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llave);
        }

        if (clave is not null)
        {
            solicitud.Headers.Add("Idempotency-Key", clave.Value.ToString());
        }

        if (contenido is not null)
        {
            solicitud.Content = JsonContent.Create(contenido, options: Formato);
        }

        return solicitud;
    }

    private static async Task<ResultadoDeLaApi<T>> LeerErrorAsync<T>(HttpResponseMessage respuesta)
    {
        ProblemaDeApi? problema = null;

        try
        {
            problema = await respuesta.Content.ReadFromJsonAsync<ProblemaDeApi>(Formato);
        }
        catch (Exception ex)
            when (ex is JsonException or NotSupportedException)
        {
        }

        var errores = problema?.Errors?
            .SelectMany(campo => campo.Value.Select(mensaje => new ErrorDeCampo
            {
                Campo = campo.Key,
                Mensaje = mensaje
            }))
            .ToList() ?? [];

        return new ResultadoDeLaApi<T>
        {
            CodigoDeError = problema?.Code,
            Errores = errores,
            SesionExpirada = respuesta.StatusCode == HttpStatusCode.Unauthorized,
            SinPermiso = respuesta.StatusCode == HttpStatusCode.Forbidden
        };
    }

    private Guid ObtenerClave(string huella)
    {
        if (!_clavesDeOperacion.TryGetValue(huella, out var clave))
        {
            clave = Guid.NewGuid();
            _clavesDeOperacion[huella] = clave;
        }

        return clave;
    }

    private static string FormatearFechaHora(DateTime fechaHora) =>
        fechaHora.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    private static string CrearHuella(HttpMethod metodo, string ruta, object? contenido)
    {
        var cuerpo = contenido is null
            ? string.Empty
            : JsonSerializer.Serialize(contenido, Formato);

        return $"{metodo} {ruta} {cuerpo}";
    }

    private static ResultadoDeLaApi<T> Error<T>(string codigo) =>
        new() { CodigoDeError = codigo };

    private static ResultadoDeInicioDeSesion Rechazo(MotivoDeRechazo motivo) =>
        new() { Rechazo = motivo };

    private async Task RegistrarAsync(string codigo, string operacion, string detalle)
    {
        try
        {
            await _registro.RegistrarAsync(new ErrorTecnico
            {
                Codigo = codigo,
                FechaHora = DateTime.UtcNow,
                Operacion = operacion,
                Detalle = detalle
            });
        }
        catch
        {
        }
    }

    private sealed class ProblemaDeApi
    {
        public string? Code { get; set; }

        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
