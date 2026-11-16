using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.ValueObjects;

namespace Kataleya.Dominio.Entities;

public sealed class Pasaje
{
    public const int LongitudMaximaNombrePasajero = 100;
    public const int LongitudMaximaNumeroDocumento = 20;
    public const int LongitudMaximaCelular = 12;
    public const int LongitudMaximaNombrePadreMadre = 100;
    public const int LongitudMaximaMotivoMenor = 200;
    public const int LongitudMaximaMotivoDeAnulacion = 200;

    private readonly List<Pago> _pagos = [];

    public Pasaje(
        UnidadDeSalida unidad,
        SolicitudDePasaje solicitud,
        DatosDelViaje viaje,
        decimal importe,
        EstadoPasaje estado,
        Usuario emisor,
        DateTime fechaVenta,
        bool esDiferido = false)
        : this(
            0,
            (unidad ?? throw new ArgumentNullException(nameof(unidad))).Id,
            null,
            (solicitud ?? throw new ArgumentNullException(nameof(solicitud))).NumeroAsiento,
            solicitud.NombrePasajero,
            solicitud.TipoDocumento,
            solicitud.NumeroDocumento,
            solicitud.Celular,
            (viaje ?? throw new ArgumentNullException(nameof(viaje))).Origen,
            viaje.Destino,
            viaje.LugarEmbarque,
            unidad.FechaHoraSalida,
            fechaVenta,
            importe,
            estado,
            TipoDeRegistroDePasaje.Individual,
            (emisor ?? throw new ArgumentNullException(nameof(emisor))).Id,
            emisor.Nombre,
            solicitud.EsMenor,
            solicitud.NombrePadreMadre,
            solicitud.MotivoMenor,
            false,
            null,
            esDiferido: esDiferido)
    {
    }

    public Pasaje(
        long grupoId,
        string codigoDeGrupo,
        IntegranteDeGrupo integrante,
        DatosDelViaje viaje,
        decimal importe,
        EstadoPasaje estado,
        Usuario emisor,
        DateTime fechaVenta)
        : this(
            0,
            null,
            grupoId,
            null,
            (integrante ?? throw new ArgumentNullException(nameof(integrante))).Nombre,
            integrante.TipoDocumento,
            integrante.NumeroDocumento,
            null,
            (viaje ?? throw new ArgumentNullException(nameof(viaje))).Origen,
            viaje.Destino,
            viaje.LugarEmbarque,
            viaje.FechaHoraSalida,
            fechaVenta,
            importe,
            estado,
            TipoDeRegistroDePasaje.Grupo,
            (emisor ?? throw new ArgumentNullException(nameof(emisor))).Id,
            emisor.Nombre,
            false,
            null,
            null,
            integrante.EsResponsable,
            codigoDeGrupo)
    {
    }

    internal Pasaje(
        long id,
        long? unidadDeSalidaId,
        long? grupoId,
        int? numeroAsiento,
        string nombrePasajero,
        TipoDocumento tipoDocumento,
        string numeroDocumento,
        string? celular,
        string origen,
        string destino,
        string lugarEmbarque,
        DateTime fechaHoraSalida,
        DateTime fechaVenta,
        decimal importe,
        EstadoPasaje estado,
        TipoDeRegistroDePasaje tipoRegistro,
        int emisorId,
        string emisor,
        bool esMenor,
        string? nombrePadreMadre,
        string? motivoMenor,
        bool esResponsable,
        string? codigoDeGrupo,
        string? motivoDeAnulacion = null,
        DateTime? fechaDeAnulacion = null,
        bool esDiferido = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emisor);

