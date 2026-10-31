using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioConsultas
{
    private readonly IRepositorioPasajes _pasajes;

    public ServicioConsultas(IRepositorioPasajes pasajes) =>
        _pasajes = pasajes ?? throw new ArgumentNullException(nameof(pasajes));

    public Task<int> ContarPasajesDelHorarioAsync(TimeOnly hora) =>
        _pasajes.ContarPasajesDelHorarioAsync(hora);
}
