using System.Net;
using System.Security.Cryptography;
using System.Text;
using Kataleya.Aplicacion.Services;
using Kataleya.Datos;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Pruebas;

public sealed class CopiaDeDatosPruebas
{
    private static readonly DateTime Domingo = new(2026, 10, 25, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void La_copia_cifrada_se_descifra_con_la_misma_clave()
    {
        var datos = Encoding.UTF8.GetBytes("pasaje,1,Huancayo");

        var cifrado = CopiaDeLaBase.Cifrar(datos, "clave-de-prueba");

        Assert.NotEqual(datos, cifrado[^datos.Length..]);
        Assert.Equal(datos, CopiaDeLaBase.Descifrar(cifrado, "clave-de-prueba"));
    }

    [Fact]
    public void Con_otra_clave_la_copia_no_se_descifra()
    {
        var cifrado = CopiaDeLaBase.Cifrar([1, 2, 3], "clave-de-prueba");

        Assert.ThrowsAny<CryptographicException>(() => CopiaDeLaBase.Descifrar(cifrado, "otra-clave"));
    }

    [Fact]
    public async Task Sin_copias_anteriores_hace_la_copia_y_la_anota()
    {
        var escenario = new EscenarioDeCopia(Domingo);

        var hecha = await escenario.Servicio.HacerCopiaSiCorrespondeAsync();

        Assert.True(hecha);
        Assert.Single(escenario.Almacen.Subidas);
        Assert.Equal(ServicioCopiaDeDatos.CopiasQueSeConservan, escenario.Almacen.Conservadas);
        var copia = Assert.Single(escenario.Copias.Registros);
        Assert.True(copia.Correcta);
        Assert.Equal(escenario.Almacen.Subidas[0].NombreArchivo, copia.NombreArchivo);
    }

    [Fact]
    public async Task Si_no_pasaron_7_dias_no_hace_otra_copia()
    {
        var escenario = new EscenarioDeCopia(Domingo);
        escenario.Copias.Registros.Add(new CopiaDeRespaldo { FechaHora = Domingo.AddDays(-6), Correcta = true });

        var hecha = await escenario.Servicio.HacerCopiaSiCorrespondeAsync();

        Assert.False(hecha);
        Assert.Empty(escenario.Almacen.Subidas);
    }

    [Fact]
    public async Task El_domingo_siguiente_hace_la_copia_aunque_falten_minutos_para_los_7_dias()
    {
        var escenario = new EscenarioDeCopia(Domingo);
        escenario.Copias.Registros.Add(new CopiaDeRespaldo
        {
            FechaHora = Domingo.AddDays(-7).AddMinutes(2),
            Correcta = true
        });

        Assert.True(await escenario.Servicio.HacerCopiaSiCorrespondeAsync());
    }

    [Fact]
    public async Task Si_la_subida_falla_anota_el_fallo_y_avisa_al_segundo()
    {
        var escenario = new EscenarioDeCopia(Domingo);
        escenario.Almacen.Fallar = true;

        await Assert.ThrowsAsync<HttpRequestException>(() => escenario.Servicio.HacerCopiaSiCorrespondeAsync());
        await Assert.ThrowsAsync<HttpRequestException>(() => escenario.Servicio.HacerCopiaSiCorrespondeAsync());

        var estado = await escenario.Servicio.ObtenerEstadoAsync();
        Assert.Equal(2, estado.FallosSeguidos);
        Assert.False(estado.UltimaCopiaCorrecta);
        Assert.Null(estado.FechaHoraUltimaCopia);
    }

    [Fact]
    public async Task Google_Drive_sube_la_copia_por_partes_y_deja_las_8_ultimas()
    {
        var archivos = string.Join(",", Enumerable.Range(1, 10).Select(i => $$"""{"id":"c{{i}}","name":"kataleya-copia-{{i}}"}"""));
        var manejador = new ManejadorDeDrive()
            .Responder(HttpStatusCode.OK, """{"access_token":"token"}""")
            .Responder(HttpStatusCode.OK, "{}", "https://subida.example/sesion")
            .Responder(HttpStatusCode.OK, """{"id":"nuevo"}""")
            .Responder(HttpStatusCode.OK, """{"access_token":"token"}""")
            .Responder(HttpStatusCode.OK, $$"""{"files":[{{archivos}}]}""")
            .Responder(HttpStatusCode.NoContent)
            .Responder(HttpStatusCode.NoContent);
        var almacen = new AlmacenEnGoogleDrive(new HttpClient(manejador), new OpcionesDeGoogleDrive
        {
            ClienteId = "cliente",
            ClienteSecreto = "secreto",
            TokenDeActualizacion = "actualizacion",
            CarpetaId = "carpeta"
        });

        await almacen.SubirAsync(new CopiaCifrada { NombreArchivo = "kataleya-copia-x.cifrada", Contenido = [1, 2, 3] });
        await almacen.BorrarLasViejasAsync(8);

        Assert.Equal("https://subida.example/sesion", manejador.Solicitudes[2].RequestUri!.ToString());
        Assert.Equal(HttpMethod.Put, manejador.Solicitudes[2].Method);
        Assert.Equal(
            ["c9", "c10"],
            manejador.Solicitudes
                .Where(solicitud => solicitud.Method == HttpMethod.Delete)
                .Select(solicitud => solicitud.RequestUri!.Segments[^1]));
    }

    [Fact]
    public async Task Sin_permiso_de_Google_Drive_no_se_puede_copiar()
    {
        var almacen = new AlmacenEnGoogleDrive(new HttpClient(new ManejadorDeDrive()), new OpcionesDeGoogleDrive());

        Assert.False(almacen.EstaConfigurado);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            almacen.SubirAsync(new CopiaCifrada { NombreArchivo = "x", Contenido = [1] }));
    }

