using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Kataleya.Contratos.Configuracion;
using Kataleya.Dominio.Enums;

namespace Kataleya.Escritorio.Features.Configuracion.TarifasYParametros;

// Una fila de la tabla de tarifas programadas (IU-P10).
public partial class FilaDeTarifa : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public FilaDeTarifa(DatosDeTarifa tarifa, bool esFilaAlterna)
    {
        Tarifa = tarifa ?? throw new ArgumentNullException(nameof(tarifa));
        EsFilaAlterna = esFilaAlterna;
    }

    public DatosDeTarifa Tarifa { get; }

    public int Id => Tarifa.Id;

    public string Nombre => Tarifa.Nombre;

    public string Importe => $"S/ {Tarifa.Importe.ToString("N2", Cultura)}";

    public string Desde => Tarifa.VigenciaDesde?.ToString("dd/MM/yyyy", Cultura) ?? "—";

    public string Hasta => Tarifa.VigenciaHasta?.ToString("dd/MM/yyyy", Cultura) ?? "—";

    public string Sentido => TextoDelSentido(Tarifa.Sentido);

    public string FijadaPor => Tarifa.FijadaPor;

    public bool EsFilaAlterna { get; }

    [ObservableProperty]
    private bool esSeleccionada;

    public static string TextoDelSentido(SentidoDelViaje sentido) => sentido switch
    {
        SentidoDelViaje.Ida => "Huancayo → Ica",
        SentidoDelViaje.Vuelta => "Ica → Huancayo",
        _ => "Ambos"
    };
}

// Un horario de salida; el último no puede quitarse (CDU-16 A3).
public sealed record HorarioVisible(string Hora, bool SePuedeQuitar);
