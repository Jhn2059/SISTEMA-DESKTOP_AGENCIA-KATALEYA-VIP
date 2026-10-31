using Kataleya.Aplicacion.Excepciones;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioPasajes
{
    private const string OperacionRegistrarPasaje = "Registrar pasaje";

    private readonly IRepositorioPasajes _pasajes;
    private readonly IRepositorioUnidadesSalida _unidadesSalida;
    private readonly IRepositorioTarifas _tarifas;
    private readonly ServicioBitacora _bitacora;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public ServicioPasajes(
        IRepositorioPasajes pasajes,
        IRepositorioUnidadesSalida unidadesSalida,
        IRepositorioTarifas tarifas,
        ServicioBitacora bitacora,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _pasajes = pasajes ?? throw new ArgumentNullException(nameof(pasajes));
        _unidadesSalida = unidadesSalida ?? throw new ArgumentNullException(nameof(unidadesSalida));
        _tarifas = tarifas ?? throw new ArgumentNullException(nameof(tarifas));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public async Task<IReadOnlyList<UnidadSalidaDisponible>> ListarUnidadesDisponiblesAsync(DateOnly fecha) =>
        await _unidadesSalida.ListarDisponiblesAsync(fecha);

    public async Task<long> RegistrarPasajeAsync(SolicitudDeRegistroPasaje solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var actor = Actor;
        var unidad = await _unidadesSalida.ObtenerAsync(solicitud.UnidadSalidaId)
            ?? throw new ReglaDeNegocioException("No se encontró la unidad de salida.", "unidadSalidaId");

        if (unidad.Estado != "Programada")
        {
            throw new ReglaDeNegocioException("La unidad de salida no está programada.", "unidadSalidaId");
        }

        var fechaLima = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("America/Lima"));

        var fechaSalida = DateTime.SpecifyKind(solicitud.FechaHoraSalida, DateTimeKind.Utc);
        if (fechaSalida <= fechaLima)
        {
            throw new ReglaDeNegocioException("La salida debe ser desde ahora hasta dentro de 7 días.", "fechaHoraSalida");
        }

        var sieteDias = fechaLima.AddDays(7);
        if (fechaSalida > sieteDias)
        {
            throw new ReglaDeNegocioException("La salida debe ser desde ahora hasta dentro de 7 días.", "fechaHoraSalida");
        }

        var asientoOcupado = await _pasajes.AsientoEstaOcupadoAsync(
            solicitud.UnidadSalidaId,
            solicitud.NumeroAsiento);
        if (asientoOcupado)
        {
            throw new ReglaDeNegocioException("Ese asiento ya está ocupado para esta salida.", "numeroAsiento");
        }

        var tarifa = await _tarifas.BuscarVigenteAsync(
            DateOnly.FromDateTime(fechaSalida),
            unidad.Sentido)
            ?? throw new ReglaDeNegocioException("No hay una tarifa vigente para ese sentido y fecha.", VAL01);

        var pasaje = new Pasaje(solicitud, solicitud.NumeroAsiento, actor);

        var errores = pasaje.ValidarNombreYDocumento();
        errores.AddRange(pasaje.ValidarContactoYRuta());
        errores.AddRange(pasaje.ValidarFechaYAsiento());
        errores.AddRange(pasaje.ValidarMenor());
        errores.AddRange(pasaje.ValidarAdelanto());

        var primerError = errores.FirstOrDefault();
        if (primerError is not null)
        {
            throw new ReglaDeNegocioException(primerError.Mensaje, primerError.Campo);
        }

        var id = 0L;
        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            id = await _pasajes.RegistrarPasajeAsync(pasaje);
            await RegistrarBitacoraAsync(OperacionRegistrarPasaje, null, Serializar(pasaje));
        });

        return id;
    }

    public async Task<Pasaje?> ObtenerPasajeAsync(long id) =>
        await _pasajes.ObtenerPasajeAsync(id);

    private Usuario Actor =>
        _contexto.Usuario
        ?? throw new InvalidOperationException("No se encontró el usuario de la sesión.");

    private const string VAL01 = "tarifa";

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

    private static string Serializar(Pasaje pasaje)
    {
        var guardado = new PasajeGuardado(
            pasaje.Id,
            pasaje.UnidadSalidaId,
            pasaje.NumeroAsiento,
            pasaje.NombrePasajero,
            pasaje.TipoDocumento,
            pasaje.NumeroDocumento,
            pasaje.Celular,
            pasaje.Origen,
            pasaje.Destino,
            pasaje.LugarEmbarque,
            pasaje.FechaHoraSalida,
            pasaje.FechaVenta,
            pasaje.Adelanto,
            pasaje.Estado,
            pasaje.IdUsuario,
            pasaje.VendidaPor,
            pasaje.EsMenor,
            pasaje.NombrePadreMadre,
            pasaje.MotivoMenor);

        return System.Text.Json.JsonSerializer.Serialize(guardado);
    }
}