using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public sealed class UnidadDeSalida
{
    public UnidadDeSalida(
        int numero,
        Sentido sentido,
        short tipoVehiculoId,
        DateTime fechaHoraSalida,
        Usuario abiertaPor)
        : this(
            0,
            numero,
            null,
            sentido,
            tipoVehiculoId,
            fechaHoraSalida,
            EstadoUnidadDeSalida.Programada,
            (abiertaPor ?? throw new ArgumentNullException(nameof(abiertaPor))).Id,
            abiertaPor.Nombre)
    {
    }

    public UnidadDeSalida(
        int numero,
        long grupoId,
        short tipoVehiculoId,
        DateTime fechaHoraSalida,
        Usuario abiertaPor)
        : this(
            0,
            numero,
            grupoId,
            null,
            tipoVehiculoId,
            fechaHoraSalida,
            EstadoUnidadDeSalida.Programada,
            (abiertaPor ?? throw new ArgumentNullException(nameof(abiertaPor))).Id,
            abiertaPor.Nombre)
    {
    }

    internal UnidadDeSalida(
        long id,
        int numero,
        long? grupoId,
        Sentido? sentido,
        short tipoVehiculoId,
        DateTime fechaHoraSalida,
        EstadoUnidadDeSalida estado,
        int abiertaPorId,
        string abiertaPor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numero);
        ArgumentException.ThrowIfNullOrWhiteSpace(abiertaPor);

        if (grupoId is null && sentido is null)
        {
            throw new ArgumentException(
                "Una unidad regular debe tener sentido.",
                nameof(sentido));
        }

        Id = id;
        Numero = numero;
        GrupoId = grupoId;
        Sentido = sentido;
        TipoVehiculoId = tipoVehiculoId;
        FechaHoraSalida = fechaHoraSalida;
        Estado = estado;
        AbiertaPorId = abiertaPorId;
        AbiertaPor = abiertaPor;
    }

    public long Id { get; private set; }

    public int Numero { get; }

    public long? GrupoId { get; }

    public Sentido? Sentido { get; }

    public short TipoVehiculoId { get; }

    public DateTime FechaHoraSalida { get; }

    public EstadoUnidadDeSalida Estado { get; }

    public int AbiertaPorId { get; }

    public string AbiertaPor { get; }

    public bool EsDeGrupo => GrupoId is not null;

    public void AsignarId(long id)
    {
        if (Id != 0)
        {
            throw new InvalidOperationException("La unidad de salida ya tiene identificador.");
        }

        Id = id;
    }

    public static UnidadDeSalida Reconstruir(UnidadDeSalidaGuardada guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return new UnidadDeSalida(
            guardado.Id,
            guardado.Numero,
            guardado.GrupoId,
            guardado.Sentido,
            guardado.TipoVehiculoId,
            guardado.FechaHoraSalida,
            guardado.Estado,
            guardado.AbiertaPorId,
            guardado.AbiertaPor);
    }
}
