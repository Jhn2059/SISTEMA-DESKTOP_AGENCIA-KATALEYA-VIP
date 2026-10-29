using Kataleya.Contratos.Bitacora;
using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.Bitacora;

public partial class BitacoraPage : PaginaDeModulo
{
    public BitacoraPage()
    {
        OpcionActiva = "bitacora";
        InitializeComponent();

        FechaDesde.Date = DateTime.Today;
        FechaHasta.Date = DateTime.Today;
    }

    protected override async void AlAparecer() => await CargarAsync();

    private async void AlFiltrar(object? remitente, TappedEventArgs argumentos) =>
        await CargarAsync();

    private async Task CargarAsync()
    {
        long? pasajeId = null;
        if (!string.IsNullOrWhiteSpace(CampoPasaje.Text))
        {
            if (!long.TryParse(CampoPasaje.Text, out var id))
            {
                await DisplayAlertAsync(
                    "Pasaje inválido",
                    "Escriba un número de pasaje válido.",
                    "Aceptar");
                return;
            }

            pasajeId = id;
        }

        var filtro = new FiltroDeBitacora
        {
            Desde = DateOnly.FromDateTime(FechaDesde.Date ?? DateTime.Today),
            Hasta = DateOnly.FromDateTime(FechaHasta.Date ?? DateTime.Today),
            PasajeId = pasajeId
        };

        var resultado = await Cliente.ObtenerBitacoraAsync(filtro);

        if (!resultado.Exito || resultado.Datos is null)
        {
            await DisplayAlertAsync(
                "No se pudo consultar",
                resultado.SinPermiso
                    ? "No tiene permiso para consultar la bitácora."
                    : "No se pudo consultar la bitácora.",
                "Aceptar");
            return;
        }

        TablaBitacora.ItemsSource = resultado.Datos;
    }
}
