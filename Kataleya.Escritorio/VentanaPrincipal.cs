using Kataleya.Dominio.Interfaces;
using Kataleya.Infraestructura;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Kataleya.Escritorio;

public sealed class VentanaPrincipal : Form
{
    private readonly WebView2 _web = new();
    private readonly Puente _puente;

    public VentanaPrincipal(ClienteApi cliente, IRegistroTecnico registro)
    {
        _puente = new Puente(cliente, registro);

        Text = "Kataleya VIP";
        MinimumSize = new Size(1024, 768);
        WindowState = FormWindowState.Maximized;

        _web.Dock = DockStyle.Fill;
        Controls.Add(_web);

        Load += async (_, _) => await IniciarAsync();
    }

    private async Task IniciarAsync()
    {
        try
        {
            await _web.EnsureCoreWebView2Async();
        }
        catch
        {
            MessageBox.Show(
                "No se pudo iniciar el componente web. Instale el runtime de WebView2.",
                Text);
            Close();
            return;
        }

        _web.CoreWebView2.WebMessageReceived += AlRecibirMensaje;

        var carpetaWeb = BuscarCarpetaWeb();
        if (carpetaWeb is null)
        {
            MessageBox.Show("No se encontró la carpeta Kataleya.Presentacion.", Text);
            return;
        }

        _web.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "kataleya.local",
            carpetaWeb,
            CoreWebView2HostResourceAccessKind.Allow);
        _web.CoreWebView2.Navigate("https://kataleya.local/pages/inicio-sesion/inicio-sesion.html");
    }

    private async void AlRecibirMensaje(object? remitente, CoreWebView2WebMessageReceivedEventArgs evento)
    {
        var mensaje = evento.TryGetWebMessageAsString();
        if (mensaje is null)
        {
            return;
        }

        var respuesta = await _puente.ProcesarAsync(mensaje);

        try
        {
            _web.CoreWebView2.PostWebMessageAsJson(respuesta);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string? BuscarCarpetaWeb()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null)
        {
            var candidata = Path.Combine(carpeta.FullName, "Kataleya.Presentacion");
            if (Directory.Exists(candidata))
            {
                return candidata;
            }

            carpeta = carpeta.Parent;
        }

        return null;
    }
}
