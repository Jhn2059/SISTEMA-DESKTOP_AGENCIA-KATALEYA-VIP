using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public sealed class TarifaRegular : Tarifa
{
    public TarifaRegular(Sentido sentido, decimal importe, Usuario fijadaPor)
        : this(
            0,
            sentido,
            importe,
            (fijadaPor ?? throw new ArgumentNullException(nameof(fijadaPor))).Id,
            fijadaPor.Nombre)
    {
    }

    internal TarifaRegular(
        int id,
        Sentido sentido,
        decimal importe,
        int idUsuario,
        string fijadaPor)
        : base(id, importe, idUsuario, fijadaPor) =>
        Sentido = sentido;

    public Sentido Sentido { get; }

    public override TipoDeTarifa Tipo => TipoDeTarifa.Regular;

    public override List<ErrorDeCampo> Validar()
    {
        var errores = new List<ErrorDeCampo>();

        if (Importe <= 0)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "importe",
                Mensaje = "Escriba un importe mayor que cero."
            });
        }
        else if (decimal.Round(Importe, 2) != Importe)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "importe",
                Mensaje = "Escriba un importe con dos decimales."
            });
        }

        return errores;
    }
}
