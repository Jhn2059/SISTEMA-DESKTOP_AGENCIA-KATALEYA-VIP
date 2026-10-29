using Kataleya.Contratos.Pasajes;

namespace Kataleya.Escritorio.Servicios.Impresion;

public sealed class AdaptadorImpresora : IImpresora
{
    public ConfiguracionDeImpresora? Configuracion { get; set; }

    public Task<EstadoImpresora> ImprimirAsync(DatosDePasaje pasaje)
    {
        ArgumentNullException.ThrowIfNull(pasaje);

        if (Configuracion is not ConfiguracionDeImpresora configuracion)
        {
            return Task.FromResult(EstadoImpresora.SinRespuesta);
        }

        var datos = BoletoEnEscPos.Armar(pasaje, configuracion.AnchoDePapel);
        return Task.Run(() => ColaDeImpresion.EnviarAsync(
            configuracion.NombreImpresora,
            $"Boleto {pasaje.Boleto?.NumeroCompleto}",
            datos));
    }

    public Task<EstadoImpresora> ConsultarEstadoAsync() =>
        Configuracion is ConfiguracionDeImpresora configuracion
            ? Task.Run(() => ColaDeImpresion.ConsultarEstado(configuracion.NombreImpresora))
            : Task.FromResult(EstadoImpresora.SinRespuesta);

    public Task<IReadOnlyList<string>> ListarImpresorasAsync() =>
        Task.Run(() => ColaDeImpresion.ListarImpresoras());

    public Task<EstadoImpresora> ImprimirPruebaAsync()
    {
        if (Configuracion is not ConfiguracionDeImpresora configuracion)
        {
            return Task.FromResult(EstadoImpresora.SinRespuesta);
        }

        var datos = BoletoEnEscPos.ArmarPrueba(
            configuracion.NombreImpresora,
            configuracion.AnchoDePapel,
            DateTime.Now);

        return Task.Run(() => ColaDeImpresion.EnviarAsync(
            configuracion.NombreImpresora,
            "Prueba de impresión",
            datos));
    }
}
