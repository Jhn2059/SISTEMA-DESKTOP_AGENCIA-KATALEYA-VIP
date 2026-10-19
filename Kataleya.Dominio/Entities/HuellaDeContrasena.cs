using System.Security.Cryptography;

namespace Kataleya.Dominio.Entities;

internal static class HuellaDeContrasena
{
    private const int Iteraciones = 100_000;
    private const int LongitudDeSal = 16;
    private const int LongitudDeResumen = 32;
    private static readonly HashAlgorithmName Algoritmo = HashAlgorithmName.SHA256;

    public static (string Resumen, string Sal) Crear(string contrasena)
    {
        var sal = RandomNumberGenerator.GetBytes(LongitudDeSal);
        var resumen = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, Algoritmo, LongitudDeResumen);
        return (Convert.ToBase64String(resumen), Convert.ToBase64String(sal));
    }

    public static bool Verificar(string contrasena, string resumenBase64, string salBase64)
    {
        if (string.IsNullOrEmpty(contrasena))
        {
            return false;
        }

        try
        {
            var sal = Convert.FromBase64String(salBase64);
            var esperado = Convert.FromBase64String(resumenBase64);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, Algoritmo, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (Exception excepcion)
            when (excepcion is FormatException or ArgumentException or CryptographicException)
        {
            return false;
        }
    }
}
