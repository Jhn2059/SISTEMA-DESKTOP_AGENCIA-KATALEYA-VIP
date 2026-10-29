using Kataleya.Dominio.DataObjects;

namespace Kataleya.Dominio.Entities;

// La fecha y hora la pone la base al guardar (IS-BD-07).
public sealed class RegistroDeBitacora
{
    public RegistroDeBitacora(MovimientoDeBitacora movimiento, Usuario usuario, Guid? claveDeOperacion)
    {
        ArgumentNullException.ThrowIfNull(movimiento);
        ArgumentException.ThrowIfNullOrWhiteSpace(movimiento.Operacion);

        Operacion = movimiento.Operacion;
        PasajeId = movimiento.IdPasaje;
        ValorAnterior = movimiento.ValorAnterior;
        ValorNuevo = movimiento.ValorNuevo;
        Usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
        ClaveDeOperacion = claveDeOperacion;
    }

    public string Operacion { get; }

    public Usuario Usuario { get; }

    public string? ValorAnterior { get; }

    public string? ValorNuevo { get; }

    public Guid? ClaveDeOperacion { get; }

    public long? PasajeId { get; }
}
