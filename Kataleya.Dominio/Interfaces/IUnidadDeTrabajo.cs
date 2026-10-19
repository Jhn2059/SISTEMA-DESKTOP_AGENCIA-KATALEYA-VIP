namespace Kataleya.Dominio.Interfaces;

public interface IUnidadDeTrabajo
{
    Task EjecutarEnTransaccionAsync(Func<Task> trabajo);
}
