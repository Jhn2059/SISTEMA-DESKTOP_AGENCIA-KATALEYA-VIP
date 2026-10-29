using System.Globalization;
using Kataleya.Contratos.Cuadre;
using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.CuadreDeVentas;

public partial class CuadreDeVentasPage : PaginaDeModulo
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private DatosDelCuadre? _cuadre;
    private DatosDeFilaDeCuadre? _filaSeleccionada;

    public CuadreDeVentasPage()
    {
        OpcionActiva = "cuadre-ventas";
        InitializeComponent();

        FechaDesde.Date = DateTime.Today;
        FechaHasta.Date = DateTime.Today;
    }

    protected override async void AlAparecer() => await GenerarAsync();

    private async void AlGenerar(object? remitente, TappedEventArgs argumentos) =>
        await GenerarAsync();

    private async void AlSeleccionarFila(object? remitente, SelectionChangedEventArgs argumentos)
    {
        _filaSeleccionada = argumentos.CurrentSelection.FirstOrDefault() as DatosDeFilaDeCuadre;
        BotonVerDetalle.IsEnabled = _filaSeleccionada is not null;
        BotonVerDetalle.Opacity = _filaSeleccionada is null ? 0.45 : 1;
        await Task.CompletedTask;
    }

    private async void AlVerDetalle(object? remitente, EventArgs argumentos)
    {
        if (_cuadre is null || _filaSeleccionada is null)
        {
            return;
        }

        var pasajes = _cuadre.Pasajes
            .Where(pasaje => pasaje.Emisor == _filaSeleccionada.Emisor)
            .ToList();

        if (pasajes.Count == 0)
        {
            await DisplayAlertAsync(
                "Detalle del emisor",
                "No hay pasajes para este emisor en el rango consultado.",
                "Aceptar");
            return;
        }

        var detalle = string.Join(
            Environment.NewLine,
            pasajes.Select(pasaje =>
                $"{pasaje.Pasajero} · {pasaje.Estado} · S/ {pasaje.Pagado:N2}" +
                $"{(string.IsNullOrWhiteSpace(pasaje.Boleto) ? string.Empty : $" · {pasaje.Boleto}")}"));

        await DisplayAlertAsync(
            $"Pasajes de {_filaSeleccionada.Emisor}",
            detalle,
            "Cerrar");
    }

    private async Task GenerarAsync()
    {
        var filtro = new FiltroDeCuadre
        {
            Desde = DateOnly.FromDateTime(FechaDesde.Date ?? DateTime.Today),
            Hasta = DateOnly.FromDateTime(FechaHasta.Date ?? DateTime.Today)
        };

        var resultado = await Cliente.ObtenerCuadreAsync(filtro);

        if (!resultado.Exito || resultado.Datos is null)
        {
            await DisplayAlertAsync(
                "No se pudo generar",
                resultado.SinPermiso
                    ? "No tiene permiso para consultar el cuadre."
                    : "No se pudo generar el cuadre de ventas.",
                "Aceptar");
            return;
        }

        _cuadre = resultado.Datos;
        _filaSeleccionada = null;
        TablaCuadre.ItemsSource = _cuadre.Filas;
        BotonVerDetalle.IsEnabled = false;
        BotonVerDetalle.Opacity = 0.45;

        LblResumen.Text =
            $"Generado el {_cuadre.GeneradoEl.ToString("dd/MM/yyyy HH:mm", Cultura)} por " +
            $"{_cuadre.GeneradoPor} · Sede {_cuadre.Sede} · {_cuadre.Pasajes.Count} pasajes.";
    }
}
