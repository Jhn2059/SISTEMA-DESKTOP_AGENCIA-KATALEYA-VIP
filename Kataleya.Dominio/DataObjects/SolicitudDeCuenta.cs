namespace Kataleya.Dominio.DataObjects;

// Lo que pide el Administrador para crear una cuenta (CDU-09).
public sealed record SolicitudDeCuenta(
    string NombrePersona,
    string NombreUsuario,
    string ContrasenaInicial,
    short IdRol,
    short IdSede);
