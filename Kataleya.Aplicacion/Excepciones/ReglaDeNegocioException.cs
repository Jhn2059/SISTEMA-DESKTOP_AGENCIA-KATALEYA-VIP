namespace Kataleya.Aplicacion.Excepciones;

public sealed class ReglaDeNegocioException : Exception
{
    public ReglaDeNegocioException(string mensaje, string? campo = null, string? codigo = null)
        : base(mensaje)
    {
        Campo = campo;
        Codigo = codigo;
    }

    public string? Campo { get; }

    public string? Codigo { get; }
}
