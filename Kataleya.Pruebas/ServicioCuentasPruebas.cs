using Kataleya.Aplicacion.Excepciones;
using Kataleya.Contratos.Session;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;

namespace Kataleya.Pruebas;

public sealed class ServicioCuentasPruebas
{
    [Fact]
    public async Task Crear_cuenta_valida_guarda_contrasena_y_bitacora()
    {
        var escenario = new Escenario();

        var cuenta = await escenario.Cuentas.CrearCuentaAsync(Solicitud("vendedor1"));

        Assert.True(cuenta.Id > 0);
        Assert.Equal("vendedor1", cuenta.NombreUsuario);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
        Assert.True(cuenta.VerificarContrasena("Clave1234"));
        Assert.Single(escenario.Bitacora.Registros);
        Assert.Equal("Crear cuenta", escenario.Bitacora.Registros[0].Operacion);
        Assert.Equal("admin", escenario.Bitacora.Registros[0].Usuario.NombreUsuario);
    }

    [Fact]
    public async Task Crear_cuenta_con_usuario_duplicado_devuelve_campo()
    {
        var escenario = new Escenario();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Cuentas.CrearCuentaAsync(Solicitud("admin")));

        Assert.Equal("nombreUsuario", excepcion.Campo);
        Assert.Equal("Ese nombre de usuario ya existe.", excepcion.Message);
    }

    [Fact]
    public async Task Crear_cuenta_con_nombre_demasiado_largo_devuelve_campo()
    {
        var escenario = new Escenario();
        var solicitud = Solicitud("vendedor1") with { NombrePersona = new string('A', 101) };

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Cuentas.CrearCuentaAsync(solicitud));

        Assert.Equal("nombrePersona", excepcion.Campo);
        Assert.Contains("100", excepcion.Message);
    }

    [Fact]
    public async Task Crear_cuenta_con_usuario_demasiado_largo_devuelve_campo()
    {
        var escenario = new Escenario();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Cuentas.CrearCuentaAsync(Solicitud(new string('u', 31))));

        Assert.Equal("nombreUsuario", excepcion.Campo);
        Assert.Contains("30", excepcion.Message);
    }

    [Fact]
    public async Task Crear_cuenta_con_contrasena_corta_devuelve_campo()
    {
        var escenario = new Escenario();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Cuentas.CrearCuentaAsync(Solicitud("vendedor1", "corta")));

        Assert.Equal("contrasenaInicial", excepcion.Campo);
        Assert.Contains("al menos 8", excepcion.Message);
    }

    [Fact]
    public async Task Desactivar_administradora_falla()
    {
        var escenario = new Escenario();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Cuentas.DesactivarAsync(escenario.Administradora));

        Assert.Equal("cuenta", excepcion.Campo);
        Assert.Equal(EstadoCuenta.Activa, escenario.Administradora.Estado);
    }

    [Fact]
    public async Task Desactivar_y_reactivar_cuenta_cambia_estado()
    {
        var escenario = new Escenario();
        var cuenta = await escenario.Cuentas.CrearCuentaAsync(Solicitud("vendedor1"));

        await escenario.Cuentas.DesactivarAsync(cuenta);
        Assert.Equal(EstadoCuenta.Desactivada, cuenta.Estado);

        await escenario.Cuentas.ReactivarAsync(cuenta);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
        Assert.Equal(3, escenario.Bitacora.Registros.Count);
    }

    [Fact]
    public async Task Desbloquear_cuenta_bloqueada_limpia_intentos()
    {
        var escenario = new Escenario();
        var cuenta = escenario.AgregarUsuario("bloqueado", "Clave1234");

        for (var intento = 0; intento < 5; intento++)
        {
            cuenta.RegistrarIntentoFallido(escenario.Reloj.Ahora);
        }

        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);

        await escenario.Cuentas.DesbloquearAsync(cuenta);

        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
        Assert.Equal(0, cuenta.CantidadDeIntentosFallidos);
        Assert.Null(cuenta.FinBloqueo);
    }

    [Fact]
    public async Task Desbloquear_cuenta_desactivada_falla()
    {
        var escenario = new Escenario();
        var cuenta = escenario.AgregarUsuario("inactivo", "Clave1234", EstadoCuenta.Desactivada);

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Cuentas.DesbloquearAsync(cuenta));

        Assert.Equal("cuenta", excepcion.Campo);
        Assert.Equal(EstadoCuenta.Desactivada, cuenta.Estado);
    }

    [Fact]
    public async Task Restablecer_contrasena_permite_ingresar_con_la_nueva()
    {
        var escenario = new Escenario();
        var cuenta = await escenario.Cuentas.CrearCuentaAsync(Solicitud("vendedor1"));

        await escenario.Cuentas.RestablecerContrasenaAsync(cuenta, "NuevaClave1");

        Assert.False(cuenta.VerificarContrasena("Clave1234"));
        Assert.True(cuenta.VerificarContrasena("NuevaClave1"));

        var inicio = await escenario.Sesion.IniciarSesionAsync("vendedor1", "NuevaClave1");
        Assert.NotNull(inicio.Cuenta);
    }

    [Fact]
    public async Task Operacion_idempotente_no_duplica_cuenta_ni_bitacora()
    {
        var escenario = new Escenario();
        escenario.Contexto.ClaveDeOperacion = Guid.NewGuid();

        var primera = await escenario.Cuentas.CrearCuentaAsync(Solicitud("vendedor1"));
        var segunda = await escenario.Cuentas.CrearCuentaAsync(Solicitud("vendedor1"));

        Assert.Equal(primera.NombreUsuario, segunda.NombreUsuario);
        Assert.Single(escenario.Usuarios.Todos, usuario => usuario.NombreUsuario == "vendedor1");
        Assert.Single(escenario.Bitacora.Registros);
    }

    [Fact]
    public async Task Login_bloquea_despues_de_cinco_intentos_y_se_libera_al_pasar_el_tiempo()
    {
        var escenario = new Escenario();
        escenario.AgregarUsuario("vendedor1", "Clave1234");

        ResultadoDeInicioDeSesion? ultimo = null;

        for (var intento = 0; intento < 5; intento++)
        {
            ultimo = await escenario.Sesion.IniciarSesionAsync("vendedor1", "incorrecta");
        }

        Assert.NotNull(ultimo);
        Assert.Equal(MotivoDeRechazo.CuentaBloqueada, ultimo.Rechazo);

        escenario.Reloj.Ahora = escenario.Reloj.Ahora.AddMinutes(4);

        var inicio = await escenario.Sesion.IniciarSesionAsync("vendedor1", "Clave1234");

        Assert.NotNull(inicio.Cuenta);
        Assert.Equal(EstadoCuenta.Activa, inicio.Cuenta.Estado);
    }

    [Fact]
    public async Task Login_de_cuenta_desactivada_devuelve_motivo()
    {
        var escenario = new Escenario();
        escenario.AgregarUsuario("inactivo", "Clave1234", EstadoCuenta.Desactivada);

        var inicio = await escenario.Sesion.IniciarSesionAsync("inactivo", "Clave1234");

        Assert.Equal(MotivoDeRechazo.CuentaDesactivada, inicio.Rechazo);
        Assert.Null(inicio.Cuenta);
    }

    [Fact]
    public async Task Login_correcto_registra_acceso_y_limpia_intentos()
    {
        var escenario = new Escenario();
        var cuenta = escenario.AgregarUsuario("vendedor1", "Clave1234");

        await escenario.Sesion.IniciarSesionAsync("vendedor1", "incorrecta");
        Assert.Equal(1, cuenta.CantidadDeIntentosFallidos);

        var inicio = await escenario.Sesion.IniciarSesionAsync("vendedor1", "Clave1234");

        Assert.NotNull(inicio.Cuenta);
        Assert.Equal(0, cuenta.CantidadDeIntentosFallidos);
        Assert.Contains(
            escenario.Bitacora.Registros,
            registro => registro.Operacion == "Iniciar sesión");
    }

    [Fact]
    public async Task La_cuenta_principal_no_se_desactiva()
    {
        var escenario = new Escenario();

        var excepcion = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => escenario.Cuentas.DesactivarAsync(escenario.Administradora));

        Assert.Equal("La cuenta de la administradora no puede desactivarse.", excepcion.Message);
        Assert.Equal(EstadoCuenta.Activa, escenario.Administradora.Estado);
        Assert.Throws<InvalidOperationException>(() => escenario.Administradora.Desactivar());
    }

    [Fact]
    public async Task Una_cuenta_nueva_se_puede_desactivar()
    {
        var escenario = new Escenario();
        var cuenta = await escenario.Cuentas.CrearCuentaAsync(Solicitud("vendedor1"));

        Assert.True(cuenta.PuedeDesactivarse());

        await escenario.Cuentas.DesactivarAsync(cuenta);

        Assert.Equal(EstadoCuenta.Desactivada, cuenta.Estado);
    }

    [Fact]
    public void EstaBloqueada_solo_pregunta_y_el_intento_siguiente_vuelve_a_contar()
    {
        var escenario = new Escenario();
        var cuenta = escenario.AgregarUsuario("vendedor1", "Clave1234");
        var ahora = escenario.Reloj.Ahora;

        for (var intento = 0; intento < 5; intento++)
        {
            cuenta.RegistrarIntentoFallido(ahora);
        }

        Assert.True(cuenta.EstaBloqueada(ahora.AddMinutes(2)));
        Assert.False(cuenta.EstaBloqueada(ahora.AddMinutes(4)));
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);

        cuenta.RegistrarIntentoFallido(ahora.AddMinutes(4));

        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
        Assert.Equal(1, cuenta.CantidadDeIntentosFallidos);
    }

    [Fact]
    public async Task El_tema_elegido_vuelve_al_iniciar_sesion()
    {
        var escenario = new Escenario();
        var cuenta = escenario.AgregarUsuario("vendedor1", "Clave1234");

        var primera = await escenario.Sesion.IniciarSesionAsync("vendedor1", "Clave1234");
        Assert.Null(primera.Cuenta!.Tema);

        escenario.Contexto.Usuario = cuenta;
        await escenario.Cuentas.ElegirTemaAsync(Tema.Claro);

        var segunda = await escenario.Sesion.IniciarSesionAsync("vendedor1", "Clave1234");
        Assert.Equal(Tema.Claro, segunda.Cuenta!.Tema);
    }

    private static SolicitudDeCuenta Solicitud(
        string nombreUsuario,
        string contrasena = "Clave1234") =>
        new("Vendedor Uno", nombreUsuario, contrasena, IdRol: 2, IdSede: 1);
}
