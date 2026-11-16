namespace Kataleya.Dominio.DataObjects;

public sealed record TotalesDeCuadre(
    decimal CobradoEfectivo,
    decimal CobradoYape,
    decimal Devoluciones,
    decimal AdelantosRetenidos,
    int CantidadPasajesAnulados,
    decimal ImportePasajesAnulados,
    int CantidadAdelantosPendientes,
    decimal ImporteAdelantosPendientes)
{
    public static TotalesDeCuadre Vacio { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);

    public decimal TotalNeto => CobradoEfectivo + CobradoYape - Devoluciones;
}

public sealed record CuadrePorEmisor(
    int EmisorId,
    string Emisor,
    TotalesDeCuadre Totales,
    IReadOnlyList<MovimientoDeCuadre> Movimientos,
    IReadOnlyList<PasajeDeCuadre> PasajesAnulados,
    IReadOnlyList<PasajeDeCuadre> AdelantosPendientes);

public sealed record Cuadre(
    DateOnly Desde,
    DateOnly Hasta,
    TotalesDeCuadre Totales,
    IReadOnlyList<CuadrePorEmisor> Emisores);
