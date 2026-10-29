namespace Kataleya.Aplicacion;

public static class FechasLocales
{
    public static DateTime Combinar(DateOnly fecha, TimeOnly hora) =>
        DateTime.SpecifyKind(fecha.ToDateTime(hora), DateTimeKind.Local);
}
