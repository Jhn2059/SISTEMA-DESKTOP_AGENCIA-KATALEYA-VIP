namespace Kataleya.Contratos.Panel;

public sealed class DatosDelPanel
{
    public DateOnly Fecha { get; set; }

    public int ReservasRegistradas { get; set; }

    public int Ventas { get; set; }

    public decimal CobradoEnEfectivo { get; set; }

    public decimal CobradoPorYape { get; set; }

    public decimal AdelantosPorCobrar { get; set; }

    public List<DatosDeHorario> Horarios { get; set; } = [];
}

public sealed class DatosDeHorario
{
    public string Hora { get; set; } = string.Empty;

    public int Pasajes { get; set; }

    public int Pagados { get; set; }

    public int Reservados { get; set; }

    public int ConAdelanto { get; set; }
}
