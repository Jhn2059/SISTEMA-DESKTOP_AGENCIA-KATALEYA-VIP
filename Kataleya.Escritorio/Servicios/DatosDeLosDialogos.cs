using Kataleya.Contratos.Cuentas;
using Kataleya.Dominio.DataObjects;

namespace Kataleya.Escritorio.Servicios;

// Cada operación de IDialogos recibe dos parámetros como máximo; si hacen falta más, van juntos aquí.
public sealed record Confirmacion(
    string Titulo,
    string Mensaje,
    string TextoAceptar,
    string TextoCancelar);

public sealed record FormularioDeCuenta(
    IReadOnlyList<DatosDeRol> Roles,
    IReadOnlyList<DatosDeSede> Sedes,
    IReadOnlyDictionary<string, string>? Errores = null,
    SolicitudDeCuenta? Anterior = null);
