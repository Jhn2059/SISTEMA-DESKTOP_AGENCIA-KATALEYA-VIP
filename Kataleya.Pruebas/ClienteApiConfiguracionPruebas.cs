using System.Net;
using System.Text;
using Kataleya.Cliente;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Pruebas;

public sealed class ClienteApiConfiguracionPruebas
{
    [Fact]
    public async Task Obtener_tarifas_deserializa_los_datos()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.OK,
            """
            [
              {
                "id": 1,
                "nombre": null,
                "importe": 120,
                "vigencia": null,
                "fijadaPor": "Administradora",
                "sentido": "HuancayoIca"
              }
            ]
            """);

        var resultado = await CrearCliente(manejador).ObtenerTarifasAsync();

        Assert.True(resultado.Exito);
        Assert.NotNull(resultado.Datos);
        var tarifa = Assert.Single(resultado.Datos);
        Assert.Equal(120, tarifa.Importe);
        Assert.Equal("HuancayoIca", tarifa.Sentido);
        Assert.Equal("/tarifas", manejador.Envios[0].Ruta.AbsolutePath);
    }

    [Fact]
    public async Task Fijar_tarifa_regular_envia_put_con_idempotencia()
    {
        var manejador = new ManejadorFalso().Encolar(HttpStatusCode.NoContent);

        var resultado = await CrearCliente(manejador).FijarTarifaRegularAsync(
            Sentido.IcaHuancayo,
            125);

        Assert.True(resultado.Exito);
        var envio = Assert.Single(manejador.Envios);
        Assert.Equal(HttpMethod.Put, envio.Metodo);
        Assert.Equal("/tarifas/regular/IcaHuancayo", envio.Ruta.AbsolutePath);
        Assert.Contains("\"importe\":125", envio.Cuerpo);
        Assert.NotNull(envio.ClaveDeIdempotencia);
    }

    [Fact]
    public async Task Agregar_y_quitar_horario_formatean_la_hora()
    {
        var manejador = new ManejadorFalso()
            .Encolar(HttpStatusCode.NoContent)
            .Encolar(HttpStatusCode.NoContent);

        var cliente = CrearCliente(manejador);

        Assert.True((await cliente.AgregarHorarioAsync(new TimeOnly(7, 30))).Exito);
        Assert.True((await cliente.QuitarHorarioAsync(new TimeOnly(7, 30))).Exito);

        Assert.Equal("/parametros/horarios", manejador.Envios[0].Ruta.AbsolutePath);
        Assert.Contains("\"hora\":\"07:30\"", manejador.Envios[0].Cuerpo);
        Assert.Equal(HttpMethod.Delete, manejador.Envios[1].Metodo);
        Assert.Equal("/parametros/horarios/07:30", manejador.Envios[1].Ruta.AbsolutePath);
    }

    [Fact]
    public async Task Actualizar_tarifa_envia_put_con_idempotencia()
    {
        var manejador = new ManejadorFalso().Encolar(
            HttpStatusCode.OK,
            """
            {
              "id": 5,
              "nombre": "Fiestas patrias",
              "importe": 180,
              "vigencia": { "desde": "2026-07-28", "hasta": "2026-08-02" },
              "fijadaPor": "Administradora",
              "sentido": "HuancayoIca"
            }
            """);
        var solicitud = new SolicitudDeTarifa(
            "Fiestas patrias",
            180,
            new Periodo(new DateOnly(2026, 7, 28), new DateOnly(2026, 8, 2)),
            Sentido.HuancayoIca);

        var resultado = await CrearCliente(manejador).ActualizarTarifaAsync(5, solicitud);

        Assert.True(resultado.Exito);
        Assert.Equal(5, resultado.Datos!.Id);
        var envio = Assert.Single(manejador.Envios);
        Assert.Equal(HttpMethod.Put, envio.Metodo);
        Assert.Equal("/tarifas/5", envio.Ruta.AbsolutePath);
        Assert.Contains("\"importe\":180", envio.Cuerpo);
        Assert.NotNull(envio.ClaveDeIdempotencia);
    }

    [Fact]
    public async Task Contar_pasajes_envia_la_hora_consultada()
    {
        var manejador = new ManejadorFalso().Encolar(HttpStatusCode.OK, "3");

        var resultado = await CrearCliente(manejador).ContarPasajesDelHorarioAsync(
            new TimeOnly(7, 30));

        Assert.True(resultado.Exito);
        Assert.Equal(3, resultado.Datos);
        var envio = Assert.Single(manejador.Envios);
        Assert.Equal(HttpMethod.Get, envio.Metodo);
        Assert.Equal("/pasajes/cantidad", envio.Ruta.AbsolutePath);
        Assert.Contains("hora=07%3A30", envio.Ruta.Query);
    }

    private static ClienteApi CrearCliente(ManejadorFalso manejador)
    {
        var http = new HttpClient(manejador)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        return new ClienteApi(http, new RegistroTecnicoFalso(), reintentosMaximos: 0);
    }

    private sealed class RegistroTecnicoFalso : IRegistroTecnico
    {
        public Task RegistrarAsync(ErrorTecnico error) =>
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
