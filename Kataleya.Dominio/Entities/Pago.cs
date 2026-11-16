using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public sealed class Pago
{
    public Pago(
        long? pasajeId,
        long? grupoId,
        decimal monto,
        MedioPago medioDePago,
        TipoDePago tipo,
        DateTime fechaPago,
        Usuario cobrador)
        : this(
            0,
            pasajeId,
            grupoId,
            monto,
            medioDePago,
            tipo,
            fechaPago,
            (cobrador ?? throw new ArgumentNullException(nameof(cobrador))).Id,
            cobrador.Nombre)
    {
    }

    internal Pago(
        long id,
        long? pasajeId,
        long? grupoId,
        decimal monto,
        MedioPago medioDePago,
        TipoDePago tipo,
        DateTime fechaPago,
        int cobradorId,
        string cobrador)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cobrador);

        Id = id;
        PasajeId = pasajeId;
        GrupoId = grupoId;
        Monto = monto;
        MedioDePago = medioDePago;
        Tipo = tipo;
        FechaPago = fechaPago;
        CobradorId = cobradorId;
        Cobrador = cobrador;
    }

    public long Id { get; }

    public long? PasajeId { get; }

    public long? GrupoId { get; }

    public decimal Monto { get; }

    public MedioPago MedioDePago { get; }

    public TipoDePago Tipo { get; }

    public DateTime FechaPago { get; }

    public int CobradorId { get; }

    public string Cobrador { get; }

    public bool EsDevolucion => Tipo == TipoDePago.Devolucion;

    public List<ErrorDeCampo> Validar()
    {
        var errores = new List<ErrorDeCampo>();

        if (Monto <= 0)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "monto",
                Mensaje = "Escriba un importe mayor que cero."
            });
        }
        else if (decimal.Round(Monto, 2) != Monto)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "monto",
                Mensaje = "El importe solo puede tener dos decimales."
            });
        }

        if (MedioDePago is not (MedioPago.Efectivo or MedioPago.Yape))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "medioDePago",
                Mensaje = "Elija Efectivo o Yape."
            });
        }

        if ((PasajeId is null) == (GrupoId is null))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "pasaje",
                Mensaje = "El pago debe estar asociado a un pasaje o a un grupo, no a ambos."
            });
        }

        return errores;
    }

    public static Pago Reconstruir(PagoGuardado guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return new Pago(
            guardado.Id,
            guardado.PasajeId,
            guardado.GrupoId,
            guardado.Monto,
            guardado.MedioDePago,
            guardado.Tipo,
            guardado.FechaPago,
            guardado.CobradorId,
            guardado.Cobrador);
    }
}
