using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.ValueObjects;

namespace Kataleya.Dominio.Entities;

public sealed class Grupo
{
    public const int MinimoDeIntegrantes = 2;
    public const int LongitudMaximaTelefono = 12;

    private readonly List<UnidadDeSalida> _unidades = [];
    private readonly List<Pasaje> _pasajes = [];
    private readonly List<Pago> _pagos = [];

    public Grupo(
        string codigo,
        string telefonoContacto,
        decimal precioAcordado,
        DatosDelViaje viaje,
        Usuario creadoPor,
        DateTime fechaRegistro,
        Guid? claveDeOperacion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        Codigo = codigo.Trim();
        TelefonoContacto = telefonoContacto?.Trim() ?? string.Empty;
        PrecioAcordado = precioAcordado;
        Viaje = (viaje ?? throw new ArgumentNullException(nameof(viaje))).Normalizar();
        CreadoPorId = (creadoPor ?? throw new ArgumentNullException(nameof(creadoPor))).Id;
        CreadoPor = creadoPor.Nombre;
        FechaRegistro = fechaRegistro;
        ClaveDeOperacion = claveDeOperacion;
    }

    public long Id { get; private set; }

    public string Codigo { get; }

    public string TelefonoContacto { get; }

    public decimal PrecioAcordado { get; }

    public DatosDelViaje Viaje { get; }

    public string Origen => Viaje.Origen;

    public string Destino => Viaje.Destino;

    public string LugarDeRecojo => Viaje.LugarEmbarque;

    public DateTime FechaHoraSalida => Viaje.FechaHoraSalida;

    public DateTime FechaRegistro { get; }

    public int CreadoPorId { get; }

    public string CreadoPor { get; }

    public Guid? ClaveDeOperacion { get; }

    public IReadOnlyList<UnidadDeSalida> Unidades => _unidades;

    public IReadOnlyList<Pasaje> Pasajes => _pasajes;

    public IReadOnlyList<Pago> Pagos => _pagos;

    public string? Responsable =>
        _pasajes.FirstOrDefault(pasaje => pasaje.EsResponsable)?.NombrePasajero;

    public decimal Pagado => _pagos.Sum(pago =>
        pago.Tipo == TipoDePago.Devolucion ? -pago.Monto : pago.Monto);

    public decimal Saldo => Math.Max(0, PrecioAcordado - Pagado);

    public EstadoPasaje Estado =>
        Pagado >= PrecioAcordado
            ? EstadoPasaje.Pagado
            : Pagado > 0
                ? EstadoPasaje.ReservadoConAdelanto
                : EstadoPasaje.Reservado;

    public void AsignarId(long id)
    {
        if (Id != 0)
        {
            throw new InvalidOperationException("El grupo ya tiene identificador.");
        }

        Id = id;
    }

    public void AgregarUnidad(UnidadDeSalida unidad)
    {
        ArgumentNullException.ThrowIfNull(unidad);
        _unidades.Add(unidad);
    }

    public void AgregarPasaje(Pasaje pasaje)
    {
        ArgumentNullException.ThrowIfNull(pasaje);
        _pasajes.Add(pasaje);
    }

    public void AgregarPago(Pago pago)
    {
        ArgumentNullException.ThrowIfNull(pago);

        if (pago.Id != 0 && _pagos.Any(existente => existente.Id == pago.Id))
        {
            return;
        }

        _pagos.Add(pago);

        foreach (var pasaje in _pasajes)
        {
            pasaje.AgregarPago(pago);
        }
    }

    public List<ErrorDeCampo> Validar()
    {
        var errores = new List<ErrorDeCampo>();

        if (PrecioAcordado <= 0)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "precioAcordado",
                Mensaje = "El precio acordado debe ser mayor que cero."
            });
        }
        else if (decimal.Round(PrecioAcordado, 2) != PrecioAcordado)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "precioAcordado",
                Mensaje = "El precio acordado solo puede tener dos decimales."
            });
        }

        if (string.IsNullOrWhiteSpace(TelefonoContacto))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "telefonoContacto",
                Mensaje = "El celular debe tener 9 dígitos y empezar con 9."
            });
        }
        else if (TelefonoContacto.Length != 9 ||
                 !TelefonoContacto.All(char.IsDigit) ||
                 !TelefonoContacto.StartsWith("9", StringComparison.Ordinal))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "telefonoContacto",
                Mensaje = "El celular debe tener 9 dígitos y empezar con 9."
            });
        }

        AgregarErroresDelViaje(errores);

        if (_pasajes.Count < MinimoDeIntegrantes)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "integrantes",
                Mensaje = "Un grupo tiene al menos dos integrantes."
            });
        }

        if (_pasajes.Count(pasaje => pasaje.EsResponsable) != 1)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "responsable",
                Mensaje = "Marque un único responsable del grupo."
            });
        }

        if (_unidades.Count == 0)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "tiposDeVehiculo",
                Mensaje = "Elija al menos una unidad para el grupo."
            });
        }

        return errores;
    }

    private void AgregarErroresDelViaje(List<ErrorDeCampo> errores)
    {
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

        if (string.IsNullOrWhiteSpace(LugarDeRecojo))
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "lugarDeRecojo",
                Mensaje = "Escriba el lugar de recojo."
            });
        }
        else if (LugarDeRecojo.Length > DatosDelViaje.LongitudMaximaLugarEmbarque)
        {
            errores.Add(new ErrorDeCampo
            {
                Campo = "lugarDeRecojo",
                Mensaje = $"El lugar de recojo no puede superar los {DatosDelViaje.LongitudMaximaLugarEmbarque} caracteres."
            });
        }
    }

    public static Grupo Reconstruir(GrupoGuardado guardado)
    {
        ArgumentNullException.ThrowIfNull(guardado);

        var grupo = new Grupo(
            guardado.Codigo,
            guardado.TelefonoContacto,
            guardado.PrecioAcordado,
            new DatosDelViaje(
                guardado.Origen,
                guardado.Destino,
                guardado.LugarDeRecojo,
                guardado.FechaHoraSalida),
            new Usuario(
                guardado.CreadoPorId,
                guardado.CreadoPor,
                guardado.CreadoPor,
                string.Empty,
                string.Empty,
                EstadoCuenta.Activa,
                0,
                null,
                new Rol(0, "Desconocido"),
                new Sede(0, "Desconocida")),
            guardado.FechaRegistro,
            guardado.ClaveDeOperacion);

        grupo.AsignarId(guardado.Id);
        return grupo;
    }
}
