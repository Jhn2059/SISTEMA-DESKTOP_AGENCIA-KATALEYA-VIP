using Kataleya.Contratos.Reportes;

namespace Kataleya.Escritorio.Servicios;

// Deja el PDF del reporte anual en la carpeta del respaldo de la laptop (CDU-12).
public sealed class ServicioRespaldo : IRespaldo
{
    private readonly string _carpeta;

    public ServicioRespaldo(string carpeta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carpeta);
        _carpeta = carpeta;
    }

    public async Task<string> GuardarReporteAnualAsync(ArchivoProtegido pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        if (pdf.Contenido.Length == 0)
        {
            throw new IOException("El PDF del reporte llegó vacío.");
        }

        Directory.CreateDirectory(_carpeta);
        var ruta = Path.Combine(_carpeta, pdf.Nombre);

        await File.WriteAllBytesAsync(ruta, pdf.Contenido);

        if (new FileInfo(ruta).Length != pdf.Contenido.Length)
        {
            throw new IOException("El PDF del reporte no se guardó completo.");
        }

        return ruta;
    }
}
