using Kataleya.Dominio.DataObjects;

namespace Kataleya.Dominio.Entities;

public sealed class ParametroDelNegocio
{
    public ParametroDelNegocio(string nombre, string valor)
        : this(nombre, valor, 0)
    {
    }

    internal ParametroDelNegocio(string nombre, string valor, int idUsuario)
    {
        Nombre = nombre;
        Valor = valor;
        IdUsuario = idUsuario;
    }

    public string Nombre { get; }

    public string Valor { get; private set; }

    public int IdUsuario { get; private set; }

    public void CambiarValor(string valor, Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        Valor = valor;
        IdUsuario = usuario.Id;
    }

    public static ParametroDelNegocio Reconstruir(ParametroDelNegocioGuardado guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return new ParametroDelNegocio(
            guardado.Nombre,
            guardado.Valor,
            guardado.IdUsuario);
    }
}
