namespace Kataleya.Dominio.Enums;

public enum EstadoPasaje
{
    Reservado,
    ReservadoConAdelanto,
    Pagado,
    Anulado,
    NoSePresento
}

public enum TipoRegistroDePasaje
{
    Normal,
    Diferido
}

public enum TipoDocumento
{
    DNI,
    Pasaporte
}

public enum MedioDePago
{
    Efectivo,
    Yape
}

public enum TipoDeMovimiento
{
    Pago,
    Devolucion
}

public enum TipoDeTarifa
{
    Regular,
    Programada
}

public enum SentidoDelViaje
{
    Ida,
    Vuelta,
    Ambos
}

public enum TipoDePasaje
{
    Individual,
    Grupo
}
