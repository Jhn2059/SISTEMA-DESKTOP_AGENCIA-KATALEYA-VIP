using Kataleya.Contratos.Reportes;

namespace Kataleya.Escritorio.Servicios;

public interface IRespaldo
{
    Task<string> GuardarReporteAnualAsync(ArchivoProtegido pdf);
}
