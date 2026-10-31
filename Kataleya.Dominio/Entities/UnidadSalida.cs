using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public sealed class UnidadSalida
{
    public UnidadSalida(
        Sentido sentido,
        short tipoVehiculoId,
        DateTime fechaHoraSalida,
        int idUsuario,
        string vendidaPor)
        : this(0, sentido, tipoVehiculoId, fechaHoraSalida, "Programada", idUsuario, vendidaPor)
    {
    }

    internal UnidadSalida(
        long id,
        Sentido sentido,
        short tipoVehiculoId,
        DateTime fechaHoraSalida,
        string estado,
        int idUsuario,
        string vendidaPor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(estado);
        ArgumentException.ThrowIfNullOrWhiteSpace(vendidaPor);

        Id = id;
        Sentido = sentido;
        TipoVehiculoId = tipoVehiculoId;
        FechaHoraSalida = fechaHoraSalida;
        Estado = estado;
        UsuarioId = idUsuario;
        VendidaPor = vendidaPor;
    }

    public long Id { get; }
    public Sentido Sentido { get; }
    public short TipoVehiculoId { get; }
    public DateTime FechaHoraSalida { get; }
    public string Estado { get; }
    public int UsuarioId { get; }
    public string VendidaPor { get; }

    public static UnidadSalida Reconstruir(UnidadSalidaGuardada guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return new UnidadSalida(
            guardado.Id,
            guardado.Sentido,
            guardado.TipoVehiculoId,
            guardado.FechaHoraSalida,
            guardado.Estado,
            guardado.UsuarioId,
            guardado.VendidaPor);
    }
}