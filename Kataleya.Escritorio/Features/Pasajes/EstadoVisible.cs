using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.UIHelpers;
using Microsoft.Maui.Graphics;

namespace Kataleya.Escritorio.Features.Pasajes;

// Nombre y colores con que se muestra cada estado del pasaje (ERS, Anexo E).
public static class EstadoVisible
{
    public static string Nombre(EstadoPasaje estado) =>
        estado switch
        {
            EstadoPasaje.Reservado => "Reservado",
            EstadoPasaje.ReservadoConAdelanto => "Reservado con adelanto",
            EstadoPasaje.Pagado => "Pagado",
            EstadoPasaje.Anulado => "Anulado",
            EstadoPasaje.NoSePresento => "No se presentó",
            _ => estado.ToString()
        };

    public static Color Fondo(EstadoPasaje estado) =>
        ServicioDeTema.Color(estado switch
        {
            EstadoPasaje.Pagado => "ExitoFondo",
            EstadoPasaje.ReservadoConAdelanto => "AdvertenciaFondo",
            EstadoPasaje.Reservado => "InfoFondo",
            EstadoPasaje.NoSePresento => "PeligroHover",
            _ => "SuperficieActiva"
        });

    public static Color Texto(EstadoPasaje estado) =>
        ServicioDeTema.Color(estado switch
        {
            EstadoPasaje.Pagado => "Exito",
            EstadoPasaje.ReservadoConAdelanto => "AdvertenciaTexto",
            EstadoPasaje.Reservado => "InfoTexto",
            EstadoPasaje.NoSePresento => "PeligroTexto",
            _ => "TextoSuave"
        });
}
