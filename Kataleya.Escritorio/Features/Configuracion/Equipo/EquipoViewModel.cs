using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.Servicios.Impresion;

namespace Kataleya.Escritorio.Features.Configuracion.Equipo;

// Pestaña «Equipo» de Configuración: la impresora de esta laptop (CDU-21) y,
// solo para leer, la última copia semanal que hizo la Web API (CDU-22, RNF-17).
public partial class EquipoViewModel : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly AnchoDePapel[] Anchos = [AnchoDePapel.Mm58, AnchoDePapel.Mm80];

    private readonly ClienteApi _cliente;
    private readonly ServicioImpresion _impresion;
    private readonly IDialogos _dialogos;
    private bool _cargando;

    public EquipoViewModel(ClienteApi cliente, ServicioImpresion impresion, IDialogos dialogos)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _impresion = impresion ?? throw new ArgumentNullException(nameof(impresion));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
    }

    public ObservableCollection<string> Impresoras { get; } = [];

    public IReadOnlyList<string> AnchosDePapel { get; } = ["58 mm", "80 mm"];

    [ObservableProperty]
    private int impresoraElegida = -1;

    [ObservableProperty]
    private int anchoElegido;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImprimirPruebaCommand))]
    private bool impresoraConfigurada;

    [ObservableProperty]
    private string estadoDeLaImpresora = "Impresora sin configurar.";

    [ObservableProperty]
    private bool impresoraLista;

    [ObservableProperty]
    private string ultimaCopia = "Última copia: —";

    [ObservableProperty]
    private bool ultimaCopiaCorrecta = true;

    public async Task CargarAsync()
    {
        _cargando = true;

        try
        {
            var impresoras = (await _impresion.ListarImpresorasAsync()).ToList();
            var configuracion = await _impresion.ObtenerConfiguracionAsync();

            // La impresora guardada se muestra aunque hoy no esté conectada.
            if (configuracion is not null && !impresoras.Contains(configuracion.NombreImpresora))
            {
                impresoras.Insert(0, configuracion.NombreImpresora);
            }

            Impresoras.Clear();
            foreach (var impresora in impresoras)
            {
                Impresoras.Add(impresora);
            }

            ImpresoraElegida = configuracion is null ? -1 : impresoras.IndexOf(configuracion.NombreImpresora);
            AnchoElegido = Math.Max(0, Array.IndexOf(Anchos, configuracion?.AnchoDePapel ?? AnchoDePapel.Mm58));
        }
        finally
        {
            _cargando = false;
        }

        await MostrarEstadoDeLaImpresoraAsync();
        await MostrarUltimaCopiaAsync();
    }

    partial void OnImpresoraElegidaChanged(int value) =>
        _ = GuardarImpresoraAsync();

    partial void OnAnchoElegidoChanged(int value) =>
        _ = GuardarImpresoraAsync();

    private async Task GuardarImpresoraAsync()
    {
        if (_cargando ||
            ImpresoraElegida < 0 ||
            ImpresoraElegida >= Impresoras.Count ||
            AnchoElegido < 0 ||
            AnchoElegido >= Anchos.Length)
        {
            return;
        }

        await _impresion.GuardarConfiguracionAsync(new ConfiguracionDeImpresora(
            Impresoras[ImpresoraElegida],
            Anchos[AnchoElegido]));

        await MostrarEstadoDeLaImpresoraAsync();
    }

    [RelayCommand(CanExecute = nameof(ImpresoraConfigurada), AllowConcurrentExecutions = false)]
    private async Task ImprimirPruebaAsync()
    {
        var estado = await _impresion.ImprimirPruebaAsync();

        if (estado != EstadoImpresora.Lista)
        {
            await _dialogos.MostrarMensajeAsync(
                estado == EstadoImpresora.SinPapel ? "IMP-02" : "IMP-01",
                estado == EstadoImpresora.SinPapel
                    ? "La impresora no tiene papel. Revísela y vuelva a pulsar «Imprimir prueba»."
                    : "La impresora no responde. Revísela y vuelva a pulsar «Imprimir prueba».");
        }

        MostrarEstado(estado);
    }

    private async Task MostrarEstadoDeLaImpresoraAsync()
    {
        ImpresoraConfigurada = await _impresion.ObtenerConfiguracionAsync() is not null;

        if (!ImpresoraConfigurada)
        {
            EstadoDeLaImpresora = "Impresora sin configurar.";
            ImpresoraLista = false;
            return;
        }

        MostrarEstado(await _impresion.ConsultarEstadoAsync());
    }

    private void MostrarEstado(EstadoImpresora estado)
    {
        ImpresoraLista = estado == EstadoImpresora.Lista;
        EstadoDeLaImpresora = estado switch
        {
            EstadoImpresora.Lista => "La impresora está lista.",
            EstadoImpresora.SinPapel => "La impresora no tiene papel.",
            _ => "La impresora no responde."
        };
    }

    private async Task MostrarUltimaCopiaAsync()
    {
        var resultado = await _cliente.ObtenerEstadoDeLaCopiaAsync();

        if (!resultado.Exito || resultado.Datos is null)
        {
            UltimaCopia = "No se pudo consultar la última copia.";
            UltimaCopiaCorrecta = false;
            return;
        }

        var estado = resultado.Datos;

        if (estado.FechaHoraUltimaCopia is not DateTime fecha)
        {
            UltimaCopia = estado.FallosSeguidos > 0
                ? "Todavía no se guardó ninguna copia: la última no se pudo guardar."
                : "Todavía no hay copias.";
            UltimaCopiaCorrecta = false;
            return;
        }

        var texto = $"Última copia: {fecha.ToLocalTime().ToString("dddd dd/MM/yyyy, HH:mm", Cultura)}.";

        UltimaCopiaCorrecta = estado.UltimaCopiaCorrecta;
        UltimaCopia = estado.UltimaCopiaCorrecta
            ? $"{texto} Se guardó bien."
            : $"{texto} Después, {estado.FallosSeguidos} no se pudieron guardar.";
    }
}
