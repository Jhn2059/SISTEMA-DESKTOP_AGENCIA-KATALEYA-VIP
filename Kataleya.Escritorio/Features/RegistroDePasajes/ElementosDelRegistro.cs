using CommunityToolkit.Mvvm.ComponentModel;
using Kataleya.Escritorio.UIHelpers;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.Features.RegistroDePasajes;

// Un lugar del dibujo de la unidad (IU-09): el conductor o un asiento libre, ocupado o elegido.
public sealed class AsientoVisible
{
    private AsientoVisible(short numero, string texto, bool esConductor, bool ocupado, bool elegido)
    {
        Numero = numero;
        Texto = texto;
        EsConductor = esConductor;
        Ocupado = ocupado;
        Elegido = elegido;
        Fondo = ServicioDeTema.Color(elegido ? "Info" : ocupado || esConductor ? "DeshabilitadoFondo" : "Superficie");
        Borde = ServicioDeTema.Color(ocupado || esConductor ? "BordeDeshabilitado" : elegido ? "Info" : "BordeControl");
        ColorDelTexto = elegido ? Colors.White : ServicioDeTema.Color(ocupado || esConductor ? "DeshabilitadoTexto" : "Texto");
    }

    public short Numero { get; }

    public string Texto { get; }

    public bool EsConductor { get; }

    public bool Ocupado { get; }

    public bool Elegido { get; }

    public bool SePuedeElegir => !EsConductor && !Ocupado;

    public Color Fondo { get; }

    public Color Borde { get; }

    public Color ColorDelTexto { get; }

    public static AsientoVisible Conductor() =>
        new(0, "Cond.", esConductor: true, ocupado: false, elegido: false);

    public static AsientoVisible Asiento(short numero, bool ocupado, bool elegido) =>
        new(numero, numero.ToString(), esConductor: false, ocupado, elegido);
}

public partial class IntegranteEditable : ObservableObject
{
    public IntegranteEditable(int numero) =>
        this.numero = numero;

    [ObservableProperty]
    private int numero;

    [ObservableProperty]
    private string nombre = string.Empty;

    [ObservableProperty]
    private int tipoDeDocumento;

    [ObservableProperty]
    private string numeroDocumento = string.Empty;

    [ObservableProperty]
    private string error = string.Empty;
}

public sealed record UnidadDelGrupoVisible(int Numero, short TipoVehiculoId, string Vehiculo, short Asientos);
