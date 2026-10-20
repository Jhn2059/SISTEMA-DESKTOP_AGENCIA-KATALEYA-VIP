namespace Kataleya.Escritorio.Servicios;

public interface INavegacion
{
    Task IrAlPanelAsync();

    Task IrAlInicioDeSesionAsync();

    Task IrAGestionDeUsuariosAsync();

    Task VolverAsync();
}
