using Kataleya.Contratos.Configuracion;
using Kataleya.Contratos.Cuentas;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.DataObjects;

namespace Kataleya.Escritorio.Servicios;

public interface IDialogos
{
    Task<SolicitudDeCuenta?> PedirDatosDeCuentaAsync(FormularioDeCuenta formulario);

    Task<string?> PedirContrasenaAsync(
        string nombreUsuario,
        IReadOnlyDictionary<string, string>? errores = null);

    Task<string?> PedirContrasenaDelReporteAsync(int anio);

    Task<SolicitudDeCobro?> PedirCobroAsync(DatosDePasaje pasaje);

    Task<SolicitudDeAnulacion?> PedirAnulacionAsync(DatosDePasaje pasaje, RevisionDeAnulacion revision);

    Task<CambiosDelPasaje?> PedirCambiosAsync(DatosDePasaje pasaje, bool soloAsiento);

    Task<SolicitudDeTarifa?> PedirTarifaProgramadaAsync(
        DatosDeTarifa? tarifa,
        IReadOnlyDictionary<string, string>? errores = null);

    Task<bool> ConfirmarAsync(Confirmacion confirmacion);

    Task MostrarMensajeAsync(string titulo, string mensaje);
}
