using System.Net;
using System.Text;
using Kataleya.Cliente;
using Kataleya.Contratos.Cuentas;
using Kataleya.Dominio.Interfaces;

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

        var cliente = CrearCliente(manejador);
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

    private static ClienteApi CrearCliente(ManejadorFalso manejador)
    {
        var http = new HttpClient(manejador)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        return new ClienteApi(http, new RegistroTecnicoFalso());
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
