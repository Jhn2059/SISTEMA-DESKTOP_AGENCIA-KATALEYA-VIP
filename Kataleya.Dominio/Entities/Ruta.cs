using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public static class Ruta
{
    private static readonly char[] Separadores = [' ', ',', '.', '-', '(', ')', '/'];

    public static SentidoDelViaje? SentidoSegunElOrigen(string? origen)
    {
        var palabras = (origen ?? string.Empty)
            .ToLowerInvariant()
            .Split(Separadores, StringSplitOptions.RemoveEmptyEntries);

        if (palabras.Contains("huancayo"))
        {
            return SentidoDelViaje.Ida;
        }

        if (palabras.Contains("ica"))
        {
            return SentidoDelViaje.Vuelta;
        }

        return null;
    }
}
