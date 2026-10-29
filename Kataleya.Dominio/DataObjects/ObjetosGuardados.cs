using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

// El estado que lee cada repositorio, para rehacer la entidad con Reconstruir.
public sealed record RolGuardado(short Id, string Nombre);

public sealed record SedeGuardada(short Id, string Nombre);

public sealed record UsuarioGuardado(
    int Id,
    string NombrePersona,
    string NombreUsuario,
    string ResumenContrasena,
    string Sal,
    bool EsCuentaPrincipal,
    RolGuardado Rol,
    SedeGuardada Sede,
    EstadoCuenta Estado,
    int CantidadDeIntentosFallidos,
    DateTime? FinBloqueo);
