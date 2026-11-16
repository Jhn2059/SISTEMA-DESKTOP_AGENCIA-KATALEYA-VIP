namespace Kataleya.Contratos.Cuadre;

public sealed class DatosDeCuadrePorEmisor
{
    public int EmisorId { get; set; }

    public string Emisor { get; set; } = string.Empty;

    public DatosDeTotalesDeCuadre Totales { get; set; } = new();

    public List<DatosDeMovimientoDeCuadre> Movimientos { get; set; } = [];

    public List<DatosDePasajeAnuladoCuadre> PasajesAnulados { get; set; } = [];

    public List<DatosDeAdelantoPendienteCuadre> AdelantosPendientes { get; set; } = [];
}
