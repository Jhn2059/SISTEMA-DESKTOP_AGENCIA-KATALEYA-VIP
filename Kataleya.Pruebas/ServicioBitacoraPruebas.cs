using Kataleya.Aplicacion.Excepciones;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;

namespace Kataleya.Pruebas;

public sealed class ServicioBitacoraPruebas
{
    [Fact]
    public async Task Administrador_consulta_toda_la_bitacora_del_rango()
    {
        var escenario = new EscenarioDeConfiguracion();
        var encargado = CrearEncargado();
        Sembrar(escenario, escenario.Administradora, "Abrir unidad de salida", null,
            new DateTime(2026, 10, 15, 13, 0, 0, DateTimeKind.Utc));
        Sembrar(escenario, encargado, "Anular pasaje", 7,
            new DateTime(2026, 10, 15, 14, 0, 0, DateTimeKind.Utc));
        Sembrar(escenario, escenario.Administradora, "Fuera del rango", null,
            new DateTime(2026, 10, 16, 5, 0, 0, DateTimeKind.Utc));

        var registros = await escenario.BitacoraServicio.ConsultarAsync(
            new ConsultaDeBitacora(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 15)));

        Assert.Equal(2, registros.Count);
        Assert.Equal("Anular pasaje", registros[0].Operacion);
        Assert.Equal("Abrir unidad de salida", registros[1].Operacion);
    }

    [Fact]
    public async Task Encargado_solo_consulta_sus_propios_registros()
    {
        var escenario = new EscenarioDeConfiguracion();
        var encargado = CrearEncargado();
        Sembrar(escenario, escenario.Administradora, "Abrir unidad de salida", null,
            new DateTime(2026, 10, 15, 13, 0, 0, DateTimeKind.Utc));
        Sembrar(escenario, encargado, "Anular pasaje", 7,
            new DateTime(2026, 10, 15, 14, 0, 0, DateTimeKind.Utc));
        escenario.Contexto.Usuario = encargado;

        var registros = await escenario.BitacoraServicio.ConsultarAsync(
            new ConsultaDeBitacora(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 15)));

        var registro = Assert.Single(registros);
        Assert.Equal(encargado.Id, registro.Usuario.Id);
        Assert.Equal("Anular pasaje", registro.Operacion);
    }

    [Fact]
    public async Task Consulta_expone_pasaje_y_valores_guardados_sin_alterarlos()
    {
        var escenario = new EscenarioDeConfiguracion();
        const string anterior = """{"estado":"Reservado"}""";
        const string nuevo = """{"estado":"Anulado"}""";
        Sembrar(
            escenario,
            escenario.Administradora,
            "Anular pasaje",
            9,
            new DateTime(2026, 10, 15, 14, 0, 0, DateTimeKind.Utc),
            anterior,
            nuevo);

        var registro = Assert.Single(await escenario.BitacoraServicio.ConsultarAsync(
            new ConsultaDeBitacora(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 15))));

        Assert.Equal(9, registro.PasajeId);
        Assert.Equal(anterior, registro.ValorAnterior);
        Assert.Equal(nuevo, registro.ValorNuevo);
    }

    [Fact]
    public async Task Rango_con_desde_posterior_a_hasta_falla()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.BitacoraServicio.ConsultarAsync(
                new ConsultaDeBitacora(new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 15))));

        Assert.Equal("desde", excepcion.Campo);
    }

    private static void Sembrar(
        EscenarioDeConfiguracion escenario,
        Usuario usuario,
        string operacion,
        long? pasajeId,
        DateTime fechaHora,
        string? valorAnterior = null,
        string? valorNuevo = null) =>
        escenario.Bitacora.Registros.Add(new RegistroDeBitacora(
            0,
            fechaHora,
            operacion,
            usuario,
            valorAnterior,
            valorNuevo,
            pasajeId: pasajeId));

    private static Usuario CrearEncargado()
    {
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
        encargado.FijarContrasena("Encargado1234");
        return encargado;
    }
}
