using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Mapeo;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
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

    public async Task<IReadOnlyList<DatosDeUnidad>> ListarAsync(
        DateOnly fecha,
        TimeOnly hora,
        SentidoDelViaje sentido)
    {
        ValidarSentido(sentido);

        var unidades = await _unidades.ListarDeLaSalidaAsync(FechasLocales.Combinar(fecha, hora), sentido);
        return unidades.Select(u => u.ADatos()).ToList();
    }

    public async Task<DatosDeUnidad> AbrirAsync(SolicitudDeUnidad solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidarSentido(solicitud.Sentido);

        var tipo = await _configuracion.BuscarTipoDeVehiculoAsync(solicitud.TipoVehiculoId)
            ?? throw new ReglaDeNegocioException("Elija un tipo de vehículo válido.", "tipoVehiculoId");

        var fechaHora = FechasLocales.Combinar(solicitud.Fecha, solicitud.Hora);
        var existentes = await _unidades.ListarDeLaSalidaAsync(fechaHora, solicitud.Sentido);

        var numero = (existentes
            .Select(u => int.TryParse(u.Numero, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1).ToString();

        var unidad = new UnidadDeSalida
        {
            FechaHoraSalida = fechaHora,
            Sentido = solicitud.Sentido,
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

    private static void ValidarSentido(SentidoDelViaje sentido)
    {
        if (sentido == SentidoDelViaje.Ambos)
        {
            throw new ReglaDeNegocioException(
                "Una unidad sale de Huancayo o de Ica: elija uno de los dos sentidos.",
                "sentido");
        }
    }
}
