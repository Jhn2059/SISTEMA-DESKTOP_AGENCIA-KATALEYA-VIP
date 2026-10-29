using Kataleya.Dominio.DataObjects;

namespace Kataleya.Dominio.Interfaces;

public interface IAlmacenDeCopias
{
    bool EstaConfigurado { get; }

    Task SubirAsync(CopiaCifrada copia);

    Task BorrarLasViejasAsync(int conservar);
}
