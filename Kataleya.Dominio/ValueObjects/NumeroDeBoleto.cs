using System.Globalization;

namespace Kataleya.Dominio.ValueObjects;

public sealed record NumeroDeBoleto
{
    public const string SeriePredeterminada = "001";
    public const int LongitudDeSerie = 3;

    public NumeroDeBoleto(string serie, int correlativo)
    {
        if (string.IsNullOrWhiteSpace(serie) ||
            serie.Trim().Length != LongitudDeSerie ||
            !serie.Trim().All(char.IsLetterOrDigit))
        {
            throw new ArgumentException(
                $"La serie del boleto debe tener {LongitudDeSerie} caracteres alfanuméricos.",
                nameof(serie));
        }

        if (correlativo <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correlativo),
                "El correlativo del boleto debe ser mayor que cero.");
        }

        Serie = serie.Trim().ToUpperInvariant();
        Correlativo = correlativo;
    }

    public string Serie { get; }

    public int Correlativo { get; }

    public string Texto => $"{Serie}-{Correlativo.ToString("D6", CultureInfo.InvariantCulture)}";

    public override string ToString() => Texto;
}
