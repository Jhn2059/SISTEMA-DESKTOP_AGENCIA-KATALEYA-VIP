using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Bitacora;
using Kataleya.Contratos.Cuentas;
using Kataleya.Escritorio.Servicios;

namespace Kataleya.Escritorio.Features.Bitacora;

public partial class BitacoraViewModel : ObservableObject
{
    private const string Todos = "Todos";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private readonly ClienteApi _cliente;
    private readonly IDialogos _dialogos;
    private List<DatosDeCuenta> _cuentas = [];

    public BitacoraViewModel(ClienteApi cliente, IDialogos dialogos)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
    }

    public ObservableCollection<string> Usuarios { get; } = [Todos];

    public ObservableCollection<FilaDeBitacora> Filas { get; } = [];

    [ObservableProperty]
    private int usuarioElegido;

    [ObservableProperty]
    private DateTime? desde = DateTime.Today;

    [ObservableProperty]
    private DateTime? hasta = DateTime.Today;

    [ObservableProperty]
    private string numeroDePasaje = string.Empty;

    [ObservableProperty]
    private string errorDeFechas = string.Empty;

    [ObservableProperty]
    private string errorDePasaje = string.Empty;

    public async Task CargarAsync()
    {
        if (_cuentas.Count == 0)
        {
            var cuentas = await _cliente.ObtenerCuentasAsync();
            if (cuentas.Exito && cuentas.Datos is not null)
            {
                _cuentas = cuentas.Datos;
                Usuarios.Clear();
                Usuarios.Add(Todos);

                foreach (var cuenta in _cuentas)
                {
                    Usuarios.Add(cuenta.NombreUsuario);
                }

                UsuarioElegido = 0;
            }
        }

        await FiltrarAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task FiltrarAsync()
    {
        var desde = DateOnly.FromDateTime(Desde ?? DateTime.Today);
        var hasta = DateOnly.FromDateTime(Hasta ?? DateTime.Today);

        ErrorDeFechas = desde > hasta
            ? "La fecha inicial no puede ser posterior a la final."
            : string.Empty;
        ErrorDePasaje = string.Empty;

        long? pasaje = null;
        var texto = NumeroDePasaje.Trim().TrimStart('P', 'p').TrimStart('-');

        if (texto.Length > 0)
        {
            if (!long.TryParse(texto, NumberStyles.None, Cultura, out var numero))
            {
                ErrorDePasaje = "Escriba el número del pasaje; por ejemplo, P-0042.";
                return;
            }

            pasaje = numero;
        }

        if (ErrorDeFechas.Length > 0)
        {
            return;
        }

        var resultado = await _cliente.ObtenerBitacoraAsync(new FiltroDeBitacora
        {
            UsuarioId = UsuarioElegido > 0 && UsuarioElegido <= _cuentas.Count
                ? _cuentas[UsuarioElegido - 1].Id
                : null,
            Desde = desde,
            Hasta = hasta,
            PasajeId = pasaje
        });

        if (!resultado.Exito || resultado.Datos is null)
        {
            await _dialogos.MostrarMensajeAsync(
                "No se pudo consultar",
                resultado.SinPermiso
                    ? "No tiene permiso para consultar la bitácora."
                    : "No se pudo consultar la bitácora.");
            return;
        }

        Filas.Clear();
        foreach (var entrada in resultado.Datos)
        {
            Filas.Add(new FilaDeBitacora(
                entrada.FechaHora.ToLocalTime().ToString("dd/MM HH:mm", Cultura),
                entrada.Usuario,
                entrada.Operacion,
                entrada.PasajeId is long id ? $"P-{id:0000}" : "—",
                Legible(entrada.ValorAnterior),
                Legible(entrada.ValorNuevo)));
        }
    }

    // Los valores se guardan en JSON; en la tabla se muestran como «Campo valor · Campo valor».
    private static string Legible(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "—";
        }

        try
        {
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;

            return raiz.ValueKind == JsonValueKind.Object
                ? string.Join(" · ", raiz.EnumerateObject().Select(campo => $"{campo.Name} {Valor(campo.Value)}"))
                : Valor(raiz);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string Valor(JsonElement valor) =>
        valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString() ?? string.Empty,
            JsonValueKind.Null => "—",
            _ => valor.GetRawText()
        };
}

public sealed record FilaDeBitacora(
    string FechaHora,
    string Usuario,
    string Operacion,
    string Pasaje,
    string ValorAnterior,
    string ValorNuevo);
