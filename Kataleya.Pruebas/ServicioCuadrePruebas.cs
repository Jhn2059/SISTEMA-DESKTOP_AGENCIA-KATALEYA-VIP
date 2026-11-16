using Kataleya.Aplicacion.Excepciones;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;

namespace Kataleya.Pruebas;

public sealed class ServicioCuadrePruebas
{
    private static readonly DateOnly Desde = new(2026, 10, 1);
    private static readonly DateOnly Hasta = new(2026, 10, 31);

    [Fact]
    public async Task Cuadre_suma_cobros_y_resta_devoluciones()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.PagosRepositorio.MovimientosDeCuadre.AddRange(
        [
            Movimiento(pagoId: 1, monto: 100, medio: MedioPago.Efectivo),
            Movimiento(pagoId: 2, monto: 50, medio: MedioPago.Yape),
            Movimiento(
                pagoId: 3,
                monto: 30,
                medio: MedioPago.Efectivo,
                tipo: TipoDePago.Devolucion)
        ]);

        var cuadre = await escenario.Cuadre.ObtenerAsync(new ConsultaDeCuadre(Desde, Hasta));

        Assert.Equal(100, cuadre.Totales.CobradoEfectivo);
        Assert.Equal(50, cuadre.Totales.CobradoYape);
        Assert.Equal(30, cuadre.Totales.Devoluciones);
        Assert.Equal(120, cuadre.Totales.TotalNeto);
        var emisor = Assert.Single(cuadre.Emisores);
        Assert.Equal("Administradora", emisor.Emisor);
        Assert.Equal(3, emisor.Movimientos.Count);
        Assert.Equal(120, emisor.Totales.TotalNeto);
    }

    [Fact]
    public async Task Cuadre_usa_los_dias_de_lima()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.PagosRepositorio.MovimientosDeCuadre.AddRange(
        [
            Movimiento(
                pagoId: 1,
                fecha: new DateTime(2026, 10, 1, 4, 59, 0, DateTimeKind.Utc)),
            Movimiento(
                pagoId: 2,
                fecha: new DateTime(2026, 10, 1, 5, 0, 0, DateTimeKind.Utc)),
            Movimiento(
                pagoId: 3,
                fecha: new DateTime(2026, 11, 1, 4, 59, 0, DateTimeKind.Utc)),
            Movimiento(
                pagoId: 4,
                fecha: new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc))
        ]);

        var cuadre = await escenario.Cuadre.ObtenerAsync(new ConsultaDeCuadre(Desde, Hasta));

        var emisor = Assert.Single(cuadre.Emisores);
        Assert.Equal(2, emisor.Movimientos.Count);
        Assert.Equal(200, cuadre.Totales.CobradoEfectivo);
    }

    [Fact]
    public async Task Cuadre_informa_anulados_y_adelantos_retenidos_sin_sumarlos()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.PasajesRepositorio.AnuladosDeCuadre.Add(
            Anulado(pasajeId: 10, importe: 120, pagado: 50));

        var cuadre = await escenario.Cuadre.ObtenerAsync(new ConsultaDeCuadre(Desde, Hasta));

        Assert.Equal(1, cuadre.Totales.CantidadPasajesAnulados);
        Assert.Equal(120, cuadre.Totales.ImportePasajesAnulados);
        Assert.Equal(50, cuadre.Totales.AdelantosRetenidos);
        Assert.Equal(0, cuadre.Totales.CobradoEfectivo);
        Assert.Equal(0, cuadre.Totales.TotalNeto);
        var emisor = Assert.Single(cuadre.Emisores);
        var anulado = Assert.Single(emisor.PasajesAnulados);
        Assert.Equal("Motivo de la anulación", anulado.MotivoDeAnulacion);
    }

    [Fact]
    public async Task Cuadre_lista_adelantos_pendientes_de_saldo()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.PasajesRepositorio.PendientesDeCuadre.Add(
            Pendiente(pasajeId: 11, importe: 120, pagado: 50, saldo: 70));

        var cuadre = await escenario.Cuadre.ObtenerAsync(new ConsultaDeCuadre(Desde, Hasta));

        Assert.Equal(1, cuadre.Totales.CantidadAdelantosPendientes);
        Assert.Equal(70, cuadre.Totales.ImporteAdelantosPendientes);
        var emisor = Assert.Single(cuadre.Emisores);
        var pendiente = Assert.Single(emisor.AdelantosPendientes);
        Assert.Equal(70, pendiente.Saldo);
        Assert.Equal(EstadoPasaje.ReservadoConAdelanto, pendiente.Estado);
    }

    [Fact]
    public async Task Cuadre_solo_suma_efectivo_y_yape_al_cobrado()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.PagosRepositorio.MovimientosDeCuadre.Add(
            Movimiento(pagoId: 1, monto: 40, medio: MedioPago.Tarjeta));

        var cuadre = await escenario.Cuadre.ObtenerAsync(new ConsultaDeCuadre(Desde, Hasta));

        Assert.Equal(0, cuadre.Totales.CobradoEfectivo);
        Assert.Equal(0, cuadre.Totales.CobradoYape);
        Assert.Equal(0, cuadre.Totales.TotalNeto);
        Assert.Single(Assert.Single(cuadre.Emisores).Movimientos);
    }

    [Fact]
    public async Task Cuadre_sin_datos_devuelve_ceros()
    {
        var escenario = new EscenarioDeConfiguracion();

        var cuadre = await escenario.Cuadre.ObtenerAsync(new ConsultaDeCuadre(Desde, Hasta));

        Assert.Empty(cuadre.Emisores);
        Assert.Equal(TotalesDeCuadre.Vacio, cuadre.Totales);
    }

    [Fact]
    public async Task Cuadre_rechaza_desde_posterior_a_hasta()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Cuadre.ObtenerAsync(new ConsultaDeCuadre(Hasta, Desde)));

        Assert.Equal("desde", excepcion.Campo);
    }

    [Fact]
    public async Task Cuadre_del_administrador_muestra_todos_los_emisores()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.PagosRepositorio.MovimientosDeCuadre.AddRange(
        [
            Movimiento(pagoId: 1, emisorId: 1, emisor: "Administradora", monto: 100),
            Movimiento(pagoId: 2, emisorId: 2, emisor: "Encargado", monto: 50)
        ]);

        var cuadre = await escenario.Cuadre.ObtenerAsync(new ConsultaDeCuadre(Desde, Hasta));

        Assert.Equal(2, cuadre.Emisores.Count);
        Assert.Equal(150, cuadre.Totales.CobradoEfectivo);
    }

    [Fact]
    public async Task Cuadre_del_encargado_solo_muestra_su_propio_emisor()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.PagosRepositorio.MovimientosDeCuadre.AddRange(
        [
            Movimiento(pagoId: 1, emisorId: 1, emisor: "Administradora", monto: 100),
            Movimiento(pagoId: 2, emisorId: 2, emisor: "Encargado", monto: 50)
        ]);

        var encargado = new Usuario(
            2,
            "Encargado",
            "encargado",
            string.Empty,
            string.Empty,
            EstadoCuenta.Activa,
            0,
            null,
            new Rol(2, "Encargado de ventas"),
            new Sede(1, "Central"));
        escenario.CambiarActor(encargado);

        var cuadre = await escenario.Cuadre.ObtenerAsync(new ConsultaDeCuadre(Desde, Hasta));

        var emisor = Assert.Single(cuadre.Emisores);
        Assert.Equal(2, emisor.EmisorId);
        Assert.Equal(50, cuadre.Totales.CobradoEfectivo);
    }

    private static MovimientoDeCuadre Movimiento(
        long pagoId,
        int emisorId = 1,
        string emisor = "Administradora",
        decimal monto = 100,
        MedioPago medio = MedioPago.Efectivo,
        TipoDePago tipo = TipoDePago.PagoTotal,
        DateTime? fecha = null) =>
        new(
            pagoId,
            pagoId,
            null,
            fecha ?? new DateTime(2026, 10, 15, 15, 0, 0, DateTimeKind.Utc),
            monto,
            medio,
            tipo,
            emisorId,
            emisor,
            emisorId,
            emisor,
            "Pasajero",
            "12345678",
            1,
            "Huancayo",
            "Ica",
            new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc),
            120,
            EstadoPasaje.Pagado,
            null,
            null,
            null);

    private static PasajeDeCuadre Anulado(
        long pasajeId,
        int emisorId = 1,
        string emisor = "Administradora",
        decimal importe = 120,
        decimal pagado = 50) =>
        new(
            pasajeId,
            null,
            1,
            "Pasajero",
            "12345678",
            "Huancayo",
            "Ica",
            new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc),
            importe,
            pagado,
            0,
            EstadoPasaje.Anulado,
            emisorId,
            emisor,
            null,
            "Motivo de la anulación",
            new DateTime(2026, 10, 15, 18, 0, 0, DateTimeKind.Utc));

    private static PasajeDeCuadre Pendiente(
        long pasajeId,
        int emisorId = 1,
        string emisor = "Administradora",
        decimal importe = 120,
        decimal pagado = 50,
        decimal saldo = 70) =>
        new(
            pasajeId,
            null,
            1,
            "Pasajero",
            "12345678",
            "Huancayo",
            "Ica",
            new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc),
            importe,
            pagado,
            saldo,
            EstadoPasaje.ReservadoConAdelanto,
            emisorId,
            emisor,
            null,
            null,
            null);
}
