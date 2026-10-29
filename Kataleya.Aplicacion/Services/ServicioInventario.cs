using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Mapeo;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioInventario
{
    private readonly IRepositorioUnidades _unidades;
    private readonly IRepositorioConfiguracion _configuracion;

    public ServicioInventario(
        IRepositorioUnidades unidades,
        IRepositorioConfiguracion configuracion)
    {
        _unidades = unidades ?? throw new ArgumentNullException(nameof(unidades));
        _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
    }

    public async Task<IReadOnlyList<DatosDeUnidad>> ListarAsync(DateOnly fecha, TimeOnly hora)
    {
        var unidades = await _unidades.ListarDeLaSalidaAsync(FechasLocales.Combinar(fecha, hora));
        return unidades.Select(u => u.ADatos()).ToList();
    }

    public async Task<DatosDeUnidad> AbrirAsync(
        DateOnly fecha,
        TimeOnly hora,
        short tipoVehiculoId)
    {
        var tipo = await _configuracion.BuscarTipoDeVehiculoAsync(tipoVehiculoId)
            ?? throw new ReglaDeNegocioException("Elija un tipo de vehículo válido.", "tipoVehiculoId");

        var fechaHora = FechasLocales.Combinar(fecha, hora);
        var existentes = await _unidades.ListarDeLaSalidaAsync(fechaHora);

        var numero = (existentes
            .Select(u => int.TryParse(u.Numero, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1).ToString();

        var unidad = new UnidadDeSalida
        {
            FechaHoraSalida = fechaHora,
            Numero = numero,
            TipoVehiculoId = tipo.Id,
            TipoVehiculoNombre = tipo.Nombre,
            NumeroAsientos = tipo.NumeroAsientos
        };

        unidad.Id = await _unidades.AgregarAsync(unidad);
        return unidad.ADatos();
    }

    public async Task<IReadOnlyList<short>> AsientosOcupadosAsync(long unidadId) =>
        await _unidades.AsientosOcupadosAsync(unidadId);
}
