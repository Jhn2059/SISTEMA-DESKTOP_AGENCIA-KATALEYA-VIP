using Kataleya.Aplicacion.Excepciones;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;

namespace Kataleya.Pruebas;

public sealed class ServicioGruposPruebas
{
    private static readonly DateTime FechaDelGrupo =
        new(2026, 11, 2, 13, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public async Task Grupo_pagado_emite_un_boleto_por_integrante()
    {
        var escenario = new EscenarioDeConfiguracion();

        var grupo = await escenario.Ventas.RegistrarGrupoAsync(
            Solicitud(monto: 300, medio: MedioPago.Efectivo));

        Assert.True(grupo.Id > 0);
        Assert.Equal("G0001", grupo.Codigo);
        Assert.Equal(2, grupo.Pasajes.Count);
        Assert.All(grupo.Pasajes, pasaje =>
        {
            Assert.Null(pasaje.UnidadDeSalidaId);
            Assert.Null(pasaje.NumeroAsiento);
            Assert.Equal(grupo.Id, pasaje.GrupoId);
            Assert.Equal("G0001", pasaje.CodigoDeGrupo);
            Assert.Equal(300, pasaje.Importe);
            Assert.Equal(300, pasaje.Pagado);
            Assert.Equal(0, pasaje.Saldo);
            Assert.Equal(EstadoPasaje.Pagado, pasaje.Estado);
            Assert.NotNull(pasaje.Boleto);
        });
        Assert.Single(grupo.Pasajes, pasaje => pasaje.EsResponsable);
        Assert.Equal(300, grupo.Pagado);
        Assert.Equal(0, grupo.Saldo);
        Assert.Equal(EstadoPasaje.Pagado, grupo.Estado);
        Assert.Single(grupo.Unidades);
        Assert.Null(grupo.Unidades[0].Sentido);
        Assert.Equal(2, escenario.BoletosRepositorio.Boletos.Count);
        var pago = Assert.Single(escenario.PagosRepositorio.Pagos);
        Assert.Null(pago.PasajeId);
        Assert.Equal(grupo.Id, pago.GrupoId);
        Assert.Equal(TipoDePago.PagoTotal, pago.Tipo);
        Assert.Equal("Registrar grupo", escenario.Bitacora.Registros[^1].Operacion);
        Assert.Null(escenario.Bitacora.Registros[^1].PasajeId);
    }

    [Fact]
    public async Task Grupo_sin_pago_queda_reservado_sin_boletos()
    {
        var escenario = new EscenarioDeConfiguracion();

        var grupo = await escenario.Ventas.RegistrarGrupoAsync(Solicitud());

        Assert.Equal(EstadoPasaje.Reservado, grupo.Estado);
        Assert.All(grupo.Pasajes, pasaje =>
        {
            Assert.Equal(EstadoPasaje.Reservado, pasaje.Estado);
            Assert.Null(pasaje.Boleto);
        });
        Assert.Empty(escenario.PagosRepositorio.Pagos);
        Assert.Empty(escenario.BoletosRepositorio.Boletos);
    }

    [Fact]
    public async Task Grupo_exige_al_menos_dos_integrantes()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarGrupoAsync(
                Solicitud(integrantes: [Integrante(1, responsable: true)])));

