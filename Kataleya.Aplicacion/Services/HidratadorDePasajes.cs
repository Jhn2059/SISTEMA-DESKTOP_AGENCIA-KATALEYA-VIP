using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class HidratadorDePasajes
{
    private readonly IRepositorioPagos _pagos;
    private readonly IRepositorioBoletos _boletos;

    public HidratadorDePasajes(
        IRepositorioPagos pagos,
        IRepositorioBoletos boletos)
    {
        _pagos = pagos ?? throw new ArgumentNullException(nameof(pagos));
        _boletos = boletos ?? throw new ArgumentNullException(nameof(boletos));
    }

    public async Task<Pasaje> HidratarAsync(Pasaje pasaje)
    {
        ArgumentNullException.ThrowIfNull(pasaje);

        var pagos = pasaje.GrupoId is long grupoId
            ? await _pagos.ListarPorGrupoAsync(grupoId)
            : await _pagos.ListarPorPasajeAsync(pasaje.Id);

        foreach (var pago in pagos)
        {
            pasaje.AgregarPago(pago);
        }

        var boleto = await _boletos.BuscarPorPasajeAsync(pasaje.Id);
        if (boleto is not null)
        {
            pasaje.FijarBoleto(boleto);
        }

        return pasaje;
    }
}
