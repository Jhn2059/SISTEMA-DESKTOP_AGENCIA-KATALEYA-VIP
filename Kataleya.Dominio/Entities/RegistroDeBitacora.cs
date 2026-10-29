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
        Guid? claveDeOperacion = null,
        long? pasajeId = null)
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
        PasajeId = pasajeId;
    }

    public long Id { get; }

    public DateTime FechaHora { get; }

    public string Operacion { get; }

    public Usuario Usuario { get; }

    public string? ValorAnterior { get; }

    public string? ValorNuevo { get; }

    public Guid? ClaveDeOperacion { get; private set; }

    public long? PasajeId { get; }

    public void FijarClaveDeOperacion(Guid? clave) =>
        ClaveDeOperacion = clave;
}
