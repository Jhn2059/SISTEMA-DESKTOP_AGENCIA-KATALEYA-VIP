namespace Kataleya.Escritorio.UIHelpers;

public static class ServicioDeTema
{
    private const string ClaveDePreferencia = "tema";

    private static readonly IReadOnlyDictionary<string, string> PaletaOscura =
        new Dictionary<string, string>
        {
            ["Fondo"] = "#0D0F13",
            ["Superficie"] = "#161A21",
            ["SuperficieElevada"] = "#1A1E25",
            ["SuperficieActiva"] = "#22262E",
            ["SuperficieHover"] = "#1C2028",
            ["SuperficieAlterna"] = "#191D24",
            ["Borde"] = "#2A2F38",
            ["BordeControl"] = "#646B78",
            ["BordeControlHover"] = "#8A909A",
            ["BordeDeshabilitado"] = "#3A404A",
            ["Texto"] = "#F1F2F4",
            ["TextoSuave"] = "#A9AFB9",
            ["TextoTenue"] = "#646B78",
            ["TextoInverso"] = "#16181D",
            ["Acento"] = "#ED1C24",
            ["AcentoFondo"] = "#2E0F11",
            ["AcentoBorde"] = "#7F1D22",
            ["Peligro"] = "#EF4444",
            ["PeligroTexto"] = "#FCA5A5",
            ["PeligroHover"] = "#251014",
            ["PeligroBordeHover"] = "#F87171",
            ["Error"] = "#FCA5A5",
            ["Exito"] = "#86EFAC",
            ["ExitoFondo"] = "#0F2A1A",
            ["ExitoBorde"] = "#1F6B3D",
            ["Advertencia"] = "#F59E0B",
            ["AdvertenciaTexto"] = "#FCD38D",
            ["AdvertenciaFondo"] = "#2D1F08",
            ["AdvertenciaBorde"] = "#7A5312",
            ["Info"] = "#3B82F6",
            ["InfoFondo"] = "#0E1D36",
            ["InfoBorde"] = "#1E4A8C",
            ["InfoTexto"] = "#93C5FD",
            ["IconoVenta"] = "#22C55E",
            ["IconoYape"] = "#A855F7",
            ["BotonClaro"] = "#F1F2F4",
            ["BotonClaroHover"] = "#FFFFFF",
            ["BotonClaroTexto"] = "#16181D",
            ["BotonOscuro"] = "#1A1E25",
            ["BotonOscuroHover"] = "#232935",
            ["CerrarHover"] = "#C42B1C",
            ["Seleccion"] = "#0E1D36",
            ["SeleccionBorde"] = "#4C8DFF",
            ["EncabezadoTabla"] = "#22262E",
            ["CampoSoloLectura"] = "#1A1E25",
            ["DeshabilitadoFondo"] = "#22262E",
            ["DeshabilitadoTexto"] = "#646B78"
        };

    private static readonly IReadOnlyDictionary<string, string> PaletaClara =
        new Dictionary<string, string>
        {
            ["Fondo"] = "#F3F4F6",
            ["Superficie"] = "#FFFFFF",
            ["SuperficieElevada"] = "#FFFFFF",
            ["SuperficieActiva"] = "#F1F2F4",
            ["SuperficieHover"] = "#F1F2F4",
            ["SuperficieAlterna"] = "#FAFAFB",
            ["Borde"] = "#DDE0E5",
            ["BordeControl"] = "#8A909A",
            ["BordeControlHover"] = "#5B6270",
            ["BordeDeshabilitado"] = "#D1D5DB",
            ["Texto"] = "#16181D",
            ["TextoSuave"] = "#5B6270",
            ["TextoTenue"] = "#8A909A",
            ["TextoInverso"] = "#FFFFFF",
            ["Acento"] = "#ED1C24",
            ["AcentoFondo"] = "#FDECEC",
            ["AcentoBorde"] = "#F5C2C4",
            ["Peligro"] = "#DC2626",
            ["PeligroTexto"] = "#B4141A",
            ["PeligroHover"] = "#FDECEC",
            ["PeligroBordeHover"] = "#B4141A",
            ["Error"] = "#B4141A",
            ["Exito"] = "#16A34A",
            ["ExitoFondo"] = "#E8F5EC",
            ["ExitoBorde"] = "#B7DFC3",
            ["Advertencia"] = "#D97706",
            ["AdvertenciaTexto"] = "#92400E",
            ["AdvertenciaFondo"] = "#FEF4E2",
            ["AdvertenciaBorde"] = "#F6D7A4",
            ["Info"] = "#2563EB",
            ["InfoFondo"] = "#EAF1FE",
            ["InfoBorde"] = "#C3D5FB",
            ["InfoTexto"] = "#1E40AF",
            ["IconoVenta"] = "#16A34A",
            ["IconoYape"] = "#7C3AED",
            ["BotonClaro"] = "#1F2329",
            ["BotonClaroHover"] = "#3A414C",
            ["BotonClaroTexto"] = "#FFFFFF",
            ["BotonOscuro"] = "#FFFFFF",
            ["BotonOscuroHover"] = "#F1F2F4",
            ["CerrarHover"] = "#C42B1C",
            ["Seleccion"] = "#EAF1FE",
            ["SeleccionBorde"] = "#2563EB",
            ["EncabezadoTabla"] = "#EEF0F3",
            ["CampoSoloLectura"] = "#EEF0F3",
            ["DeshabilitadoFondo"] = "#E4E6EA",
            ["DeshabilitadoTexto"] = "#8A909A"
        };

    public static event EventHandler? Cambio;

    public static bool EsOscuro { get; private set; } = true;

    public static void Inicializar()
    {
        var guardado = Preferences.Default.Get(ClaveDePreferencia, "oscuro");
        Aplicar(string.Equals(guardado, "claro", StringComparison.OrdinalIgnoreCase) ? false : true, guardar: false);
    }

    public static void Alternar() => Aplicar(!EsOscuro);

    public static Color Color(string clave)
    {
        if (Application.Current?.Resources.TryGetValue(clave, out var valor) == true &&
            valor is Color color)
        {
            return color;
        }

        return Colors.Transparent;
    }

    private static void Aplicar(bool oscuro, bool guardar = true)
    {
        EsOscuro = oscuro;

        var paleta = oscuro ? PaletaOscura : PaletaClara;
        var recursos = Application.Current!.Resources;

        foreach (var (clave, valor) in paleta)
        {
            recursos[clave] = Microsoft.Maui.Graphics.Color.FromArgb(valor);
        }

        Application.Current.UserAppTheme = oscuro ? AppTheme.Dark : AppTheme.Light;

        if (guardar)
        {
            Preferences.Default.Set(ClaveDePreferencia, oscuro ? "oscuro" : "claro");
        }

        Cambio?.Invoke(null, EventArgs.Empty);
    }
}
