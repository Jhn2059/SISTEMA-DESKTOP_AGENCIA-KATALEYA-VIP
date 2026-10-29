using Kataleya.Dominio.Entities;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioReportesAnuales
{
    Task<bool> ExisteDelAnioAsync(int anio);

    Task AgregarAsync(ReporteAnual reporte);
}
