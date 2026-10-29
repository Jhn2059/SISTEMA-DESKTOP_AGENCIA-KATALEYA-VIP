using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using Kataleya.Contratos.Bitacora;
using Kataleya.Contratos.Configuracion;
using Kataleya.Contratos.Cuadre;
using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Panel;
using Kataleya.Contratos.Pasajes;
using Kataleya.Contratos.Reportes;
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
    private readonly CopiaLocal? _copiaLocal;
    private readonly Dictionary<string, Guid> _clavesDeOperacion = [];
    private string? _llave;
    private DateTime? _venceLlave;

    public ClienteApi(
        HttpClient http,
        IRegistroTecnico registro,
        int reintentosMaximos = ReintentosPredeterminados,
        bool permitirHttpSinTls = false,
        CopiaLocal? copiaLocal = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
        _copiaLocal = copiaLocal;

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

    // RNF-16: desde cuándo la Web API no responde; null si hay conexión.
    public DateTime? SinConexionDesde { get; private set; }

    public event EventHandler? ConexionCambiada;

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

    public Task<ResultadoDeLaApi<List<DatosDePasaje>>> ObtenerPasajesAsync(
        FiltroDePasajes? filtro = null) =>
        EnviarAsync<List<DatosDePasaje>>(
            HttpMethod.Get,
            $"pasajes{CrearConsulta(filtro)}",
            null,
            "Consultar pasajes");

    public Task<ResultadoDeLaApi<DatosDePasaje>> ObtenerPasajeAsync(long id) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Get,
            $"pasajes/{id}",
            null,
            "Consultar pasaje");

    public Task<ResultadoDeLaApi<DatosDeGrupo>> ObtenerGrupoAsync(long id) =>
        EnviarAsync<DatosDeGrupo>(
            HttpMethod.Get,
            $"pasajes/grupos/{id}",
            null,
            "Consultar grupo");

    public Task<ResultadoDeLaApi<DatosDePasaje>> RegistrarReservaAsync(
        SolicitudDePasaje solicitud) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            "pasajes/reservas",
            solicitud,
            "Registrar reserva",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDePasaje>> RegistrarVentaAsync(
        SolicitudDePasaje solicitud) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            "pasajes/ventas",
            solicitud,
            "Registrar venta",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDePasaje>> RegistrarDiferidoAsync(
        SolicitudDePasaje solicitud) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            "pasajes/diferidos",
            solicitud,
            "Registrar pasaje diferido",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDeGrupo>> RegistrarGrupoReservaAsync(
        SolicitudDeGrupo solicitud) =>
        EnviarAsync<DatosDeGrupo>(
            HttpMethod.Post,
            "pasajes/grupos/reservas",
            solicitud,
            "Registrar reserva de grupo",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDeGrupo>> RegistrarGrupoVentaAsync(
        SolicitudDeGrupo solicitud) =>
        EnviarAsync<DatosDeGrupo>(
            HttpMethod.Post,
            "pasajes/grupos/ventas",
            solicitud,
            "Registrar venta de grupo",
            idempotente: true);

    public Task<ResultadoDeLaApi<RevisionDeAnulacion>> RevisarAnulacionAsync(long id) =>
        EnviarAsync<RevisionDeAnulacion>(
            HttpMethod.Get,
            $"pasajes/{id}/revision-de-anulacion",
            null,
            "Revisar anulación");

    public Task<ResultadoDeLaApi<DatosDePasaje>> AnularAsync(long id, SolicitudDeAnulacion solicitud) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            $"pasajes/{id}/anulacion",
            solicitud,
            "Anular pasaje",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDePasaje>> RegistrarCobroAsync(
        long id,
        SolicitudDeCobro solicitud) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            $"pasajes/{id}/cobros",
            solicitud,
            "Registrar cobro",
            idempotente: true);

    public Task<ResultadoDeLaApi<ResultadoDeRevision>> ModificarAsync(
        long id,
        CambiosDelPasaje cambios) =>
        EnviarAsync<ResultadoDeRevision>(
            HttpMethod.Post,
            $"pasajes/{id}/modificacion",
            cambios,
            "Modificar pasaje",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDePasaje>> MarcarNoPresentadoAsync(long id) =>
        EnviarAsync<DatosDePasaje>(
            HttpMethod.Post,
            $"pasajes/{id}/no-presentado",
            null,
            "Marcar no presentado",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDeBoleto>> ReimprimirBoletoAsync(
        string serie,
        long numero) =>
        EnviarAsync<DatosDeBoleto>(
            HttpMethod.Post,
            $"boletos/{Uri.EscapeDataString(serie)}/{numero}/reimpresiones",
            null,
            "Reimprimir boleto",
            idempotente: true);

    public Task<ResultadoDeLaApi> RegistrarFallaDeBoletoAsync(
        string serie,
        long numero,
        string descripcion) =>
        EnviarSinDatosAsync(
            HttpMethod.Post,
            $"boletos/{Uri.EscapeDataString(serie)}/{numero}/fallas",
            new SolicitudDeFallaDeBoleto { Descripcion = descripcion },
            "Registrar falla de boleto",
            idempotente: true);

    public Task<ResultadoDeLaApi<List<DatosDeUnidad>>> ObtenerUnidadesAsync(
        DateOnly fecha,
        TimeOnly hora,
        SentidoDelViaje sentido) =>
        EnviarAsync<List<DatosDeUnidad>>(
            HttpMethod.Get,
            $"inventario/unidades{CrearConsulta(new { fecha, hora, sentido })}",
            null,
            "Consultar unidades");

    public Task<ResultadoDeLaApi<DatosDeUnidad>> AbrirUnidadAsync(
        SolicitudDeUnidad solicitud) =>
        EnviarAsync<DatosDeUnidad>(
            HttpMethod.Post,
            "inventario/unidades",
            solicitud,
            "Abrir unidad",
            idempotente: true);

    public Task<ResultadoDeLaApi<List<short>>> ObtenerAsientosOcupadosAsync(long idUnidad) =>
        EnviarAsync<List<short>>(
            HttpMethod.Get,
            $"inventario/unidades/{idUnidad}/asientos",
            null,
            "Consultar asientos ocupados");

    public Task<ResultadoDeLaApi<List<DatosDeTarifa>>> ObtenerTarifasAsync() =>
        EnviarAsync<List<DatosDeTarifa>>(
            HttpMethod.Get,
            "configuracion/tarifas",
            null,
            "Consultar tarifas");

    public Task<ResultadoDeLaApi<DatosDeTarifa>> ObtenerTarifaVigenteAsync(
        DateOnly? fecha = null,
        SentidoDelViaje? sentido = null) =>
        EnviarAsync<DatosDeTarifa>(
            HttpMethod.Get,
            $"configuracion/tarifas/vigente{CrearConsulta(new { fecha, sentido })}",
            null,
            "Consultar tarifa vigente");

    public Task<ResultadoDeLaApi<DatosDeTarifa>> GuardarTarifaAsync(
        int? id,
        SolicitudDeTarifa solicitud) =>
        EnviarAsync<DatosDeTarifa>(
            id is null ? HttpMethod.Post : HttpMethod.Put,
            id is null ? "configuracion/tarifas" : $"configuracion/tarifas/{id}",
            solicitud,
            "Guardar tarifa",
            idempotente: true);

    public Task<ResultadoDeLaApi> EliminarTarifaAsync(int id) =>
        EnviarSinDatosAsync(
            HttpMethod.Delete,
            $"configuracion/tarifas/{id}",
            null,
            "Eliminar tarifa",
            idempotente: true);

    public Task<ResultadoDeLaApi<List<DatosDeTipoDeVehiculo>>> ObtenerTiposDeVehiculoAsync() =>
        EnviarAsync<List<DatosDeTipoDeVehiculo>>(
            HttpMethod.Get,
            "configuracion/tipos-de-vehiculo",
            null,
            "Consultar tipos de vehículo");

    public Task<ResultadoDeLaApi<List<DatosDeParametro>>> ObtenerParametrosAsync() =>
        EnviarAsync<List<DatosDeParametro>>(
            HttpMethod.Get,
            "configuracion/parametros",
            null,
            "Consultar parámetros");

    public Task<ResultadoDeLaApi<DatosDeParametro>> GuardarParametroAsync(
        string nombre,
        string valor) =>
        EnviarAsync<DatosDeParametro>(
            HttpMethod.Put,
            $"configuracion/parametros/{Uri.EscapeDataString(nombre)}",
            new SolicitudDeParametro { Valor = valor },
            "Guardar parámetro",
            idempotente: true);

    public Task<ResultadoDeLaApi<DatosDelCuadre>> ObtenerCuadreAsync(
        FiltroDeCuadre? filtro = null) =>
        EnviarAsync<DatosDelCuadre>(
            HttpMethod.Get,
            $"cuadre{CrearConsulta(filtro)}",
            null,
            "Consultar cuadre");

    public Task<ResultadoDeLaApi<DatosDelPanel>> ObtenerPanelAsync(DateOnly? fecha = null) =>
        EnviarAsync<DatosDelPanel>(
            HttpMethod.Get,
            $"panel{CrearConsulta(new { fecha })}",
            null,
            "Consultar panel");

    public Task<ResultadoDeLaApi<List<DatosDeBitacora>>> ObtenerBitacoraAsync(
        FiltroDeBitacora? filtro = null) =>
        EnviarAsync<List<DatosDeBitacora>>(
            HttpMethod.Get,
            $"bitacora{CrearConsulta(filtro)}",
            null,
            "Consultar bitácora");

    public Task<ResultadoDeLaApi<Kataleya.Dominio.Entities.EstadoDelRespaldo>> ObtenerEstadoDeLaCopiaAsync() =>
        EnviarAsync<Kataleya.Dominio.Entities.EstadoDelRespaldo>(
            HttpMethod.Get,
            "copia-de-datos/estado",
            null,
            "Consultar estado de la copia");

    public Task<ResultadoDeLaApi<bool>> HayReportePendienteAsync() =>
        EnviarAsync<bool>(
            HttpMethod.Get,
            "reportes-anuales/pendiente",
            null,
            "Consultar reporte anual pendiente");

    public Task<ResultadoDeLaApi<ArchivoProtegido>> DescargarReporteAnualAsync(
        int anio,
        string contrasenaPdf) =>
        EnviarAsync<ArchivoProtegido>(
            HttpMethod.Post,
            $"reportes-anuales/{anio}",
            new SolicitudDeReporteAnual { ContrasenaPdf = contrasenaPdf },
            "Generar reporte anual");

    public Task<ResultadoDeLaApi<DatosDelReporteAnual>> PedirDepuracionAsync(
        int anio,
        string rutaPdf) =>
        EnviarAsync<DatosDelReporteAnual>(
            HttpMethod.Post,
            $"reportes-anuales/{anio}/depuracion",
            new SolicitudDeDepuracion { RutaPdf = rutaPdf },
            "Depurar pasajes del reporte anual",
            idempotente: true);

    // Guarda en la laptop los pasajes del día, para consultarlos si se corta internet (RNF-16).
    public async Task ActualizarCopiaLocalAsync()
    {
        if (_copiaLocal is null || _llave is null)
        {
            return;
        }

        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var pasajes = await ObtenerPasajesAsync(new FiltroDePasajes { Desde = hoy, Hasta = hoy });

        if (pasajes.Exito && pasajes.Datos is not null && _llave is not null)
        {
            await _copiaLocal.GuardarAsync(pasajes.Datos, DateTime.Now, _llave);
        }
    }

    public async Task<CopiaDelDia?> LeerCopiaLocalAsync() =>
        _copiaLocal is null || _llave is null
            ? null
            : await _copiaLocal.LeerAsync(_llave);

    public void CerrarSesion()
    {
        _copiaLocal?.Borrar();
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
            MarcarConexion(hayConexion: true);

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
            MarcarConexion(hayConexion: false);
            await RegistrarAsync(
                "CON-02",
                operacion,
                "El servidor no respondió tras agotar los reintentos.");
            return Error<T>("CON-02");
        }
        catch (HttpRequestException ex)
            when (ex.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError)
        {
            MarcarConexion(hayConexion: false);
            var sinInternet = !NetworkInterface.GetIsNetworkAvailable();
            await RegistrarAsync(
                sinInternet ? "CON-01" : "CON-02",
                operacion,
                ex.GetType().Name);
            return Error<T>(sinInternet ? "CON-01" : "CON-02");
        }
        catch (HttpRequestException ex)
        {
            MarcarConexion(hayConexion: false);
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

    private void MarcarConexion(bool hayConexion)
    {
        var anterior = SinConexionDesde;
        SinConexionDesde = hayConexion ? null : anterior ?? DateTime.Now;

        if (anterior != SinConexionDesde)
        {
            ConexionCambiada?.Invoke(this, EventArgs.Empty);
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

    private static string CrearHuella(HttpMethod metodo, string ruta, object? contenido)
    {
        var cuerpo = contenido is null
            ? string.Empty
            : JsonSerializer.Serialize(contenido, Formato);

        return $"{metodo} {ruta} {cuerpo}";
    }

    private static string CrearConsulta(object? filtro)
    {
        if (filtro is null)
        {
            return string.Empty;
        }

        var partes = filtro
            .GetType()
            .GetProperties()
            .Select(propiedad => (propiedad.Name, Valor: propiedad.GetValue(filtro)))
            .Where(campo => campo.Valor is not null)
            .Select(campo =>
                $"{Uri.EscapeDataString(campo.Name)}={Uri.EscapeDataString(FormatearValor(campo.Valor!))}")
            .ToList();

        return partes.Count == 0 ? string.Empty : $"?{string.Join("&", partes)}";
    }

    private static string FormatearValor(object valor) =>
        valor switch
        {
            DateOnly fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly hora => hora.ToString("HH:mm", CultureInfo.InvariantCulture),
            DateTime fechaHora => fechaHora.ToString("O", CultureInfo.InvariantCulture),
            bool booleano => booleano ? "true" : "false",
            IFormattable formateable => formateable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => valor.ToString() ?? string.Empty
        };

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
