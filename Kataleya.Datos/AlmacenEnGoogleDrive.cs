using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Datos;

public sealed class OpcionesDeGoogleDrive
{
    public const string Seccion = "GoogleDrive";

    public string ClienteId { get; set; } = string.Empty;

    public string ClienteSecreto { get; set; } = string.Empty;

    public string TokenDeActualizacion { get; set; } = string.Empty;

    public string CarpetaId { get; set; } = string.Empty;
}

// Sube la copia a la carpeta de Google Drive de la empresa con su permiso (OAuth, token de actualización)
// y deja solo las últimas copias (RNF-17).
public sealed class AlmacenEnGoogleDrive : IAlmacenDeCopias
{
    private const string Prefijo = "kataleya-copia-";

    private readonly HttpClient _http;
    private readonly OpcionesDeGoogleDrive _opciones;

    public AlmacenEnGoogleDrive(HttpClient http, OpcionesDeGoogleDrive opciones)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
    }

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(_opciones.ClienteId) &&
        !string.IsNullOrWhiteSpace(_opciones.ClienteSecreto) &&
        !string.IsNullOrWhiteSpace(_opciones.TokenDeActualizacion) &&
        !string.IsNullOrWhiteSpace(_opciones.CarpetaId);

    public async Task SubirAsync(CopiaCifrada copia)
    {
        ArgumentNullException.ThrowIfNull(copia);

        var token = await ObtenerTokenAsync();

        using var inicio = new HttpRequestMessage(
            HttpMethod.Post,
            "https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable&fields=id")
        {
            Content = JsonContent.Create(new { name = copia.NombreArchivo, parents = new[] { _opciones.CarpetaId } })
        };
        inicio.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        inicio.Headers.Add("X-Upload-Content-Type", "application/octet-stream");
        inicio.Headers.Add("X-Upload-Content-Length", copia.Contenido.Length.ToString());

        using var respuestaDeInicio = await _http.SendAsync(inicio);
        await AsegurarExitoAsync(respuestaDeInicio, "iniciar la subida");

        var destino = respuestaDeInicio.Headers.Location
            ?? throw new HttpRequestException("Google Drive no devolvió la dirección de la subida.");

        using var subida = new HttpRequestMessage(HttpMethod.Put, destino)
        {
            Content = new ByteArrayContent(copia.Contenido)
        };
        subida.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        subida.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var respuesta = await _http.SendAsync(subida);
        await AsegurarExitoAsync(respuesta, "subir la copia");
    }

    public async Task BorrarLasViejasAsync(int conservar)
    {
        var token = await ObtenerTokenAsync();
        var consulta = Uri.EscapeDataString(
            $"'{_opciones.CarpetaId}' in parents and name contains '{Prefijo}' and trashed = false");

        using var lista = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://www.googleapis.com/drive/v3/files?q={consulta}&orderBy=createdTime%20desc" +
            "&fields=files(id,name,createdTime)&pageSize=100");
        lista.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var respuesta = await _http.SendAsync(lista);
        await AsegurarExitoAsync(respuesta, "listar las copias");

        var archivos = await respuesta.Content.ReadFromJsonAsync<ListaDeArchivos>()
            ?? new ListaDeArchivos();

        foreach (var archivo in archivos.Files.Skip(conservar))
        {
            using var borrar = new HttpRequestMessage(
                HttpMethod.Delete,
                $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(archivo.Id)}");
            borrar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var respuestaDeBorrado = await _http.SendAsync(borrar);
            await AsegurarExitoAsync(respuestaDeBorrado, "borrar una copia vieja");
        }
    }

    private async Task<string> ObtenerTokenAsync()
    {
        if (!EstaConfigurado)
        {
            throw new InvalidOperationException("Google Drive no está configurado (sección GoogleDrive).");
        }

        using var respuesta = await _http.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _opciones.ClienteId,
                ["client_secret"] = _opciones.ClienteSecreto,
                ["refresh_token"] = _opciones.TokenDeActualizacion,
                ["grant_type"] = "refresh_token"
            }));
        await AsegurarExitoAsync(respuesta, "obtener el permiso");

        var token = await respuesta.Content.ReadFromJsonAsync<RespuestaDeToken>();
        return string.IsNullOrWhiteSpace(token?.AccessToken)
            ? throw new HttpRequestException("Google Drive no devolvió el permiso de acceso.")
            : token.AccessToken;
    }

    private static async Task AsegurarExitoAsync(HttpResponseMessage respuesta, string paso)
    {
        if (respuesta.IsSuccessStatusCode)
        {
            return;
        }

        var detalle = await respuesta.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Google Drive respondió {(int)respuesta.StatusCode} al {paso}: {detalle}",
            null,
            respuesta.StatusCode);
    }

    private sealed class RespuestaDeToken
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }

    private sealed class ListaDeArchivos
    {
        [JsonPropertyName("files")]
        public List<ArchivoDeDrive> Files { get; set; } = [];
    }

    private sealed class ArchivoDeDrive
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
