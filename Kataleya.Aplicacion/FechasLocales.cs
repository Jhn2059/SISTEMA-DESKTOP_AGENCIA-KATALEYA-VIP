namespace Kataleya.Aplicacion;

public static class FechasLocales
{
    // Perú no cambia de hora en el año: siempre UTC-5.
    private static readonly TimeSpan DiferenciaConUtc = TimeSpan.FromHours(-5);

    public static DateTime Combinar(DateOnly fecha, TimeOnly hora) =>
        DateTime.SpecifyKind(fecha.ToDateTime(hora) - DiferenciaConUtc, DateTimeKind.Utc);

    public static DateOnly Hoy(DateTime ahora) =>
        DateOnly.FromDateTime(ahora.ToUniversalTime() + DiferenciaConUtc);
}
