using System.Text;
using System.Text.Json;
using Kataleya.Dominio.Interfaces;
using Kataleya.Dominio.DataObjects;

namespace Kataleya.Infraestructura;

public sealed class RegistroTecnico : IRegistroTecnico
{
    private static readonly JsonSerializerOptions Formato = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _turno = new(1, 1);
    private readonly string _ruta;

    public RegistroTecnico()
    {
        var carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kataleya");

        Directory.CreateDirectory(carpeta);
        _ruta = Path.Combine(carpeta, "registro-tecnico.log");
    }

    public async Task RegistrarAsync(ErrorTecnico error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var linea = JsonSerializer.Serialize(error, Formato) + Environment.NewLine;

        await _turno.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_ruta, linea, Encoding.UTF8);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        finally
        {
            _turno.Release();
        }
    }
}
