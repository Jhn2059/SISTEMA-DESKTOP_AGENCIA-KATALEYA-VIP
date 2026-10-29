using PdfSharp.Fonts;

namespace Kataleya.Aplicacion.Reportes;

// En el servidor de Render no hay fuentes de Windows: el PDF usa la Inter que trae el proyecto.
internal sealed class FuentesDelReporte : IFontResolver
{
    private static readonly object Candado = new();

    public static void Registrar()
    {
        lock (Candado)
        {
            if (GlobalFontSettings.FontResolver is not FuentesDelReporte)
            {
                GlobalFontSettings.FontResolver = new FuentesDelReporte();
            }
        }
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "Inter-Bold" : "Inter-Regular");

    public byte[] GetFont(string faceName)
    {
        using var recurso = typeof(FuentesDelReporte).Assembly
            .GetManifestResourceStream($"Kataleya.Fuentes.{faceName}.ttf")
            ?? throw new InvalidOperationException($"No se encontró la fuente {faceName}.");
        using var memoria = new MemoryStream();
        recurso.CopyTo(memoria);
        return memoria.ToArray();
    }
}
