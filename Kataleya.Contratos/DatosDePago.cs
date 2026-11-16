using Kataleya.Dominio.Enums;

namespace Kataleya.Contratos;

public sealed class DatosDePago
{
    public decimal Monto { get; set; }

    public MedioPago MedioDePago { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public DateTime FechaPago { get; set; }

    public string Cobrador { get; set; } = string.Empty;
}
