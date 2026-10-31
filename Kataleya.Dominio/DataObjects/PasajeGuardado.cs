using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.DataObjects;

public sealed record PasajeGuardado(
    long Id,
    long UnidadSalidaId,
    int NumeroAsiento,
    string NombrePasajero,
    TipoDocumento TipoDocumento,
    string NumeroDocumento,
    string Celular,
    string Origen,
    string Destino,
    string LugarEmbarque,
    DateTime FechaHoraSalida,
    DateTime FechaVenta,
    decimal Adelanto,
    string Estado,
    int UsuarioId,
    string VendidaPor,
    bool EsMenor,
    string? NombrePadreMadre,
    string? MotivoMenor);