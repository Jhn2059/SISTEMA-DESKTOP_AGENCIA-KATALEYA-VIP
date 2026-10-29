using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Escritorio.Features.Configuracion.CuentasDeUsuario;
using Kataleya.Escritorio.Features.Configuracion.Equipo;
using Kataleya.Escritorio.Features.Configuracion.TarifasYParametros;

namespace Kataleya.Escritorio.Features.Configuracion;

// Configuración (IU-P10), solo del Administrador, con tres pestañas.
public partial class ConfiguracionViewModel : ObservableObject
{
    public ConfiguracionViewModel(
        TarifasYParametrosViewModel tarifas,
        CuentasDeUsuarioViewModel cuentas,
        EquipoViewModel equipo)
    {
        Tarifas = tarifas ?? throw new ArgumentNullException(nameof(tarifas));
        Cuentas = cuentas ?? throw new ArgumentNullException(nameof(cuentas));
        Equipo = equipo ?? throw new ArgumentNullException(nameof(equipo));
    }

    public TarifasYParametrosViewModel Tarifas { get; }

    public CuentasDeUsuarioViewModel Cuentas { get; }

    public EquipoViewModel Equipo { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnTarifas))]
    [NotifyPropertyChangedFor(nameof(EnCuentas))]
    [NotifyPropertyChangedFor(nameof(EnEquipo))]
    private PestanaDeConfiguracion pestanaActiva = PestanaDeConfiguracion.TarifasYParametros;

    public bool EnTarifas => PestanaActiva == PestanaDeConfiguracion.TarifasYParametros;

    public bool EnCuentas => PestanaActiva == PestanaDeConfiguracion.CuentasDeUsuario;

    public bool EnEquipo => PestanaActiva == PestanaDeConfiguracion.Equipo;

    public async Task CargarAsync()
    {
        await Tarifas.CargarAsync();
        await Cuentas.CargarAsync();

        if (EnEquipo)
        {
            await Equipo.CargarAsync();
        }
    }

    [RelayCommand]
    private async Task ElegirPestanaAsync(PestanaDeConfiguracion pestana)
    {
        Tarifas.CerrarAvisoCommand.Execute(null);
        PestanaActiva = pestana;

        if (pestana == PestanaDeConfiguracion.Equipo)
        {
            await Equipo.CargarAsync();
        }
    }
}
