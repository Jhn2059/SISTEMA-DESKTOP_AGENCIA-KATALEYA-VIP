namespace Kataleya.Escritorio;

public static class Configuracion
{
    private const string VariableDeEntorno = "KATALEYA_API";
    private const string DireccionPredeterminada = "http://localhost:5080/";

    public static Uri DireccionApi
    {
        get
        {
            var valor = Environment.GetEnvironmentVariable(VariableDeEntorno);
            var texto = string.IsNullOrWhiteSpace(valor) ? DireccionPredeterminada : valor;

            if (!texto.EndsWith('/'))
            {
                texto += "/";
            }

            return Uri.TryCreate(texto, UriKind.Absolute, out var direccion)
                ? direccion
                : new Uri(DireccionPredeterminada);
        }
    }

    public static int Reintentos =>
        int.TryParse(Environment.GetEnvironmentVariable("KATALEYA_REINTENTOS"), out var valor) &&
        valor >= 0
            ? valor
            : 3;

    public static bool PermitirHttpSinTls =>
        string.Equals(
            Environment.GetEnvironmentVariable("KATALEYA_PERMITIR_HTTP"),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
