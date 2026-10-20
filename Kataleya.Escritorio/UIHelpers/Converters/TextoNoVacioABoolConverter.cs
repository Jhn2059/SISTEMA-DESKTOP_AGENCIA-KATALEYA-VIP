using System.Globalization;

namespace Kataleya.Escritorio.UIHelpers.Converters;

public sealed class TextoNoVacioABoolConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is string texto && !string.IsNullOrWhiteSpace(texto);

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        throw new NotSupportedException();
}
