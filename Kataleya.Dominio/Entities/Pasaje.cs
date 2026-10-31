using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Entities;

public sealed class Pasaje
{
    public const int LongitudMaximaNombrePasajero = 100;
    public const int LongitudMaximaNumeroDocumento = 20;
    public const int LongitudMaximaCelular = 12;
    public const int LongitudMaximaOrigenDestino = 50;
    public const int LongitudMaximaLugarEmbarque = 50;
    public const int LongitudMaximaNombrePadreMadre = 100;
    public const int LongitudMaximaMotivoMenor = 200;

    public Pasaje(
        SolicitudDeRegistroPasaje solicitud,
        int numeroAsiento,
        Usuario vendidaPor)
        : this(
            0,
            solicitud.UnidadSalidaId,
            numeroAsiento,
            (solicitud ?? throw new ArgumentNullException(nameof(solicitud))).NombrePasajero,
            solicitud.TipoDocumento,
            solicitud.NumeroDocumento,
            solicitud.Celular,
            solicitud.Origen,
            solicitud.Destino,
            solicitud.LugarEmbarque,
            solicitud.FechaHoraSalida,
            solicitud.Adelanto,
            solicitud.EsMenor,
            solicitud.NombrePadreMadre,
            solicitud.MotivoMenor,
            (vendidaPor ?? throw new ArgumentNullException(nameof(vendidaPor))).Id,
            vendidaPor.Nombre)
    {
    }

    internal Pasaje(
        long id,
        long unidadSalidaId,
        int numeroAsiento,
        string nombrePasajero,
        TipoDocumento tipoDocumento,
        string numeroDocumento,
        string celular,
        string origen,
        string destino,
        string lugarEmbarque,
        DateTime fechaHoraSalida,
        decimal adelanto,
        bool esMenor,
        string? nombrePadreMadre,
        string? motivoMenor,
        int idUsuario,
        string vendidaPor)
    {
        Id = id;
        UnidadSalidaId = unidadSalidaId;
        NumeroAsiento = numeroAsiento;
        NombrePasajero = nombrePasajero.Trim();
        TipoDocumento = tipoDocumento;
        NumeroDocumento = numeroDocumento.Trim();
        Celular = celular.Trim();
        Origen = origen.Trim();
        Destino = destino.Trim();
        LugarEmbarque = lugarEmbarque.Trim();
        FechaHoraSalida = fechaHoraSalida;
        Adelanto = adelanto;
        EsMenor = esMenor;
        NombrePadreMadre = nombrePadreMadre is not null ? nombrePadreMadre.Trim() : null;
        MotivoMenor = motivoMenor is not null ? motivoMenor.Trim() : null;
        IdUsuario = idUsuario;
        VendidaPor = vendidaPor;
        Estado = EstadoPasaje.Activo.ToString();
        FechaVenta = DateTime.UtcNow;
    }

    public long Id { get; }
    public long UnidadSalidaId { get; }
    public int NumeroAsiento { get; }
    public string NombrePasajero { get; }
    public TipoDocumento TipoDocumento { get; }
    public string NumeroDocumento { get; }
    public string Celular { get; }
    public string Origen { get; }
    public string Destino { get; }
    public string LugarEmbarque { get; }
    public DateTime FechaHoraSalida { get; }
    public DateTime FechaVenta { get; }
    public decimal Adelanto { get; }
    public string Estado { get; }
    public int IdUsuario { get; }
    public string VendidaPor { get; }
    public bool EsMenor { get; }
    public string? NombrePadreMadre { get; }
    public string? MotivoMenor { get; }

    public List<ErrorDeCampo> ValidarNombreYDocumento()
    {
        var errores = new List<ErrorDeCampo>();
        if (string.IsNullOrWhiteSpace(NombrePasajero))
        {
            errores.Add(new ErrorDeCampo { Campo = "nombrePasajero", Mensaje = "Escriba el nombre del pasajero." });
        }
        else if (NombrePasajero.Length > LongitudMaximaNombrePasajero)
        {
            errores.Add(new ErrorDeCampo { Campo = "nombrePasajero", Mensaje = $"El nombre no puede superar los {LongitudMaximaNombrePasajero} caracteres." });
        }
        if (TipoDocumento == TipoDocumento.DNI)
        {
            var digitos = new string(NumeroDocumento.Where(char.IsDigit).ToArray());
            if (digitos.Length != 8)
            {
                errores.Add(new ErrorDeCampo { Campo = "numeroDocumento", Mensaje = "El DNI debe tener 8 dígitos." });
            }
        }
        else if (!string.IsNullOrWhiteSpace(NumeroDocumento) && NumeroDocumento.Length > 12)
        {
            errores.Add(new ErrorDeCampo { Campo = "numeroDocumento", Mensaje = "El pasaporte o el carné de extranjería tiene hasta 12 caracteres." });
        }
        return errores;
    }