        Id = id;
        UnidadDeSalidaId = unidadDeSalidaId;
        GrupoId = grupoId;
        NumeroAsiento = numeroAsiento;
        NombrePasajero = nombrePasajero?.Trim() ?? string.Empty;
        TipoDocumento = tipoDocumento;
        NumeroDocumento = numeroDocumento?.Trim() ?? string.Empty;
        Celular = string.IsNullOrWhiteSpace(celular) ? null : celular.Trim();
        Origen = origen?.Trim() ?? string.Empty;
        Destino = destino?.Trim() ?? string.Empty;
        LugarEmbarque = lugarEmbarque?.Trim() ?? string.Empty;
        FechaHoraSalida = fechaHoraSalida;
        FechaVenta = fechaVenta;
        Importe = importe;
        Estado = estado;
        TipoRegistro = tipoRegistro;
        EmisorId = emisorId;
        Emisor = emisor;
        EsMenor = esMenor;
        NombrePadreMadre = nombrePadreMadre?.Trim();
        MotivoMenor = motivoMenor?.Trim();
        EsResponsable = esResponsable;
        CodigoDeGrupo = string.IsNullOrWhiteSpace(codigoDeGrupo) ? null : codigoDeGrupo.Trim();
        MotivoDeAnulacion = string.IsNullOrWhiteSpace(motivoDeAnulacion)
            ? null
            : motivoDeAnulacion.Trim();
        FechaDeAnulacion = fechaDeAnulacion;
        EsDiferido = esDiferido;
    }

    public long Id { get; private set; }

    public long? UnidadDeSalidaId { get; }

    public long? GrupoId { get; }

    public int? NumeroAsiento { get; }

    public string NombrePasajero { get; }

    public TipoDocumento TipoDocumento { get; }

    public string NumeroDocumento { get; }

    public string? Celular { get; }

    public string Origen { get; }

    public string Destino { get; }

    public string LugarEmbarque { get; }

    public DateTime FechaHoraSalida { get; }

    public DateTime FechaVenta { get; }

    public decimal Importe { get; }

    public EstadoPasaje Estado { get; private set; }

    public TipoDeRegistroDePasaje TipoRegistro { get; }

    public int EmisorId { get; }

    public string Emisor { get; }

    public bool EsMenor { get; }

    public string? NombrePadreMadre { get; }

    public string? MotivoMenor { get; }

    public bool EsResponsable { get; }

    public string? CodigoDeGrupo { get; }

    public string? MotivoDeAnulacion { get; }

    public DateTime? FechaDeAnulacion { get; }

    public bool EsDiferido { get; }

    public bool EsDeGrupo => TipoRegistro == TipoDeRegistroDePasaje.Grupo;

    public IReadOnlyList<Pago> Pagos => _pagos;

    public BoletoDeViaje? Boleto { get; private set; }

    public decimal Pagado => _pagos.Sum(pago =>
        pago.Tipo == TipoDePago.Devolucion ? -pago.Monto : pago.Monto);

    public decimal Saldo => Estado is EstadoPasaje.Anulado or EstadoPasaje.NoSePresento
        ? 0
        : Math.Max(0, Importe - Pagado);

    public bool EstaPagadoCompleto => Pagado >= Importe;

    public void AsignarId(long id)
    {
        if (Id != 0)
        {
            throw new InvalidOperationException("El pasaje ya tiene identificador.");
        }

        Id = id;
    }

    public void AgregarPago(Pago pago)
    {
        ArgumentNullException.ThrowIfNull(pago);

        if (pago.Id != 0 && _pagos.Any(existente => existente.Id == pago.Id))
        {
            return;
        }

        _pagos.Add(pago);

        if (Estado is not (EstadoPasaje.Anulado or EstadoPasaje.NoSePresento))
        {
            ActualizarEstadoPorPagos();
        }
    }

    public void FijarBoleto(BoletoDeViaje boleto)
    {
        ArgumentNullException.ThrowIfNull(boleto);
        Boleto = boleto;
    }

    public void MarcarPagado() =>
        Estado = EstadoPasaje.Pagado;

    public List<ErrorDeCampo> ValidarDatosDelPasajero()
    {
        var errores = new List<ErrorDeCampo>();

        if (string.IsNullOrWhiteSpace(NombrePasajero))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "nombrePasajero",
                Mensaje = "Escriba el nombre del pasajero."
            });
        }
        else if (NombrePasajero.Length > LongitudMaximaNombrePasajero)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "nombrePasajero",
                Mensaje = $"El nombre no puede superar los {LongitudMaximaNombrePasajero} caracteres."
            });
        }

        if (TipoDocumento == TipoDocumento.DNI)
        {
            if (NumeroDocumento.Length != 8 || !NumeroDocumento.All(char.IsDigit))
            {
                errores.Add(new ErrorDeCampo
                {
                    Campo = "numeroDocumento",
                    Mensaje = "El DNI debe tener 8 dígitos."
                });
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(NumeroDocumento))
            {
                errores.Add(new ErrorDeCampo
                {
                    Campo = "numeroDocumento",
                    Mensaje = "Escriba el número de documento."
                });
            }
            else if (NumeroDocumento.Length > 12 ||
                     !NumeroDocumento.All(char.IsLetterOrDigit))
            {
                errores.Add(new ErrorDeCampo
                {
                    Campo = "numeroDocumento",
                    Mensaje = "El pasaporte o el carné de extranjería tiene hasta 12 caracteres."
                });
            }
        }

        return errores;
    }

    public List<ErrorDeCampo> ValidarContacto()
    {
        var errores = new List<ErrorDeCampo>();

        if (string.IsNullOrWhiteSpace(Celular))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "celular",
                Mensaje = "El celular debe tener 9 dígitos y empezar con 9."
            });
        }
        else if (Celular.Length != 9 ||
                 !Celular.All(char.IsDigit) ||
                 !Celular.StartsWith("9", StringComparison.Ordinal))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "celular",
                Mensaje = "El celular debe tener 9 dígitos y empezar con 9."
            });
        }

        return errores;
    }

    public List<ErrorDeCampo> ValidarRuta()
    {
        var errores = new List<ErrorDeCampo>();

        if (string.IsNullOrWhiteSpace(Origen))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "origen",
                Mensaje = "Escriba el origen."
            });
        }
        else if (Origen.Length > DatosDelViaje.LongitudMaximaOrigenDestino)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "origen",
                Mensaje = $"El origen no puede superar los {DatosDelViaje.LongitudMaximaOrigenDestino} caracteres."
            });
        }

        if (string.IsNullOrWhiteSpace(Destino))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "destino",
                Mensaje = "Escriba el destino."
            });
        }
        else if (Destino.Length > DatosDelViaje.LongitudMaximaOrigenDestino)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "destino",
                Mensaje = $"El destino no puede superar los {DatosDelViaje.LongitudMaximaOrigenDestino} caracteres."
            });
        }

        if (string.IsNullOrWhiteSpace(LugarEmbarque))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "lugarEmbarque",
                Mensaje = "Escriba el lugar de embarque."
            });
        }
        else if (LugarEmbarque.Length > DatosDelViaje.LongitudMaximaLugarEmbarque)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "lugarEmbarque",
                Mensaje = $"El lugar de embarque no puede superar los {DatosDelViaje.LongitudMaximaLugarEmbarque} caracteres."
            });
        }

        return errores;
    }

    public List<ErrorDeCampo> ValidarMenor()
    {
        var errores = new List<ErrorDeCampo>();

        if (!EsMenor)
        {
            return errores;
        }

        if (string.IsNullOrWhiteSpace(NombrePadreMadre))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "nombrePadreMadre",
                Mensaje = "Escriba el nombre del padre o la madre."
            });
        }
        else if (NombrePadreMadre.Length > LongitudMaximaNombrePadreMadre)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "nombrePadreMadre",
                Mensaje = $"El nombre no puede superar los {LongitudMaximaNombrePadreMadre} caracteres."
            });
        }

        if (string.IsNullOrWhiteSpace(MotivoMenor))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "motivoMenor",
                Mensaje = "Escriba el motivo por el cual viaja sin el padre o la madre."
            });
        }
        else if (MotivoMenor.Length > LongitudMaximaMotivoMenor)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "motivoMenor",
                Mensaje = $"El motivo no puede superar los {LongitudMaximaMotivoMenor} caracteres."
            });
        }

        return errores;
    }

    public List<ErrorDeCampo> ValidarAsientoYVenta()
    {
        var errores = new List<ErrorDeCampo>();

        if (TipoRegistro == TipoDeRegistroDePasaje.Individual &&
            NumeroAsiento is null or <= 0)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "numeroAsiento",
                Mensaje = "Elija un asiento."
            });
        }

        if (Importe <= 0)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "importe",
                Mensaje = "No hay una tarifa vigente para ese sentido y fecha."
            });
        }

        return errores;
    }

    private void ActualizarEstadoPorPagos()
    {
        if (EstaPagadoCompleto)
        {
            Estado = EstadoPasaje.Pagado;
        }
        else if (Pagado > 0)
        {
            Estado = EstadoPasaje.ReservadoConAdelanto;
        }
    }

    public static Pasaje Reconstruir(PasajeGuardado guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return new Pasaje(
            guardado.Id,
            guardado.UnidadDeSalidaId,
            guardado.GrupoId,
            guardado.NumeroAsiento,
            guardado.NombrePasajero,
            guardado.TipoDocumento,
            guardado.NumeroDocumento,
            guardado.Celular,
            guardado.Origen,
            guardado.Destino,
            guardado.LugarEmbarque,
            guardado.FechaHoraSalida,
            guardado.FechaVenta,
            guardado.Importe,
            guardado.Estado,
            guardado.TipoRegistro,
            guardado.EmisorId,
            guardado.Emisor,
            guardado.EsMenor,
            guardado.NombrePadreMadre,
            guardado.MotivoMenor,
            guardado.EsResponsable,
            guardado.CodigoDeGrupo,
            guardado.MotivoDeAnulacion,
            guardado.FechaDeAnulacion,
            guardado.EsDiferido);
    }
}
