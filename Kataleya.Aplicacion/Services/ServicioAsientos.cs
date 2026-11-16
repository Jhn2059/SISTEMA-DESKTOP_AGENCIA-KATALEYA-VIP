using System.Text.Json;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Time;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioAsientos
{
    private const string OperacionAbrirUnidad = "Abrir unidad de salida";

    private readonly IRepositorioUnidadesSalida _unidades;
    private readonly IRepositorioParametros _parametros;
    private readonly ServicioParametros _servicioParametros;
    private readonly ServicioBitacora _bitacora;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public ServicioAsientos(
        IRepositorioUnidadesSalida unidades,
        IRepositorioParametros parametros,
        ServicioParametros servicioParametros,
        ServicioBitacora bitacora,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _unidades = unidades ?? throw new ArgumentNullException(nameof(unidades));
        _parametros = parametros ?? throw new ArgumentNullException(nameof(parametros));
        _servicioParametros = servicioParametros ?? throw new ArgumentNullException(nameof(servicioParametros));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public Task<IReadOnlyList<UnidadDeSalidaDisponible>> ObtenerAsientosAsync(
        Sentido sentido,
        DateTime fechaHoraLocal) =>
        _unidades.ListarPorSalidaAsync(
            sentido,
            ZonaHorariaLima.ALocalAUtc(fechaHoraLocal));

    public async Task<UnidadDeSalidaDisponible> AbrirUnidadAsync(
        Sentido sentido,
        DateTime fechaHoraLocal,
        SolicitudDeUnidadDeSalida solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var fechaHoraSalida = ZonaHorariaLima.ALocalAUtc(fechaHoraLocal);
        var plazo = await _servicioParametros.ObtenerPlazoDeReservaAsync();
        var ahora = _reloj.Ahora;
        var horaLocal = ZonaHorariaLima.AHoraLocal(fechaHoraSalida);

        if (fechaHoraSalida <= ahora || fechaHoraSalida > ahora.AddDays(plazo))
        {
            throw new ReglaDeNegocioException(
                $"La salida debe ser desde ahora hasta dentro de {plazo} días.",
                "fechaHora");
        }

        var horarios = await _servicioParametros.ObtenerHorariosAsync();
        if (!horarios.Contains(new TimeOnly(horaLocal.Hour, horaLocal.Minute)))
        {
            throw new ReglaDeNegocioException(
                "Elija uno de los horarios de salida configurados.",
                "fechaHora");
        }

        var tipo = await _parametros.BuscarTipoDeVehiculoAsync(solicitud.TipoVehiculoId)
            ?? throw new ReglaDeNegocioException(
                "Elija un tipo de vehículo válido.",
                "tipoVehiculoId");

        if (await YaSeRegistroAsync())
        {
            var existentes = await _unidades.ListarPorSalidaAsync(sentido, fechaHoraSalida);
            var registrada = existentes.LastOrDefault();
            if (registrada is not null)
            {
                return registrada;
            }
        }

        var numero = await _unidades.SiguienteNumeroAsync(sentido, fechaHoraSalida);
        var unidad = new UnidadDeSalida(numero, sentido, tipo.Id, fechaHoraSalida, Actor);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var id = await _unidades.RegistrarAsync(unidad);
            unidad.AsignarId(id);
            await RegistrarBitacoraAsync(unidad, id);
        });

        return new UnidadDeSalidaDisponible(
            unidad.Id,
            unidad.Numero,
            unidad.GrupoId,
            unidad.Sentido
                ?? throw new InvalidOperationException(
                    "Una unidad individual debe tener sentido."),
            unidad.TipoVehiculoId,
            tipo.Nombre,
            tipo.CantidadDeAsientos,
            [],
            unidad.FechaHoraSalida,
            unidad.Estado,
            unidad.AbiertaPor);
    }

    private Usuario Actor =>
        _contexto.Usuario
        ?? throw new InvalidOperationException("No se encontró el usuario de la sesión.");

    private Task<bool> YaSeRegistroAsync() =>
        _contexto.ClaveDeOperacion is Guid clave
            ? _bitacora.ExisteAsync(clave)
            : Task.FromResult(false);

    private async Task RegistrarBitacoraAsync(UnidadDeSalida unidad, long id)
    {
        var guardado = new UnidadDeSalidaGuardada(
            id,
            unidad.Numero,
            unidad.GrupoId,
            unidad.Sentido
                ?? throw new InvalidOperationException(
                    "Una unidad individual debe tener sentido."),
            unidad.TipoVehiculoId,
            unidad.FechaHoraSalida,
            unidad.Estado,
            unidad.AbiertaPorId,
            unidad.AbiertaPor);

        await _bitacora.RegistrarAsync(new RegistroDeBitacora(
            0,
            _reloj.Ahora,
            OperacionAbrirUnidad,
            Actor,
            null,
            JsonSerializer.Serialize(guardado)));
    }
}
