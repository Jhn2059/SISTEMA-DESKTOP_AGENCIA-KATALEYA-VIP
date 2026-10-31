using System.Globalization;
using System.Text.Json;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioTarifas
{
    private const string OperacionFijarRegular = "Fijar tarifa regular";
    private const string OperacionGuardarProgramada = "Guardar tarifa programada";
    private const string OperacionEditarProgramada = "Editar tarifa programada";
    private const string OperacionQuitarProgramada = "Quitar tarifa programada";

    private readonly IRepositorioTarifas _tarifas;
    private readonly ServicioBitacora _bitacora;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public ServicioTarifas(
        IRepositorioTarifas tarifas,
        ServicioBitacora bitacora,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _tarifas = tarifas ?? throw new ArgumentNullException(nameof(tarifas));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public Task<IReadOnlyList<Tarifa>> ListarTarifasAsync() =>
        _tarifas.ListarAsync();

    public async Task<Tarifa> ObtenerTarifaVigenteAsync(DateOnly fecha, Sentido sentido) =>
        await _tarifas.BuscarVigenteAsync(fecha, sentido)
        ?? throw new ReglaDeNegocioException(
            "No hay una tarifa vigente para ese sentido.",
            "tarifa");

    public async Task FijarTarifaRegularAsync(Sentido sentido, decimal importe)
    {
        if (await YaSeRegistroAsync())
        {
            return;
        }

        var actor = Actor;
        var existente = (await _tarifas.ListarAsync())
            .OfType<TarifaRegular>()
            .FirstOrDefault(tarifa => tarifa.Sentido == sentido);

        var tarifa = existente is null
            ? new TarifaRegular(sentido, importe, actor)
            : (TarifaRegular)Tarifa.Reconstruir(new TarifaGuardada(
                existente.Id,
                TipoDeTarifa.Regular,
                null,
                importe,
                null,
                actor.Id,
                actor.Nombre,
                sentido));

        Validar(tarifa);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _tarifas.GuardarAsync(tarifa);
            await RegistrarBitacoraAsync(
                OperacionFijarRegular,
                existente is null ? null : Serializar(existente),
                Serializar(tarifa));
        });
    }

    public async Task<Tarifa> GuardarTarifaAsync(SolicitudDeTarifa solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.Vigencia is null)
        {
            throw new ReglaDeNegocioException("Escriba la vigencia de la tarifa.", "vigencia");
        }

        var actor = Actor;
        var tarifa = new TarifaProgramada(solicitud, actor);
        Validar(tarifa);

        if (await YaSeRegistroAsync())
        {
            return await BuscarProgramadaGuardadaAsync(solicitud)
                ?? throw new InvalidOperationException(
                    "La operación ya se registró, pero no se encontró la tarifa guardada.");
        }

        var cruce = (await _tarifas.ListarAsync())
            .OfType<TarifaProgramada>()
            .FirstOrDefault(existente => SeCruzan(existente, tarifa));

        if (cruce is not null)
        {
            throw TarifaCruzada(cruce);
        }

        var id = 0;

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            id = await _tarifas.GuardarAsync(tarifa);
            await RegistrarBitacoraAsync(
                OperacionGuardarProgramada,
                null,
                Serializar(tarifa));
        });

        return Tarifa.Reconstruir(new TarifaGuardada(
            id,
            TipoDeTarifa.Programada,
            tarifa.Nombre,
            tarifa.Importe,
            tarifa.Vigencia,
            actor.Id,
            actor.Nombre,
            tarifa.Sentido));
    }

    public async Task<Tarifa> ActualizarTarifaAsync(int id, SolicitudDeTarifa solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.Vigencia is null)
        {
            throw new ReglaDeNegocioException("Escriba la vigencia de la tarifa.", "vigencia");
        }

        var actual = (await _tarifas.ListarAsync())
            .FirstOrDefault(item => item.Id == id)
            ?? throw new ReglaDeNegocioException("La tarifa ya no existe.", "id");

        if (actual is not TarifaProgramada programada)
        {
            throw new ReglaDeNegocioException(
                "Solo se puede editar una tarifa programada.",
                "id");
        }

        if (await YaSeRegistroAsync())
        {
            return actual;
        }

        var actor = Actor;
        var tarifa = (TarifaProgramada)Tarifa.Reconstruir(new TarifaGuardada(
            id,
            TipoDeTarifa.Programada,
            solicitud.Nombre,
            solicitud.Importe,
            solicitud.Vigencia,
            actor.Id,
            actor.Nombre,
            solicitud.Sentido));

        Validar(tarifa);

        var cruce = (await _tarifas.ListarAsync())
            .OfType<TarifaProgramada>()
            .FirstOrDefault(existente => existente.Id != id && SeCruzan(existente, tarifa));

        if (cruce is not null)
        {
            throw TarifaCruzada(cruce);
        }

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _tarifas.GuardarAsync(tarifa);
            await RegistrarBitacoraAsync(
                OperacionEditarProgramada,
                Serializar(programada),
                Serializar(tarifa));
        });

        return tarifa;
    }

    public async Task QuitarTarifaAsync(int id)
    {
        if (await YaSeRegistroAsync())
        {
            return;
        }

        var tarifa = (await _tarifas.ListarAsync())
            .FirstOrDefault(item => item.Id == id)
            ?? throw new ReglaDeNegocioException("La tarifa ya no existe.", "id");

        if (tarifa is not TarifaProgramada programada)
        {
            throw new ReglaDeNegocioException(
                "Solo se puede quitar una tarifa programada.",
                "id");
        }

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _tarifas.QuitarAsync(programada);
            await RegistrarBitacoraAsync(
                OperacionQuitarProgramada,
                Serializar(programada),
                null);
        });
    }

    private Usuario Actor =>
        _contexto.Usuario
        ?? throw new InvalidOperationException("No se encontró el usuario de la sesión.");

    private async Task<bool> YaSeRegistroAsync() =>
        _contexto.ClaveDeOperacion is Guid clave &&
        await _bitacora.ExisteAsync(clave);

    private async Task<Tarifa?> BuscarProgramadaGuardadaAsync(SolicitudDeTarifa solicitud) =>
        (await _tarifas.ListarAsync())
            .OfType<TarifaProgramada>()
            .FirstOrDefault(tarifa =>
                tarifa.Nombre.Equals(solicitud.Nombre.Trim(), StringComparison.Ordinal) &&
                tarifa.Importe == solicitud.Importe &&
                tarifa.Vigencia == solicitud.Vigencia &&
                tarifa.Sentido == solicitud.Sentido);

    private static void Validar(Tarifa tarifa)
    {
        var error = tarifa.Validar().FirstOrDefault();
        if (error is not null)
        {
            throw new ReglaDeNegocioException(error.Mensaje, error.Campo);
        }
    }

    private static bool SeCruzan(TarifaProgramada primera, TarifaProgramada segunda)
    {
        var sentidosSeCruzan =
            primera.Sentido is null ||
            segunda.Sentido is null ||
            primera.Sentido == segunda.Sentido;

        var fechasSeCruzan =
            primera.Vigencia.Desde <= segunda.Vigencia.Hasta &&
            segunda.Vigencia.Desde <= primera.Vigencia.Hasta;

        return sentidosSeCruzan && fechasSeCruzan;
    }

    private static ReglaDeNegocioException TarifaCruzada(TarifaProgramada cruce) =>
        new(
            $"Esas fechas se cruzan con la tarifa «{cruce.Nombre}» " +
            $"({cruce.Vigencia.Desde.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} a " +
            $"{cruce.Vigencia.Hasta.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}) " +
            "del mismo sentido. Cambie las fechas o quite esa tarifa.",
            "vigencia");

    private async Task RegistrarBitacoraAsync(
        string operacion,
        string? anterior,
        string? nuevo) =>
        await _bitacora.RegistrarAsync(new RegistroDeBitacora(
            0,
            _reloj.Ahora,
            operacion,
            Actor,
            anterior,
            nuevo));

    private static string Serializar(Tarifa tarifa)
    {
        var guardado = tarifa switch
        {
            TarifaRegular regular => new TarifaGuardada(
                regular.Id,
                TipoDeTarifa.Regular,
                null,
                regular.Importe,
                null,
                regular.IdUsuario,
                regular.FijadaPor,
                regular.Sentido),
            TarifaProgramada programada => new TarifaGuardada(
                programada.Id,
                TipoDeTarifa.Programada,
                programada.Nombre,
                programada.Importe,
                programada.Vigencia,
                programada.IdUsuario,
                programada.FijadaPor,
                programada.Sentido),
            _ => throw new ArgumentOutOfRangeException(nameof(tarifa), "El tipo de tarifa no es válido.")
        };

        return JsonSerializer.Serialize(guardado);
    }
}
