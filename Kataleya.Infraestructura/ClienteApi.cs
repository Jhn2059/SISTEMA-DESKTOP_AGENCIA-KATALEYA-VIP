using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kataleya.Contratos.Session;
using Kataleya.Dominio.Interfaces;
using Kataleya.Dominio.DataObjects;

namespace Kataleya.Infraestructura;

public sealed class ClienteApi
{
    private static readonly JsonSerializerOptions Formato = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly IRegistroTecnico _registro;
    private string? _llave;
    private DateTime? _venceLlave;

    public ClienteApi(HttpClient http, IRegistroTecnico registro)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
    }

    public bool SesionActiva =>
        _llave is not null && _venceLlave is not null && _venceLlave > DateTime.UtcNow;

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
        catch (JsonException ex)
        {
            await RegistrarAsync("SERVIDOR", "Iniciar sesión", ex.GetType().Name);
            return Rechazo(MotivoDeRechazo.ServidorNoResponde);
        }
    }

    public void CerrarSesion()
    {
        _llave = null;
        _venceLlave = null;
    }

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
}
