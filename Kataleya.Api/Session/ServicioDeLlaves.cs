using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Session;
using Kataleya.Dominio.Interfaces;
using Microsoft.Extensions.Options;

namespace Kataleya.Api.Session;

public sealed class ServicioDeLlaves
{
    private static readonly JsonSerializerOptions Formato = new(JsonSerializerDefaults.Web);

    private readonly byte[] _clave;
    private readonly TimeSpan _validez;
    private readonly IReloj _reloj;

    public ServicioDeLlaves(IOptions<OpcionesDeSesion> opciones, IReloj reloj)
    {
        var configuracion = opciones?.Value ?? throw new ArgumentNullException(nameof(opciones));

        if (string.IsNullOrWhiteSpace(configuracion.ClaveSecreta) || configuracion.ClaveSecreta.Length < 32)
        {
            throw new InvalidOperationException(
                "La clave secreta de la sesión debe tener al menos 32 caracteres.");
        }

        _clave = Encoding.UTF8.GetBytes(configuracion.ClaveSecreta);
        _validez = TimeSpan.FromHours(configuracion.HorasDeValidez > 0 ? configuracion.HorasDeValidez : 12);
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    public (string Llave, DateTime Vence) Crear(DatosDeCuenta cuenta)
    {
        ArgumentNullException.ThrowIfNull(cuenta);

        var vence = _reloj.Ahora.Add(_validez);
        var carga = new CargaDeLlave
        {
            Sub = cuenta.Id,
            NombreUsuario = cuenta.NombreUsuario,
            Rol = cuenta.Rol,
            Sede = cuenta.Sede,
            Exp = new DateTimeOffset(vence).ToUnixTimeSeconds()
        };

        var contenido = JsonSerializer.SerializeToUtf8Bytes(carga, Formato);
        var firma = HMACSHA256.HashData(_clave, contenido);

        return ($"{Codificar(contenido)}.{Codificar(firma)}", vence);
    }

    public bool TryValidar(string llave, out CargaDeLlave? carga)
    {
        carga = null;

        if (string.IsNullOrWhiteSpace(llave))
        {
            return false;
        }

        var partes = llave.Split('.');
        if (partes.Length != 2)
        {
            return false;
        }

        try
        {
            var contenido = Decodificar(partes[0]);
            var firma = Decodificar(partes[1]);
            var esperada = HMACSHA256.HashData(_clave, contenido);

            if (!CryptographicOperations.FixedTimeEquals(firma, esperada))
            {
                return false;
            }

            var datos = JsonSerializer.Deserialize<CargaDeLlave>(contenido, Formato);
            if (datos is null || datos.Exp <= new DateTimeOffset(_reloj.Ahora).ToUnixTimeSeconds())
            {
                return false;
            }

            carga = datos;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Codificar(byte[] datos) =>
        Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decodificar(string texto)
    {
        var base64 = texto.Replace('-', '+').Replace('_', '/');

        base64 = (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            _ => base64
        };

        return Convert.FromBase64String(base64);
    }
}

public sealed class CargaDeLlave
{
    [JsonPropertyName("sub")]
    public int Sub { get; init; }

    [JsonPropertyName("nombreUsuario")]
    public string NombreUsuario { get; init; } = string.Empty;

    [JsonPropertyName("rol")]
    public string Rol { get; init; } = string.Empty;

    [JsonPropertyName("sede")]
    public string Sede { get; init; } = string.Empty;

    [JsonPropertyName("exp")]
    public long Exp { get; init; }
}
