using System.Net;
using System.Text;
using Kataleya.Cliente;
using Kataleya.Contratos.Cuentas;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Kataleya.Dominio.ValueObjects;

namespace Kataleya.Pruebas;

public sealed class ClienteApiPruebas
{
    [Fact]
    public async Task Obtener_cuentas_con_respuesta_exitosa_devuelve_los_datos()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.OK,
            """
            [
              {
                "id": 1,
                "nombre": "Administrador",
                "nombreUsuario": "admin",
                "rol": "Administrador",
                "sede": "Huancayo",
                "estado": "Activa",
                "finBloqueo": null
              }
            ]
            """);

        var resultado = await CrearCliente(manejador).ObtenerCuentasAsync();

        Assert.True(resultado.Exito);
        Assert.NotNull(resultado.Datos);
        Assert.Single(resultado.Datos);
        Assert.Equal("admin", resultado.Datos[0].NombreUsuario);
    }

    [Fact]
    public async Task Respuesta_de_error_sin_errores_de_campo_no_es_exitosa()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.NotFound,
            """{"title":"No se encontró la cuenta."}""");

        var resultado = await CrearCliente(manejador).DesactivarAsync(99);

        Assert.False(resultado.Exito);
    }

    [Fact]
    public async Task Respuesta_401_marca_la_sesion_como_expirada()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.Unauthorized,
            """{"title":"La sesión venció."}""");

        var resultado = await CrearCliente(manejador).ObtenerRolesAsync();

        Assert.False(resultado.Exito);
        Assert.True(resultado.SesionExpirada);
    }

    [Fact]
    public async Task Respuesta_400_con_errores_de_campo_los_expone()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.BadRequest,
            """
            {
              "title": "One or more validation errors occurred.",
              "errors": {
                "nombreUsuario": ["Ese nombre de usuario ya existe."]
              }
            }
            """);

        var resultado = await CrearCliente(manejador).CrearCuentaAsync(Solicitud("vendedor1"));

        Assert.False(resultado.Exito);
        var error = Assert.Single(resultado.Errores);
        Assert.Equal("nombreUsuario", error.Campo);
        Assert.Equal("Ese nombre de usuario ya existe.", error.Mensaje);
    }

    [Fact]
    public async Task Reintentar_la_misma_operacion_reutiliza_la_clave_de_idempotencia()
    {
        var manejador = new ManejadorFalso()
            .Encolar(HttpStatusCode.InternalServerError, """{"title":"Error"}""")
            .Encolar(
                HttpStatusCode.OK,
                """
                {
                  "id": 2,
                  "nombre": "Vendedor Uno",
                  "nombreUsuario": "vendedor1",
                  "rol": "Encargado de ventas",
                  "sede": "Huancayo",
                  "estado": "Activa",
                  "finBloqueo": null
                }
                """);

        var cliente = CrearCliente(manejador, reintentos: 0);
        var solicitud = Solicitud("vendedor1");

        var primera = await cliente.CrearCuentaAsync(solicitud);
        var segunda = await cliente.CrearCuentaAsync(solicitud);

        Assert.False(primera.Exito);
        Assert.True(segunda.Exito);
        Assert.Equal(2, manejador.Envios.Count);
        Assert.False(string.IsNullOrWhiteSpace(manejador.Envios[0].ClaveDeIdempotencia));
        Assert.Equal(
            manejador.Envios[0].ClaveDeIdempotencia,
            manejador.Envios[1].ClaveDeIdempotencia);
    }

    [Fact]
    public async Task Reintenta_automaticamente_un_error_transitorio()
    {
        var manejador = new ManejadorFalso()
            .Encolar(HttpStatusCode.InternalServerError, """{"title":"Error"}""")
            .Encolar(HttpStatusCode.OK, "[]");

        var resultado = await CrearCliente(manejador).ObtenerCuentasAsync();

        Assert.True(resultado.Exito);
        Assert.Equal(2, manejador.Envios.Count);
    }

    [Fact]
    public async Task Agota_los_tres_reintentos_ante_errores_transitorios()
    {
        var manejador = new ManejadorFalso()
            .Encolar(HttpStatusCode.InternalServerError, """{"title":"Error"}""")
            .Encolar(HttpStatusCode.InternalServerError, """{"title":"Error"}""")
            .Encolar(HttpStatusCode.InternalServerError, """{"title":"Error"}""")
            .Encolar(HttpStatusCode.InternalServerError, """{"title":"Error"}""");

        var resultado = await CrearCliente(manejador).ObtenerCuentasAsync();

        Assert.False(resultado.Exito);
        Assert.Equal(4, manejador.Envios.Count);
    }

    [Fact]
    public async Task No_reintenta_un_error_de_validacion()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.BadRequest,
            """{"title":"Datos inválidos","errors":{"nombre":["Escriba el nombre."]}}""");

        var resultado = await CrearCliente(manejador).CrearCuentaAsync(Solicitud("vendedor1"));

        Assert.False(resultado.Exito);
        Assert.Single(manejador.Envios);
    }

    [Fact]
    public async Task Reintento_automatico_reutiliza_la_clave_de_idempotencia()
    {
        var manejador = new ManejadorFalso()
            .Encolar(HttpStatusCode.InternalServerError, """{"title":"Error"}""")
            .Encolar(
                HttpStatusCode.OK,
                """
                {
                  "id": 2,
                  "nombre": "Vendedor Uno",
                  "nombreUsuario": "vendedor1",
                  "rol": "Encargado de ventas",
                  "sede": "Huancayo",
                  "estado": "Activa",
                  "finBloqueo": null
                }
                """);

        var resultado = await CrearCliente(manejador).CrearCuentaAsync(Solicitud("vendedor1"));

        Assert.True(resultado.Exito);
        Assert.Equal(2, manejador.Envios.Count);
        Assert.False(string.IsNullOrWhiteSpace(manejador.Envios[0].ClaveDeIdempotencia));
        Assert.Equal(
            manejador.Envios[0].ClaveDeIdempotencia,
            manejador.Envios[1].ClaveDeIdempotencia);
    }

    [Fact]
    public async Task Registrar_grupo_envia_la_solicitud_con_clave_de_idempotencia()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.OK,
            """
            {
              "id": 1,
              "codigo": "G0001",
              "origen": "Huancayo",
              "destino": "Lima",
              "lugarDeRecojo": "Terminal principal",
              "fechaHoraSalida": "2026-11-17T18:00:00Z",
              "precioAcordado": 300,
              "pagado": 300,
              "saldo": 0,
              "estado": "Pagado",
              "telefonoContacto": "987654321",
              "responsable": "Ana Perez",
              "unidades": [],
              "pasajes": [],
              "pagos": []
            }
            """);

        var resultado = await CrearCliente(manejador).RegistrarGrupoAsync(
            new SolicitudDeGrupo(
                "Huancayo",
                "Lima",
                "Terminal principal",
                new DateTime(2026, 11, 17, 13, 0, 0),
                300,
                "987654321",
                [1],
                [
                    new IntegranteDeGrupo(
                        "Ana Perez",
                        TipoDocumento.DNI,
                        "12345678",
                        true),
                    new IntegranteDeGrupo(
                        "Luis Perez",
                        TipoDocumento.DNI,
                        "87654321",
                        false)
                ],
                300,
                MedioPago.Efectivo));

        Assert.True(resultado.Exito);
        Assert.Equal("G0001", resultado.Datos!.Codigo);
        var envio = Assert.Single(manejador.Envios);
        Assert.Equal("/grupos", envio.Ruta.AbsolutePath);
        Assert.False(string.IsNullOrWhiteSpace(envio.ClaveDeIdempotencia));
        Assert.Contains("integrantes", envio.Cuerpo);
    }

    [Fact]
    public async Task Registrar_diferido_envia_post_con_talonario_y_clave_de_idempotencia()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.OK,
            """
            {
              "id": 1,
              "estado": "Pagado",
              "esDiferido": true
            }
            """);

        var resultado = await CrearCliente(manejador).RegistrarDiferidoAsync(
            new SolicitudDePasaje(
                1,
                4,
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
                new NumeroDeBoleto("001", 152),
                new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc)));

        Assert.True(resultado.Exito);
        Assert.True(resultado.Datos!.EsDiferido);
        var envio = Assert.Single(manejador.Envios);
        Assert.Equal(HttpMethod.Post, envio.Metodo);
        Assert.Equal("/pasajes-diferidos", envio.Ruta.AbsolutePath);
        Assert.False(string.IsNullOrWhiteSpace(envio.ClaveDeIdempotencia));
        Assert.Contains("talonario", envio.Cuerpo);
        Assert.Contains("fechaHoraEmisionTalonario", envio.Cuerpo);
    }

    [Fact]
    public async Task Modificar_pasaje_envia_put_con_clave_de_idempotencia()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.OK,
            """
            {
              "id": 7,
              "estado": "Reservado",
              "requiereReimpresion": true
            }
            """
        );

        var resultado = await CrearCliente(manejador).ModificarPasajeAsync(
            7,
            new SolicitudDeModificacionDePasaje(
                NumeroAsiento: 3,
                LugarEmbarque: "Nuevo terminal"));

        Assert.True(resultado.Exito);
        Assert.True(resultado.Datos!.RequiereReimpresion);
        var envio = Assert.Single(manejador.Envios);
        Assert.Equal(HttpMethod.Put, envio.Metodo);
        Assert.Equal("/pasajes/7", envio.Ruta.AbsolutePath);
        Assert.False(string.IsNullOrWhiteSpace(envio.ClaveDeIdempotencia));
        Assert.Contains("numeroAsiento", envio.Cuerpo);
    }

    [Fact]
    public async Task Anular_pasaje_envia_motivo_y_clave_de_idempotencia()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.OK,
            """
            {
              "id": 7,
              "estado": "Anulado",
              "motivoAnulacion": "Cancelación.",
              "pagado": 0
            }
            """
        );

        var resultado = await CrearCliente(manejador).AnularPasajeAsync(
            7,
            new SolicitudDeAnulacionDePasaje("Cancelación."));

        Assert.True(resultado.Exito);
        Assert.Equal("Anulado", resultado.Datos!.Estado);
        var envio = Assert.Single(manejador.Envios);
        Assert.Equal(HttpMethod.Post, envio.Metodo);
        Assert.Equal("/pasajes/7/anulacion", envio.Ruta.AbsolutePath);
        Assert.False(string.IsNullOrWhiteSpace(envio.ClaveDeIdempotencia));
        Assert.Contains("motivo", envio.Cuerpo);
    }

    [Fact]
    public async Task Marcar_no_se_presento_envia_post_sin_cuerpo()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.OK,
            """
            {
              "id": 7,
              "estado": "NoSePresento"
            }
            """
        );

        var resultado = await CrearCliente(manejador).MarcarNoSePresentoAsync(7);

        Assert.True(resultado.Exito);
        Assert.Equal("NoSePresento", resultado.Datos!.Estado);
        var envio = Assert.Single(manejador.Envios);
        Assert.Equal(HttpMethod.Post, envio.Metodo);
        Assert.Equal("/pasajes/7/no-se-presento", envio.Ruta.AbsolutePath);
        Assert.Null(envio.Cuerpo);
        Assert.False(string.IsNullOrWhiteSpace(envio.ClaveDeIdempotencia));
    }

    [Fact]
    public async Task Obtener_cuadre_envia_el_rango_de_fechas()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.OK,
            """
            {
              "desde": "2026-10-01",
              "hasta": "2026-10-31",
              "totales": {
                "cobradoEfectivo": 100,
                "cobradoYape": 50,
                "devoluciones": 0,
                "totalNeto": 150,
                "adelantosRetenidos": 0,
                "cantidadPasajesAnulados": 0,
                "importePasajesAnulados": 0,
                "cantidadAdelantosPendientes": 0,
                "importeAdelantosPendientes": 0
              },
              "emisores": []
            }
            """);

        var resultado = await CrearCliente(manejador).ObtenerCuadreAsync(
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31));

        Assert.True(resultado.Exito);
        Assert.NotNull(resultado.Datos);
        Assert.Equal(150, resultado.Datos.Totales.TotalNeto);
        Assert.Empty(resultado.Datos.Emisores);
        var envio = Assert.Single(manejador.Envios);
        Assert.Equal(HttpMethod.Get, envio.Metodo);
        Assert.Equal("/cuadre", envio.Ruta.AbsolutePath);
        Assert.Contains("desde=2026-10-01", envio.Ruta.Query);
        Assert.Contains("hasta=2026-10-31", envio.Ruta.Query);
    }

    [Fact]
    public async Task Consulta_bitacora_por_rango_y_deserializa_registros()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.OK,
            """
            [
              {
                "id": 10,
                "fechaHora": "2026-10-15T14:00:00Z",
                "usuarioId": 2,
                "usuario": "Encargado",
                "operacion": "Anular pasaje",
                "pasajeId": 7,
                "valorAnterior": "{\"estado\":\"Reservado\"}",
                "valorNuevo": "{\"estado\":\"Anulado\"}",
                "claveDeOperacion": "6f9619ff-8b86-d011-b42d-00cf4fc964ff"
              }
            ]
            """);

        var resultado = await CrearCliente(manejador).ConsultarBitacoraAsync(
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31));

        Assert.True(resultado.Exito);
        Assert.NotNull(resultado.Datos);
        var registro = Assert.Single(resultado.Datos);
        Assert.Equal(7, registro.PasajeId);
        Assert.Equal("Anular pasaje", registro.Operacion);
        Assert.Equal("Encargado", registro.Usuario);
        Assert.Contains("\"Reservado\"", registro.ValorAnterior);
        var envio = Assert.Single(manejador.Envios);
        Assert.Equal(HttpMethod.Get, envio.Metodo);
        Assert.Equal("/bitacora", envio.Ruta.AbsolutePath);
        Assert.Contains("desde=2026-10-01", envio.Ruta.Query);
        Assert.Contains("hasta=2026-10-31", envio.Ruta.Query);
    }

    [Fact]
    public void Rechaza_http_sin_tls_fuera_de_localhost()
    {
        var http = new HttpClient(new ManejadorFalso())
        {
            BaseAddress = new Uri("http://ejemplo.com/")
        };

        var excepcion = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = new ClienteApi(http, new RegistroTecnicoFalso());
        });

        Assert.Contains("HTTPS", excepcion.Message);
    }

    [Fact]
    public void Permite_http_sin_tls_configurado_para_desarrollo()
    {
        var http = new HttpClient(new ManejadorFalso())
        {
            BaseAddress = new Uri("http://ejemplo.com/")
        };

        var cliente = new ClienteApi(
            http,
            new RegistroTecnicoFalso(),
            permitirHttpSinTls: true);

        Assert.NotNull(cliente);
    }

    private static ClienteApi CrearCliente(ManejadorFalso manejador, int reintentos = 3)
    {
        var http = new HttpClient(manejador)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        return new ClienteApi(http, new RegistroTecnicoFalso(), reintentos);
    }

    private static SolicitudDeCuenta Solicitud(string nombreUsuario) =>
        new()
        {
            Nombre = "Vendedor Uno",
            NombreUsuario = nombreUsuario,
            ContrasenaInicial = "Clave1234",
            IdRol = 2,
            IdSede = 1
        };

    private sealed class RegistroTecnicoFalso : IRegistroTecnico
    {
        public Task RegistrarAsync(Dominio.DataObjects.ErrorTecnico error) =>
            Task.CompletedTask;
    }

    private sealed class ManejadorFalso : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respuestas = new();

        public List<Envio> Envios { get; } = [];

        public ManejadorFalso Encolar(HttpStatusCode estado, string? contenido = null)
        {
            _respuestas.Enqueue(new HttpResponseMessage(estado)
            {
                Content = new StringContent(
                    contenido ?? string.Empty,
                    Encoding.UTF8,
                    "application/json")
            });

            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage solicitud,
            CancellationToken cancellationToken)
        {
            var cuerpo = solicitud.Content is null
                ? null
                : await solicitud.Content.ReadAsStringAsync(cancellationToken);

            var clave = solicitud.Headers.TryGetValues("Idempotency-Key", out var valores)
                ? valores.Single()
                : null;

            Envios.Add(new Envio(
                solicitud.Method,
                solicitud.RequestUri!,
                clave,
                cuerpo));

            if (!_respuestas.TryDequeue(out var respuesta))
            {
                throw new InvalidOperationException("No hay más respuestas preparadas.");
            }

            return respuesta;
        }
    }

    private sealed record Envio(
        HttpMethod Metodo,
        Uri Ruta,
        string? ClaveDeIdempotencia,
        string? Cuerpo);
}
