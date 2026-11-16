using Kataleya.Aplicacion.Excepciones;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.ValueObjects;

namespace Kataleya.Pruebas;

public sealed class ServicioPasajesPruebas
{
    [Fact]
    public async Task Abrir_unidad_asigna_numero_consecutivo_en_el_horario()
    {
        var escenario = new EscenarioDeConfiguracion();

        var primera = await escenario.Asientos.AbrirUnidadAsync(
            Sentido.HuancayoIca,
            new DateTime(2026, 10, 30, 13, 0, 0),
            new SolicitudDeUnidadDeSalida(1));
        var segunda = await escenario.Asientos.AbrirUnidadAsync(
            Sentido.HuancayoIca,
            new DateTime(2026, 10, 30, 13, 0, 0),
            new SolicitudDeUnidadDeSalida(2));

        Assert.True(primera.Id > 0);
        Assert.Equal(1, primera.Numero);
        Assert.Equal(2, segunda.Numero);
        Assert.Equal(2, escenario.UnidadesSalidaRepositorio.Unidades.Count);
        Assert.Equal("Abrir unidad de salida", escenario.Bitacora.Registros[^1].Operacion);
    }

    [Fact]
    public async Task Abrir_unidad_fuera_de_horario_falla()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Asientos.AbrirUnidadAsync(
                Sentido.HuancayoIca,
                new DateTime(2026, 10, 30, 12, 0, 0),
                new SolicitudDeUnidadDeSalida(1)));

        Assert.Equal("fechaHora", excepcion.Campo);
    }

    [Fact]
    public async Task Reserva_sin_adelanto_queda_reservada_sin_boleto()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);

        var pasaje = await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id));

        Assert.True(pasaje.Id > 0);
        Assert.Equal(EstadoPasaje.Reservado, pasaje.Estado);
        Assert.Equal(120, pasaje.Importe);
        Assert.Equal(0, pasaje.Pagado);
        Assert.Null(pasaje.Boleto);
        Assert.Empty(escenario.PagosRepositorio.Pagos);
        Assert.Equal("Registrar reserva", escenario.Bitacora.Registros[^1].Operacion);
        Assert.Equal(pasaje.Id, escenario.Bitacora.Registros[^1].PasajeId);
    }

    [Fact]
    public async Task Reserva_con_adelanto_queda_con_saldo_y_un_pago()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);

        var pasaje = await escenario.Ventas.RegistrarReservaAsync(
            Solicitud(unidad.Id, monto: 50, medio: MedioPago.Yape));

        Assert.Equal(EstadoPasaje.ReservadoConAdelanto, pasaje.Estado);
        Assert.Equal(50, pasaje.Pagado);
        Assert.Equal(70, pasaje.Saldo);
        Assert.Single(escenario.PagosRepositorio.Pagos);
        Assert.Equal(TipoDePago.Adelanto, escenario.PagosRepositorio.Pagos[0].Tipo);
    }

    [Fact]
    public async Task Venta_emite_boleto_y_siguiente_correlativo()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);

        var primera = await escenario.Ventas.RegistrarVentaAsync(
            Solicitud(unidad.Id, asiento: 1, monto: 150, medio: MedioPago.Efectivo));
        var segunda = await escenario.Ventas.RegistrarVentaAsync(
            Solicitud(unidad.Id, asiento: 2, monto: 120, medio: MedioPago.Yape));

        Assert.Equal(EstadoPasaje.Pagado, primera.Estado);
        Assert.NotNull(primera.Boleto);
        Assert.Equal("001-000001", primera.Boleto!.Numero.Texto);
        Assert.Equal("001-000002", segunda.Boleto!.Numero.Texto);
        Assert.Equal(120, primera.Pagado);
        Assert.Equal(0, primera.Saldo);
        Assert.Equal(2, escenario.BoletosRepositorio.Boletos.Count);
        Assert.Equal(segunda.Id, escenario.Bitacora.Registros[^1].PasajeId);
    }

    [Fact]
    public async Task Venta_con_pago_insuficiente_falla()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarVentaAsync(
                Solicitud(unidad.Id, monto: 100, medio: MedioPago.Efectivo)));

        Assert.Equal("montoDelPago", excepcion.Campo);
        Assert.Empty(escenario.PasajesRepositorio.Pasajes);
    }

    [Fact]
    public async Task No_se_puede_reservar_un_asiento_ocupado()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);
        await escenario.Ventas.RegistrarVentaAsync(
            Solicitud(unidad.Id, asiento: 3, monto: 120, medio: MedioPago.Efectivo));

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id, asiento: 3)));

        Assert.Equal("numeroAsiento", excepcion.Campo);
        Assert.Contains("ocupado", excepcion.Message);
    }

    [Fact]
    public async Task No_se_puede_reservar_sin_tarifa_vigente()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = escenario.SembrarUnidad(
            Sentido.HuancayoIca,
            new DateTime(2026, 10, 30, 11, 0, 0, DateTimeKind.Utc));

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id)));

        Assert.Equal("tarifa", excepcion.Campo);
        Assert.Equal("VAL-01", excepcion.Codigo);
    }

    [Fact]
    public async Task Ruta_con_sentido_contrario_falla()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarReservaAsync(Solicitud(
                unidad.Id,
                origen: "Ica",
                destino: "Huancayo")));

        Assert.Equal("destino", excepcion.Campo);
        Assert.Contains("sentido", excepcion.Message);
    }

    [Fact]
    public async Task Cobro_del_saldo_completa_el_pago_y_emite_boleto()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(
            Solicitud(unidad.Id, monto: 20, medio: MedioPago.Efectivo));

        var pasaje = await escenario.Cobros.RegistrarCobroDelSaldoAsync(
            reserva.Id,
            new SolicitudDeCobro(100, MedioPago.Yape));

        Assert.Equal(EstadoPasaje.Pagado, pasaje.Estado);
        Assert.Equal(120, pasaje.Pagado);
        Assert.Equal(0, pasaje.Saldo);
        Assert.NotNull(pasaje.Boleto);
        Assert.Equal(TipoDePago.Saldo, escenario.PagosRepositorio.Pagos[^1].Tipo);
        Assert.Equal("Cobrar saldo del pasaje", escenario.Bitacora.Registros[^1].Operacion);
        Assert.Equal(pasaje.Id, escenario.Bitacora.Registros[^1].PasajeId);
    }

    [Fact]
    public async Task Cobro_parcial_conserva_el_saldo()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id));

        var pasaje = await escenario.Cobros.RegistrarCobroDelSaldoAsync(
            reserva.Id,
            new SolicitudDeCobro(50, MedioPago.Efectivo));

        Assert.Equal(EstadoPasaje.ReservadoConAdelanto, pasaje.Estado);
        Assert.Equal(70, pasaje.Saldo);
        Assert.Null(pasaje.Boleto);
    }

    [Fact]
    public async Task Cobro_mayor_al_saldo_falla()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);
        var reserva = await escenario.Ventas.RegistrarReservaAsync(Solicitud(unidad.Id));

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Cobros.RegistrarCobroDelSaldoAsync(
                reserva.Id,
                new SolicitudDeCobro(121, MedioPago.Efectivo)));

        Assert.Equal("monto", excepcion.Campo);
        Assert.Contains("supera el saldo", excepcion.Message);
    }

    [Fact]
    public async Task Reserva_idempotente_no_duplica_pasaje_ni_pago()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);
        escenario.Contexto.ClaveDeOperacion = Guid.NewGuid();
        var solicitud = Solicitud(unidad.Id, monto: 20, medio: MedioPago.Efectivo);

        var primera = await escenario.Ventas.RegistrarReservaAsync(solicitud);
        var segunda = await escenario.Ventas.RegistrarReservaAsync(solicitud);

        Assert.Equal(primera.Id, segunda.Id);
        Assert.Single(escenario.PasajesRepositorio.Pasajes);
        Assert.Single(escenario.PagosRepositorio.Pagos);
    }

    [Fact]
    public async Task Consulta_de_detalle_incluye_pagos_y_boleto()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);
        var venta = await escenario.Ventas.RegistrarVentaAsync(
            Solicitud(unidad.Id, monto: 120, medio: MedioPago.Efectivo));

        var pasaje = await escenario.Consultas.ObtenerDetalleAsync(venta.Id);

        Assert.NotNull(pasaje);
        Assert.Single(pasaje!.Pagos);
        Assert.NotNull(pasaje.Boleto);
        Assert.Equal(120, pasaje.Pagado);
    }

    [Fact]
    public async Task Reserva_rechaza_dni_con_separadores()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarReservaAsync(
                Solicitud(unidad.Id, numeroDocumento: "1234-5678")));

        Assert.Equal("numeroDocumento", excepcion.Campo);
    }

    [Fact]
    public async Task Reserva_rechaza_celular_con_separadores()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarReservaAsync(
                Solicitud(unidad.Id, celular: "987-654-321")));

        Assert.Equal("celular", excepcion.Campo);
    }

    [Fact]
    public async Task Listado_filtra_por_fecha_y_documento()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);
        await escenario.Ventas.RegistrarVentaAsync(
            Solicitud(unidad.Id, monto: 120, medio: MedioPago.Efectivo));

        var encontrados = await escenario.Consultas.ConsultarAsync(new FiltroDePasajes(
            new DateOnly(2026, 10, 30),
            null,
            null,
            null,
            "12345678"));
        var vacio = await escenario.Consultas.ConsultarAsync(new FiltroDePasajes(
            new DateOnly(2026, 10, 30),
            null,
            null,
            null,
            "87654321"));

        var pasaje = Assert.Single(encontrados);
        Assert.Single(pasaje.Pagos);
        Assert.NotNull(pasaje.Boleto);
        Assert.Equal(120, pasaje.Pagado);
        Assert.Empty(vacio);
    }

    [Fact]
    public void El_correlativo_del_boleto_no_se_limita_a_tres_digitos()
    {
        var numero = new NumeroDeBoleto("001", 1000);

        Assert.Equal("001-001000", numero.Texto);
    }

    [Fact]
    public async Task Registro_diferido_guarda_pagado_con_talonario_y_bitacora()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);
        var solicitud = SolicitudDiferida(unidad.Id, correlativo: 152, asiento: 4);

        var pasaje = await escenario.Ventas.RegistrarDiferidoAsync(solicitud);

        Assert.True(pasaje.Id > 0);
        Assert.Equal(EstadoPasaje.Pagado, pasaje.Estado);
        Assert.True(pasaje.EsDiferido);
        Assert.Equal(4, pasaje.NumeroAsiento);
        Assert.NotNull(pasaje.Boleto);
        Assert.Equal("001-000152", pasaje.Boleto!.Numero.Texto);
        Assert.Equal(solicitud.FechaHoraEmisionTalonario, pasaje.Boleto.FechaEmision);
        Assert.Equal(escenario.Reloj.Ahora, pasaje.FechaVenta);
        Assert.Empty(escenario.PagosRepositorio.Pagos);

        var registro = escenario.Bitacora.Registros[^1];
        Assert.Equal("Registrar pasaje diferido", registro.Operacion);
        Assert.Equal(pasaje.Id, registro.PasajeId);
        Assert.Contains("fechaHoraRegistro", registro.ValorNuevo);
        Assert.Contains("fechaHoraEmision", registro.ValorNuevo);
    }

    [Fact]
    public async Task Registro_diferido_rechaza_un_numero_de_boleto_repetido()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);
        await escenario.Ventas.RegistrarDiferidoAsync(
            SolicitudDiferida(unidad.Id, correlativo: 152, asiento: 1));

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarDiferidoAsync(
                SolicitudDiferida(unidad.Id, correlativo: 152, asiento: 2)));

        Assert.Equal("numeroBoleto", excepcion.Campo);
        Assert.Contains("existe", excepcion.Message);
        Assert.Single(escenario.PasajesRepositorio.Pasajes);
        Assert.Single(escenario.BoletosRepositorio.Boletos);
    }

    [Fact]
    public async Task Registro_diferido_rechaza_una_fecha_de_emision_futura()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarDiferidoAsync(
                SolicitudDiferida(
                    unidad.Id,
                    fechaHoraEmision: escenario.Reloj.Ahora.AddMinutes(1))));

        Assert.Equal("fechaHoraEmisionTalonario", excepcion.Campo);
        Assert.Contains("posterior", excepcion.Message);
        Assert.Empty(escenario.PasajesRepositorio.Pasajes);
    }

    [Fact]
    public async Task Registro_diferido_rechaza_un_asiento_ocupado_y_conserva_el_numero()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);
        await escenario.Ventas.RegistrarVentaAsync(
            Solicitud(unidad.Id, asiento: 1, monto: 120, medio: MedioPago.Efectivo));

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarDiferidoAsync(
                SolicitudDiferida(unidad.Id, correlativo: 152, asiento: 1)));

        Assert.Equal("numeroAsiento", excepcion.Campo);
        Assert.Contains("ocupado", excepcion.Message);
        Assert.Single(escenario.PasajesRepositorio.Pasajes);
        Assert.Single(escenario.BoletosRepositorio.Boletos);
    }

    [Fact]
    public async Task Registro_diferido_es_idempotente()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);
        escenario.Contexto.ClaveDeOperacion = Guid.NewGuid();
        var solicitud = SolicitudDiferida(unidad.Id, correlativo: 152, asiento: 4);

        var primera = await escenario.Ventas.RegistrarDiferidoAsync(solicitud);
        var segunda = await escenario.Ventas.RegistrarDiferidoAsync(solicitud);

        Assert.Equal(primera.Id, segunda.Id);
        Assert.Single(escenario.PasajesRepositorio.Pasajes);
        Assert.Single(escenario.BoletosRepositorio.Boletos);
        Assert.Single(
            escenario.Bitacora.Registros,
            registro => registro.Operacion == "Registrar pasaje diferido");
    }

    [Fact]
    public async Task Registro_diferido_exige_el_numero_del_talonario()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarDiferidoAsync(Solicitud(unidad.Id)));

        Assert.Equal("numeroBoleto", excepcion.Campo);
        Assert.Empty(escenario.PasajesRepositorio.Pasajes);
    }

    [Fact]
    public async Task Registro_diferido_exige_la_fecha_de_emision_del_talonario()
    {
        var escenario = new EscenarioDeConfiguracion();
        var unidad = await PrepararUnidadAsync(escenario);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarDiferidoAsync(
                Solicitud(unidad.Id) with
                {
                    Talonario = new NumeroDeBoleto("001", 152)
                }));

        Assert.Equal("fechaHoraEmisionTalonario", excepcion.Campo);
        Assert.Empty(escenario.PasajesRepositorio.Pasajes);
    }

    [Fact]
    public async Task Registro_diferido_permite_regular_una_salida_ya_pasada()
    {
        var escenario = new EscenarioDeConfiguracion();
        await escenario.Tarifas.FijarTarifaRegularAsync(Sentido.HuancayoIca, 120);
        var unidad = escenario.SembrarUnidad(
            Sentido.HuancayoIca,
            new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc));

        var pasaje = await escenario.Ventas.RegistrarDiferidoAsync(
            SolicitudDiferida(
                unidad.Id,
                fechaHoraEmision: new DateTime(2026, 10, 30, 7, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(EstadoPasaje.Pagado, pasaje.Estado);
        Assert.True(pasaje.EsDiferido);
        Assert.NotNull(pasaje.Boleto);
    }

    private static async Task<Kataleya.Dominio.Entities.UnidadDeSalida> PrepararUnidadAsync(
        EscenarioDeConfiguracion escenario)
    {
        await escenario.Tarifas.FijarTarifaRegularAsync(Sentido.HuancayoIca, 120);

        return escenario.SembrarUnidad(
            Sentido.HuancayoIca,
            new DateTime(2026, 10, 30, 11, 0, 0, DateTimeKind.Utc));
    }

    private static SolicitudDePasaje Solicitud(
        long unidadDeSalidaId,
        int asiento = 1,
        decimal? monto = null,
        MedioPago? medio = null,
        string origen = "Huancayo",
        string destino = "Ica",
        string numeroDocumento = "12345678",
        string celular = "987654321") =>
        new(
            unidadDeSalidaId,
            asiento,
            "Pasajero de prueba",
            TipoDocumento.DNI,
            numeroDocumento,
            celular,
            origen,
            destino,
            "Terminal principal",
            false,
            null,
            null,
            monto,
            medio);

    private static SolicitudDePasaje SolicitudDiferida(
        long unidadDeSalidaId,
        string serie = "001",
        int correlativo = 152,
        DateTime? fechaHoraEmision = null,
        int asiento = 1) =>
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
            null,
            null,
            new NumeroDeBoleto(serie, correlativo),
            fechaHoraEmision ?? new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc));
}
