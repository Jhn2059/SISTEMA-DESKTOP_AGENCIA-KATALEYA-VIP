using Kataleya.Escritorio.Servicios.Impresion;

namespace Kataleya.Escritorio.Features.GestionDeUsuarios;

public partial class GestionDeUsuariosPage
{
    private static readonly AnchoDePapel[] Anchos = [AnchoDePapel.Mm58, AnchoDePapel.Mm80];

    private List<string> _impresoras = [];
    private bool _cargandoEquipo;

    private async Task CargarEquipoAsync()
    {
        if (_cargandoEquipo)
        {
            return;
        }

        _cargandoEquipo = true;

        try
        {
            _impresoras = (await _impresion.ListarImpresorasAsync()).ToList();
            var configuracion = await _impresion.ObtenerConfiguracionAsync();

            // La impresora guardada se muestra aunque hoy no esté conectada.
            if (configuracion is not null && !_impresoras.Contains(configuracion.NombreImpresora))
            {
                _impresoras.Insert(0, configuracion.NombreImpresora);
            }

            SelectorImpresora.ItemsSource = _impresoras;
            SelectorImpresora.SelectedIndex = configuracion is null
                ? -1
                : _impresoras.IndexOf(configuracion.NombreImpresora);
            SelectorAnchoPapel.SelectedIndex = Array.IndexOf(
                Anchos,
                configuracion?.AnchoDePapel ?? AnchoDePapel.Mm58);
        }
        finally
        {
            _cargandoEquipo = false;
        }

        await MostrarEstadoDeLaImpresoraAsync();
    }

    private async void AlCambiarImpresora(object? remitente, EventArgs argumentos)
    {
        if (_cargandoEquipo ||
            SelectorImpresora.SelectedIndex < 0 ||
            SelectorAnchoPapel.SelectedIndex < 0)
        {
            return;
        }

        await _impresion.GuardarConfiguracionAsync(new ConfiguracionDeImpresora(
            _impresoras[SelectorImpresora.SelectedIndex],
            Anchos[SelectorAnchoPapel.SelectedIndex]));

        await MostrarEstadoDeLaImpresoraAsync();
    }

    private async void AlImprimirPrueba(object? remitente, EventArgs argumentos)
    {
        BotonImprimirPrueba.IsEnabled = false;

        try
        {
            var estado = await _impresion.ImprimirPruebaAsync();

            if (estado != EstadoImpresora.Lista)
            {
                await DisplayAlertAsync(
                    "No se pudo imprimir",
                    estado == EstadoImpresora.SinPapel
                        ? "La impresora no tiene papel. Revísela y vuelva a pulsar «Imprimir prueba»."
                        : "La impresora no responde. Revísela y vuelva a pulsar «Imprimir prueba».",
                    "Aceptar");
            }

            PintarEstadoDeLaImpresora(estado);
        }
        finally
        {
            BotonImprimirPrueba.IsEnabled = true;
        }
    }

    private async Task MostrarEstadoDeLaImpresoraAsync()
    {
        var configurada = await _impresion.ObtenerConfiguracionAsync() is not null;

        BotonImprimirPrueba.IsEnabled = configurada;
        BotonImprimirPrueba.Opacity = configurada ? 1 : 0.45;

        if (!configurada)
        {
            PintarEstado("Impresora sin configurar.", lista: false);
            return;
        }

        PintarEstadoDeLaImpresora(await _impresion.ConsultarEstadoAsync());
    }

    private void PintarEstadoDeLaImpresora(EstadoImpresora estado) =>
        PintarEstado(
            estado switch
            {
                EstadoImpresora.Lista => "La impresora está lista.",
                EstadoImpresora.SinPapel => "La impresora no tiene papel.",
                _ => "La impresora no responde."
            },
            estado == EstadoImpresora.Lista);

    private void PintarEstado(string texto, bool lista)
    {
        var color = lista ? "Exito" : "AdvertenciaTexto";
        var recuadro = lista ? "Exito" : "Advertencia";

        LblEstadoImpresora.Text = texto;
        LblEstadoImpresora.SetDynamicResource(Label.TextColorProperty, color);
        IconoEstadoImpresora.SetDynamicResource(Microsoft.Maui.Controls.Shapes.Shape.StrokeProperty, color);
        RecuadroEstadoImpresora.SetDynamicResource(VisualElement.BackgroundColorProperty, recuadro + "Fondo");
        RecuadroEstadoImpresora.SetDynamicResource(Border.StrokeProperty, recuadro + "Borde");
    }
}
