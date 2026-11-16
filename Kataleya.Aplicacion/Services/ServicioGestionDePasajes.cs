using System.Text.Json;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Kataleya.Dominio.ValueObjects;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioGestionDePasajes
{
    private const string OperacionModificar = "Modificar pasaje";
    private const string OperacionAnular = "Anular pasaje";
    private const string OperacionNoSePresento = "Marcar no se presentó";

    private readonly IRepositorioPasajes _pasajes;
    private readonly IRepositorioPagos _pagos;
    private readonly IRepositorioUnidadesSalida _unidades;
    private readonly IRepositorioParametros _parametros;
    private readonly ServicioParametros _servicioParametros;
    private readonly HidratadorDePasajes _hidratador;
    private readonly ServicioBitacora _bitacora;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public ServicioGestionDePasajes(
        IRepositorioPasajes pasajes,
        IRepositorioPagos pagos,
        IRepositorioUnidadesSalida unidades,
        IRepositorioParametros parametros,
        ServicioParametros servicioParametros,
        HidratadorDePasajes hidratador,
        ServicioBitacora bitacora,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _pasajes = pasajes ?? throw new ArgumentNullException(nameof(pasajes));
        _pagos = pagos ?? throw new ArgumentNullException(nameof(pagos));
        _unidades = unidades ?? throw new ArgumentNullException(nameof(unidades));
        _parametros = parametros ?? throw new ArgumentNullException(nameof(parametros));
        _servicioParametros = servicioParametros ?? throw new ArgumentNullException(nameof(servicioParametros));
        _hidratador = hidratador ?? throw new ArgumentNullException(nameof(hidratador));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public async Task<ResultadoDeModificacionDePasaje> ModificarAsync(
        long pasajeId,
        SolicitudDeModificacionDePasaje solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var actor = Actor;
        var pasaje = await ObtenerHidratadoAsync(pasajeId);
        var requiereReimpresion = pasaje.Boleto is not null;

        if (await YaSeRegistroAsync())
        {
            return new ResultadoDeModificacionDePasaje(pasaje, requiereReimpresion);
        }

        if (pasaje.GrupoId is not null || pasaje.EsDeGrupo)
        {
            throw new ReglaDeNegocioException(
                "Los pasajes de un grupo se administran junto con el grupo.",
                "pasajeId");
        }

        if (pasaje.Estado == EstadoPasaje.Anulado)
        {
            throw new ReglaDeNegocioException(
                "Un pasaje anulado no se puede modificar.",
                "estado");
        }

        if (pasaje.Estado == EstadoPasaje.NoSePresento)
        {
            throw new ReglaDeNegocioException(
                "Un pasaje marcado como No se presentó no se puede modificar.",
                "estado");
        }

        var ahora = _reloj.Ahora;
        if (pasaje.FechaHoraSalida <= ahora)
        {
            throw new ReglaDeNegocioException(
                "El pasaje ya salió y no se puede modificar.",
                "fechaHoraSalida");
        }

        var unidad = await ObtenerUnidadAsync(pasaje, solicitud.UnidadDeSalidaId);
        var plazo = await _servicioParametros.ObtenerPlazoDeReservaAsync();

        if (unidad.FechaHoraSalida <= ahora ||
            unidad.FechaHoraSalida > ahora.AddDays(plazo))
        {
            throw new ReglaDeNegocioException(
                $"La salida debe ser desde ahora hasta dentro de {plazo} días.",
                "fechaHoraSalida");
        }

        var nuevoAsiento = solicitud.NumeroAsiento ?? pasaje.NumeroAsiento ?? 0;
        var nuevoLugar = solicitud.LugarEmbarque ?? pasaje.LugarEmbarque;
        var nuevoNombre = solicitud.NombrePasajero ?? pasaje.NombrePasajero;
        var nuevoTipoDocumento = solicitud.TipoDocumento ?? pasaje.TipoDocumento;
        var nuevoNumeroDocumento = solicitud.NumeroDocumento ?? pasaje.NumeroDocumento;
        var nuevoCelular = solicitud.Celular ?? pasaje.Celular;
        var nuevoEsMenor = solicitud.EsMenor ?? pasaje.EsMenor;
        var nuevoPadreMadre = solicitud.NombrePadreMadre ?? pasaje.NombrePadreMadre;
        var nuevoMotivoMenor = solicitud.MotivoMenor ?? pasaje.MotivoMenor;

        if (!nuevoEsMenor)
        {
            nuevoPadreMadre = null;
            nuevoMotivoMenor = null;
        }

        var cambioDeFechaUHorario = unidad.FechaHoraSalida != pasaje.FechaHoraSalida;
        var avisoPrevio = await _servicioParametros.ObtenerAvisoPrevioAsync();
        var montoAdicional = solicitud.MontoAdicional ?? 0;

        if (montoAdicional < 0)
        {
            throw new ReglaDeNegocioException(
                "El cobro adicional no puede ser negativo.",
                "montoAdicional");
        }

        if (montoAdicional > 0 && !cambioDeFechaUHorario)
        {
            throw new ReglaDeNegocioException(
                "El cobro adicional solo corresponde cuando cambia la fecha o el horario.",
                "montoAdicional");
        }

        if (montoAdicional > 0 &&
            pasaje.FechaHoraSalida >= ahora.Add(avisoPrevio))
        {
            throw new ReglaDeNegocioException(
                "El cambio de fecha u horario no tiene cobro adicional con el aviso previo requerido.",
                "montoAdicional");
        }

        if (montoAdicional > 0 && solicitud.MedioDePago is null)
        {
            throw new ReglaDeNegocioException(
                "Elija el medio de pago del cobro adicional.",
                "medioDePago");
        }

        var nuevoImporte = pasaje.Importe + montoAdicional;
        var candidato = Pasaje.Reconstruir(new PasajeGuardado(
            pasaje.Id,
            unidad.Id,
            pasaje.GrupoId,
            nuevoAsiento,
            nuevoNombre,
            nuevoTipoDocumento,
            nuevoNumeroDocumento,
            nuevoCelular,
            pasaje.Origen,
            pasaje.Destino,
            nuevoLugar,
            unidad.FechaHoraSalida,
            pasaje.FechaVenta,
            nuevoImporte,
            pasaje.Estado,
            pasaje.TipoRegistro,
            pasaje.EmisorId,
            pasaje.Emisor,
            nuevoEsMenor,
            nuevoPadreMadre,
            nuevoMotivoMenor,
            pasaje.EsResponsable,
            pasaje.CodigoDeGrupo,
            pasaje.MotivoDeAnulacion,
            pasaje.FechaDeAnulacion,
            pasaje.EsDiferido));

        foreach (var pago in pasaje.Pagos.Where(pago => !pago.EsDevolucion))
        {
            candidato.AgregarPago(pago);
        }

        Pago? pagoAdicional = null;
        if (montoAdicional > 0)
        {
            pagoAdicional = new Pago(
                pasaje.Id,
                null,
                montoAdicional,
                solicitud.MedioDePago!.Value,
                TipoDePago.Adicional,
                ahora,
                actor);

            ValidarPago(pagoAdicional);
            candidato.AgregarPago(pagoAdicional);
        }

        var tipo = await _parametros.BuscarTipoDeVehiculoAsync(unidad.TipoVehiculoId)
            ?? throw new ReglaDeNegocioException(
                "No se encontró el tipo de vehículo de la unidad.",
                "unidadDeSalidaId");

        var errores = new List<ErrorDeCampo>();
        errores.AddRange(candidato.ValidarDatosDelPasajero());
        errores.AddRange(candidato.ValidarContacto());
        errores.AddRange(candidato.ValidarRuta());
        errores.AddRange(candidato.ValidarMenor());
        errores.AddRange(candidato.ValidarAsientoYVenta());

        if (nuevoAsiento < 1 || nuevoAsiento > tipo.CantidadDeAsientos)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "numeroAsiento",
                Mensaje = $"Elija un asiento entre 1 y {tipo.CantidadDeAsientos}."
            });
        }

        if (!DatosDelViaje.TryDeterminarSentido(
                candidato.Origen,
                candidato.Destino,
                out var sentido))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "destino",
                Mensaje = "El origen y el destino deben corresponder a Huancayo e Ica."
            });
        }
        else if (unidad.Sentido is null || sentido != unidad.Sentido)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "destino",
                Mensaje = "El origen y el destino no corresponden al sentido de la salida."
            });
        }

        var primerError = errores.FirstOrDefault();
        if (primerError is not null)
        {
            throw new ReglaDeNegocioException(primerError.Mensaje, primerError.Campo);
        }

        if (await _pasajes.AsientoEstaOcupadoAsync(
                unidad.Id,
                nuevoAsiento,
                pasaje.Id))
        {
            throw new ReglaDeNegocioException(
                "Ese asiento ya está ocupado para esta salida. Actualice el mapa de asientos.",
                "numeroAsiento");
        }

        var anterior = CrearGuardado(pasaje);
        var nuevo = CrearGuardado(candidato);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _pasajes.ActualizarAsync(candidato);

            if (pagoAdicional is not null)
            {
                await _pagos.RegistrarAsync(pagoAdicional);
            }

            await RegistrarBitacoraAsync(
                pasaje.Id,
                OperacionModificar,
                JsonSerializer.Serialize(anterior),
                JsonSerializer.Serialize(new
                {
                    pasaje = nuevo,
                    solicitud.MontoAdicional,
                    solicitud.MedioDePago,
                    RequiereReimpresion = requiereReimpresion
                }));
        });

        var actualizado = await _pasajes.BuscarAsync(pasaje.Id) ?? candidato;
        actualizado = await _hidratador.HidratarAsync(actualizado);

        return new ResultadoDeModificacionDePasaje(actualizado, requiereReimpresion);
    }

    public async Task<Pasaje> AnularAsync(
        long pasajeId,
        SolicitudDeAnulacionDePasaje solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var actor = Actor;
        var pasaje = await ObtenerHidratadoAsync(pasajeId);

        if (await YaSeRegistroAsync())
        {
            return pasaje;
        }

        if (pasaje.GrupoId is not null || pasaje.EsDeGrupo)
        {
            throw new ReglaDeNegocioException(
                "Los pasajes de un grupo se administran junto con el grupo.",
                "pasajeId");
        }

        if (pasaje.Estado == EstadoPasaje.Anulado)
        {
            throw new ReglaDeNegocioException(
                "El pasaje ya está anulado.",
                "estado");
        }

        if (pasaje.Estado == EstadoPasaje.NoSePresento)
        {
            throw new ReglaDeNegocioException(
                "El pasaje fue marcado como No se presentó y no se puede anular.",
                "estado");
        }

        var ahora = _reloj.Ahora;
        if (ahora >= pasaje.FechaHoraSalida)
        {
            throw new ReglaDeNegocioException(
                "Después de la salida solo puede marcar que el pasajero no se presentó.",
                "fechaHoraSalida");
        }

        var motivo = solicitud.Motivo?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ReglaDeNegocioException(
                "Escriba el motivo de la anulación.",
                "motivo");
        }

        if (motivo.Length > Pasaje.LongitudMaximaMotivoDeAnulacion)
        {
            throw new ReglaDeNegocioException(
                $"El motivo no puede superar los {Pasaje.LongitudMaximaMotivoDeAnulacion} caracteres.",
                "motivo");
        }

        var avisoPrevio = await _servicioParametros.ObtenerAvisoPrevioAsync();
        var correspondeDevolucion = pasaje.FechaHoraSalida >= ahora.Add(avisoPrevio);
        var devolver = pasaje.Pagado > 0 &&
            (correspondeDevolucion ||
             solicitud.EsCancelacionDeLaAgencia ||
             solicitud.DevolverComoExcepcion);

        var candidato = Pasaje.Reconstruir(new PasajeGuardado(
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
            EstadoPasaje.Anulado,
            pasaje.TipoRegistro,
            pasaje.EmisorId,
            pasaje.Emisor,
            pasaje.EsMenor,
            pasaje.NombrePadreMadre,
            pasaje.MotivoMenor,
            pasaje.EsResponsable,
            pasaje.CodigoDeGrupo,
            motivo,
            ahora,
            pasaje.EsDiferido));

        var devoluciones = new List<Pago>();
        if (devolver)
        {
            foreach (var pago in pasaje.Pagos.Where(pago => !pago.EsDevolucion))
            {
                var devolucion = new Pago(
                    pasaje.Id,
                    null,
                    pago.Monto,
                    pago.MedioDePago,
                    TipoDePago.Devolucion,
                    ahora,
                    actor);

                ValidarPago(devolucion);
                devoluciones.Add(devolucion);
            }
        }

        var anterior = CrearGuardado(pasaje);
        var nuevo = CrearGuardado(candidato);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _pasajes.ActualizarAsync(candidato);

            foreach (var devolucion in devoluciones)
            {
                await _pagos.RegistrarAsync(devolucion);
            }

            await RegistrarBitacoraAsync(
                pasaje.Id,
                OperacionAnular,
                JsonSerializer.Serialize(anterior),
                JsonSerializer.Serialize(new
                {
                    pasaje = nuevo,
                    DevolucionTotal = devoluciones.Sum(pago => pago.Monto),
                    solicitud.DevolverComoExcepcion,
                    solicitud.EsCancelacionDeLaAgencia
                }));
        });

        var actualizado = await _pasajes.BuscarAsync(pasaje.Id) ?? candidato;
        return await _hidratador.HidratarAsync(actualizado);
    }

    public async Task<Pasaje> MarcarNoSePresentoAsync(long pasajeId)
    {
        var pasaje = await ObtenerHidratadoAsync(pasajeId);

        if (await YaSeRegistroAsync())
        {
            return pasaje;
        }

        if (pasaje.GrupoId is not null || pasaje.EsDeGrupo)
        {
            throw new ReglaDeNegocioException(
                "Los pasajes de un grupo se administran junto con el grupo.",
                "pasajeId");
        }

        if (pasaje.Estado == EstadoPasaje.Anulado)
        {
            throw new ReglaDeNegocioException(
                "Un pasaje anulado no puede marcarse como No se presentó.",
                "estado");
        }

        if (pasaje.Estado == EstadoPasaje.NoSePresento)
        {
            throw new ReglaDeNegocioException(
                "El pasaje ya está marcado como No se presentó.",
                "estado");
        }

        if (_reloj.Ahora <= pasaje.FechaHoraSalida)
        {
            throw new ReglaDeNegocioException(
                "Solo puede marcar No se presentó después de la hora de salida.",
                "fechaHoraSalida");
        }

        var candidato = Pasaje.Reconstruir(new PasajeGuardado(
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
            EstadoPasaje.NoSePresento,
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
            pasaje.EsDiferido));

        var anterior = CrearGuardado(pasaje);
        var nuevo = CrearGuardado(candidato);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _pasajes.ActualizarAsync(candidato);
            await RegistrarBitacoraAsync(
                pasaje.Id,
                OperacionNoSePresento,
                JsonSerializer.Serialize(anterior),
                JsonSerializer.Serialize(nuevo));
        });

        var actualizado = await _pasajes.BuscarAsync(pasaje.Id) ?? candidato;
        return await _hidratador.HidratarAsync(actualizado);
    }

    private async Task<Pasaje> ObtenerHidratadoAsync(long pasajeId)
    {
        var pasaje = await _pasajes.BuscarAsync(pasajeId)
            ?? throw new ReglaDeNegocioException(
                "No se encontró el pasaje.",
                "id");

        return await _hidratador.HidratarAsync(pasaje);
    }

    private async Task<UnidadDeSalida> ObtenerUnidadAsync(
        Pasaje pasaje,
        long? unidadDeSalidaId)
    {
        var id = unidadDeSalidaId ?? pasaje.UnidadDeSalidaId
            ?? throw new ReglaDeNegocioException(
                "El pasaje no tiene una unidad de salida.",
                "unidadDeSalidaId");

        var unidad = await _unidades.BuscarAsync(id)
            ?? throw new ReglaDeNegocioException(
                "No se encontró la unidad de salida.",
                "unidadDeSalidaId");

        if (unidad.EsDeGrupo)
        {
            throw new ReglaDeNegocioException(
                "La unidad pertenece a un grupo y no admite pasajes individuales.",
                "unidadDeSalidaId");
        }

        if (unidad.Estado != EstadoUnidadDeSalida.Programada)
        {
            throw new ReglaDeNegocioException(
                "La unidad de salida no está programada.",
                "unidadDeSalidaId");
        }

        return unidad;
    }

    private static void ValidarPago(Pago pago)
    {
        var error = pago.Validar().FirstOrDefault();
        if (error is not null)
        {
            throw new ReglaDeNegocioException(error.Mensaje, error.Campo);
        }
    }

    private Usuario Actor =>
        _contexto.Usuario
        ?? throw new InvalidOperationException("No se encontró el usuario de la sesión.");

    private Task<bool> YaSeRegistroAsync() =>
        _contexto.ClaveDeOperacion is Guid clave
            ? _bitacora.ExisteAsync(clave)
            : Task.FromResult(false);

    private async Task RegistrarBitacoraAsync(
        long pasajeId,
        string operacion,
        string valorAnterior,
        string valorNuevo)
    {
        await _bitacora.RegistrarAsync(new RegistroDeBitacora(
            0,
            _reloj.Ahora,
            operacion,
            Actor,
            valorAnterior,
            valorNuevo,
            pasajeId: pasajeId));
    }

    private static PasajeGuardado CrearGuardado(Pasaje pasaje) =>
        new(
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
}
