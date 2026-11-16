using System.Text.Json;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Time;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Kataleya.Dominio.ValueObjects;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioReservasYVentas
{
    private const string OperacionReserva = "Registrar reserva";
    private const string OperacionVenta = "Registrar venta";
    private const string OperacionGrupo = "Registrar grupo";
    private const string OperacionDiferido = "Registrar pasaje diferido";

    private readonly IRepositorioPasajes _pasajes;
    private readonly IRepositorioUnidadesSalida _unidades;
    private readonly IRepositorioGrupos _grupos;
    private readonly IRepositorioPagos _pagos;
    private readonly IRepositorioBoletos _boletos;
    private readonly IRepositorioTarifas _tarifas;
    private readonly IRepositorioParametros _parametros;
    private readonly ServicioParametros _servicioParametros;
    private readonly HidratadorDePasajes _hidratador;
    private readonly ServicioBitacora _bitacora;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public ServicioReservasYVentas(
        IRepositorioPasajes pasajes,
        IRepositorioUnidadesSalida unidades,
        IRepositorioGrupos grupos,
        IRepositorioPagos pagos,
        IRepositorioBoletos boletos,
        IRepositorioTarifas tarifas,
        IRepositorioParametros parametros,
        ServicioParametros servicioParametros,
        HidratadorDePasajes hidratador,
        ServicioBitacora bitacora,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _pasajes = pasajes ?? throw new ArgumentNullException(nameof(pasajes));
        _unidades = unidades ?? throw new ArgumentNullException(nameof(unidades));
        _grupos = grupos ?? throw new ArgumentNullException(nameof(grupos));
        _pagos = pagos ?? throw new ArgumentNullException(nameof(pagos));
        _boletos = boletos ?? throw new ArgumentNullException(nameof(boletos));
        _tarifas = tarifas ?? throw new ArgumentNullException(nameof(tarifas));
        _parametros = parametros ?? throw new ArgumentNullException(nameof(parametros));
        _servicioParametros = servicioParametros ?? throw new ArgumentNullException(nameof(servicioParametros));
        _hidratador = hidratador ?? throw new ArgumentNullException(nameof(hidratador));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public Task<Pasaje> RegistrarReservaAsync(SolicitudDePasaje solicitud) =>
        RegistrarAsync(solicitud, esVenta: false);

    public Task<Pasaje> RegistrarVentaAsync(SolicitudDePasaje solicitud) =>
        RegistrarAsync(solicitud, esVenta: true);

    public async Task<Pasaje> RegistrarDiferidoAsync(SolicitudDePasaje solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var actor = Actor;
        var ahora = _reloj.Ahora;
        var talonario = solicitud.Talonario
            ?? throw new ReglaDeNegocioException(
                "Escriba el número de boleto del talonario.",
                "numeroBoleto");
        var emision = solicitud.FechaHoraEmisionTalonario
            ?? throw new ReglaDeNegocioException(
                "Escriba la fecha y hora de emisión del talonario.",
                "fechaHoraEmisionTalonario");

        if (await YaSeRegistroAsync())
        {
            var registrado = await _boletos.BuscarPorNumeroAsync(
                talonario.Serie,
                talonario.Correlativo);

            if (registrado is not null)
            {
                var pasajeRegistrado = await _pasajes.BuscarAsync(registrado.PasajeId);
                if (pasajeRegistrado is not null)
                {
                    return await _hidratador.HidratarAsync(pasajeRegistrado);
                }
            }
        }

        var fechaEmision = emision.Kind == DateTimeKind.Utc
            ? emision
            : ZonaHorariaLima.ALocalAUtc(emision);

        if (fechaEmision > ahora)
        {
            throw new ReglaDeNegocioException(
                "La fecha de emisión no puede ser posterior a este momento.",
                "fechaHoraEmisionTalonario");
        }

        if (await _boletos.BuscarPorNumeroAsync(
                talonario.Serie,
                talonario.Correlativo) is not null)
        {
            throw new ReglaDeNegocioException(
                "El número de boleto ya existe.",
                "numeroBoleto");
        }

        var unidad = await _unidades.BuscarAsync(solicitud.UnidadDeSalidaId)
            ?? throw new ReglaDeNegocioException(
                "No se encontró la unidad de salida.",
                "unidadDeSalidaId");

        if (unidad.EsDeGrupo)
        {
            throw new ReglaDeNegocioException(
                "La unidad pertenece a un grupo y no admite pasajes individuales.",
                "unidadDeSalidaId");
        }

        var tipo = await _parametros.BuscarTipoDeVehiculoAsync(unidad.TipoVehiculoId)
            ?? throw new ReglaDeNegocioException(
                "No se encontró el tipo de vehículo de la unidad.",
                "unidadDeSalidaId");

        var viaje = new DatosDelViaje(
            solicitud.Origen,
            solicitud.Destino,
            solicitud.LugarEmbarque,
            unidad.FechaHoraSalida).Normalizar();

        var pasaje = new Pasaje(
            unidad,
            solicitud,
            viaje,
            await ObtenerImporteAsync(unidad),
            EstadoPasaje.Pagado,
            actor,
            ahora,
            esDiferido: true);

        var errores = pasaje.ValidarDatosDelPasajero();
        errores.AddRange(pasaje.ValidarContacto());
        errores.AddRange(pasaje.ValidarRuta());
        errores.AddRange(pasaje.ValidarMenor());
        errores.AddRange(pasaje.ValidarAsientoYVenta());

        if (pasaje.NumeroAsiento is null or < 1 || pasaje.NumeroAsiento > tipo.CantidadDeAsientos)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "numeroAsiento",
                Mensaje = $"Elija un asiento entre 1 y {tipo.CantidadDeAsientos}."
            });
        }

        if (!DatosDelViaje.TryDeterminarSentido(viaje.Origen, viaje.Destino, out var sentido))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "destino",
                Mensaje = "El origen y el destino deben corresponder a Huancayo e Ica."
            });
        }
        else if (sentido != unidad.Sentido)
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
            var codigo = primerError.Campo == "importe" ? "VAL-01" : null;
            throw new ReglaDeNegocioException(primerError.Mensaje, primerError.Campo, codigo);
        }

        if (await _pasajes.AsientoEstaOcupadoAsync(
                solicitud.UnidadDeSalidaId,
                solicitud.NumeroAsiento))
        {
            throw new ReglaDeNegocioException(
                "El asiento ya está ocupado por otro pasaje de esa salida. Elija otro asiento; el número del talonario se conserva.",
                "numeroAsiento");
        }

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var id = await _pasajes.RegistrarAsync(pasaje);
            pasaje.AsignarId(id);

            var boleto = await _boletos.RegistrarAsync(
                id,
                talonario.Serie,
                talonario.Correlativo,
                fechaEmision,
                actor.Id,
                actor.Nombre);

            pasaje.FijarBoleto(boleto);
            await RegistrarBitacoraDiferidaAsync(pasaje);
        });

        return pasaje;
    }

    private async Task<Pasaje> RegistrarAsync(SolicitudDePasaje solicitud, bool esVenta)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var actor = Actor;
        var operacion = esVenta ? OperacionVenta : OperacionReserva;

        if (await YaSeRegistroAsync())
        {
            var registrado = await _pasajes.BuscarPorAsientoAsync(
                solicitud.UnidadDeSalidaId,
                solicitud.NumeroAsiento);

            if (registrado is not null)
            {
                return await _hidratador.HidratarAsync(registrado);
            }
        }

        var unidad = await _unidades.BuscarAsync(solicitud.UnidadDeSalidaId)
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

        var plazo = await _servicioParametros.ObtenerPlazoDeReservaAsync();
        var ahora = _reloj.Ahora;

        if (unidad.FechaHoraSalida <= ahora || unidad.FechaHoraSalida > ahora.AddDays(plazo))
        {
            throw new ReglaDeNegocioException(
                $"La salida debe ser desde ahora hasta dentro de {plazo} días.",
                "fechaHoraSalida");
        }

        var tipo = await _parametros.BuscarTipoDeVehiculoAsync(unidad.TipoVehiculoId)
            ?? throw new ReglaDeNegocioException(
                "No se encontró el tipo de vehículo de la unidad.",
                "unidadDeSalidaId");

        var viaje = new DatosDelViaje(
            solicitud.Origen,
            solicitud.Destino,
            solicitud.LugarEmbarque,
            unidad.FechaHoraSalida).Normalizar();

        var pasaje = new Pasaje(
            unidad,
            solicitud,
            viaje,
            await ObtenerImporteAsync(unidad),
            EstadoPasaje.Reservado,
            actor,
            ahora);

        var errores = pasaje.ValidarDatosDelPasajero();
        errores.AddRange(pasaje.ValidarContacto());
        errores.AddRange(pasaje.ValidarRuta());
        errores.AddRange(pasaje.ValidarMenor());
        errores.AddRange(pasaje.ValidarAsientoYVenta());

        if (pasaje.NumeroAsiento is null or < 1 || pasaje.NumeroAsiento > tipo.CantidadDeAsientos)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "numeroAsiento",
                Mensaje = $"Elija un asiento entre 1 y {tipo.CantidadDeAsientos}."
            });
        }

        if (!DatosDelViaje.TryDeterminarSentido(viaje.Origen, viaje.Destino, out var sentido))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "destino",
                Mensaje = "El origen y el destino deben corresponder a Huancayo e Ica."
            });
        }
        else if (sentido != unidad.Sentido)
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
            var codigo = primerError.Campo == "importe" ? "VAL-01" : null;
            throw new ReglaDeNegocioException(primerError.Mensaje, primerError.Campo, codigo);
        }

        if (await _pasajes.AsientoEstaOcupadoAsync(
                solicitud.UnidadDeSalidaId,
                solicitud.NumeroAsiento))
        {
            throw new ReglaDeNegocioException(
                "Ese asiento ya está ocupado para esta salida. Actualice el mapa de asientos.",
                "numeroAsiento");
        }

        var pago = PrepararPago(solicitud, pasaje, actor, esVenta, ahora);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var id = await _pasajes.RegistrarAsync(pasaje);
            pasaje.AsignarId(id);

            if (pago is not null)
            {
                var pagoDelPasaje = new Pago(
                    id,
                    null,
                    pago.Monto,
                    pago.MedioDePago,
                    pago.Tipo,
                    pago.FechaPago,
                    actor);

                await _pagos.RegistrarAsync(pagoDelPasaje);
                pasaje.AgregarPago(pagoDelPasaje);
            }

            if (pasaje.Estado != EstadoPasaje.Reservado)
            {
                await _pasajes.ActualizarEstadoAsync(id, pasaje.Estado);
            }

            if (pasaje.Estado == EstadoPasaje.Pagado && pasaje.Boleto is null)
            {
                var boleto = await _boletos.EmitirAsync(
                    id,
                    NumeroDeBoleto.SeriePredeterminada,
                    ahora,
                    actor.Id,
                    actor.Nombre);

                pasaje.FijarBoleto(boleto);
            }

            await RegistrarBitacoraAsync(operacion, pasaje);
        });

        return pasaje;
    }


    public async Task<Grupo> RegistrarGrupoAsync(SolicitudDeGrupo solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var actor = Actor;
        var ahora = _reloj.Ahora;

        if (_contexto.ClaveDeOperacion is Guid clave)
        {
            var registrado = await _grupos.BuscarPorClaveDeOperacionAsync(clave);
            if (registrado is not null)
            {
                return await HidratarGrupoAsync(registrado);
            }
        }

        var integrantes = solicitud.Integrantes as IReadOnlyList<IntegranteDeGrupo>
            ?? solicitud.Integrantes?.ToList()
            ?? [];
        var tipos = solicitud.TiposDeVehiculo as IReadOnlyList<short>
            ?? solicitud.TiposDeVehiculo?.ToList()
            ?? [];

        if (tipos.Count == 0)
        {
            throw new ReglaDeNegocioException(
                "Elija al menos una unidad para el grupo.",
                "tiposDeVehiculo");
        }

        var fechaHoraSalida = solicitud.FechaHoraSalida.Kind == DateTimeKind.Utc
            ? solicitud.FechaHoraSalida
            : ZonaHorariaLima.ALocalAUtc(solicitud.FechaHoraSalida);
        var plazo = await _servicioParametros.ObtenerPlazoDeReservaAsync();

        if (fechaHoraSalida <= ahora || fechaHoraSalida > ahora.AddDays(plazo))
        {
            throw new ReglaDeNegocioException(
                $"La salida debe ser desde ahora hasta dentro de {plazo} días.",
                "fechaHoraSalida");
        }

        var viaje = new DatosDelViaje(
            solicitud.Origen,
            solicitud.Destino,
            solicitud.LugarDeRecojo,
            fechaHoraSalida).Normalizar();

        var grupo = new Grupo(
            "G0000",
            solicitud.TelefonoContacto,
            solicitud.PrecioAcordado,
            viaje,
            actor,
            ahora,
            _contexto.ClaveDeOperacion);

        var errores = new List<ErrorDeCampo>();
        for (var indice = 0; indice < integrantes.Count; indice++)
        {
            var integrante = integrantes[indice]
                ?? throw new ReglaDeNegocioException(
                    "Complete los datos de todos los integrantes.",
                    $"integrantes[{indice}]");

            var pasaje = new Pasaje(
                0,
                grupo.Codigo,
                integrante,
                viaje,
                grupo.PrecioAcordado,
                EstadoPasaje.Reservado,
                actor,
                ahora);

            foreach (var error in pasaje.ValidarDatosDelPasajero())
            {
                errores.Add(new ErrorDeCampo
                {
                    Campo = $"integrantes[{indice}].{error.Campo}",
                    Mensaje = error.Mensaje
                });
            }

            grupo.AgregarPasaje(pasaje);
        }

        var tiposValidos = new List<TipoDeVehiculo>();
        var capacidadTotal = 0;
        foreach (var tipoId in tipos)
        {
            var tipo = await _parametros.BuscarTipoDeVehiculoAsync(tipoId)
                ?? throw new ReglaDeNegocioException(
                    "Elija un tipo de vehículo válido.",
                    "tiposDeVehiculo");

            tiposValidos.Add(tipo);
            capacidadTotal += tipo.CantidadDeAsientos;
            grupo.AgregarUnidad(new UnidadDeSalida(
                tiposValidos.Count,
                0L,
                tipo.Id,
                fechaHoraSalida,
                actor));
        }

        if (integrantes.Count > capacidadTotal)
        {
            throw new ReglaDeNegocioException(
                $"Hay {integrantes.Count} integrantes y {capacidadTotal} asientos. " +
                "Agregue otra unidad o elija un vehículo más grande.",
                "tiposDeVehiculo");
        }

        errores.AddRange(grupo.Validar());

        var primerError = errores.FirstOrDefault();
        if (primerError is not null)
        {
            throw new ReglaDeNegocioException(
                primerError.Mensaje,
                primerError.Campo,
                primerError.Campo == "precioAcordado" ? "VAL-04" : null);
        }

        var pago = PrepararPagoDeGrupo(solicitud, grupo, actor, ahora);
        var codigo = await _grupos.SiguienteCodigoAsync();
        grupo = new Grupo(
            codigo,
            grupo.TelefonoContacto,
            grupo.PrecioAcordado,
            grupo.Viaje,
            actor,
            grupo.FechaRegistro,
            grupo.ClaveDeOperacion);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var id = await _grupos.RegistrarAsync(grupo);
            grupo.AsignarId(id);

            for (var indice = 0; indice < tiposValidos.Count; indice++)
            {
                var unidad = new UnidadDeSalida(
                    indice + 1,
                    grupo.Id,
                    tiposValidos[indice].Id,
                    fechaHoraSalida,
                    actor);

                var unidadId = await _unidades.RegistrarAsync(unidad);
                unidad.AsignarId(unidadId);
                grupo.AgregarUnidad(unidad);
            }

            foreach (var integrante in integrantes)
            {
                var pasaje = new Pasaje(
                    grupo.Id,
                    grupo.Codigo,
                    integrante,
                    grupo.Viaje,
                    grupo.PrecioAcordado,
                    pago is null ? EstadoPasaje.Reservado : EstadoPasaje.Pagado,
                    actor,
                    ahora);

                var pasajeId = await _pasajes.RegistrarAsync(pasaje);
                pasaje.AsignarId(pasajeId);
                grupo.AgregarPasaje(pasaje);
            }

            var erroresDelGrupo = grupo.Validar();
            if (erroresDelGrupo.Count > 0)
            {
                var error = erroresDelGrupo[0];
                throw new ReglaDeNegocioException(error.Mensaje, error.Campo);
            }

            if (pago is not null)
            {
                var pagoDelGrupo = new Pago(
                    null,
                    grupo.Id,
                    pago.Monto,
                    pago.MedioDePago,
                    pago.Tipo,
                    pago.FechaPago,
                    actor);

                await _pagos.RegistrarAsync(pagoDelGrupo);
                grupo.AgregarPago(pagoDelGrupo);

                foreach (var pasaje in grupo.Pasajes)
                {
                    if (pasaje.Estado != EstadoPasaje.Pagado)
                    {
                        await _pasajes.ActualizarEstadoAsync(pasaje.Id, pasaje.Estado);
                    }

                    if (pasaje.Boleto is null)
                    {
                        var boleto = await _boletos.EmitirAsync(
                            pasaje.Id,
                            NumeroDeBoleto.SeriePredeterminada,
                            ahora,
                            actor.Id,
                            actor.Nombre);

                        pasaje.FijarBoleto(boleto);
                    }
                }
            }

            await RegistrarBitacoraDeGrupoAsync(grupo, solicitud);
        });

        return grupo;
    }

    private static Pago? PrepararPagoDeGrupo(
        SolicitudDeGrupo solicitud,
        Grupo grupo,
        Usuario actor,
        DateTime ahora)
    {
        if (solicitud.MontoDelPago is null or 0)
        {
            return null;
        }

        if (solicitud.MontoDelPago < 0)
        {
            throw new ReglaDeNegocioException(
                "El importe del pago no puede ser negativo.",
                "montoDelPago");
        }

        if (solicitud.MedioDePago is null)
        {
            throw new ReglaDeNegocioException(
                "Elija el medio de pago.",
                "medioDePago");
        }

        if (solicitud.MontoDelPago != grupo.PrecioAcordado)
        {
            throw new ReglaDeNegocioException(
                "El pago debe cubrir el precio total del grupo.",
                "montoDelPago");
        }

        var pago = new Pago(
            null,
            grupo.Id,
            solicitud.MontoDelPago.Value,
            solicitud.MedioDePago.Value,
            TipoDePago.PagoTotal,
            ahora,
            actor);

        ValidarPago(pago);
        return pago;
    }

    private async Task<Grupo> HidratarGrupoAsync(Grupo grupo)
    {
        foreach (var unidad in await _unidades.ListarPorGrupoAsync(grupo.Id))
        {
            grupo.AgregarUnidad(unidad);
        }

        foreach (var pasaje in await _pasajes.ListarPorGrupoAsync(grupo.Id))
        {
            await _hidratador.HidratarAsync(pasaje);
            grupo.AgregarPasaje(pasaje);
        }

        foreach (var pago in await _pagos.ListarPorGrupoAsync(grupo.Id))
        {
            grupo.AgregarPago(pago);
        }

        return grupo;
    }

    private async Task RegistrarBitacoraDeGrupoAsync(
        Grupo grupo,
        SolicitudDeGrupo solicitud)
    {
        var guardado = new GrupoGuardado(
            grupo.Id,
            grupo.Codigo,
            grupo.TelefonoContacto,
            grupo.PrecioAcordado,
            grupo.Origen,
            grupo.Destino,
            grupo.LugarDeRecojo,
            grupo.FechaHoraSalida,
            grupo.FechaRegistro,
            grupo.CreadoPorId,
            grupo.CreadoPor,
            grupo.ClaveDeOperacion);

        await _bitacora.RegistrarAsync(new RegistroDeBitacora(
            0,
            _reloj.Ahora,
            OperacionGrupo,
            Actor,
            null,
            JsonSerializer.Serialize(new
            {
                grupo = guardado,
                solicitud.TiposDeVehiculo,
                solicitud.Integrantes,
                solicitud.MontoDelPago,
                solicitud.MedioDePago
            })));
    }

    private async Task<decimal> ObtenerImporteAsync(UnidadDeSalida unidad)
    {
        var fecha = ZonaHorariaLima.FechaLocal(unidad.FechaHoraSalida);
        var sentido = unidad.Sentido
            ?? throw new InvalidOperationException(
                "Solo las unidades individuales tienen una tarifa por sentido.");
        var tarifa = await _tarifas.BuscarVigenteAsync(fecha, sentido)
            ?? throw new ReglaDeNegocioException(
                "No hay una tarifa vigente para ese sentido y fecha.",
                "tarifa",
                "VAL-01");

        return tarifa.Importe;
    }

    private static Pago? PrepararPago(
        SolicitudDePasaje solicitud,
        Pasaje pasaje,
        Usuario actor,
        bool esVenta,
        DateTime ahora)
    {
        if (esVenta)
        {
            var monto = solicitud.MontoDelPago;
            var medio = solicitud.MedioDePago;

            if (monto is null || monto <= 0)
            {
                throw new ReglaDeNegocioException(
                    "Escriba el importe del pago.",
                    "montoDelPago");
            }

            if (medio is null)
            {
                throw new ReglaDeNegocioException(
                    "Elija el medio de pago.",
                    "medioDePago");
            }

            if (monto < pasaje.Importe)
            {
                throw new ReglaDeNegocioException(
                    "El pago no cubre el importe total del pasaje.",
                    "montoDelPago");
            }

            var pago = new Pago(
                0,
                null,
                pasaje.Importe,
                medio.Value,
                TipoDePago.PagoTotal,
                ahora,
                actor);

            ValidarPago(pago);
            return pago;
        }

        if (solicitud.MontoDelPago is null or 0)
        {
            return null;
        }

        if (solicitud.MontoDelPago < 0)
        {
            throw new ReglaDeNegocioException(
                "El adelanto no puede ser negativo.",
                "montoDelPago");
        }

        if (solicitud.MedioDePago is null)
        {
            throw new ReglaDeNegocioException(
                "Elija el medio de pago del adelanto.",
                "medioDePago");
        }

        if (solicitud.MontoDelPago >= pasaje.Importe)
        {
            throw new ReglaDeNegocioException(
                "El adelanto debe ser menor que el importe total del pasaje.",
                "montoDelPago");
        }

        var adelanto = new Pago(
            0,
            null,
            solicitud.MontoDelPago.Value,
            solicitud.MedioDePago.Value,
            TipoDePago.Adelanto,
            ahora,
            actor);

        ValidarPago(adelanto);
        return adelanto;
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

    private async Task RegistrarBitacoraAsync(string operacion, Pasaje pasaje)
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
            operacion,
            Actor,
            null,
            JsonSerializer.Serialize(guardado),
            pasajeId: pasaje.Id));
    }

    private async Task RegistrarBitacoraDiferidaAsync(Pasaje pasaje)
    {
        var boleto = pasaje.Boleto
            ?? throw new InvalidOperationException("El pasaje diferido no tiene boleto.");

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
            OperacionDiferido,
            Actor,
            null,
            JsonSerializer.Serialize(new
            {
                pasaje = guardado,
                boleto = new BoletoGuardado(
                    boleto.Id,
                    boleto.PasajeId,
                    boleto.Numero.Serie,
                    boleto.Numero.Correlativo,
                    boleto.FechaEmision,
                    boleto.EmitidoPorId,
                    boleto.EmitidoPor),
                fechaHoraRegistro = pasaje.FechaVenta,
                fechaHoraEmision = boleto.FechaEmision
            }),
            pasajeId: pasaje.Id));
    }
}
