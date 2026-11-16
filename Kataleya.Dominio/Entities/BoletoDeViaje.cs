using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.ValueObjects;

namespace Kataleya.Dominio.Entities;

public sealed class BoletoDeViaje
{
    public BoletoDeViaje(
        long pasajeId,
        NumeroDeBoleto numero,
        DateTime fechaEmision,
        Usuario emitidoPor)
        : this(
            0,
            pasajeId,
            numero,
            fechaEmision,
            (emitidoPor ?? throw new ArgumentNullException(nameof(emitidoPor))).Id,
            emitidoPor.Nombre)
    {
    }

    internal BoletoDeViaje(
        long id,
        long pasajeId,
        NumeroDeBoleto numero,
        DateTime fechaEmision,
        int emitidoPorId,
        string emitidoPor)
    {
        ArgumentNullException.ThrowIfNull(numero);
        ArgumentException.ThrowIfNullOrWhiteSpace(emitidoPor);

        Id = id;
        PasajeId = pasajeId;
        Numero = numero;
        FechaEmision = fechaEmision;
        EmitidoPorId = emitidoPorId;
        EmitidoPor = emitidoPor;
    }

    public long Id { get; }

    public long PasajeId { get; }

    public NumeroDeBoleto Numero { get; }

    public DateTime FechaEmision { get; }

    public int EmitidoPorId { get; }

    public string EmitidoPor { get; }

    public static BoletoDeViaje Reconstruir(BoletoGuardado guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return new BoletoDeViaje(
            guardado.Id,
            guardado.PasajeId,
            new NumeroDeBoleto(guardado.Serie, guardado.Correlativo),
            guardado.FechaEmision,
            guardado.EmitidoPorId,
            guardado.EmitidoPor);
    }
}