        Assert.Equal("integrantes", excepcion.Campo);
        Assert.Contains("al menos dos integrantes", excepcion.Message);
        Assert.Empty(escenario.GruposRepositorio.Grupos);
    }

    [Fact]
    public async Task Grupo_exige_un_unico_responsable()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarGrupoAsync(Solicitud(
                integrantes:
                [
                    Integrante(1, responsable: true),
                    Integrante(2, responsable: true)
                ])));

        Assert.Equal("responsable", excepcion.Campo);
        Assert.Empty(escenario.GruposRepositorio.Grupos);
    }

    [Fact]
    public async Task Grupo_rechaza_pago_parcial()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarGrupoAsync(
                Solicitud(monto: 100, medio: MedioPago.Yape)));

        Assert.Equal("montoDelPago", excepcion.Campo);
        Assert.Empty(escenario.GruposRepositorio.Grupos);
        Assert.Empty(escenario.PagosRepositorio.Pagos);
    }

    [Fact]
    public async Task Grupo_rechaza_capacidad_insuficiente()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarGrupoAsync(Solicitud(
                tipos: [1],
                integrantes:
                [
                    Integrante(1, responsable: true),
                    Integrante(2),
                    Integrante(3),
                    Integrante(4),
                    Integrante(5)
                ])));

        Assert.Equal("tiposDeVehiculo", excepcion.Campo);
        Assert.Contains("5 integrantes y 4 asientos", excepcion.Message);
        Assert.Empty(escenario.GruposRepositorio.Grupos);
    }

    [Fact]
    public async Task Grupo_rechaza_fecha_fuera_de_siete_dias()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarGrupoAsync(Solicitud(
                fecha: new DateTime(2026, 11, 10, 13, 0, 0, DateTimeKind.Unspecified))));

        Assert.Equal("fechaHoraSalida", excepcion.Campo);
        Assert.Empty(escenario.GruposRepositorio.Grupos);
    }

    [Fact]
    public async Task Grupo_rechaza_documento_invalido_del_integrante()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarGrupoAsync(Solicitud(
                integrantes:
                [
                    Integrante(1, responsable: true, documento: "1234-5678"),
                    Integrante(2)
                ])));

        Assert.Equal("integrantes[0].numeroDocumento", excepcion.Campo);
        Assert.Empty(escenario.GruposRepositorio.Grupos);
    }

    [Fact]
    public async Task Grupo_rechaza_precio_no_positivo()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Ventas.RegistrarGrupoAsync(Solicitud(precio: 0)));

        Assert.Equal("precioAcordado", excepcion.Campo);
        Assert.Empty(escenario.GruposRepositorio.Grupos);
    }

    [Fact]
    public async Task Grupo_idempotente_no_duplica_datos()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.Contexto.ClaveDeOperacion = Guid.NewGuid();
        var solicitud = Solicitud(monto: 300, medio: MedioPago.Efectivo);

        var primera = await escenario.Ventas.RegistrarGrupoAsync(solicitud);
        var segunda = await escenario.Ventas.RegistrarGrupoAsync(solicitud);

        Assert.Equal(primera.Id, segunda.Id);
        Assert.Equal(primera.Codigo, segunda.Codigo);
        Assert.Single(escenario.GruposRepositorio.Grupos);
        Assert.Equal(2, escenario.PasajesRepositorio.Pasajes.Count);
        Assert.Single(escenario.PagosRepositorio.Pagos);
        Assert.Equal(2, escenario.BoletosRepositorio.Boletos.Count);
        Assert.Equal(300, segunda.Pagado);
    }

    [Fact]
    public async Task Un_pasaje_de_grupo_no_admite_cobros_sueltos()
    {
        var escenario = new EscenarioDeConfiguracion();
        var grupo = await escenario.Ventas.RegistrarGrupoAsync(Solicitud());

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Cobros.RegistrarCobroDelSaldoAsync(
                grupo.Pasajes[0].Id,
                new SolicitudDeCobro(100, MedioPago.Efectivo)));

        Assert.Equal("pasajeId", excepcion.Campo);
    }

    private static SolicitudDeGrupo Solicitud(
        DateTime? fecha = null,
        decimal precio = 300,
        IReadOnlyList<short>? tipos = null,
        IReadOnlyList<IntegranteDeGrupo>? integrantes = null,
        decimal? monto = null,
        MedioPago? medio = null) =>
        new(
            "Huancayo",
            "Lima",
            "Terminal principal",
            fecha ?? FechaDelGrupo,
            precio,
            "987654321",
            tipos ?? [2],
            integrantes ??
            [
                Integrante(1, responsable: true),
                Integrante(2)
            ],
            monto,
            medio);

    private static IntegranteDeGrupo Integrante(
        int numero,
        bool responsable = false,
        string? documento = null) =>
        new(
            $"Integrante {numero}",
            TipoDocumento.DNI,
            documento ?? $"1234567{numero}",
            responsable);
}
