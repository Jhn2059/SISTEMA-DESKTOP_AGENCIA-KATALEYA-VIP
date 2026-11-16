using System.Globalization;
using System.Text;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.ValueObjects;

public sealed record DatosDelViaje(
    string Origen,
    string Destino,
    string LugarEmbarque,
    DateTime FechaHoraSalida)
{
    public const int LongitudMaximaOrigenDestino = 50;
    public const int LongitudMaximaLugarEmbarque = 50;

    public DatosDelViaje Normalizar() =>
        new(
            Origen?.Trim() ?? string.Empty,
            Destino?.Trim() ?? string.Empty,
            LugarEmbarque?.Trim() ?? string.Empty,
            FechaHoraSalida);

    public static bool TryDeterminarSentido(
        string? origen,
        string? destino,
        out Sentido sentido)
    {
        var rutaOrigen = NormalizarTexto(origen);
        var rutaDestino = NormalizarTexto(destino);

        var origenHuancayo = rutaOrigen.Contains("huancayo", StringComparison.Ordinal);
        var origenIca = rutaOrigen.Contains("ica", StringComparison.Ordinal);
        var destinoHuancayo = rutaDestino.Contains("huancayo", StringComparison.Ordinal);
        var destinoIca = rutaDestino.Contains("ica", StringComparison.Ordinal);

        if (origenHuancayo && destinoIca)
        {
            sentido = Sentido.HuancayoIca;
            return true;
        }

        if (origenIca && destinoHuancayo)
        {
            sentido = Sentido.IcaHuancayo;
            return true;
        }

        sentido = default;
        return false;
    }

    private static string NormalizarTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var constructor = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                constructor.Append(caracter);
            }
        }

        return constructor.ToString().Normalize(NormalizationForm.FormC);
    }
}
