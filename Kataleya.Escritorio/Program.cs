using Kataleya.Infraestructura;

namespace Kataleya.Escritorio;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var http = new HttpClient
        {
            BaseAddress = Configuracion.DireccionApi,
            Timeout = TimeSpan.FromSeconds(10)
        };

        var registro = new RegistroTecnico();
        var cliente = new ClienteApi(http, registro);

        Application.Run(new VentanaPrincipal(cliente, registro));
    }
}
