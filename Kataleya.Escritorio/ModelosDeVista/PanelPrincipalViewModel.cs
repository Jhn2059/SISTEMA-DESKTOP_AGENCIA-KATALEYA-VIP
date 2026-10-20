using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Escritorio.Servicios;

namespace Kataleya.Escritorio.ModelosDeVista;

public partial class PanelPrincipalViewModel : ObservableObject
{
    private readonly ClienteApi _cliente;
    private readonly INavegacion _navegacion;

    public PanelPrincipalViewModel(ClienteApi cliente, INavegacion navegacion)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));
    }

    public string Nombre => _cliente.CuentaActual?.Nombre ?? string.Empty;

    public string Rol => _cliente.CuentaActual?.Rol ?? string.Empty;

    public string Sede => _cliente.CuentaActual?.Sede ?? string.Empty;

    public bool EsAdministrador => _cliente.EsAdministrador;

    public void Actualizar()
    {
        OnPropertyChanged(nameof(Nombre));
        OnPropertyChanged(nameof(Rol));
        OnPropertyChanged(nameof(Sede));
        OnPropertyChanged(nameof(EsAdministrador));
    }

    [RelayCommand]
    private Task AbrirGestionDeUsuariosAsync() =>
        _navegacion.IrAGestionDeUsuariosAsync();

    [RelayCommand]
    private async Task CerrarSesionAsync()
    {
        _cliente.CerrarSesion();
        await _navegacion.IrAlInicioDeSesionAsync();
    }
}
