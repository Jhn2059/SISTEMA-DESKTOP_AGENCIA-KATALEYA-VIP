using Kataleya.Dominio.Enums;

namespace Kataleya.Escritorio.Features.Pasajes;

// Mensajes debajo de cada campo del pasajero (IU-P03, RF-01).
public static class ValidacionDelPasajero
{
    public static string Nombre(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre) ? "Escriba el nombre del pasajero." : string.Empty;

    public static string Documento(TipoDocumento tipo, string? numero)
    {
        var texto = numero?.Trim() ?? string.Empty;

        return tipo switch
        {
            TipoDocumento.DNI when texto.Length != 8 || !texto.All(char.IsAsciiDigit) =>
                "El DNI debe tener 8 dígitos.",
            TipoDocumento.Pasaporte when texto.Length == 0 || texto.Length > 12 =>
                "El pasaporte o el carné de extranjería tiene hasta 12 caracteres.",
            _ => string.Empty
        };
    }

    public static string Celular(string? celular)
    {
        var texto = (celular ?? string.Empty).Replace(" ", string.Empty);

        return texto.Length == 9 && texto[0] == '9' && texto.All(char.IsAsciiDigit)
            ? string.Empty
            : "El celular debe tener 9 dígitos y empezar con 9.";
    }
}
