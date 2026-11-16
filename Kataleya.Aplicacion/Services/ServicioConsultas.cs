using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioConsultas
{
    private readonly IRepositorioPasajes _pasajes;
    private readonly HidratadorDePasajes _hidratador;

    public ServicioConsultas(
        IRepositorioPasajes pasajes,
        HidratadorDePasajes hidratador)
    {
        _pasajes = pasajes ?? throw new ArgumentNullException(nameof(pasajes));
        _hidratador = hidratador ?? throw new ArgumentNullException(nameof(hidratador));
    }

    public Task<int> ContarPasajesDelHorarioAsync(TimeOnly hora) =>
        _pasajes.ContarPasajesDelHorarioAsync(hora);

    public async Task<IReadOnlyList<Pasaje>> ConsultarAsync(FiltroDePasajes filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var pasajes = await _pasajes.ListarAsync(filtro);
        foreach (var pasaje in pasajes)
        {
            await _hidratador.HidratarAsync(pasaje);
        }

        return pasajes;
    }

    public async Task<Pasaje?> ObtenerDetalleAsync(long id)
    {
        var pasaje = await _pasajes.BuscarAsync(id);
        return pasaje is null ? null : await _hidratador.HidratarAsync(pasaje);
    }
}
