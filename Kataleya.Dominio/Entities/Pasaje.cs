using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public sealed class Pasaje
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

    public short? NumeroAsiento { get; set; }

    public string? UnidadNombre { get; set; }

    public string? TipoVehiculoNombre { get; set; }

    public long? GrupoId { get; set; }

    public string? GrupoCodigo { get; set; }

    public bool EsResponsable { get; set; }

    public decimal Importe { get; set; }

    public EstadoPasaje Estado { get; set; }

    public short SedeId { get; set; }

    public string SedeNombre { get; set; } = string.Empty;

    public int EmisorId { get; set; }

    public string EmisorNombre { get; set; } = string.Empty;

    public DateTime FechaHoraRegistro { get; set; }

    public TipoRegistroDePasaje TipoRegistro { get; set; }

    public DateTime? HoraEmisionReal { get; set; }

    public string? NumeroTalonario { get; set; }

    public string? MotivoAnulacion { get; set; }

    public string? NombrePadre { get; set; }

    public string? NombreMadre { get; set; }

    public string? MotivoViaje { get; set; }

    public decimal Pagado { get; set; }

    public decimal Saldo => Math.Max(0, Importe - Pagado);

    public BoletoDeViaje? Boleto { get; set; }

    public List<Pago> Pagos { get; set; } = [];
}

public sealed class Pago
{
    public long Id { get; set; }

    public long? PasajeId { get; set; }

    public long? GrupoId { get; set; }

    public decimal Importe { get; set; }

    public MedioDePago Medio { get; set; }

    public TipoDeMovimiento TipoMovimiento { get; set; }

    public DateTime FechaHora { get; set; }

    public int UsuarioId { get; set; }

    public string UsuarioNombre { get; set; } = string.Empty;
}

public sealed class BoletoDeViaje
{
    public long Id { get; set; }

    public long PasajeId { get; set; }

    public string Serie { get; set; } = string.Empty;

    public long Numero { get; set; }

    public DateTime FechaHoraEmision { get; set; }

    public int EmisorId { get; set; }

    public int Reimpresiones { get; set; }

    public string NumeroCompleto => $"{Serie}-{Numero:D6}";
}

public sealed class Grupo
{
    public long Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;

    public decimal PrecioAcordado { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string Destino { get; set; } = string.Empty;

    public string LugarDeRecojo { get; set; } = string.Empty;

    public DateOnly FechaSalida { get; set; }

    public TimeOnly HoraSalida { get; set; }

    public int UsuarioId { get; set; }

    public string UsuarioNombre { get; set; } = string.Empty;

    public DateTime FechaHora { get; set; }

    public decimal Pagado { get; set; }

    public List<UnidadDeSalida> Unidades { get; set; } = [];
}

public sealed class UnidadDeSalida
{
    public long Id { get; set; }

    public DateTime FechaHoraSalida { get; set; }

    public SentidoDelViaje? Sentido { get; set; }

    public string Numero { get; set; } = string.Empty;

    public short TipoVehiculoId { get; set; }

    public string TipoVehiculoNombre { get; set; } = string.Empty;

    public short NumeroAsientos { get; set; }

    public long? GrupoId { get; set; }

    public List<short> AsientosOcupados { get; set; } = [];

    public string Descripcion => $"Unidad {Numero} · {TipoVehiculoNombre.ToLowerInvariant()} de {NumeroAsientos}";
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

public sealed class FiltroDeBitacora
{
    public int? UsuarioId { get; set; }

    public DateOnly? Desde { get; set; }

    public DateOnly? Hasta { get; set; }

    public long? PasajeId { get; set; }
}

public sealed class EntradaDeBitacora
{
    public long Id { get; set; }

    public DateTime FechaHora { get; set; }

    public string UsuarioNombre { get; set; } = string.Empty;

    public string Operacion { get; set; } = string.Empty;

    public long? PasajeId { get; set; }

    public string? ValorAnterior { get; set; }

    public string? ValorNuevo { get; set; }
}
