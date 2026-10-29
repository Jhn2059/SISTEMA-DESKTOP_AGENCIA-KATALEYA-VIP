using Kataleya.Contratos.Pasajes;

namespace Kataleya.Escritorio.Servicios.Impresion;

public interface IImpresora
{
    ConfiguracionDeImpresora? Configuracion { get; set; }

    Task<EstadoImpresora> ImprimirAsync(DatosDePasaje pasaje);

    Task<EstadoImpresora> ConsultarEstadoAsync();

    Task<IReadOnlyList<string>> ListarImpresorasAsync();

    Task<EstadoImpresora> ImprimirPruebaAsync();
}
