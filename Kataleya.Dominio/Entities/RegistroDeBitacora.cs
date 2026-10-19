namespace Kataleya.Dominio.Entities;

public sealed class RegistroDeBitacora
{
    public RegistroDeBitacora(
        long id,
        DateTime fechaHora,
        string operacion,
        Usuario usuario,
        string? valorAnterior = null,
        string? valorNuevo = null,
        Guid? claveDeOperacion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operacion);
        ArgumentNullException.ThrowIfNull(usuario);

        Id = id;
        FechaHora = fechaHora;
        Operacion = operacion;
        Usuario = usuario;
        ValorAnterior = valorAnterior;
        ValorNuevo = valorNuevo;
        ClaveDeOperacion = claveDeOperacion;
    }

    public long Id { get; }

    public DateTime FechaHora { get; }

    public string Operacion { get; }

    public Usuario Usuario { get; }

    public string? ValorAnterior { get; }

    public string? ValorNuevo { get; }

    public Guid? ClaveDeOperacion { get; }
}
