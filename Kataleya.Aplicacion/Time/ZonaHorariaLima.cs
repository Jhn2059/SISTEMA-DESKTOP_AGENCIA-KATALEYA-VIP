namespace Kataleya.Aplicacion.Time;

public static class ZonaHorariaLima
{
    private static readonly TimeZoneInfo Zona =
        TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

    public static DateTime AHoraLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime(),
            Zona);

    public static DateTime ALocalAUtc(DateTime fechaLocal)
    {
        var sinZona = DateTime.SpecifyKind(fechaLocal, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(sinZona, Zona);
    }

    public static DateOnly FechaLocal(DateTime utc) =>
        DateOnly.FromDateTime(AHoraLocal(utc));
}
