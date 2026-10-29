using System.Text.Json;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Mapeo;
using Kataleya.Contratos.Configuracion;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioConfiguracion
{
    private readonly IRepositorioConfiguracion _configuracion;
    private readonly IRepositorioCatalogos _catalogos;
    private readonly RegistradorDeBitacora _bitacora;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public ServicioConfiguracion(
        IRepositorioConfiguracion configuracion,
        IRepositorioCatalogos catalogos,
        RegistradorDeBitacora bitacora,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        _catalogos = catalogos ?? throw new ArgumentNullException(nameof(catalogos));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public Task<IReadOnlyList<Tarifa>> ListarTarifasAsync() =>
        _configuracion.ListarTarifasAsync();

    public async Task<Tarifa> GuardarTarifaAsync(int? id, SolicitudDeTarifa solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitud.Nombre);

        if (solicitud.Importe <= 0)
        {
            throw new ReglaDeNegocioException("El importe debe ser mayor que cero.", "importe");
        }

        if (solicitud.Tipo == TipoDeTarifa.Programada)
        {
            if (solicitud.VigenciaDesde is null || solicitud.VigenciaHasta is null)
            {
                throw new ReglaDeNegocioException(
                    "Indique el rango de vigencia de la tarifa programada.",
                    "vigencia");
            }

            if (solicitud.VigenciaHasta < solicitud.VigenciaDesde)
            {
                throw new ReglaDeNegocioException(
                    "La fecha final de vigencia no puede ser anterior a la inicial.",
                    "vigencia");
            }

            await ValidarSuperposicionAsync(id, solicitud);
        }

        Tarifa? existente = null;

        if (id is not null)
        {
            existente = await _configuracion.BuscarTarifaAsync(id.Value)
                ?? throw new ReglaDeNegocioException("La tarifa ya no existe.", "id");
        }
        else if (solicitud.Tipo == TipoDeTarifa.Regular)
        {
            var tarifas = await _configuracion.ListarTarifasAsync();
            existente = tarifas.FirstOrDefault(t =>
                t.Tipo == TipoDeTarifa.Regular && t.Sentido == solicitud.Sentido);
        }

        var anterior = existente is null ? null : JsonSerializer.Serialize(existente.ADatos());
        var tarifa = existente ?? new Tarifa();
        tarifa.Tipo = solicitud.Tipo;
        tarifa.Nombre = solicitud.Nombre.Trim();
        tarifa.Importe = solicitud.Importe;
        tarifa.VigenciaDesde = solicitud.Tipo == TipoDeTarifa.Programada ? solicitud.VigenciaDesde : null;
        tarifa.VigenciaHasta = solicitud.Tipo == TipoDeTarifa.Programada ? solicitud.VigenciaHasta : null;
        tarifa.Sentido = solicitud.Sentido;
        tarifa.UsuarioId = _contexto.IdUsuario;
        tarifa.FechaHora = _reloj.Ahora;

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            if (tarifa.Id == 0)
            {
                tarifa.Id = await _configuracion.AgregarTarifaAsync(tarifa);
            }
            else
            {
                await _configuracion.ActualizarTarifaAsync(tarifa);
            }

            var nueva = await _configuracion.BuscarTarifaAsync(tarifa.Id)
                ?? throw new InvalidOperationException("No se encontró la tarifa guardada.");

            await _bitacora.RegistrarAsync(
                existente is null ? "Crear tarifa" : "Modificar tarifa",
                anterior,
                JsonSerializer.Serialize(nueva.ADatos()));
        });

        return await _configuracion.BuscarTarifaAsync(tarifa.Id)
            ?? throw new InvalidOperationException("No se encontró la tarifa guardada.");
    }

    public async Task EliminarTarifaAsync(int id)
    {
        var tarifa = await _configuracion.BuscarTarifaAsync(id)
            ?? throw new ReglaDeNegocioException("La tarifa ya no existe.", "id");

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _configuracion.EliminarTarifaAsync(id);
            await _bitacora.RegistrarAsync(
                "Eliminar tarifa",
                JsonSerializer.Serialize(tarifa.ADatos()),
                null);
        });
    }

    public async Task<Tarifa> ObtenerTarifaVigenteAsync(DateOnly fecha, SentidoDelViaje sentido)
    {
        var tarifas = await _configuracion.ListarTarifasAsync();

        var programada = tarifas
            .Where(t => t.Tipo == TipoDeTarifa.Programada && t.EstaVigente(fecha))
            .Where(t => t.Sentido == sentido || t.Sentido == SentidoDelViaje.Ambos)
            .OrderByDescending(t => t.Sentido == sentido)
            .ThenByDescending(t => t.FechaHora)
            .FirstOrDefault();

        if (programada is not null)
        {
            return programada;
        }

        return tarifas
            .Where(t => t.Tipo == TipoDeTarifa.Regular)
            .Where(t => t.Sentido == sentido || t.Sentido == SentidoDelViaje.Ambos)
            .OrderByDescending(t => t.Sentido == sentido)
            .FirstOrDefault()
            ?? throw new ReglaDeNegocioException(
                "No hay una tarifa vigente para ese sentido.",
                "tarifa");
    }

    public Task<IReadOnlyList<TipoDeVehiculo>> ListarTiposDeVehiculoAsync() =>
        _configuracion.ListarTiposDeVehiculoAsync();

    public Task<IReadOnlyList<ParametroDelNegocio>> ListarParametrosAsync() =>
        _configuracion.ListarParametrosAsync();

    public async Task<ParametroDelNegocio> GuardarParametroAsync(
        string nombre,
        SolicitudDeParametro solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        var parametro = new ParametroDelNegocio
        {
            Nombre = nombre.Trim(),
            Valor = solicitud.Valor.Trim(),
            UsuarioId = _contexto.IdUsuario,
            FechaHora = _reloj.Ahora
        };

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _configuracion.GuardarParametroAsync(parametro);
            await _bitacora.RegistrarAsync(
                "Cambio de parámetro",
                null,
                JsonSerializer.Serialize(new { parametro.Nombre, parametro.Valor }));
        });

        return parametro;
    }

    private async Task ValidarSuperposicionAsync(int? id, SolicitudDeTarifa solicitud)
    {
        var tarifas = await _configuracion.ListarTarifasAsync();

        var superpuesta = tarifas.FirstOrDefault(t =>
            t.Tipo == TipoDeTarifa.Programada &&
            t.Id != (id ?? 0) &&
            (t.Sentido == solicitud.Sentido || t.Sentido == SentidoDelViaje.Ambos ||
             solicitud.Sentido == SentidoDelViaje.Ambos) &&
            t.VigenciaDesde is not null &&
            t.VigenciaHasta is not null &&
            solicitud.VigenciaDesde <= t.VigenciaHasta &&
            solicitud.VigenciaHasta >= t.VigenciaDesde);

        if (superpuesta is not null)
        {
            throw new ReglaDeNegocioException(
                $"Las fechas se cruzan con la tarifa programada \"{superpuesta.Nombre}\".",
                "vigencia");
        }
    }
}
