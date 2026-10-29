using System.Globalization;
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
    private const string Horarios = "horarios_salida";
    private const string PlazoDeReserva = "plazo_maximo_reserva_dias";
    private const string AvisoPrevio = "aviso_previo_horas";
    private static readonly string[] FormatosDeHora = ["H:mm", "HH:mm"];

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

        if (string.IsNullOrWhiteSpace(solicitud.Nombre))
        {
            throw new ReglaDeNegocioException("Escriba el nombre de la tarifa.", "nombre");
        }

        if (solicitud.Importe <= 0)
        {
            throw new ReglaDeNegocioException("Escriba un importe mayor que cero.", "importe");
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
                    "La fecha final no puede ser anterior a la inicial.",
                    "vigenciaHasta");
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

    // Una tarifa regular por sentido, que se cambia en su propia tarjeta (RF-29).
    public Task<Tarifa> FijarTarifaRegularAsync(SentidoDelViaje sentido, decimal importe)
    {
        if (sentido == SentidoDelViaje.Ambos)
        {
            throw new ReglaDeNegocioException("Elija Huancayo → Ica o Ica → Huancayo.", "sentido");
        }

        return GuardarTarifaAsync(null, new SolicitudDeTarifa
        {
            Tipo = TipoDeTarifa.Regular,
            Nombre = sentido == SentidoDelViaje.Ida
                ? "Tarifa regular Huancayo - Ica"
                : "Tarifa regular Ica - Huancayo",
            Importe = importe,
            Sentido = sentido
        });
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

        var valor = solicitud.Valor.Trim();

        switch (nombre.Trim())
        {
            case PlazoDeReserva:
                ValidarEntero(valor, 1, 30, "Escriba un plazo de 1 a 30 días.", nombre);
                break;
            case AvisoPrevio:
                ValidarEntero(valor, 0, 24, "Escriba un aviso de 0 a 24 horas.", nombre);
                break;
            case Horarios:
                valor = string.Join(",", LeerHorariosEscritos(valor));
                break;
        }

        return await GuardarValorAsync(nombre.Trim(), valor, "Cambio de parámetro");
    }

    // CDU-16 A1: el horario vale para los dos sentidos y se guarda en orden.
    public async Task<ParametroDelNegocio> AgregarHorarioAsync(string hora)
    {
        if (!IntentarLeerHora(hora, out var nueva))
        {
            throw new ReglaDeNegocioException(
                "Escriba la hora en formato de 24 horas, por ejemplo 07:30.",
                "hora");
        }

        var horarios = await ListarHorariosAsync();

        if (horarios.Contains(nueva))
        {
            throw new ReglaDeNegocioException("Ese horario ya existe.", "hora");
        }

        horarios.Add(nueva);
        return await GuardarValorAsync(Horarios, UnirHorarios(horarios), "Agregar horario");
    }

    // CDU-16 A3: los pasajes ya registrados en ese horario lo conservan.
    public async Task<ParametroDelNegocio> QuitarHorarioAsync(string hora)
    {
        var horarios = await ListarHorariosAsync();

        if (!IntentarLeerHora(hora, out var quitada) || !horarios.Contains(quitada))
        {
            throw new ReglaDeNegocioException("Ese horario ya no existe.", "hora");
        }

        if (horarios.Count == 1)
        {
            throw new ReglaDeNegocioException("Debe quedar al menos un horario de salida.", "hora");
        }

        horarios.Remove(quitada);
        return await GuardarValorAsync(Horarios, UnirHorarios(horarios), "Quitar horario");
    }

    private async Task<ParametroDelNegocio> GuardarValorAsync(string nombre, string valor, string operacion)
    {
        var parametros = await _configuracion.ListarParametrosAsync();
        var anterior = parametros.FirstOrDefault(p => p.Nombre == nombre)?.Valor;

        var parametro = new ParametroDelNegocio
        {
            Nombre = nombre,
            Valor = valor,
            UsuarioId = _contexto.IdUsuario,
            FechaHora = _reloj.Ahora
        };

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _configuracion.GuardarParametroAsync(parametro);
            await _bitacora.RegistrarAsync(
                operacion,
                anterior is null ? null : JsonSerializer.Serialize(new { Nombre = nombre, Valor = anterior }),
                JsonSerializer.Serialize(new { parametro.Nombre, parametro.Valor }));
        });

        return parametro;
    }

    private async Task<SortedSet<TimeOnly>> ListarHorariosAsync()
    {
        var parametros = await _configuracion.ListarParametrosAsync();
        var valor = parametros.FirstOrDefault(p => p.Nombre == Horarios)?.Valor ?? string.Empty;
        var horarios = new SortedSet<TimeOnly>();

        foreach (var texto in valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IntentarLeerHora(texto, out var hora))
            {
                horarios.Add(hora);
            }
        }

        return horarios;
    }

    private static IEnumerable<string> LeerHorariosEscritos(string valor)
    {
        var horarios = new SortedSet<TimeOnly>();

        foreach (var texto in valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!IntentarLeerHora(texto, out var hora))
            {
                throw new ReglaDeNegocioException(
                    "Escriba la hora en formato de 24 horas, por ejemplo 07:30.",
                    "hora");
            }

            horarios.Add(hora);
        }

        if (horarios.Count == 0)
        {
            throw new ReglaDeNegocioException("Debe quedar al menos un horario de salida.", "hora");
        }

        return horarios.Select(hora => hora.ToString("HH:mm", CultureInfo.InvariantCulture));
    }

    private static string UnirHorarios(IEnumerable<TimeOnly> horarios) =>
        string.Join(",", horarios.Select(hora => hora.ToString("HH:mm", CultureInfo.InvariantCulture)));

    private static bool IntentarLeerHora(string? texto, out TimeOnly hora) =>
        TimeOnly.TryParseExact(
            (texto ?? string.Empty).Trim(),
            FormatosDeHora,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out hora);

    private static void ValidarEntero(string valor, int minimo, int maximo, string mensaje, string campo)
    {
        if (!int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ||
            numero < minimo ||
            numero > maximo)
        {
            throw new ReglaDeNegocioException(mensaje, campo);
        }
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
                $"Esas fechas se cruzan con la tarifa «{superpuesta.Nombre}» " +
                $"({superpuesta.VigenciaDesde?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} a " +
                $"{superpuesta.VigenciaHasta?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}) " +
                "del mismo sentido. Cambie las fechas o quite esa tarifa.",
                "vigenciaDesde");
        }
    }
}
