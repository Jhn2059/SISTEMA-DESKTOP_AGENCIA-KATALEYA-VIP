using Kataleya.Dominio.Entities;

namespace Kataleya.Aplicacion;

public sealed class ContextoDeOperacion
{
    public Guid? ClaveDeOperacion { get; set; }

    public int IdUsuario { get; set; }

    public Usuario? Usuario { get; set; }
}
