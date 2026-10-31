using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Services;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;

namespace Kataleya.Pruebas;

public sealed class ServicioConfiguracionPruebas
{
    [Fact]
    public async Task Guardar_tarifa_programada_valida_guarda_id_y_bitacora()
    {
        var escenario = new EscenarioDeConfiguracion();

        var tarifa = await escenario.Tarifas.GuardarTarifaAsync(Solicitud("Fiestas", 150));

        var guardada = Assert.IsType<TarifaProgramada>(tarifa);
        Assert.True(guardada.Id > 0);
        Assert.Equal("Fiestas", guardada.Nombre);
        Assert.Equal("Administradora", guardada.FijadaPor);
        Assert.Single(escenario.TarifasRepositorio.Tarifas);
        Assert.Single(escenario.Bitacora.Registros);
        Assert.Equal("Guardar tarifa programada", escenario.Bitacora.Registros[0].Operacion);
    }

    [Fact]
    public async Task Guardar_tarifa_programada_con_fecha_final_anterior_falla()
    {
        var escenario = new EscenarioDeConfiguracion();
        var solicitud = new SolicitudDeTarifa(
            "Fiestas",
            150,
            new Periodo(new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 26)),
            null);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Tarifas.GuardarTarifaAsync(solicitud));

        Assert.Equal("vigencia", excepcion.Campo);
    }

    [Fact]
    public async Task Guardar_tarifa_programada_cruzada_falla()
    {
        var escenario = new EscenarioDeConfiguracion();
        await escenario.Tarifas.GuardarTarifaAsync(Solicitud("Fiestas", 150));
        var otra = new SolicitudDeTarifa(
            "Escolar",
            130,
            new Periodo(new DateOnly(2026, 7, 30), new DateOnly(2026, 8, 2)),
            null);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Tarifas.GuardarTarifaAsync(otra));

        Assert.Equal("vigencia", excepcion.Campo);
        Assert.Contains("Fiestas", excepcion.Message);
    }

    [Fact]
    public async Task Actualizar_tarifa_programada_conserva_id_y_registra_bitacora()
    {
        var escenario = new EscenarioDeConfiguracion();
        var tarifa = await escenario.Tarifas.GuardarTarifaAsync(Solicitud("Fiestas", 150));
        var solicitud = new SolicitudDeTarifa(
            "Fiestas patrias",
            180,
            new Periodo(new DateOnly(2026, 7, 28), new DateOnly(2026, 8, 2)),
            Sentido.HuancayoIca);

        var tarifaActualizada = await escenario.Tarifas.ActualizarTarifaAsync(
            tarifa.Id,
            solicitud);
        var actualizada = Assert.IsType<TarifaProgramada>(tarifaActualizada);

        Assert.Equal(tarifa.Id, actualizada.Id);
        Assert.Equal("Fiestas patrias", actualizada.Nombre);
        Assert.Equal(180, actualizada.Importe);
        Assert.Equal(Sentido.HuancayoIca, actualizada.Sentido);
        Assert.Single(escenario.TarifasRepositorio.Tarifas);
        Assert.Equal(2, escenario.Bitacora.Registros.Count);
        Assert.Equal("Editar tarifa programada", escenario.Bitacora.Registros[^1].Operacion);
    }

    [Fact]
    public async Task Actualizar_tarifa_programada_cruzada_falla()
    {
        var escenario = new EscenarioDeConfiguracion();
        await escenario.Tarifas.GuardarTarifaAsync(Solicitud("Fiestas", 150));
        var segunda = await escenario.Tarifas.GuardarTarifaAsync(new SolicitudDeTarifa(
            "Escolar",
            130,
            new Periodo(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5)),
            null));
        var solicitud = new SolicitudDeTarifa(
            "Escolar",
            130,
            new Periodo(new DateOnly(2026, 7, 30), new DateOnly(2026, 8, 2)),
            null);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Tarifas.ActualizarTarifaAsync(segunda.Id, solicitud));

        Assert.Equal("vigencia", excepcion.Campo);
        Assert.Contains("Fiestas", excepcion.Message);
    }

    [Fact]
    public async Task No_se_puede_actualizar_una_tarifa_regular()
    {
        var escenario = new EscenarioDeConfiguracion();
        await escenario.Tarifas.FijarTarifaRegularAsync(Sentido.HuancayoIca, 120);
        var id = escenario.TarifasRepositorio.Tarifas[0].Id;

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Tarifas.ActualizarTarifaAsync(id, Solicitud("Fiestas", 150)));

        Assert.Equal("id", excepcion.Campo);
    }

    [Fact]
    public async Task Fijar_tarifa_regular_crea_y_luego_actualiza()
    {
        var escenario = new EscenarioDeConfiguracion();

        await escenario.Tarifas.FijarTarifaRegularAsync(Sentido.HuancayoIca, 120);
        await escenario.Tarifas.FijarTarifaRegularAsync(Sentido.HuancayoIca, 130);

        var regular = Assert.Single(escenario.TarifasRepositorio.Tarifas);
        Assert.IsType<TarifaRegular>(regular);
        Assert.Equal(130, regular.Importe);
        Assert.Equal(2, escenario.Bitacora.Registros.Count);
    }

    [Fact]
    public async Task Quitar_tarifa_programada_la_elimina()
    {
        var escenario = new EscenarioDeConfiguracion();
        var tarifa = await escenario.Tarifas.GuardarTarifaAsync(Solicitud("Fiestas", 150));

        await escenario.Tarifas.QuitarTarifaAsync(tarifa.Id);

        Assert.Empty(escenario.TarifasRepositorio.Tarifas);
        Assert.Equal("Quitar tarifa programada", escenario.Bitacora.Registros[^1].Operacion);
    }

    [Fact]
    public async Task No_se_puede_quitar_una_tarifa_regular()
    {
        var escenario = new EscenarioDeConfiguracion();
        await escenario.Tarifas.FijarTarifaRegularAsync(Sentido.HuancayoIca, 120);
        var id = escenario.TarifasRepositorio.Tarifas[0].Id;

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Tarifas.QuitarTarifaAsync(id));

        Assert.Equal("id", excepcion.Campo);
    }

    [Fact]
    public async Task Contar_pasajes_de_un_horario_consulta_el_repositorio()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.PasajesRepositorio.Cantidad = 3;

        var cantidad = await escenario.Consultas.ContarPasajesDelHorarioAsync(new TimeOnly(6, 0));

        Assert.Equal(3, cantidad);
        Assert.Equal(new TimeOnly(6, 0), escenario.PasajesRepositorio.HoraConsultada);
    }

    [Fact]
    public async Task Obtener_tarifa_vigente_prefiere_la_programada()
    {
        var escenario = new EscenarioDeConfiguracion();
        await escenario.Tarifas.FijarTarifaRegularAsync(Sentido.HuancayoIca, 120);
        await escenario.Tarifas.GuardarTarifaAsync(Solicitud("Fiestas", 150));

        var programada = await escenario.Tarifas.ObtenerTarifaVigenteAsync(
            new DateOnly(2026, 7, 27),
            Sentido.HuancayoIca);
        var regular = await escenario.Tarifas.ObtenerTarifaVigenteAsync(
            new DateOnly(2026, 8, 2),
            Sentido.HuancayoIca);

        Assert.Equal(150, programada.Importe);
        Assert.Equal(120, regular.Importe);
    }

    [Fact]
    public async Task Reintento_idempotente_de_tarifa_programada_no_duplica()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.Contexto.ClaveDeOperacion = Guid.NewGuid();

        var primera = await escenario.Tarifas.GuardarTarifaAsync(Solicitud("Fiestas", 150));
        var segunda = await escenario.Tarifas.GuardarTarifaAsync(Solicitud("Fiestas", 150));

        Assert.Equal(primera.Id, segunda.Id);
        Assert.Single(escenario.TarifasRepositorio.Tarifas);
        Assert.Single(escenario.Bitacora.Registros);
    }

    [Fact]
    public async Task Agregar_horario_ordena_y_evita_duplicados()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.ParametrosRepositorio.Sembrar(ServicioParametros.Horarios, "06:00");

        await escenario.Parametros.AgregarHorarioAsync(new TimeOnly(7, 30));

        var parametro = await escenario.ParametrosRepositorio.BuscarAsync(ServicioParametros.Horarios);
        Assert.Equal("06:00,07:30", parametro!.Valor);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Parametros.AgregarHorarioAsync(new TimeOnly(6, 0)));
        Assert.Equal("hora", excepcion.Campo);
        Assert.Single(escenario.Bitacora.Registros);
    }

    [Fact]
    public async Task Agregar_horario_existente_devuelve_campo()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.ParametrosRepositorio.Sembrar(ServicioParametros.Horarios, "06:00");

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Parametros.AgregarHorarioAsync(new TimeOnly(6, 0)));

        Assert.Equal("hora", excepcion.Campo);
    }

    [Fact]
    public async Task Quitar_el_ultimo_horario_falla()
    {
        var escenario = new EscenarioDeConfiguracion();
        escenario.ParametrosRepositorio.Sembrar(ServicioParametros.Horarios, "06:00");

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Parametros.QuitarHorarioAsync(new TimeOnly(6, 0)));

        Assert.Equal("hora", excepcion.Campo);
        Assert.Contains("al menos un horario", excepcion.Message);
    }

    [Fact]
    public async Task Guardar_plazo_fuera_de_rango_falla()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Parametros.GuardarParametroAsync(
                ServicioParametros.PlazoDeReserva,
                "31"));

        Assert.Equal("valor", excepcion.Campo);
    }

    [Fact]
    public async Task Guardar_parametro_valido_actualiza_valor_y_bitacora()
    {
        var escenario = new EscenarioDeConfiguracion();

        var parametro = await escenario.Parametros.GuardarParametroAsync(
            ServicioParametros.PlazoDeReserva,
            "10");

        Assert.Equal("10", parametro.Valor);
        Assert.Equal(1, parametro.IdUsuario);
        var guardado = await escenario.ParametrosRepositorio.BuscarAsync(
            ServicioParametros.PlazoDeReserva);
        Assert.Equal("10", guardado!.Valor);
        Assert.Single(escenario.Bitacora.Registros);
    }

    [Fact]
    public async Task Obtener_aviso_previo_usa_el_valor_guardado()
    {
        var escenario = new EscenarioDeConfiguracion();

        Assert.Equal(TimeSpan.FromHours(3), await escenario.Parametros.ObtenerAvisoPrevioAsync());

        escenario.ParametrosRepositorio.Sembrar(ServicioParametros.AvisoPrevio, "5");

        Assert.Equal(TimeSpan.FromHours(5), await escenario.Parametros.ObtenerAvisoPrevioAsync());
    }

    [Fact]
    public async Task Obtener_tipos_de_vehiculo_devuelve_los_registrados()
    {
        var escenario = new EscenarioDeConfiguracion();

        var tipos = await escenario.Parametros.ObtenerTiposDeVehiculoAsync();

        Assert.Equal(2, tipos.Count);
        Assert.Contains(tipos, tipo => tipo.Nombre == "Auto" && tipo.CantidadDeAsientos == 4);
    }

    [Fact]
    public async Task Guardar_tarifa_con_importe_invalido_devuelve_campo()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Tarifas.GuardarTarifaAsync(Solicitud("Fiestas", 0)));

        Assert.Equal("importe", excepcion.Campo);
    }

    [Fact]
    public async Task Guardar_tarifa_con_mas_de_dos_decimales_falla()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Tarifas.GuardarTarifaAsync(Solicitud("Fiestas", 150.999m)));

        Assert.Equal("importe", excepcion.Campo);
        Assert.Contains("dos decimales", excepcion.Message);
    }

    [Fact]
    public async Task Fijar_tarifa_regular_con_mas_de_dos_decimales_falla()
    {
        var escenario = new EscenarioDeConfiguracion();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Tarifas.FijarTarifaRegularAsync(Sentido.HuancayoIca, 120.999m));

        Assert.Equal("importe", excepcion.Campo);
        Assert.Contains("dos decimales", excepcion.Message);
    }

    [Fact]
    public void Tarifa_programada_valida_nombre_y_vigencia()
    {
        var usuario = new Usuario(
            1,
            "Administradora",
            "admin",
            string.Empty,
            string.Empty,
            EstadoCuenta.Activa,
            0,
            null,
            new Rol(1, "Administrador"),
            new Sede(1, "Central"));
        var tarifa = new TarifaProgramada(
            new SolicitudDeTarifa(
                " ",
                0,
                new Periodo(new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 26)),
                null),
            usuario);

        var errores = tarifa.Validar();

        Assert.Contains(errores, error => error.Campo == "nombre");
        Assert.Contains(errores, error => error.Campo == "importe");
        Assert.Contains(errores, error => error.Campo == "vigencia");
    }

    private static SolicitudDeTarifa Solicitud(string nombre, decimal importe) =>
        new(
            nombre,
            importe,
            new Periodo(new DateOnly(2026, 7, 26), new DateOnly(2026, 7, 31)),
            null);
}
