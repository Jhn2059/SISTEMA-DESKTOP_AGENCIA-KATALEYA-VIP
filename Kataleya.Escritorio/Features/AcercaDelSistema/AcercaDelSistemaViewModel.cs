using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Escritorio.Servicios;
using Microsoft.Win32;

namespace Kataleya.Escritorio.Features.AcercaDelSistema;

// IS-01: versiones en uso y licencias del software de terceros.
public partial class AcercaDelSistemaViewModel : ObservableObject
{
    private readonly INavegacion _navegacion;

    public AcercaDelSistemaViewModel(INavegacion navegacion)
    {
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));

        var maui = typeof(Application).Assembly.GetName().Version;

        SoftwareEnUso =
        [
            new("Sistema operativo", SistemaOperativo()),
            new("Plataforma", $".NET {Environment.Version.Major}.{Environment.Version.Minor}"),
            new("Interfaz", $".NET MAUI {maui?.Major}.{maui?.Minor}"),
            new("Web API", "ASP.NET Core de .NET 10.0, en Render"),
            new("Base de datos", "PostgreSQL, en Supabase"),
            new("Controlador de la base", "Npgsql 10.0")
        ];
    }

    public string Modulo => $"Módulo de Reservas y Venta de Pasajes · versión {AppInfo.Current.VersionString}";

    public IReadOnlyList<DatoDelSistema> SoftwareEnUso { get; }

    public IReadOnlyList<DatoDelSistema> Licencias { get; } =
    [
        new("CommunityToolkit.Mvvm", "MIT"),
        new(".NET MAUI", "MIT"),
        new("Npgsql", "PostgreSQL License"),
        new("PDFsharp", "MIT"),
        new("Fuente Inter", "SIL Open Font License 1.1"),
        new("Íconos Lucide", "ISC")
    ];

    [RelayCommand]
    private Task CerrarAsync() =>
        _navegacion.CerrarModalAsync();

    private static string SistemaOperativo()
    {
        var version = Environment.OSVersion.Version;
        var nombre = version.Build >= 22000 ? "Windows 11" : "Windows 10";
        var edicion = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "DisplayVersion",
            null) as string;
        var bits = Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits";

        return string.IsNullOrWhiteSpace(edicion)
            ? $"{nombre} ({version.Build}), {bits}"
            : $"{nombre} versión {edicion}, {bits}";
    }
}

public sealed record DatoDelSistema(string Nombre, string Valor);
