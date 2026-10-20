using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Session;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Cliente;

public sealed class ClienteApi
{
    private static readonly JsonSerializerOptions Formato = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly IRegistroTecnico _registro;
    private readonly Dictionary<string, Guid> _clavesDeOperacion = [];
    private string? _llave;
    private DateTime? _venceLlave;

    public ClienteApi(HttpClient http, IRegistroTecnico registro)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
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
            using var respuesta = await _http.PostAsJsonAsync(
                "sesiones",
                new SolicitudDeInicioDeSesion
                {
                    NombreUsuario = nombreUsuario,
                    Contrasena = contrasena
                },
                Formato);

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
            await RegistrarAsync("CON-02", "Iniciar sesión", "El servidor no respondió en 10 segundos.");
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
            using var solicitud = CrearSolicitud(metodo, ruta, contenido, clave);
            using var respuesta = await _http.SendAsync(solicitud);

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
            await RegistrarAsync("CON-02", operacion, "El servidor no respondió en 10 segundos.");
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
