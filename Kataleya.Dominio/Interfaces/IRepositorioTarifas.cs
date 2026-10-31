using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioTarifas
{
    Task<Tarifa?> BuscarVigenteAsync(DateOnly fecha, Sentido sentido);

    Task<IReadOnlyList<Tarifa>> ListarAsync();

    Task<int> GuardarAsync(Tarifa tarifa);

    Task QuitarAsync(TarifaProgramada tarifa);
}
