namespace Kataleya.Escritorio.Servicios.Impresion;

public enum EstadoImpresora
{
    Lista,
    SinPapel,
    SinRespuesta
}

public enum AnchoDePapel
{
    Mm58,
    Mm80
}

public sealed record ConfiguracionDeImpresora(string NombreImpresora, AnchoDePapel AnchoDePapel);
