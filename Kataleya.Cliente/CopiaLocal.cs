using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kataleya.Contratos.Pasajes;

namespace Kataleya.Cliente;

// RNF-16: copia de solo lectura de los pasajes del día, para consultar durante un corte de internet.
// Se cifra con la llave de la sesión: si la aplicación se cierra, no se puede abrir hasta volver a iniciar sesión.
public sealed class CopiaLocal
{
    private const int LongitudDeNonce = 12;
    private const int LongitudDeEtiqueta = 16;

    private static readonly JsonSerializerOptions Formato = new(JsonSerializerDefaults.Web);

    private readonly string _ruta;

    public CopiaLocal()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kataleya",
            "copia-local.bin"))
    {
    }

    public CopiaLocal(string ruta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        _ruta = ruta;
    }

    public async Task GuardarAsync(IReadOnlyList<DatosDePasaje> pasajes, DateTime fechaHora, string llaveDeSesion)
    {
        ArgumentNullException.ThrowIfNull(pasajes);

        var datos = JsonSerializer.SerializeToUtf8Bytes(new CopiaDelDia(fechaHora, pasajes.ToList()), Formato);
        var nonce = RandomNumberGenerator.GetBytes(LongitudDeNonce);
        var etiqueta = new byte[LongitudDeEtiqueta];
        var cifrado = new byte[datos.Length];

        using (var aes = new AesGcm(Llave(llaveDeSesion), LongitudDeEtiqueta))
        {
            aes.Encrypt(nonce, datos, cifrado, etiqueta);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_ruta)!);
        await File.WriteAllBytesAsync(_ruta, [.. nonce, .. etiqueta, .. cifrado]);
    }

    public async Task<CopiaDelDia?> LeerAsync(string llaveDeSesion)
    {
        if (!File.Exists(_ruta))
        {
            return null;
        }

        try
        {
            var contenido = await File.ReadAllBytesAsync(_ruta);
            if (contenido.Length < LongitudDeNonce + LongitudDeEtiqueta)
            {
                return null;
            }

            var datos = new byte[contenido.Length - LongitudDeNonce - LongitudDeEtiqueta];
            using (var aes = new AesGcm(Llave(llaveDeSesion), LongitudDeEtiqueta))
            {
                aes.Decrypt(
                    contenido.AsSpan(0, LongitudDeNonce),
                    contenido.AsSpan(LongitudDeNonce + LongitudDeEtiqueta),
                    contenido.AsSpan(LongitudDeNonce, LongitudDeEtiqueta),
                    datos);
            }

            return JsonSerializer.Deserialize<CopiaDelDia>(datos, Formato);
        }
        catch (Exception excepcion)
            when (excepcion is CryptographicException or JsonException or IOException)
        {
            return null;
        }
    }

    public void Borrar()
    {
        try
        {
            File.Delete(_ruta);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static byte[] Llave(string llaveDeSesion) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(llaveDeSesion));
}

public sealed record CopiaDelDia(DateTime FechaHora, List<DatosDePasaje> Pasajes);
