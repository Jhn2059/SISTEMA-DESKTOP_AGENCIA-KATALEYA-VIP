using Kataleya.Dominio.Enums;

namespace Kataleya.Contratos.Pasajes;

public sealed class SolicitudDePasaje
{
    public string PasajeroNombre { get; set; } = string.Empty;

    public TipoDocumento TipoDocumento { get; set; }

    public string NumeroDocumento { get; set; } = string.Empty;

    public string? Telefono { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string Destino { get; set; } = string.Empty;

    public string LugarDeEmbarque { get; set; } = string.Empty;

    public DateOnly FechaSalida { get; set; }

    public TimeOnly HoraSalida { get; set; }

    public long UnidadSalidaId { get; set; }

    public short NumeroAsiento { get; set; }

    public decimal? Monto { get; set; }

    public MedioDePago? Medio { get; set; }

    public bool MenorSinPadres { get; set; }

    public string? NombrePadre { get; set; }

    public string? NombreMadre { get; set; }

    public string? MotivoViaje { get; set; }

    public string? NumeroTalonario { get; set; }

    public DateTime? HoraEmisionReal { get; set; }
}

public sealed class IntegranteDeGrupo
{
    public string Nombre { get; set; } = string.Empty;

    public TipoDocumento TipoDocumento { get; set; }

    public string NumeroDocumento { get; set; } = string.Empty;

    public bool EsResponsable { get; set; }

    public bool MenorSinPadres { get; set; }

    public string? NombrePadre { get; set; }

    public string? NombreMadre { get; set; }

    public string? MotivoViaje { get; set; }
}

public sealed class UnidadDelGrupo
{
    public short TipoVehiculoId { get; set; }
}

public sealed class SolicitudDeGrupo
{
    public string Telefono { get; set; } = string.Empty;

    public decimal PrecioAcordado { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string Destino { get; set; } = string.Empty;

    public string LugarDeRecojo { get; set; } = string.Empty;

    public DateOnly FechaSalida { get; set; }

    public TimeOnly HoraSalida { get; set; }

    public List<IntegranteDeGrupo> Integrantes { get; set; } = [];

    public List<UnidadDelGrupo> Unidades { get; set; } = [];

    public decimal? Monto { get; set; }

    public MedioDePago? Medio { get; set; }
}

public sealed class DatosDePasaje
{
    public long Id { get; set; }

    public string PasajeroNombre { get; set; } = string.Empty;

    public TipoDocumento TipoDocumento { get; set; }

    public string NumeroDocumento { get; set; } = string.Empty;

    public string? Telefono { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string Destino { get; set; } = string.Empty;

    public string LugarDeEmbarque { get; set; } = string.Empty;

    public DateOnly FechaSalida { get; set; }

    public TimeOnly HoraSalida { get; set; }

    public long? UnidadSalidaId { get; set; }

    public string? Unidad { get; set; }

    public short? NumeroAsiento { get; set; }

    public long? GrupoId { get; set; }

    public string? GrupoCodigo { get; set; }

    public bool EsResponsable { get; set; }

    public decimal Importe { get; set; }

    public decimal Pagado { get; set; }

    public decimal Saldo { get; set; }

    public EstadoPasaje Estado { get; set; }

    public string Sede { get; set; } = string.Empty;

    public string Emisor { get; set; } = string.Empty;

    public DateTime FechaHoraRegistro { get; set; }

    public TipoRegistroDePasaje TipoRegistro { get; set; }

    public DateTime? HoraEmisionReal { get; set; }

    public string? NumeroTalonario { get; set; }

    public string? MotivoAnulacion { get; set; }

    public string? NombrePadre { get; set; }

    public string? NombreMadre { get; set; }

    public string? MotivoViaje { get; set; }

    public DatosDeBoleto? Boleto { get; set; }

    public List<DatosDePago> Pagos { get; set; } = [];
}

public sealed class DatosDeBoleto
{
    public long Id { get; set; }

    public string Serie { get; set; } = string.Empty;

    public long Numero { get; set; }

    public string NumeroCompleto => $"{Serie}-{Numero:D6}";

    public DateTime FechaHoraEmision { get; set; }

    public int Reimpresiones { get; set; }
}

public sealed class DatosDePago
{
    public long Id { get; set; }

    public decimal Importe { get; set; }

    public MedioDePago Medio { get; set; }

    public TipoDeMovimiento TipoMovimiento { get; set; }

    public DateTime FechaHora { get; set; }

    public string CobradoPor { get; set; } = string.Empty;
}

public sealed class DatosDeGrupo
{
    public long Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;

    public decimal PrecioAcordado { get; set; }

    public decimal Pagado { get; set; }

    public decimal Saldo { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string Destino { get; set; } = string.Empty;

    public string LugarDeRecojo { get; set; } = string.Empty;

    public DateOnly FechaSalida { get; set; }

    public TimeOnly HoraSalida { get; set; }

    public List<DatosDeUnidad> Unidades { get; set; } = [];

    public List<DatosDePasaje> Pasajes { get; set; } = [];

    public List<DatosDePago> Pagos { get; set; } = [];
}

public sealed class DatosDeUnidad
{
    public long Id { get; set; }

    public string Numero { get; set; } = string.Empty;

    public string TipoVehiculo { get; set; } = string.Empty;

    public short NumeroAsientos { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public DateTime FechaHoraSalida { get; set; }

    public SentidoDelViaje? Sentido { get; set; }

    public long? GrupoId { get; set; }

    public List<short> AsientosOcupados { get; set; } = [];
}

public sealed class SolicitudDeUnidad
{
    public short TipoVehiculoId { get; set; }

    public DateOnly Fecha { get; set; }

    public TimeOnly Hora { get; set; }

    public SentidoDelViaje Sentido { get; set; }
}

public sealed class FiltroDePasajes
{
    public DateOnly? Desde { get; set; }

    public DateOnly? Hasta { get; set; }

    public TimeOnly? Hora { get; set; }

    public EstadoPasaje? Estado { get; set; }

    public TipoDePasaje? Tipo { get; set; }

    public string? Pasajero { get; set; }

    public int? EmisorId { get; set; }

    public short? SedeId { get; set; }
}

public sealed class CambiosDelPasaje
{
    public string? PasajeroNombre { get; set; }

    public string? Telefono { get; set; }

    public DateOnly? FechaSalida { get; set; }

    public TimeOnly? HoraSalida { get; set; }

    public long? UnidadSalidaId { get; set; }

    public short? NumeroAsiento { get; set; }

    public string? LugarDeEmbarque { get; set; }
}

public sealed class SolicitudDeCobro
{
    public decimal Importe { get; set; }

    public MedioDePago Medio { get; set; }
}

public sealed class SolicitudDeAnulacion
{
    public string Motivo { get; set; } = string.Empty;

    public bool DevolverComoExcepcion { get; set; }
}

public sealed class RevisionDeAnulacion
{
    public List<string> Advertencias { get; set; } = [];

    public bool SeDevuelveElAdelanto { get; set; }

    public decimal ImporteADevolver { get; set; }

    public int HorasParaLaSalida { get; set; }

    public int HorasDeAviso { get; set; }
}

public sealed class ResultadoDeRevision
{
    public List<string> Advertencias { get; set; } = [];
}

public sealed class SolicitudDeFallaDeBoleto
{
    public string Descripcion { get; set; } = string.Empty;
}
