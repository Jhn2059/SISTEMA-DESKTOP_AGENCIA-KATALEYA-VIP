namespace Kataleya.Contratos.Session;

public sealed class ResultadoDeInicioDeSesion
{
    public DatosDeCuenta? Cuenta { get; set; }

    public MotivoDeRechazo? Rechazo { get; set; }

    public DateTime? FinBloqueo { get; set; }

    public string? Llave { get; set; }

    public DateTime? VenceLlave { get; set; }
}