    private sealed class EscenarioDeCopia
    {
        public EscenarioDeCopia(DateTime ahora)
        {
            Servicio = new ServicioCopiaDeDatos(new CopiaFalsa(), Almacen, Copias, new RelojFalso(ahora));
        }

        public AlmacenFalso Almacen { get; } = new();

        public RepositorioCopiasFalso Copias { get; } = new();

        public ServicioCopiaDeDatos Servicio { get; }
    }

    private sealed class CopiaFalsa : ICopiaDeLaBase
    {
        public Task<CopiaCifrada> ExportarAsync(DateTime fechaHora) =>
            Task.FromResult(new CopiaCifrada
            {
                NombreArchivo = $"kataleya-copia-{fechaHora:yyyy-MM-dd-HHmm}.cifrada",
                Contenido = [1, 2, 3]
            });
    }

    private sealed class AlmacenFalso : IAlmacenDeCopias
    {
        public List<CopiaCifrada> Subidas { get; } = [];

        public int Conservadas { get; private set; }

        public bool Fallar { get; set; }

        public bool EstaConfigurado => true;

        public Task SubirAsync(CopiaCifrada copia)
        {
            if (Fallar)
            {
                throw new HttpRequestException("Google Drive no responde.");
            }

            Subidas.Add(copia);
            return Task.CompletedTask;
        }

        public Task BorrarLasViejasAsync(int conservar)
        {
            Conservadas = conservar;
            return Task.CompletedTask;
        }
    }

    private sealed class RepositorioCopiasFalso : IRepositorioCopias
    {
        public List<CopiaDeRespaldo> Registros { get; } = [];

        public Task<CopiaDeRespaldo?> BuscarUltimaAsync() =>
            Task.FromResult(Registros
                .Where(copia => copia.Correcta)
                .OrderByDescending(copia => copia.FechaHora)
                .FirstOrDefault());

        public Task<int> ContarFallosSeguidosAsync() =>
            Task.FromResult(Registros
                .OrderByDescending(copia => copia.FechaHora)
                .ThenByDescending(copia => copia.Id)
                .TakeWhile(copia => !copia.Correcta)
                .Count());

        public Task AgregarAsync(CopiaDeRespaldo copia)
        {
            copia.Id = Registros.Count + 1;
            Registros.Add(copia);
            return Task.CompletedTask;
        }
    }

    private sealed class ManejadorDeDrive : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respuestas = new();

        public List<HttpRequestMessage> Solicitudes { get; } = [];

        public ManejadorDeDrive Responder(HttpStatusCode estado, string? contenido = null, string? ubicacion = null)
        {
            var respuesta = new HttpResponseMessage(estado)
            {
                Content = new StringContent(contenido ?? string.Empty, Encoding.UTF8, "application/json")
            };

            if (ubicacion is not null)
            {
                respuesta.Headers.Location = new Uri(ubicacion);
            }

            _respuestas.Enqueue(respuesta);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage solicitud,
            CancellationToken cancellationToken)
        {
            Solicitudes.Add(solicitud);
            return Task.FromResult(_respuestas.Dequeue());
        }
    }
}
