using Kataleya.Cliente;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Escritorio.Servicios.Impresion;

// En la laptop: el servidor numera y registra el boleto; aquí se imprime (CDU-05) y se configura la impresora (CDU-21).
public sealed class ServicioImpresion
{
    private const string ClaveDeImpresora = "impresora";
    private const string ClaveDeAncho = "ancho_papel";

    private readonly IImpresora _impresora;
    private readonly ClienteApi _clienteApi;
    private readonly IRegistroTecnico _registroTecnico;

    public ServicioImpresion(
        IImpresora impresora,
        ClienteApi clienteApi,
        IRegistroTecnico registroTecnico)
    {
        _impresora = impresora ?? throw new ArgumentNullException(nameof(impresora));
        _clienteApi = clienteApi ?? throw new ArgumentNullException(nameof(clienteApi));
        _registroTecnico = registroTecnico ?? throw new ArgumentNullException(nameof(registroTecnico));
        _impresora.Configuracion = LeerConfiguracion();
    }

    public async Task<EstadoImpresora> ImprimirBoletoAsync(DatosDePasaje pasaje)
    {
        ArgumentNullException.ThrowIfNull(pasaje);

        var boleto = pasaje.Boleto
            ?? throw new InvalidOperationException("Solo se imprime el boleto de un pasaje pagado.");

        var estado = await _impresora.ImprimirAsync(pasaje);
        if (estado != EstadoImpresora.Lista)
        {
            await RegistrarFallaAsync(boleto, estado);
        }

        return estado;
    }

    public async Task<EstadoImpresora> ImprimirBoletosAsync(IEnumerable<DatosDePasaje> pasajes)
    {
        ArgumentNullException.ThrowIfNull(pasajes);

        foreach (var pasaje in pasajes.Where(pasaje => pasaje.Boleto is not null))
        {
            var estado = await ImprimirBoletoAsync(pasaje);
            if (estado != EstadoImpresora.Lista)
            {
                return estado;
            }
        }

        return EstadoImpresora.Lista;
    }

    // La reimpresión no consume numeración; solo se anota en la bitácora si el boleto salió impreso.
    public async Task<EstadoImpresora> ReimprimirAsync(DatosDePasaje pasaje)
    {
        var estado = await ImprimirBoletoAsync(pasaje);

        if (estado == EstadoImpresora.Lista)
        {
            await _clienteApi.ReimprimirBoletoAsync(pasaje.Boleto!.Serie, pasaje.Boleto.Numero);
        }

        return estado;
    }

    public Task<IReadOnlyList<string>> ListarImpresorasAsync() =>
        _impresora.ListarImpresorasAsync();

    public Task<ConfiguracionDeImpresora?> ObtenerConfiguracionAsync() =>
        Task.FromResult(_impresora.Configuracion);

    public Task GuardarConfiguracionAsync(ConfiguracionDeImpresora configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        Preferences.Default.Set(ClaveDeImpresora, configuracion.NombreImpresora);
        Preferences.Default.Set(ClaveDeAncho, configuracion.AnchoDePapel.ToString());
        _impresora.Configuracion = configuracion;
        return Task.CompletedTask;
    }

    public Task<EstadoImpresora> ConsultarEstadoAsync() =>
        _impresora.ConsultarEstadoAsync();

    public Task<EstadoImpresora> ImprimirPruebaAsync() =>
        _impresora.ImprimirPruebaAsync();

    private async Task RegistrarFallaAsync(DatosDeBoleto boleto, EstadoImpresora estado)
    {
        var sinPapel = estado == EstadoImpresora.SinPapel;
        var descripcion = sinPapel
            ? "La impresora no tiene papel."
            : "La impresora no responde.";

        await _registroTecnico.RegistrarAsync(new ErrorTecnico
        {
            Codigo = sinPapel ? "IMP-02" : "IMP-01",
            FechaHora = DateTime.UtcNow,
            Operacion = "Imprimir boleto",
            Detalle = $"{boleto.NumeroCompleto}: {descripcion}"
        });

        await _clienteApi.RegistrarFallaDeBoletoAsync(boleto.Serie, boleto.Numero, descripcion);
    }

    private static ConfiguracionDeImpresora? LeerConfiguracion()
    {
        var nombre = Preferences.Default.Get(ClaveDeImpresora, string.Empty);
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return null;
        }

        var ancho = Enum.TryParse<AnchoDePapel>(
            Preferences.Default.Get(ClaveDeAncho, nameof(AnchoDePapel.Mm58)),
            out var valor)
            ? valor
            : AnchoDePapel.Mm58;

        return new ConfiguracionDeImpresora(nombre, ancho);
    }
}
