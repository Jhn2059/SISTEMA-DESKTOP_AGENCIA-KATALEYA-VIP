using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public abstract class Tarifa
{
    protected Tarifa(int id, decimal importe, int idUsuario, string fijadaPor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fijadaPor);

        Id = id;
        Importe = importe;
        IdUsuario = idUsuario;
        FijadaPor = fijadaPor;
    }

    public int Id { get; }

    public decimal Importe { get; }

    public int IdUsuario { get; }

    public string FijadaPor { get; }

    public abstract TipoDeTarifa Tipo { get; }

    public abstract List<ErrorDeCampo> Validar();

    public static Tarifa Reconstruir(TarifaGuardada guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return guardado.Tipo switch
        {
            TipoDeTarifa.Regular => new TarifaRegular(
                guardado.Id,
                guardado.Sentido
                    ?? throw new InvalidOperationException("La tarifa regular no tiene sentido."),
                guardado.Importe,
                guardado.IdUsuario,
                guardado.FijadaPor),
            TipoDeTarifa.Programada => new TarifaProgramada(
                guardado.Id,
                guardado.Nombre ?? string.Empty,
                guardado.Vigencia
                    ?? throw new InvalidOperationException("La tarifa programada no tiene vigencia."),
                guardado.Sentido,
                guardado.Importe,
                guardado.IdUsuario,
                guardado.FijadaPor),
            _ => throw new ArgumentOutOfRangeException(nameof(guardado), "El tipo de tarifa no es válido.")
        };
    }
}
