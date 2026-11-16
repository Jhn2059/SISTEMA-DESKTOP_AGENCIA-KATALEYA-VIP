namespace Kataleya.Contratos;

public sealed class DatosDeGrupo
{
    public long Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Origen { get; set; } = string.Empty;

    public string Destino { get; set; } = string.Empty;

    public string LugarDeRecojo { get; set; } = string.Empty;

    public DateTime FechaHoraSalida { get; set; }

    public decimal PrecioAcordado { get; set; }

    public decimal Pagado { get; set; }

    public decimal Saldo { get; set; }

    public string Estado { get; set; } = string.Empty;

    public string TelefonoContacto { get; set; } = string.Empty;

    public string? Responsable { get; set; }

    public List<DatosDeUnidadDelGrupo> Unidades { get; set; } = [];

    public List<DatosDePasaje> Pasajes { get; set; } = [];

    public List<DatosDePago> Pagos { get; set; } = [];
}
