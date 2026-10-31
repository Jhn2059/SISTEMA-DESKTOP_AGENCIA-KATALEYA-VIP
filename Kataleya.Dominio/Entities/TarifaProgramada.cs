using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public sealed class TarifaProgramada : Tarifa
{
    public const int LongitudMaximaDelNombre = 60;

    public TarifaProgramada(SolicitudDeTarifa solicitud, Usuario fijadaPor)
        : this(
            0,
            (solicitud ?? throw new ArgumentNullException(nameof(solicitud))).Nombre,
            solicitud.Vigencia,
            solicitud.Sentido,
            solicitud.Importe,
            (fijadaPor ?? throw new ArgumentNullException(nameof(fijadaPor))).Id,
            fijadaPor.Nombre)
    {
    }

    internal TarifaProgramada(
        int id,
        string nombre,
        Periodo vigencia,
        Sentido? sentido,
        decimal importe,
        int idUsuario,
        string fijadaPor)
        : base(id, importe, idUsuario, fijadaPor)
    {
        ArgumentNullException.ThrowIfNull(vigencia);

        Nombre = (nombre ?? string.Empty).Trim();
        Vigencia = vigencia;
        Sentido = sentido;
    }

    public string Nombre { get; }

    public Periodo Vigencia { get; }

    public Sentido? Sentido { get; }

    public override TipoDeTarifa Tipo => TipoDeTarifa.Programada;

    public override List<ErrorDeCampo> Validar()
    {
        var errores = new List<ErrorDeCampo>();

        if (string.IsNullOrWhiteSpace(Nombre))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "nombre",
                Mensaje = "Escriba el nombre de la tarifa."
            });
        }
        else if (Nombre.Length > LongitudMaximaDelNombre)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "nombre",
                Mensaje = $"El nombre no puede superar los {LongitudMaximaDelNombre} caracteres."
            });
        }

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

        if (Vigencia.Hasta < Vigencia.Desde)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "vigencia",
                Mensaje = "La fecha final no puede ser anterior a la inicial."
            });
        }

        return errores;
    }
}
