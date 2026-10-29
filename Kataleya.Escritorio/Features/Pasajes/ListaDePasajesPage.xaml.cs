using System.Collections.ObjectModel;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.Pasajes;

public partial class ListaDePasajesPage : PaginaDeModulo
{
    private static readonly string[] Horas = ["06:00", "10:00", "13:00", "15:00", "20:00"];

    private readonly ObservableCollection<DatosDePasaje> _pasajes = [];
    private DatosDePasaje? _seleccionado;

    public ListaDePasajesPage()
    {
        OpcionActiva = "pasajes";
        InitializeComponent();

        FechaDesde.Date = DateTime.Today.AddDays(-30);
        FechaHasta.Date = DateTime.Today.AddDays(30);
        SelectorHora.ItemsSource = new[] { "Todas" }.Concat(Horas).ToList();
        SelectorHora.SelectedIndex = 0;
        SelectorEstado.ItemsSource = new[] { "Todos" }
            .Concat(Enum.GetNames<EstadoPasaje>())
            .ToList();
        SelectorEstado.SelectedIndex = 0;
        SelectorTipo.ItemsSource = new[] { "Todos" }
            .Concat(Enum.GetNames<TipoDePasaje>())
            .ToList();
        SelectorTipo.SelectedIndex = 0;
        SelectorEmisor.ItemsSource = new[] { "Todos" };
        SelectorEmisor.SelectedIndex = 0;
        TablaPasajes.ItemsSource = _pasajes;
    }

    protected override async void AlAparecer() => await CargarAsync();

    private async void AlBuscar(object? remitente, TappedEventArgs argumentos) =>
        await CargarAsync();

    private async void AlSeleccionarPasaje(object? remitente, SelectionChangedEventArgs argumentos)
    {
        _seleccionado = argumentos.CurrentSelection.FirstOrDefault() as DatosDePasaje;
        BotonAbrirDetalle.IsEnabled = _seleccionado is not null;
        BotonAbrirDetalle.Opacity = _seleccionado is null ? 0.45 : 1;
        await Task.CompletedTask;
    }

    private async void AlAbrirDetalle(object? remitente, EventArgs argumentos)
    {
        if (_seleccionado is null)
        {
            return;
        }

        await NavegacionDeModulos.IrAlDetalleDePasajeAsync(_seleccionado.Id);
    }

    private async Task CargarAsync()
    {
        var filtro = new FiltroDePasajes
        {
            Desde = DateOnly.FromDateTime(FechaDesde.Date ?? DateTime.Today),
            Hasta = DateOnly.FromDateTime(FechaHasta.Date ?? DateTime.Today),
            Pasajero = string.IsNullOrWhiteSpace(CampoPasajero.Text)
                ? null
                : CampoPasajero.Text.Trim()
        };

        if (SelectorHora.SelectedIndex > 0 &&
            TimeOnly.TryParse(SelectorHora.SelectedItem?.ToString(), out var hora))
        {
            filtro.Hora = hora;
        }

        if (SelectorEstado.SelectedIndex > 0 &&
            Enum.TryParse<EstadoPasaje>(SelectorEstado.SelectedItem?.ToString(), out var estado))
        {
            filtro.Estado = estado;
        }

        if (SelectorTipo.SelectedIndex > 0 &&
            Enum.TryParse<TipoDePasaje>(SelectorTipo.SelectedItem?.ToString(), out var tipo))
        {
            filtro.Tipo = tipo;
        }

        var resultado = await Cliente.ObtenerPasajesAsync(filtro);

        if (!resultado.Exito || resultado.Datos is null)
        {
            await DisplayAlertAsync(
                "No se pudo consultar",
                resultado.SinPermiso
                    ? "No tiene permiso para consultar pasajes."
                    : "No se pudo obtener la lista de pasajes.",
                "Aceptar");
            return;
        }

        _pasajes.Clear();
        foreach (var pasaje in resultado.Datos)
        {
            _pasajes.Add(pasaje);
        }

        _seleccionado = null;
        BotonAbrirDetalle.IsEnabled = false;
        BotonAbrirDetalle.Opacity = 0.45;
    }
}
