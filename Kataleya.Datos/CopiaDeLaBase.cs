using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Datos;

// RNF-17: lee todas las tablas en una sola transacción, las comprime y las cifra con AES-256-GCM.
// El formato está explicado en sql/restaurar-copia.md, para el equipo de soporte.
public sealed class CopiaDeLaBase : ICopiaDeLaBase
{
    // En el orden en que se restauran, por las claves foráneas.
    public static readonly string[] Tablas =
    [
        "rol", "sede", "usuario", "tipo_vehiculo", "parametro", "tarifa", "correlativo_boleto",
        "grupo", "unidad_salida", "pasaje", "pago", "boleto_viaje", "registro_bitacora",
        "reporte_anual", "copia_respaldo", "preferencias"
    ];

    private static readonly byte[] Cabecera = "KTLY1"u8.ToArray();
    private const int Iteraciones = 200_000;
    private const int LongitudDeSal = 16;
    private const int LongitudDeNonce = 12;
    private const int LongitudDeEtiqueta = 16;

    private readonly FuenteDeDatos _fuente;
    private readonly string _claveDeCifrado;

    public CopiaDeLaBase(FuenteDeDatos fuente, string claveDeCifrado)
    {
        _fuente = fuente ?? throw new ArgumentNullException(nameof(fuente));
        _claveDeCifrado = claveDeCifrado ?? string.Empty;
    }

    public async Task<CopiaCifrada> ExportarAsync(DateTime fechaHora)
    {
        if (string.IsNullOrWhiteSpace(_claveDeCifrado))
        {
            throw new InvalidOperationException("Falta la clave de cifrado de la copia (Copia:ClaveDeCifrado).");
        }

        using var zip = new MemoryStream();

        await using (var conexion = await _fuente.AbrirConexionAsync())
        await using (var transaccion = await conexion.BeginTransactionAsync(IsolationLevel.RepeatableRead))
        using (var archivo = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var tabla in Tablas)
            {
                var entrada = archivo.CreateEntry($"{tabla}.csv", CompressionLevel.Optimal);
                await using var destino = new StreamWriter(entrada.Open(), new UTF8Encoding(false));
                using var origen = await conexion.BeginTextExportAsync(
                    $"COPY (SELECT * FROM {tabla} ORDER BY 1) TO STDOUT WITH (FORMAT csv, HEADER)");
                await destino.WriteAsync(await origen.ReadToEndAsync());
            }

            await transaccion.CommitAsync();
        }

        return new CopiaCifrada
        {
            NombreArchivo = $"kataleya-copia-{fechaHora:yyyy-MM-dd-HHmm}.cifrada",
            Contenido = Cifrar(zip.ToArray(), _claveDeCifrado)
        };
    }

    public static byte[] Cifrar(byte[] datos, string clave)
    {
        var sal = RandomNumberGenerator.GetBytes(LongitudDeSal);
        var nonce = RandomNumberGenerator.GetBytes(LongitudDeNonce);
        var etiqueta = new byte[LongitudDeEtiqueta];
        var cifrado = new byte[datos.Length];

        using var aes = new AesGcm(DerivarLlave(clave, sal), LongitudDeEtiqueta);
        aes.Encrypt(nonce, datos, cifrado, etiqueta);

        return [.. Cabecera, .. sal, .. nonce, .. etiqueta, .. cifrado];
    }

    public static byte[] Descifrar(byte[] contenido, string clave)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        var inicio = Cabecera.Length;
        if (contenido.Length < inicio + LongitudDeSal + LongitudDeNonce + LongitudDeEtiqueta ||
            !contenido.AsSpan(0, inicio).SequenceEqual(Cabecera))
        {
            throw new InvalidDataException("El archivo no es una copia de Kataleya.");
        }

        var sal = contenido.AsSpan(inicio, LongitudDeSal);
        var nonce = contenido.AsSpan(inicio + LongitudDeSal, LongitudDeNonce);
        var etiqueta = contenido.AsSpan(inicio + LongitudDeSal + LongitudDeNonce, LongitudDeEtiqueta);
        var cifrado = contenido.AsSpan(inicio + LongitudDeSal + LongitudDeNonce + LongitudDeEtiqueta);
        var datos = new byte[cifrado.Length];

        using var aes = new AesGcm(DerivarLlave(clave, sal.ToArray()), LongitudDeEtiqueta);
        aes.Decrypt(nonce, cifrado, etiqueta, datos);

        return datos;
    }

    private static byte[] DerivarLlave(string clave, byte[] sal) =>
        Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA256, 32);
}
