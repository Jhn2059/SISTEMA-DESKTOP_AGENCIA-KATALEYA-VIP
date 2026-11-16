namespace Kataleya.Contratos.Cuadre;

public sealed class DatosDeCuadre
{
    public DateOnly Desde { get; set; }

    public DateOnly Hasta { get; set; }

    public DatosDeTotalesDeCuadre Totales { get; set; } = new();

    public List<DatosDeCuadrePorEmisor> Emisores { get; set; } = [];
}

public sealed class DatosDeTotalesDeCuadre
{
    public decimal CobradoEfectivo { get; set; }

    public decimal CobradoYape { get; set; }

    public decimal Devoluciones { get; set; }

    public decimal TotalNeto { get; set; }

    public decimal AdelantosRetenidos { get; set; }

    public int CantidadPasajesAnulados { get; set; }

    public decimal ImportePasajesAnulados { get; set; }

    public int CantidadAdelantosPendientes { get; set; }

    public decimal ImporteAdelantosPendientes { get; set; }
}
