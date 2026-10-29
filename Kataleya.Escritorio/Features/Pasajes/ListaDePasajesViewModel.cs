using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.UIHelpers;
using Kataleya.Escritorio.UIHelpers.Paginas;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.Features.Pasajes;

public partial class ListaDePasajesViewModel : ObservableObject
{
    private const string Todos = "Todos";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string[] HorasPredeterminadas = ["06:00", "10:00", "13:00", "15:00", "20:00"];
    private static readonly EstadoPasaje[] EstadosDelFiltro =
    [
        EstadoPasaje.Reservado,
        EstadoPasaje.ReservadoConAdelanto,
        EstadoPasaje.Pagado,
        EstadoPasaje.Anulado,
        EstadoPasaje.NoSePresento
    ];

    private readonly ClienteApi _cliente;
    private readonly IDialogos _dialogos;
    private List<DatosDeCuenta> _cuentas = [];
    private bool _catalogosCargados;

    public ListaDePasajesViewModel(ClienteApi cliente, IDialogos dialogos)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
    }

    public ObservableCollection<string> Horarios { get; } = [Todos];

    public IReadOnlyList<string> Estados { get; } =
        [Todos, .. EstadosDelFiltro.Select(NombreDelEstado)];

    public IReadOnlyList<string> Tipos { get; } = [Todos, "Individual", "Grupo"];

    public ObservableCollection<string> Emisores { get; } = [Todos];

    public ObservableCollection<FilaDePasaje> Filas { get; } = [];

    [ObservableProperty]
    private DateTime? desde = DateTime.Today;

    [ObservableProperty]
    private DateTime? hasta = DateTime.Today.AddDays(7);

    [ObservableProperty]
    private int horarioElegido;

    [ObservableProperty]
    private int estadoElegido;

    [ObservableProperty]
    private int tipoElegido;

    [ObservableProperty]
    private int emisorElegido;

    [ObservableProperty]
    private string pasajero = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AbrirDetalleCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopiarDatosCommand))]
    private FilaDePasaje? filaElegida;

    [ObservableProperty]
    private string errorDeFechas = string.Empty;

    [ObservableProperty]
    private string avisoDeCopiaLocal = string.Empty;

    public async Task CargarAsync()
    {
        if (!_catalogosCargados)
        {
            await CargarCatalogosAsync();
        }

        await BuscarAsync();
    }

    public static string NombreDelEstado(EstadoPasaje estado) =>
        estado switch
        {
            EstadoPasaje.Reservado => "Reservado",
            EstadoPasaje.ReservadoConAdelanto => "Reservado con adelanto",
            EstadoPasaje.Pagado => "Pagado",
            EstadoPasaje.Anulado => "Anulado",
            EstadoPasaje.NoSePresento => "No se presentó",
            _ => estado.ToString()
        };

    private async Task CargarCatalogosAsync()
    {
        var horas = HorasPredeterminadas;
        var parametros = await _cliente.ObtenerParametrosAsync();
        var valor = parametros.Datos?
            .FirstOrDefault(parametro => parametro.Nombre == "horarios_salida")?.Valor;

        if (!string.IsNullOrWhiteSpace(valor))
        {
            horas = valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        Horarios.Clear();
        Horarios.Add(Todos);
        foreach (var hora in horas)
        {
            Horarios.Add(hora);
        }

        HorarioElegido = 0;

        if (_cliente.EsAdministrador)
        {
            var cuentas = await _cliente.ObtenerCuentasAsync();
            if (cuentas.Exito && cuentas.Datos is not null)
            {
                _cuentas = cuentas.Datos;
                Emisores.Clear();
                Emisores.Add(Todos);
                foreach (var cuenta in _cuentas)
                {
                    Emisores.Add(cuenta.NombreUsuario);
                }

                EmisorElegido = 0;
            }
        }

        _catalogosCargados = parametros.Exito;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task BuscarAsync()
    {
        var filtro = CrearFiltro();
        if (filtro is null)
        {
            return;
        }

        var resultado = await _cliente.ObtenerPasajesAsync(filtro);

        if (resultado.Exito && resultado.Datos is not null)
        {
            AvisoDeCopiaLocal = string.Empty;
            Mostrar(resultado.Datos);
            await _cliente.ActualizarCopiaLocalAsync();
            return;
        }

        if (resultado.CodigoDeError is "CON-01" or "CON-02" &&
            await _cliente.LeerCopiaLocalAsync() is CopiaDelDia copia)
        {
            AvisoDeCopiaLocal = $"Copia local de las {copia.FechaHora:HH:mm} · solo lectura";
            Mostrar(copia.Pasajes.Where(pasaje => Cumple(pasaje, filtro)).ToList());
            return;
        }

        await _dialogos.MostrarMensajeAsync(
            "No se pudo consultar",
            resultado.SinPermiso
                ? "No tiene permiso para consultar pasajes."
                : "No se pudo obtener la lista de pasajes.");
    }

    private FiltroDePasajes? CrearFiltro()
    {
        var desde = DateOnly.FromDateTime(Desde ?? DateTime.Today);
        var hasta = DateOnly.FromDateTime(Hasta ?? DateTime.Today);

        if (desde > hasta)
        {
            ErrorDeFechas = "La fecha inicial no puede ser posterior a la final.";
            return null;
        }

        ErrorDeFechas = string.Empty;

        var filtro = new FiltroDePasajes
        {
            Desde = desde,
            Hasta = hasta,
            Pasajero = string.IsNullOrWhiteSpace(Pasajero) ? null : Pasajero.Trim(),
            Estado = EstadoElegido > 0 ? EstadosDelFiltro[EstadoElegido - 1] : null,
            Tipo = TipoElegido switch
            {
                1 => TipoDePasaje.Individual,
                2 => TipoDePasaje.Grupo,
                _ => null
            },
            EmisorId = EmisorElegido > 0 && EmisorElegido <= _cuentas.Count
                ? _cuentas[EmisorElegido - 1].Id
                : null
        };

        if (HorarioElegido > 0 &&
            TimeOnly.TryParse(Horarios[HorarioElegido], Cultura, out var hora))
        {
            filtro.Hora = hora;
        }

        return filtro;
    }

    private static bool Cumple(DatosDePasaje pasaje, FiltroDePasajes filtro) =>
        (filtro.Desde is null || pasaje.FechaSalida >= filtro.Desde) &&
        (filtro.Hasta is null || pasaje.FechaSalida <= filtro.Hasta) &&
        (filtro.Hora is null || pasaje.HoraSalida == filtro.Hora) &&
        (filtro.Estado is null || pasaje.Estado == filtro.Estado) &&
        (filtro.Tipo is null || (filtro.Tipo == TipoDePasaje.Grupo) == (pasaje.GrupoId is not null)) &&
        (filtro.Pasajero is null ||
         pasaje.PasajeroNombre.Contains(filtro.Pasajero, StringComparison.OrdinalIgnoreCase) ||
         pasaje.NumeroDocumento.Contains(filtro.Pasajero, StringComparison.OrdinalIgnoreCase));

    private void Mostrar(IReadOnlyList<DatosDePasaje> pasajes)
    {
        FilaElegida = null;
        Filas.Clear();

        foreach (var pasaje in pasajes)
        {
            Filas.Add(FilaDePasaje.Crear(pasaje));
        }
    }

    private bool HayFilaElegida() =>
        FilaElegida is not null;

    [RelayCommand(CanExecute = nameof(HayFilaElegida))]
    private Task AbrirDetalleAsync() =>
        NavegacionDeModulos.IrAlDetalleDePasajeAsync(FilaElegida!.Pasaje.Id);

    [RelayCommand(CanExecute = nameof(HayFilaElegida))]
    private Task CopiarDatosAsync() =>
        NavegacionDeModulos.IrAlRegistroConLosDatosDeAsync(FilaElegida!.Pasaje);
}

public sealed class FilaDePasaje
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private FilaDePasaje(DatosDePasaje pasaje) =>
        Pasaje = pasaje;

    public DatosDePasaje Pasaje { get; }

    public string Salida { get; private init; } = string.Empty;

    public string Asiento { get; private init; } = string.Empty;

    public string Grupo { get; private init; } = string.Empty;

    public string Importe { get; private init; } = string.Empty;

    public string Pagado { get; private init; } = string.Empty;

    public string Saldo { get; private init; } = string.Empty;

    public string Estado { get; private init; } = string.Empty;

    public Color FondoDelEstado { get; private init; } = Colors.Transparent;

    public Color TextoDelEstado { get; private init; } = Colors.Black;

    public static FilaDePasaje Crear(DatosDePasaje pasaje)
    {
        var esIntegrante = pasaje.GrupoId is not null && !pasaje.EsResponsable;
        var (fondo, texto) = pasaje.Estado switch
        {
            EstadoPasaje.Pagado => ("ExitoFondo", "Exito"),
            EstadoPasaje.ReservadoConAdelanto => ("AdvertenciaFondo", "AdvertenciaTexto"),
            EstadoPasaje.Reservado => ("InfoFondo", "InfoTexto"),
            EstadoPasaje.NoSePresento => ("PeligroHover", "PeligroTexto"),
            _ => ("SuperficieActiva", "TextoSuave")
        };

        return new FilaDePasaje(pasaje)
        {
            Salida = $"{pasaje.FechaSalida.ToString("dd/MM", Cultura)} {pasaje.HoraSalida.ToString("HH:mm", Cultura)}",
            Asiento = pasaje.GrupoId is not null
                ? "Grupo"
                : pasaje.NumeroAsiento is null ? "—" : $"U{pasaje.Unidad} · {pasaje.NumeroAsiento}",
            Grupo = pasaje.GrupoCodigo ?? "—",
            Importe = esIntegrante ? "—" : Soles(pasaje.Importe),
            Pagado = esIntegrante ? "—" : Soles(pasaje.Pagado),
            Saldo = esIntegrante ? "—" : Soles(pasaje.Saldo),
            Estado = ListaDePasajesViewModel.NombreDelEstado(pasaje.Estado),
            FondoDelEstado = ServicioDeTema.Color(fondo),
            TextoDelEstado = ServicioDeTema.Color(texto)
        };
    }

    private static string Soles(decimal importe) =>
        $"S/ {importe.ToString("N2", Cultura)}";
}
