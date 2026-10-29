using Kataleya.Contratos.Cuentas;

namespace Kataleya.Escritorio.Servicios;

public interface IDialogos
{
    Task<SolicitudDeCuenta?> PedirDatosDeCuentaAsync(
        IReadOnlyList<DatosDeRol> roles,
        IReadOnlyList<DatosDeSede> sedes,
        IReadOnlyDictionary<string, string>? errores = null,
        SolicitudDeCuenta? anterior = null);

    Task<string?> PedirContrasenaAsync(
        string nombreUsuario,
        IReadOnlyDictionary<string, string>? errores = null);

    Task<string?> PedirContrasenaDelReporteAsync(int anio);

    Task<bool> ConfirmarAsync(
        string titulo,
        string mensaje,
        string textoAceptar,
        string textoCancelar);

    Task MostrarMensajeAsync(string titulo, string mensaje);
}
