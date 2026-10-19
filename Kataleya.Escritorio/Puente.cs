using System.Text.Json;
using System.Text.Json.Serialization;
using Kataleya.Dominio.Interfaces;
using Kataleya.Dominio.DataObjects;
using Kataleya.Infraestructura;

namespace Kataleya.Escritorio;

public sealed class Puente
{
    private const string MensajeGenerico = "No se pudo completar la operación. Vuelva a intentarlo.";

    private static readonly JsonSerializerOptions Formato = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ClienteApi _cliente;
    private readonly IRegistroTecnico _registro;

    public Puente(ClienteApi cliente, IRegistroTecnico registro)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
    }

    public async Task<string> ProcesarAsync(string mensajeJson)
    {
        MensajeDelPuente? mensaje = null;

        try
        {
            mensaje = JsonSerializer.Deserialize<MensajeDelPuente>(mensajeJson, Formato);

            if (mensaje is null || string.IsNullOrWhiteSpace(mensaje.Id) ||
                string.IsNullOrWhiteSpace(mensaje.Operacion))
            {
                return RespuestaDeError(mensaje?.Id, MensajeGenerico);
            }

            return mensaje.Operacion switch
            {
                "iniciarSesion" => await IniciarSesionAsync(mensaje),
                "cerrarSesion" => CerrarSesion(mensaje),
                _ => RespuestaDeError(mensaje.Id, MensajeGenerico)
            };
        }
        catch (JsonException)
        {
            await RegistrarAsync("Mensaje del puente con formato inválido.");
            return RespuestaDeError(mensaje?.Id, MensajeGenerico);
        }
        catch (Exception ex)
        {
            await RegistrarAsync(ex.GetType().Name);
            return RespuestaDeError(mensaje?.Id, MensajeGenerico);
        }
    }

    private async Task<string> IniciarSesionAsync(MensajeDelPuente mensaje)
    {
        var datos = mensaje.Datos;

        if (datos is null || string.IsNullOrWhiteSpace(datos.NombreUsuario) ||
            string.IsNullOrEmpty(datos.Contrasena))
        {
            return RespuestaDeError(mensaje.Id, MensajeGenerico);
        }

        var resultado = await _cliente.IniciarSesionAsync(datos.NombreUsuario.Trim(), datos.Contrasena);

        var visible = new
        {
            cuenta = resultado.Cuenta,
            rechazo = resultado.Rechazo,
            finBloqueo = resultado.FinBloqueo
        };

        return JsonSerializer.Serialize(
            new RespuestaDelPuente { Id = mensaje.Id, Resultado = visible },
            Formato);
    }

    private string CerrarSesion(MensajeDelPuente mensaje)
    {
        _cliente.CerrarSesion();
        return JsonSerializer.Serialize(
            new RespuestaDelPuente { Id = mensaje.Id, Resultado = true },
            Formato);
    }

    private static string RespuestaDeError(string? id, string error) =>
        JsonSerializer.Serialize(
            new RespuestaDelPuente { Id = id ?? string.Empty, Error = error },
            Formato);

    private async Task RegistrarAsync(string detalle)
    {
        try
        {
            await _registro.RegistrarAsync(new ErrorTecnico
            {
                Codigo = "GEN-01",
                FechaHora = DateTime.UtcNow,
                Operacion = "Puente",
                Detalle = detalle
            });
        }
        catch
        {
        }
    }

    private sealed class MensajeDelPuente
    {
        public string Id { get; set; } = string.Empty;

        public string Operacion { get; set; } = string.Empty;

        public DatosDeInicioDeSesion? Datos { get; set; }
    }

    private sealed class DatosDeInicioDeSesion
    {
        public string NombreUsuario { get; set; } = string.Empty;

        public string Contrasena { get; set; } = string.Empty;
    }

    private sealed class RespuestaDelPuente
    {
        public string Id { get; set; } = string.Empty;

        public object? Resultado { get; set; }

        public string? Error { get; set; }
    }
}