    public List<ErrorDeCampo> ValidarContactoYRuta()
    {
        var errores = new List<ErrorDeCampo>();
        if (string.IsNullOrWhiteSpace(Celular))
        {
            errores.Add(new ErrorDeCampo { Campo = "celular", Mensaje = "El celular debe tener 9 dígitos y empezar con 9." });
        }
        else
        {
            var digitos = new string(Celular.Where(char.IsDigit).ToArray());
            if (digitos.Length != 9 || !digitos.StartsWith("9"))
            {
                errores.Add(new ErrorDeCampo { Campo = "celular", Mensaje = "El celular debe tener 9 dígitos y empezar con 9." });
            }
        }
        if (string.IsNullOrWhiteSpace(Origen))
        {
            errores.Add(new ErrorDeCampo { Campo = "origen", Mensaje = "Escriba el origen." });
        }
        else if (Origen.Length > LongitudMaximaOrigenDestino)
        {
            errores.Add(new ErrorDeCampo { Campo = "origen", Mensaje = $"El origen no puede superar los {LongitudMaximaOrigenDestino} caracteres." });
        }
        if (string.IsNullOrWhiteSpace(Destino))
        {
            errores.Add(new ErrorDeCampo { Campo = "destino", Mensaje = "Escriba el destino." });
        }
        else if (Destino.Length > LongitudMaximaOrigenDestino)
        {
            errores.Add(new ErrorDeCampo { Campo = "destino", Mensaje = $"El destino no puede superar los {LongitudMaximaOrigenDestino} caracteres." });
        }
        if (string.IsNullOrWhiteSpace(LugarEmbarque))
        {
            errores.Add(new ErrorDeCampo { Campo = "lugarEmbarque", Mensaje = "Escriba el lugar de embarque." });
        }
        else if (LugarEmbarque.Length > LongitudMaximaLugarEmbarque)
        {
            errores.Add(new ErrorDeCampo { Campo = "lugarEmbarque", Mensaje = $"El lugar de embarque no puede superar los {LongitudMaximaLugarEmbarque} caracteres." });
        }
        return errores;
    }

    public List<ErrorDeCampo> ValidarFechaYAsiento()
    {
        var errores = new List<ErrorDeCampo>();
        var ahora = DateTime.UtcNow;
        var sieteDias = ahora.AddDays(7);
        if (FechaHoraSalida <= ahora)
        {
            errores.Add(new ErrorDeCampo { Campo = "fechaHoraSalida", Mensaje = "La salida debe ser desde ahora hasta dentro de 7 días." });
        }
        else if (FechaHoraSalida > sieteDias)
        {
            errores.Add(new ErrorDeCampo { Campo = "fechaHoraSalida", Mensaje = "La salida debe ser desde ahora hasta dentro de 7 días." });
        }
        if (NumeroAsiento <= 0)
        {
            errores.Add(new ErrorDeCampo { Campo = "numeroAsiento", Mensaje = "Elija un asiento." });
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
            errores.Add(new ErrorDeCampo { Campo = "nombrePadreMadre", Mensaje = "Escriba el nombre del padre o la madre." });
        }
        else if (NombrePadreMadre.Length > LongitudMaximaNombrePadreMadre)
        {
            errores.Add(new ErrorDeCampo { Campo = "nombrePadreMadre", Mensaje = $"El nombre no puede superar los {LongitudMaximaNombrePadreMadre} caracteres." });
        }
        if (string.IsNullOrWhiteSpace(MotivoMenor))
        {
            errores.Add(new ErrorDeCampo { Campo = "motivoMenor", Mensaje = "Escriba el motivo por el cual viaja sin el padre o la madre." });
        }
        else if (MotivoMenor.Length > LongitudMaximaMotivoMenor)
        {
            errores.Add(new ErrorDeCampo { Campo = "motivoMenor", Mensaje = $"El motivo no puede superar los {LongitudMaximaMotivoMenor} caracteres." });
        }
        return errores;
    }

    public List<ErrorDeCampo> ValidarAdelanto()
    {
        var errores = new List<ErrorDeCampo>();
        if (Adelanto < 0)
        {
            errores.Add(new ErrorDeCampo { Campo = "adelanto", Mensaje = "El adelanto no puede ser negativo." });
        }
        return errores;
    }

    public static Pasaje Reconstruir(PasajeGuardado guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        return new Pasaje(
            guardado.Id,
            guardado.UnidadSalidaId,
            guardado.NumeroAsiento,
            guardado.NombrePasajero,
            guardado.TipoDocumento,
            guardado.NumeroDocumento,
            guardado.Celular,
            guardado.Origen,
            guardado.Destino,
            guardado.LugarEmbarque,
            guardado.FechaHoraSalida,
            guardado.Adelanto,
            guardado.EsMenor,
            guardado.NombrePadreMadre,
            guardado.MotivoMenor,
            guardado.UsuarioId,
            guardado.VendidaPor);
    }
}