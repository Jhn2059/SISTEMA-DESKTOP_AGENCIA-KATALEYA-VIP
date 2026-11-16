using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioBoletos
{
    Task<BoletoDeViaje> EmitirAsync(
        long pasajeId,
        string serie,
        DateTime fechaEmision,
        int emitidoPorId,
        string emitidoPor);

    Task<BoletoDeViaje> RegistrarAsync(
        long pasajeId,
        string serie,
        int correlativo,
        DateTime fechaEmision,
        int emitidoPorId,
        string emitidoPor);

    Task<BoletoDeViaje?> BuscarPorPasajeAsync(long pasajeId);

    Task<BoletoDeViaje?> BuscarPorNumeroAsync(string serie, int correlativo);
}
