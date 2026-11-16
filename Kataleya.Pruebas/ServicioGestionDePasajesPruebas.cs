using Kataleya.Aplicacion.Excepciones;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;

namespace Kataleya.Pruebas;

public sealed class ServicioGestionDePasajesPruebas
{
    [Fact]
    public async Task Modificar_cambia_asiento_y_lugar_sin_cobro_extra()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id));

        var resultado = await escenario.Gestion.ModificarAsync(
            reserva.Id,
            new SolicitudDeModificacionDePasaje(
                NumeroAsiento: 2,
                LugarEmbarque: "Nuevo terminal",
                NombrePasajero: "Pasajero editado"));

        var pasaje = resultado.Pasaje;
        Assert.Equal(2, pasaje.NumeroAsiento);
        Assert.Equal("Nuevo terminal", pasaje.LugarEmbarque);
        Assert.Equal("Pasajero editado", pasaje.NombrePasajero);
        Assert.Equal(120, pasaje.Importe);
        Assert.Empty(pasaje.Pagos);
        Assert.False(resultado.RequiereReimpresion);
        Assert.False(await escenario.PasajesRepositorio.AsientoEstaOcupadoAsync(unidad.Id, 1));
        Assert.True(await escenario.PasajesRepositorio.AsientoEstaOcupadoAsync(unidad.Id, 2));
        Assert.Equal("Modificar pasaje", escenario.Bitacora.Registros[^1].Operacion);
        Assert.Equal(pasaje.Id, escenario.Bitacora.Registros[^1].PasajeId);
    }

    [Fact]
    public async Task Modificar_pasaje_pagado_indica_reimpresion()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var venta = await escenario.Ventas.RegistrarVentaAsync(
            Solicitud(unidad.Id, monto: 120, medio: MedioPago.Efectivo));

        var resultado = await escenario.Gestion.ModificarAsync(
            venta.Id,
            new SolicitudDeModificacionDePasaje(NumeroAsiento: 2));

        Assert.True(resultado.RequiereReimpresion);
        Assert.NotNull(resultado.Pasaje.Boleto);
        Assert.Equal(EstadoPasaje.Pagado, resultado.Pasaje.Estado);
        Assert.Equal(120, resultado.Pasaje.Pagado);
    }

    [Fact]
    public async Task Modificar_permite_conservar_el_mismo_asiento()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id));

        var resultado = await escenario.Gestion.ModificarAsync(
            reserva.Id,
            new SolicitudDeModificacionDePasaje(
                NumeroAsiento: 1,
                NombrePasajero: "Pasajero editado"));

        Assert.Equal(1, resultado.Pasaje.NumeroAsiento);
        Assert.Equal("Pasajero editado", resultado.Pasaje.NombrePasajero);
    }

    [Fact]
    public async Task Modificar_con_cobro_adicional_suma_importe_y_pago()
    {
        var escenario = new EscenarioDeConfiguracion();
        var original = await PrepararUnidadAsync(escenario, 11);
        var destino = escenario.SembrarUnidad(
            Sentido.HuancayoIca,
            new DateTime(2026, 10, 30, 13, 0, 0, DateTimeKind.Utc));
        var reserva = await escenario.Ventas.RegistrarReservaAsync(Solicitud(original.Id));

        var resultado = await escenario.Gestion.ModificarAsync(
            reserva.Id,
            new SolicitudDeModificacionDePasaje(
                UnidadDeSalidaId: destino.Id,
                NumeroAsiento: 1,
                MontoAdicional: 30,
                MedioDePago: MedioPago.Yape));

        var pasaje = resultado.Pasaje;
        Assert.Equal(destino.Id, pasaje.UnidadDeSalidaId);
        Assert.Equal(150, pasaje.Importe);
        Assert.Equal(30, pasaje.Pagado);
        Assert.Equal(120, pasaje.Saldo);
        Assert.Equal(EstadoPasaje.ReservadoConAdelanto, pasaje.Estado);
        Assert.Equal(TipoDePago.Adicional, escenario.PagosRepositorio.Pagos[^1].Tipo);
        Assert.Equal(30, escenario.PagosRepositorio.Pagos[^1].Monto);
    }

    [Fact]
    public async Task Modificar_rechaza_cobro_adicional_sin_cambio_de_fecha()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id));

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Gestion.ModificarAsync(
                reserva.Id,
                new SolicitudDeModificacionDePasaje(
                    NumeroAsiento: 2,
                    MontoAdicional: 10,
                    MedioDePago: MedioPago.Efectivo)));

        Assert.Equal("montoAdicional", excepcion.Campo);
        Assert.Equal(1, escenario.PasajesRepositorio.Pasajes[0].NumeroAsiento);
        Assert.Empty(escenario.PagosRepositorio.Pagos);
    }

    [Fact]
    public async Task Modificar_rechaza_asiento_ocupado()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var primera = await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id, asiento: 1));
        await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id, asiento: 2));

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Gestion.ModificarAsync(
                primera.Id,
                new SolicitudDeModificacionDePasaje(NumeroAsiento: 2)));

        Assert.Equal("numeroAsiento", excepcion.Campo);
        Assert.Equal(1, escenario.PasajesRepositorio.Pasajes[0].NumeroAsiento);
    }

    [Fact]
    public async Task Modificar_rechaza_pasaje_de_grupo()
    {
        var escenario = new EscenarioDeConfiguracion();
        var grupo = await escenario.Ventas.RegistrarGrupoAsync(new SolicitudDeGrupo(
            "Huancayo",
            "Ica",
            "Terminal principal",
            new DateTime(2026, 10, 31, 13, 0, 0),
            300,
            "987654321",
            [2],
            [
                new IntegranteDeGrupo("Integrante 1", TipoDocumento.DNI, "12345671", true),
                new IntegranteDeGrupo("Integrante 2", TipoDocumento.DNI, "12345672", false)
            ],
            null,
            null));

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Gestion.ModificarAsync(
                grupo.Pasajes[0].Id,
                new SolicitudDeModificacionDePasaje(NombrePasajero: "Otro nombre")));

        Assert.Equal("pasajeId", excepcion.Campo);
    }

    [Fact]
    public async Task Anular_con_aviso_suficiente_devuelve_pagos_y_libera_asiento()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 13);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(
            Solicitud(unidad.Id, monto: 50, medio: MedioPago.Yape));
        await escenario.Cobros.RegistrarCobroDelSaldoAsync(
            reserva.Id,
            new SolicitudDeCobro(70, MedioPago.Efectivo));

        var pasaje = await escenario.Gestion.AnularAsync(
            reserva.Id,
            new SolicitudDeAnulacionDePasaje("El pasajero canceló su viaje."));

        Assert.Equal(EstadoPasaje.Anulado, pasaje.Estado);
        Assert.Equal("El pasajero canceló su viaje.", pasaje.MotivoDeAnulacion);
        Assert.NotNull(pasaje.FechaDeAnulacion);
        Assert.Equal(0, pasaje.Pagado);
        Assert.Equal(0, pasaje.Saldo);
        Assert.Equal(2, pasaje.Pagos.Count(pago => pago.Tipo == TipoDePago.Devolucion));
        Assert.False(await escenario.PasajesRepositorio.AsientoEstaOcupadoAsync(unidad.Id, 1));
        Assert.Equal("Anular pasaje", escenario.Bitacora.Registros[^1].Operacion);
        Assert.Equal(pasaje.Id, escenario.Bitacora.Registros[^1].PasajeId);
    }

    [Fact]
    public async Task Anular_sin_aviso_suficiente_conserva_los_pagos()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(
            Solicitud(unidad.Id, monto: 50, medio: MedioPago.Efectivo));

        var pasaje = await escenario.Gestion.AnularAsync(
            reserva.Id,
            new SolicitudDeAnulacionDePasaje("Cancelación tardía."));

        Assert.Equal(EstadoPasaje.Anulado, pasaje.Estado);
        Assert.Equal(50, pasaje.Pagado);
        Assert.DoesNotContain(pasaje.Pagos, pago => pago.Tipo == TipoDePago.Devolucion);
        Assert.Single(escenario.PagosRepositorio.Pagos);
    }

    [Fact]
    public async Task Anular_sin_aviso_suficiente_por_excepcion_devuelve()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var venta = await escenario.Ventas.RegistrarVentaAsync(
            Solicitud(unidad.Id, monto: 120, medio: MedioPago.Efectivo));

        var pasaje = await escenario.Gestion.AnularAsync(
            venta.Id,
            new SolicitudDeAnulacionDePasaje(
                "Excepción autorizada por la agencia.",
                DevolverComoExcepcion: true));

        Assert.Equal(0, pasaje.Pagado);
        Assert.Single(pasaje.Pagos, pago => pago.Tipo == TipoDePago.Devolucion);
    }

    [Fact]
    public async Task Anular_sin_aviso_suficiente_por_cancelacion_de_la_agencia_devuelve()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(
            Solicitud(unidad.Id, monto: 50, medio: MedioPago.Efectivo));

        var pasaje = await escenario.Gestion.AnularAsync(
            reserva.Id,
            new SolicitudDeAnulacionDePasaje(
                "La agencia canceló la salida.",
                EsCancelacionDeLaAgencia: true));

        Assert.Equal(0, pasaje.Pagado);
        Assert.Single(escenario.PagosRepositorio.Pagos, pago => pago.Tipo == TipoDePago.Devolucion);
    }

    [Fact]
    public async Task Anular_exige_motivo()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id));

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Gestion.AnularAsync(
                reserva.Id,
                new SolicitudDeAnulacionDePasaje("   ")));

        Assert.Equal("motivo", excepcion.Campo);
    }

    [Fact]
    public async Task Anular_rechaza_pasaje_despues_de_la_salida()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id));
        escenario.Reloj.Ahora = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Gestion.AnularAsync(
                reserva.Id,
                new SolicitudDeAnulacionDePasaje("Cancelación tardía.")));

        Assert.Equal("fechaHoraSalida", excepcion.Campo);
    }

    [Fact]
    public async Task No_se_presento_despues_de_la_salida_no_devuelve_y_conserva_asiento()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var venta = await escenario.Ventas.RegistrarVentaAsync(
            Solicitud(unidad.Id, monto: 120, medio: MedioPago.Efectivo));
        escenario.Reloj.Ahora = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

        var pasaje = await escenario.Gestion.MarcarNoSePresentoAsync(venta.Id);

        Assert.Equal(EstadoPasaje.NoSePresento, pasaje.Estado);
        Assert.Equal(120, pasaje.Pagado);
        Assert.Equal(0, pasaje.Saldo);
        Assert.DoesNotContain(pasaje.Pagos, pago => pago.Tipo == TipoDePago.Devolucion);
        Assert.True(await escenario.PasajesRepositorio.AsientoEstaOcupadoAsync(unidad.Id, 1));
        Assert.Equal("Marcar no se presentó", escenario.Bitacora.Registros[^1].Operacion);
        Assert.Equal(pasaje.Id, escenario.Bitacora.Registros[^1].PasajeId);
    }

    [Fact]
    public async Task No_se_presento_antes_de_la_salida_falla()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id));

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Gestion.MarcarNoSePresentoAsync(reserva.Id));

        Assert.Equal("fechaHoraSalida", excepcion.Campo);
    }

    [Fact]
    public async Task Modificar_y_anular_son_idempotentes()
    {
        var escenario = new EscenarioDeConfiguracion();
        var original = await PrepararUnidadAsync(escenario, 11);
        var destino = escenario.SembrarUnidad(
            Sentido.HuancayoIca,
            new DateTime(2026, 10, 30, 13, 0, 0, DateTimeKind.Utc));
        var reserva = await escenario.Ventas.RegistrarReservaAsync(Solicitud(original.Id));
        escenario.Contexto.ClaveDeOperacion = Guid.NewGuid();

        var modificado = await escenario.Gestion.ModificarAsync(
            reserva.Id,
            new SolicitudDeModificacionDePasaje(
                UnidadDeSalidaId: destino.Id,
                MontoAdicional: 20,
                MedioDePago: MedioPago.Efectivo));
        var modificadoDeNuevo = await escenario.Gestion.ModificarAsync(
            reserva.Id,
            new SolicitudDeModificacionDePasaje(
                UnidadDeSalidaId: destino.Id,
                MontoAdicional: 20,
                MedioDePago: MedioPago.Efectivo));

        Assert.Equal(modificado.Pasaje.Id, modificadoDeNuevo.Pasaje.Id);
        Assert.Single(escenario.PagosRepositorio.Pagos);
        Assert.Equal(1, escenario.Bitacora.Registros.Count(
            registro => registro.Operacion == "Modificar pasaje"));

        escenario.Contexto.ClaveDeOperacion = Guid.NewGuid();
        var anulado = await escenario.Gestion.AnularAsync(
            reserva.Id,
            new SolicitudDeAnulacionDePasaje("Cancelación."));
        var anuladoDeNuevo = await escenario.Gestion.AnularAsync(
            reserva.Id,
            new SolicitudDeAnulacionDePasaje("Cancelación."));

        Assert.Equal(EstadoPasaje.Anulado, anulado.Estado);
        Assert.Equal(anulado.Estado, anuladoDeNuevo.Estado);
        Assert.Single(escenario.PagosRepositorio.Pagos,
            pago => pago.Tipo == TipoDePago.Devolucion);
        Assert.Equal(1, escenario.Bitacora.Registros.Count(
            registro => registro.Operacion == "Anular pasaje"));
    }

    [Fact]
    public async Task No_se_presento_es_idempotente()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario, 11);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id));
        escenario.Reloj.Ahora = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        escenario.Contexto.ClaveDeOperacion = Guid.NewGuid();

        var primero = await escenario.Gestion.MarcarNoSePresentoAsync(reserva.Id);
        var segundo = await escenario.Gestion.MarcarNoSePresentoAsync(reserva.Id);

        Assert.Equal(EstadoPasaje.NoSePresento, primero.Estado);
        Assert.Equal(primero.Estado, segundo.Estado);
        Assert.Equal(1, escenario.Bitacora.Registros.Count(
            registro => registro.Operacion == "Marcar no se presentó"));
    }

    private static async Task<UnidadDeSalida> PrepararUnidadAsync(
        EscenarioDeConfiguracion escenario,
        int hora)
    {
        await escenario.Tarifas.FijarTarifaRegularAsync(Sentido.HuancayoIca, 120);

        return escenario.SembrarUnidad(
            Sentido.HuancayoIca,
            new DateTime(2026, 10, 30, hora, 0, 0, DateTimeKind.Utc));
    }

    private static SolicitudDePasaje Solicitud(
        long unidadDeSalidaId,
        int asiento = 1,
        decimal? monto = null,
        MedioPago? medio = null) =>
        new(
            unidadDeSalidaId,
            asiento,
            "Pasajero de prueba",
            TipoDocumento.DNI,
            "12345678",
            "987654321",
            "Huancayo",
            "Ica",
            "Terminal principal",
            false,
            null,
            null,
            monto,
            medio);
}
