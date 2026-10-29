using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

// El tema es de cada cuenta, no de la laptop, porque la usan varias personas (CDU-20).
public sealed class Preferencias
{
    public Preferencias(int idUsuario, Tema tema)
    {
        IdUsuario = idUsuario;
        Tema = tema;
    }

    public int IdUsuario { get; }

    public Tema Tema { get; private set; }

    public void ElegirTema(Tema tema) =>
        Tema = tema;
}
