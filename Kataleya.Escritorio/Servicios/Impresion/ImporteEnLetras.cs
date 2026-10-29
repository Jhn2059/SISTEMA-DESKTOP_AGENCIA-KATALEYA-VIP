namespace Kataleya.Escritorio.Servicios.Impresion;

public static class ImporteEnLetras
{
    private static readonly string[] Unidades =
    [
        "CERO", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO",
        "DIECINUEVE", "VEINTE", "VEINTIUNO", "VEINTIDÓS", "VEINTITRÉS", "VEINTICUATRO", "VEINTICINCO",
        "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE"
    ];

    private static readonly string[] Decenas =
        ["", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"];

    private static readonly string[] Centenas =
    [
        "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS",
        "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
    ];

    public static string Convertir(decimal importe)
    {
        var redondeado = Math.Round(Math.Abs(importe), 2, MidpointRounding.AwayFromZero);
        var soles = (long)Math.Truncate(redondeado);
        var centimos = (int)((redondeado - soles) * 100);

        return $"{EnLetras(soles)} CON {centimos:00}/100 SOLES";
    }

    private static string EnLetras(long numero)
    {
        if (numero < 1000)
        {
            return MenorQueMil((int)numero);
        }

        var miles = numero / 1000;
        var resto = (int)(numero % 1000);
        var textoMiles = miles == 1 ? "MIL" : $"{Apocopar(EnLetras(miles))} MIL";

        return resto == 0 ? textoMiles : $"{textoMiles} {MenorQueMil(resto)}";
    }

    private static string MenorQueMil(int numero)
    {
        if (numero == 100)
        {
            return "CIEN";
        }

        var centena = numero / 100;
        var resto = numero % 100;
        var textoResto = resto switch
        {
            0 => string.Empty,
            < 30 => Unidades[resto],
            _ when resto % 10 == 0 => Decenas[resto / 10],
            _ => $"{Decenas[resto / 10]} Y {Unidades[resto % 10]}"
        };

        if (centena == 0)
        {
            return resto == 0 ? Unidades[0] : textoResto;
        }

        return resto == 0 ? Centenas[centena] : $"{Centenas[centena]} {textoResto}";
    }

    private static string Apocopar(string texto) =>
        texto.EndsWith("VEINTIUNO", StringComparison.Ordinal)
            ? texto[..^"VEINTIUNO".Length] + "VEINTIÚN"
            : texto.EndsWith("UNO", StringComparison.Ordinal)
                ? texto[..^1]
                : texto;
}
