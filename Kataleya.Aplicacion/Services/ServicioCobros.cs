using System.Text.Json;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Kataleya.Dominio.ValueObjects;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioCobros
{
    private const string OperacionCobrarSaldo = "Cobrar saldo del pasaje";

    private readonly IRepositorioPasajes _pasajes;
    private readonly IRepositorioPagos _pagos;
    private readonly IRepositorioBoletos _boletos;
    private readonly HidratadorDePasajes _hidratador;
    private readonly ServicioBitacora _bitacora;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public ServicioCobros(
        IRepositorioPasajes pasajes,
        IRepositorioPagos pagos,
        IRepositorioBoletos boletos,
        HidratadorDePasajes hidratador,
        ServicioBitacora bitacora,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _pasajes = pasajes ?? throw new ArgumentNullException(nameof(pasajes));
        _pagos = pagos ?? throw new ArgumentNullException(nameof(pagos));
        _boletos = boletos ?? throw new ArgumentNullException(nameof(boletos));
        _hidratador = hidratador ?? throw new ArgumentNullException(nameof(hidratador));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public async Task<Pasaje> RegistrarCobroDelSaldoAsync(long pasajeId, SolicitudDeCobro solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var actor = Actor;
        var pasaje = await _pasajes.BuscarAsync(pasajeId)
            ?? throw new ReglaDeNegocioException(
                "No se encontró el pasaje.",
                "id");

        if (pasaje.GrupoId is not null)
        {
            throw new ReglaDeNegocioException(
                "Los pagos de un grupo se registran junto con el grupo.",
                "pasajeId");
        }

        pasaje = await _hidratador.HidratarAsync(pasaje);

        if (await YaSeRegistroAsync())
        {
            return pasaje;
        }

        if (pasaje.Estado is EstadoPasaje.Anulado or EstadoPasaje.NoSePresento)
        {
            throw new ReglaDeNegocioException(
                "El pasaje no admite cobros en su estado actual.",
                "estado");
        }

        if (pasaje.Estado == EstadoPasaje.Pagado)
        {
            throw new ReglaDeNegocioException(
                "El pasaje ya está pagado.",
                "estado");
        }

        if (solicitud.Monto <= 0)
        {
            throw new ReglaDeNegocioException(
                "Escriba un importe mayor que cero.",
                "monto");
        }

        if (decimal.Round(solicitud.Monto, 2) != solicitud.Monto)
        {
            throw new ReglaDeNegocioException(
                "El importe solo puede tener dos decimales.",
                "monto");
        }

        if (solicitud.MedioDePago is not (MedioPago.Efectivo or MedioPago.Yape))
        {
            throw new ReglaDeNegocioException(
                "Elija Efectivo o Yape.",
                "medioDePago");
        }

        if (solicitud.Monto > pasaje.Saldo)
        {
            throw new ReglaDeNegocioException(
                "El importe supera el saldo pendiente del pasaje.",
                "monto");
        }

        var ahora = _reloj.Ahora;
        var pago = new Pago(
            pasaje.Id,
            null,
            solicitud.Monto,
            solicitud.MedioDePago,
            TipoDePago.Saldo,
            ahora,
            actor);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _pagos.RegistrarAsync(pago);
            pasaje.AgregarPago(pago);
            await _pasajes.ActualizarEstadoAsync(pasaje.Id, pasaje.Estado);

            if (pasaje.Estado == EstadoPasaje.Pagado && pasaje.Boleto is null)
            {
                var boleto = await _boletos.EmitirAsync(
                    pasaje.Id,
                    NumeroDeBoleto.SeriePredeterminada,
                    ahora,
                    actor.Id,
                    actor.Nombre);

                pasaje.FijarBoleto(boleto);
            }

            await RegistrarBitacoraAsync(pasaje, solicitud);
        });

        return pasaje;
    }

    private Usuario Actor =>
        _contexto.Usuario
        ?? throw new InvalidOperationException("No se encontró el usuario de la sesión.");

    private Task<bool> YaSeRegistroAsync() =>
        _contexto.ClaveDeOperacion is Guid clave
            ? _bitacora.ExisteAsync(clave)
            : Task.FromResult(false);

    private async Task RegistrarBitacoraAsync(Pasaje pasaje, SolicitudDeCobro solicitud)
    {
        var guardado = new PasajeGuardado(
            pasaje.Id,
            pasaje.UnidadDeSalidaId,
            pasaje.GrupoId,
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
            pasaje.Importe,
            pasaje.Estado,
            pasaje.TipoRegistro,
            pasaje.EmisorId,
            pasaje.Emisor,
            pasaje.EsMenor,
            pasaje.NombrePadreMadre,
            pasaje.MotivoMenor,
            pasaje.EsResponsable,
            pasaje.CodigoDeGrupo,
            pasaje.MotivoDeAnulacion,
            pasaje.FechaDeAnulacion,
            pasaje.EsDiferido);

        await _bitacora.RegistrarAsync(new RegistroDeBitacora(
            0,
            _reloj.Ahora,
            OperacionCobrarSaldo,
            Actor,
            null,
            JsonSerializer.Serialize(new
            {
                pasaje = guardado,
                solicitud.Monto,
                solicitud.MedioDePago
            }),
            pasajeId: pasaje.Id));
    }
}
