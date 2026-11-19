using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kataleya.Cliente;
using Kataleya.Contratos.Configuracion;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.Features.Pasajes;
using Kataleya.Escritorio.Servicios;
using Kataleya.Escritorio.Servicios.Impresion;
using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.RegistroDePasajes;

// IU-P03: reserva y venta (CDU-01, CDU-02), asiento (CDU-10) y grupo (CDU-11).
public partial class RegistroDePasajesViewModel : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string[] HorasPredeterminadas = ["06:00", "10:00", "13:00", "15:00", "20:00"];

    private readonly ClienteApi _cliente;
    private readonly IDialogos _dialogos;
    private readonly ServicioImpresion _impresion;
    private List<DatosDeTipoDeVehiculo> _tipos = [];
    private List<DatosDeUnidad> _unidades = [];
    private DatosDePasaje? _plantilla;
    private bool _cargado;
    private decimal _tarifa;

    public RegistroDePasajesViewModel(ClienteApi cliente, IDialogos dialogos, ServicioImpresion impresion)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
        _impresion = impresion ?? throw new ArgumentNullException(nameof(impresion));
    }

    public IReadOnlyList<string> TiposDeDocumento { get; } = ["DNI", "Pasaporte"];

    public IReadOnlyList<string> Medios { get; } = ["Efectivo", "Yape"];

    public IReadOnlyList<string> Origenes { get; } = ["Huancayo", "Ica"];

    public ObservableCollection<string> Horarios { get; } = [];

    public ObservableCollection<string> Unidades { get; } = [];

    public ObservableCollection<FilaDeAsientosVisible> FilasDeAsientos { get; } = [];

    public ObservableCollection<string> TiposDeVehiculo { get; } = [];

    public ObservableCollection<IntegranteEditable> Integrantes { get; } = [];

    public ObservableCollection<UnidadDelGrupoVisible> UnidadesDelGrupo { get; } = [];

    public string Emisor => _cliente.CuentaActual?.NombreUsuario ?? "—";

    public bool EsIndividual => !EsGrupo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsIndividual))]
    private bool esGrupo;

    [ObservableProperty]
    private string nombre = string.Empty;

    [ObservableProperty]
    private int tipoDeDocumento;

    [ObservableProperty]
    private string numeroDocumento = string.Empty;

    [ObservableProperty]
    private string telefono = string.Empty;

    [ObservableProperty]
    private bool menorSinPadres;

    [ObservableProperty]
    private string nombrePadre = string.Empty;

    [ObservableProperty]
    private string nombreMadre = string.Empty;

    [ObservableProperty]
    private string motivoViaje = string.Empty;

    [ObservableProperty]
    private int origenElegido;

    [ObservableProperty]
    private string destino = string.Empty;

    [ObservableProperty]
    private string lugarDeEmbarque = "Agencia";

    [ObservableProperty]
    private DateTime? fechaSalida = DateTime.Today;

    [ObservableProperty]
    private int horarioElegido = -1;

    [ObservableProperty]
    private string tarifaVigente = "—";

    [ObservableProperty]
    private int medioElegido;

    [ObservableProperty]
    private string monto = string.Empty;

    [ObservableProperty]
    private string pagado = "S/ 0.00";

    [ObservableProperty]
    private string saldo = "S/ 0.00";

    [ObservableProperty]
    private int unidadElegida = -1;

    [ObservableProperty]
    private short asientoElegido;

    [ObservableProperty]
    private string textoDelAsiento = "Asiento elegido: —";

    [ObservableProperty]
    private string nombreDeLaUnidad = string.Empty;

    [ObservableProperty]
    private string avisoDeUnidades = string.Empty;

    [ObservableProperty]
    private string origenDelGrupo = string.Empty;

    [ObservableProperty]
    private string destinoDelGrupo = string.Empty;

    [ObservableProperty]
    private string lugarDeRecojo = string.Empty;

    [ObservableProperty]
    private DateTime? fechaDelGrupo = DateTime.Today;

    [ObservableProperty]
    private TimeSpan? horaDelGrupo = new(7, 0, 0);

    [ObservableProperty]
    private string precioAcordado = string.Empty;

    [ObservableProperty]
    private int tipoDelGrupo;

    [ObservableProperty]
    private string capacidad = string.Empty;

    [ObservableProperty]
    private string montoDelGrupo = string.Empty;

    [ObservableProperty]
    private int medioDelGrupo;

    [ObservableProperty]
    private string errorNombre = string.Empty;

    [ObservableProperty]
    private string errorDocumento = string.Empty;

    [ObservableProperty]
    private string errorTelefono = string.Empty;

    [ObservableProperty]
    private string errorPadre = string.Empty;

    [ObservableProperty]
    private string errorMadre = string.Empty;

    [ObservableProperty]
    private string errorMotivo = string.Empty;

    [ObservableProperty]
    private string errorDestino = string.Empty;

    [ObservableProperty]
    private string errorEmbarque = string.Empty;

    [ObservableProperty]
    private string errorFecha = string.Empty;

    [ObservableProperty]
    private string errorAsiento = string.Empty;

    [ObservableProperty]
    private string errorMonto = string.Empty;

    [ObservableProperty]
    private string errorDelGrupo = string.Empty;

    [ObservableProperty]
    private string errorPrecio = string.Empty;

    [ObservableProperty]
    private string mensajeDeExito = string.Empty;

    private SentidoDelViaje Sentido => OrigenElegido == 1 ? SentidoDelViaje.Vuelta : SentidoDelViaje.Ida;

    public async Task CargarAsync()
    {
        OnPropertyChanged(nameof(Emisor));

        if (!_cargado)
        {
            var tipos = await _cliente.ObtenerTiposDeVehiculoAsync();
            _tipos = tipos.Datos ?? [];
            TiposDeVehiculo.Clear();
            foreach (var tipo in _tipos)
            {
                TiposDeVehiculo.Add($"{tipo.Nombre} · {tipo.NumeroAsientos} asientos");
            }

            var parametros = await _cliente.ObtenerParametrosAsync();
            var horas = parametros.Datos?
                .FirstOrDefault(parametro => parametro.Nombre == "horarios_salida")?.Valor
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? HorasPredeterminadas;

            Horarios.Clear();
            foreach (var hora in horas)
            {
                Horarios.Add(hora);
            }

            var sede = Origenes.ToList().IndexOf(_cliente.CuentaActual?.Sede ?? string.Empty);
            OrigenElegido = sede >= 0 ? sede : 0;
            HorarioElegido = Horarios.Count > 0 ? 0 : -1;
            TipoDelGrupo = _tipos.Count > 0 ? 0 : -1;
            ReiniciarGrupo();
            _cargado = true;
        }

        AplicarPlantilla();
        await ActualizarSalidaAsync();
    }

    // IU-P04 «Copiar datos para otro pasajero»: se copia el viaje; los datos del pasajero quedan vacíos.
    public void CopiarDatosDe(DatosDePasaje pasaje)
    {
        _plantilla = pasaje ?? throw new ArgumentNullException(nameof(pasaje));

        if (_cargado)
        {
            AplicarPlantilla();
            _ = ActualizarSalidaAsync();
        }
    }

    private void AplicarPlantilla()
    {
        if (_plantilla is null)
        {
            return;
        }

        var pasaje = _plantilla;
        _plantilla = null;
        EsGrupo = false;

        var origen = Origenes.ToList().IndexOf(pasaje.Origen);
        if (origen >= 0)
        {
            OrigenElegido = origen;
        }

        Destino = pasaje.Destino;
        LugarDeEmbarque = pasaje.LugarDeEmbarque;
        FechaSalida = pasaje.FechaSalida.ToDateTime(TimeOnly.MinValue);

        var hora = Horarios.IndexOf(pasaje.HoraSalida.ToString("HH:mm", Cultura));
        if (hora >= 0)
        {
            HorarioElegido = hora;
        }
    }

    partial void OnOrigenElegidoChanged(int value) =>
        _ = ActualizarSalidaAsync();

    partial void OnFechaSalidaChanged(DateTime? value) =>
        _ = ActualizarSalidaAsync();

    partial void OnHorarioElegidoChanged(int value) =>
        _ = ActualizarSalidaAsync();

    partial void OnUnidadElegidaChanged(int value) =>
        MostrarAsientos();

    partial void OnMontoChanged(string value) =>
        ActualizarCobro();

    partial void OnTipoDelGrupoChanged(int value)
    {
        if (UnidadesDelGrupo.Count <= 1)
        {
            ReiniciarUnidadesDelGrupo();
        }
    }

    private bool LeerSalida(out DateOnly fecha, out TimeOnly hora)
    {
        fecha = DateOnly.FromDateTime(FechaSalida ?? DateTime.Today);
        hora = default;

        return _cargado &&
               HorarioElegido >= 0 &&
               HorarioElegido < Horarios.Count &&
               TimeOnly.TryParse(Horarios[HorarioElegido], Cultura, out hora);
    }

    private async Task ActualizarSalidaAsync()
    {
        if (!LeerSalida(out var fecha, out var hora))
        {
            return;
        }

        var tarifa = await _cliente.ObtenerTarifaVigenteAsync(fecha, Sentido);
        _tarifa = tarifa.Exito && tarifa.Datos is not null ? tarifa.Datos.Importe : 0;
        TarifaVigente = _tarifa > 0
            ? $"{(tarifa.Datos!.Tipo == TipoDeTarifa.Programada ? tarifa.Datos.Nombre : "Regular")} · S/ {_tarifa.ToString("N2", Cultura)}"
            : "Sin tarifa vigente";
        ActualizarCobro();

        await CargarUnidadesAsync(fecha, hora);
    }

    private async Task CargarUnidadesAsync(DateOnly fecha, TimeOnly hora, long? elegir = null)
    {
        var resultado = await _cliente.ObtenerUnidadesAsync(fecha, hora, Sentido);
        var anterior = UnidadElegida >= 0 && UnidadElegida < _unidades.Count ? _unidades[UnidadElegida].Id : (long?)null;

        _unidades = resultado.Datos ?? [];
        Unidades.Clear();
        foreach (var unidad in _unidades)
        {
            Unidades.Add($"Unidad {unidad.Numero} · {unidad.TipoVehiculo.ToLowerInvariant()} de {unidad.NumeroAsientos}");
        }

        var buscada = elegir ?? anterior;
        var posicion = buscada is null ? -1 : _unidades.FindIndex(unidad => unidad.Id == buscada);
        UnidadElegida = posicion >= 0 ? posicion : _unidades.Count > 0 ? 0 : -1;

        AvisoDeUnidades = _unidades.Count > 0 &&
                          _unidades.All(unidad => unidad.AsientosOcupados.Count >= unidad.NumeroAsientos)
            ? "Las unidades de esta salida están llenas. Abra otra unidad para seguir vendiendo."
            : _unidades.Count == 0
                ? "No hay unidades abiertas para esta salida. Use «Abrir otra unidad»."
                : string.Empty;
        MostrarAsientos();
    }

    private void MostrarAsientos()
    {
        FilasDeAsientos.Clear();

        if (UnidadElegida < 0 || UnidadElegida >= _unidades.Count)
        {
            NombreDeLaUnidad = string.Empty;
            ElegirAsientoDe(0);
            return;
        }

        var unidad = _unidades[UnidadElegida];
        NombreDeLaUnidad = $"{unidad.TipoVehiculo} de {unidad.NumeroAsientos} asientos";

        if (unidad.AsientosOcupados.Contains(AsientoElegido) || AsientoElegido > unidad.NumeroAsientos)
        {
            AsientoElegido = 0;
        }

        AsientoVisible Asiento(short numero) =>
            AsientoVisible.Asiento(numero, unidad.AsientosOcupados.Contains(numero), numero == AsientoElegido);

        FilasDeAsientos.Add(FilaDeAsientosVisible.ConConductor(Asiento(1)));

        var numero = (short)2;
        var restantes = unidad.NumeroAsientos - 1;
        while (restantes > 0)
        {
            if (restantes >= 5 || restantes == 3)
            {
                FilasDeAsientos.Add(FilaDeAsientosVisible.DeTres(
                    Asiento(numero),
                    Asiento((short)(numero + 1)),
                    Asiento((short)(numero + 2))));
                numero += 3;
                restantes -= 3;
            }
            else if (restantes == 1)
            {
                FilasDeAsientos.Add(FilaDeAsientosVisible.DeUna(Asiento(numero)));
                numero += 1;
                restantes -= 1;
            }
            else
            {
                FilasDeAsientos.Add(FilaDeAsientosVisible.DeDos(
                    Asiento(numero),
                    Asiento((short)(numero + 1))));
                numero += 2;
                restantes -= 2;
            }
        }

        TextoDelAsiento = AsientoElegido > 0 ? $"Asiento elegido: {AsientoElegido}" : "Asiento elegido: —";
    }

    private void ElegirAsientoDe(short numero)
    {
        AsientoElegido = numero;
        TextoDelAsiento = numero > 0 ? $"Asiento elegido: {numero}" : "Asiento elegido: —";
    }

    [RelayCommand]
    private void ElegirAsiento(AsientoVisible? asiento)
    {
        if (asiento is null || !asiento.SePuedeElegir)
        {
            return;
        }

        ElegirAsientoDe(asiento.Numero);
        ErrorAsiento = string.Empty;
        MostrarAsientos();
    }

    private void ActualizarCobro()
    {
        var pagado = LeerImporte(Monto) ?? 0;
        Pagado = $"S/ {pagado.ToString("N2", Cultura)}";
        Saldo = $"S/ {Math.Max(0, _tarifa - pagado).ToString("N2", Cultura)}";
    }

    [RelayCommand]
    private void ElegirIndividual() =>
        EsGrupo = false;

    [RelayCommand]
    private void ElegirGrupo() =>
        EsGrupo = true;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AbrirUnidadAsync()
    {
        if (!LeerSalida(out var fecha, out var hora) || _tipos.Count == 0)
        {
            return;
        }

        var opciones = _tipos.Select(tipo => $"{tipo.Nombre} ({tipo.NumeroAsientos})").ToArray();
        var elegido = await Shell.Current.CurrentPage.DisplayActionSheetAsync(
            "Tipo de vehículo", "Cancelar", null, opciones);
        var indice = Array.IndexOf(opciones, elegido);

        if (indice < 0)
        {
            return;
        }

        var resultado = await _cliente.AbrirUnidadAsync(new SolicitudDeUnidad
        {
            TipoVehiculoId = _tipos[indice].Id,
            Fecha = fecha,
            Hora = hora,
            Sentido = Sentido
        });

        if (!resultado.Exito || resultado.Datos is null)
        {
            await _dialogos.MostrarMensajeAsync("No se pudo abrir la unidad", Detalle(resultado));
            return;
        }

        await CargarUnidadesAsync(fecha, hora, resultado.Datos.Id);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task GuardarReservaAsync() =>
        RegistrarIndividualAsync(esVenta: false);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task RegistrarVentaAsync() =>
        RegistrarIndividualAsync(esVenta: true);

    private async Task RegistrarIndividualAsync(bool esVenta)
    {
        MensajeDeExito = string.Empty;

        if (!ValidarIndividual(esVenta, out var importe) || !LeerSalida(out var fecha, out var hora))
        {
            return;
        }

        if (_tarifa <= 0)
        {
            await _dialogos.MostrarMensajeAsync(
                "VAL-01",
                $"No hay tarifa vigente para el {fecha.ToString("dd/MM/yyyy", Cultura)}. Un Administrador debe fijarla " +
                "en Configuración, en «Tarifas y parámetros».");
            return;
        }

        if (importe > _tarifa)
        {
            await _dialogos.MostrarMensajeAsync(
                "PAG-01",
                $"El monto (S/ {importe.ToString("N2", Cultura)}) es mayor que el importe " +
                $"(S/ {_tarifa.ToString("N2", Cultura)}). Corrija el monto.");
            return;
        }

        if (esVenta && importe < _tarifa)
        {
            await _dialogos.MostrarMensajeAsync(
                "PAG-02",
                $"Falta S/ {(_tarifa - importe).ToString("N2", Cultura)} para completar el importe. " +
                "Regístrelo como reserva o complete el pago.");
            return;
        }

        var unidad = _unidades[UnidadElegida];
        var solicitud = new SolicitudDePasaje
        {
            PasajeroNombre = Nombre.Trim(),
            TipoDocumento = TipoDeDocumento == 1 ? TipoDocumento.Pasaporte : TipoDocumento.DNI,
            NumeroDocumento = NumeroDocumento.Trim(),
            Telefono = Telefono.Replace(" ", string.Empty),
            Origen = Origenes[OrigenElegido],
            Destino = Destino.Trim(),
            LugarDeEmbarque = LugarDeEmbarque.Trim(),
            FechaSalida = fecha,
            HoraSalida = hora,
            UnidadSalidaId = unidad.Id,
            NumeroAsiento = AsientoElegido,
            Monto = importe,
            Medio = importe > 0 ? (MedioElegido == 1 ? MedioDePago.Yape : MedioDePago.Efectivo) : null,
            MenorSinPadres = MenorSinPadres,
            NombrePadre = MenorSinPadres ? NombrePadre.Trim() : null,
            NombreMadre = MenorSinPadres ? NombreMadre.Trim() : null,
            MotivoViaje = MenorSinPadres ? MotivoViaje.Trim() : null
        };

        var resultado = esVenta
            ? await _cliente.RegistrarVentaAsync(solicitud)
            : await _cliente.RegistrarReservaAsync(solicitud);

        if (!resultado.Exito || resultado.Datos is null)
        {
            await MostrarErrorDelRegistroAsync(resultado, fecha, hora);
            return;
        }

        var pasaje = resultado.Datos;

        if (pasaje.Boleto is null)
        {
            MensajeDeExito = "Reserva registrada.";
        }
        else
        {
            var impresion = await _impresion.ImprimirBoletoAsync(pasaje);
            MensajeDeExito = impresion == EstadoImpresora.Lista
                ? $"{(esVenta ? "Venta" : "Reserva")} registrada. Boleto N.º {pasaje.Boleto.Numero:D6} enviado a la impresora."
                : $"{(esVenta ? "Venta" : "Reserva")} registrada.";
            await AvisarFallaDeImpresionAsync(impresion);
        }

        LimpiarPasajero();
        await CargarUnidadesAsync(fecha, hora);
    }

    private bool ValidarIndividual(bool esVenta, out decimal importe)
    {
        var tipo = TipoDeDocumento == 1 ? TipoDocumento.Pasaporte : TipoDocumento.DNI;
        var leido = LeerImporte(Monto);
        importe = leido ?? 0;

        ErrorNombre = ValidacionDelPasajero.Nombre(Nombre);
        ErrorDocumento = ValidacionDelPasajero.Documento(tipo, NumeroDocumento);
        ErrorTelefono = ValidacionDelPasajero.Celular(Telefono);
        ErrorPadre = MenorSinPadres && string.IsNullOrWhiteSpace(NombrePadre) ? "Escriba el nombre del padre." : string.Empty;
        ErrorMadre = MenorSinPadres && string.IsNullOrWhiteSpace(NombreMadre) ? "Escriba el nombre de la madre." : string.Empty;
        ErrorMotivo = MenorSinPadres && string.IsNullOrWhiteSpace(MotivoViaje) ? "Describa el motivo del viaje." : string.Empty;
        ErrorDestino = string.IsNullOrWhiteSpace(Destino) ? "Escriba el destino." : string.Empty;
        ErrorEmbarque = string.IsNullOrWhiteSpace(LugarDeEmbarque) ? "Escriba el lugar de embarque." : string.Empty;
        ErrorAsiento = UnidadElegida < 0 || AsientoElegido <= 0 ? "Elija un asiento." : string.Empty;
        ErrorMonto = esVenta && importe <= 0
            ? "Escriba un monto mayor que cero."
            : leido is null && Monto.Trim().Length > 0
                ? "Escriba un monto mayor que cero."
                : string.Empty;
        ErrorFecha = ValidarFecha(FechaSalida);

        return new[]
        {
            ErrorNombre, ErrorDocumento, ErrorTelefono, ErrorPadre, ErrorMadre, ErrorMotivo,
            ErrorDestino, ErrorEmbarque, ErrorAsiento, ErrorMonto, ErrorFecha
        }.All(error => error.Length == 0);
    }

    // El plazo máximo lo revisa la Web API con el parámetro del negocio; aquí solo se evita una fecha pasada.
    private static string ValidarFecha(DateTime? fecha) =>
        (fecha ?? DateTime.Today).Date < DateTime.Today
            ? "La salida debe ser desde ahora hasta dentro de 7 días."
            : string.Empty;

    private async Task MostrarErrorDelRegistroAsync(ResultadoDeLaApi resultado, DateOnly fecha, TimeOnly hora)
    {
        var mostrado = false;

        foreach (var error in resultado.Errores)
        {
            switch (error.Campo)
            {
                case "numeroAsiento":
                    await _dialogos.MostrarMensajeAsync(
                        "VAL-02",
                        $"El asiento {AsientoElegido} ya está ocupado. Elija otro: el dibujo ya está actualizado.");
                    ElegirAsientoDe(0);
                    await CargarUnidadesAsync(fecha, hora);
                    return;
                case "pasajeroNombre":
                    ErrorNombre = error.Mensaje;
                    mostrado = true;
                    break;
                case "numeroDocumento":
                    ErrorDocumento = error.Mensaje;
                    mostrado = true;
                    break;
                case "fechaSalida":
                    ErrorFecha = error.Mensaje;
                    mostrado = true;
                    break;
                case "monto":
                    ErrorMonto = error.Mensaje;
                    mostrado = true;
                    break;
            }
        }

        if (!mostrado)
        {
            await _dialogos.MostrarMensajeAsync("No se registró el pasaje", Detalle(resultado));
        }
    }

    [RelayCommand]
    private void AgregarIntegrante()
    {
        Integrantes.Add(new IntegranteEditable(Integrantes.Count + 2));
        ActualizarCapacidad();
    }

    [RelayCommand]
    private void QuitarIntegrante(IntegranteEditable? integrante)
    {
        if (integrante is null)
        {
            return;
        }

        Integrantes.Remove(integrante);
        for (var i = 0; i < Integrantes.Count; i++)
        {
            Integrantes[i].Numero = i + 2;
        }

        ActualizarCapacidad();
    }

    [RelayCommand]
    private void AgregarUnidadAlGrupo()
    {
        if (TipoDelGrupo < 0 || TipoDelGrupo >= _tipos.Count)
        {
            return;
        }

        var tipo = _tipos[TipoDelGrupo];
        UnidadesDelGrupo.Add(new UnidadDelGrupoVisible(UnidadesDelGrupo.Count + 1, tipo.Id, tipo.Nombre, tipo.NumeroAsientos));
        ActualizarCapacidad();
    }

    private void ReiniciarGrupo()
    {
        Integrantes.Clear();
        Integrantes.Add(new IntegranteEditable(2));
        ReiniciarUnidadesDelGrupo();
    }

    private void ReiniciarUnidadesDelGrupo()
    {
        UnidadesDelGrupo.Clear();
        AgregarUnidadAlGrupo();
    }

    private void ActualizarCapacidad() =>
        Capacidad = $"Capacidad total: {UnidadesDelGrupo.Sum(unidad => unidad.Asientos)} · " +
                    $"Integrantes: {Integrantes.Count + 1}";

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RegistrarGrupoAsync()
    {
        MensajeDeExito = string.Empty;
        var tipo = TipoDeDocumento == 1 ? TipoDocumento.Pasaporte : TipoDocumento.DNI;
        var precio = LeerImporte(PrecioAcordado) ?? 0;
        var monto = LeerImporte(MontoDelGrupo) ?? 0;

        ErrorNombre = ValidacionDelPasajero.Nombre(Nombre);
        ErrorDocumento = ValidacionDelPasajero.Documento(tipo, NumeroDocumento);
        ErrorTelefono = ValidacionDelPasajero.Celular(Telefono);
        ErrorPrecio = precio <= 0 ? "Escriba un precio mayor que cero." : string.Empty;
        ErrorFecha = ValidarFecha(FechaDelGrupo);
        ErrorDelGrupo = string.IsNullOrWhiteSpace(OrigenDelGrupo) ||
                        string.IsNullOrWhiteSpace(DestinoDelGrupo) ||
                        string.IsNullOrWhiteSpace(LugarDeRecojo)
            ? "Complete el origen, el destino y el lugar de recojo."
            : string.Empty;

        foreach (var integrante in Integrantes)
        {
            var documento = ValidacionDelPasajero.Documento(
                integrante.TipoDeDocumento == 1 ? TipoDocumento.Pasaporte : TipoDocumento.DNI,
                integrante.NumeroDocumento);
            integrante.Error = string.IsNullOrWhiteSpace(integrante.Nombre) ? "Escriba el nombre." : documento;
        }

        if (new[] { ErrorNombre, ErrorDocumento, ErrorTelefono, ErrorPrecio, ErrorFecha, ErrorDelGrupo }
                .Any(error => error.Length > 0) ||
            Integrantes.Any(integrante => integrante.Error.Length > 0))
        {
            return;
        }

        if (Integrantes.Count + 1 < 2)
        {
            await _dialogos.MostrarMensajeAsync(
                "VAL-03",
                "Un grupo tiene al menos dos integrantes. Agregue otro o regístrelo como pasaje individual.");
            return;
        }

        var asientos = UnidadesDelGrupo.Sum(unidad => unidad.Asientos);
        if (Integrantes.Count + 1 > asientos)
        {
            await _dialogos.MostrarMensajeAsync(
                "VAL-04",
                $"Hay {Integrantes.Count + 1} integrantes y {asientos} asientos. " +
                "Agregue otra unidad o elija un vehículo más grande.");
            return;
        }

        if (monto > precio)
        {
            await _dialogos.MostrarMensajeAsync(
                "PAG-01",
                $"El monto (S/ {monto.ToString("N2", Cultura)}) es mayor que el precio acordado " +
                $"(S/ {precio.ToString("N2", Cultura)}). Corrija el monto.");
            return;
        }

        var integrantes = new List<IntegranteDeGrupo>
        {
            new()
            {
                Nombre = Nombre.Trim(),
                TipoDocumento = tipo,
                NumeroDocumento = NumeroDocumento.Trim(),
                EsResponsable = true
            }
        };
        integrantes.AddRange(Integrantes.Select(integrante => new IntegranteDeGrupo
        {
            Nombre = integrante.Nombre.Trim(),
            TipoDocumento = integrante.TipoDeDocumento == 1 ? TipoDocumento.Pasaporte : TipoDocumento.DNI,
            NumeroDocumento = integrante.NumeroDocumento.Trim()
        }));

        var hora = HoraDelGrupo ?? TimeSpan.Zero;
        var solicitud = new SolicitudDeGrupo
        {
            Telefono = Telefono.Replace(" ", string.Empty),
            PrecioAcordado = precio,
            Origen = OrigenDelGrupo.Trim(),
            Destino = DestinoDelGrupo.Trim(),
            LugarDeRecojo = LugarDeRecojo.Trim(),
            FechaSalida = DateOnly.FromDateTime(FechaDelGrupo ?? DateTime.Today),
            HoraSalida = new TimeOnly(hora.Hours, hora.Minutes),
            Integrantes = integrantes,
            Unidades = UnidadesDelGrupo.Select(unidad => new UnidadDelGrupo { TipoVehiculoId = unidad.TipoVehiculoId }).ToList(),
            Monto = monto,
            Medio = monto > 0 ? (MedioDelGrupo == 1 ? MedioDePago.Yape : MedioDePago.Efectivo) : null
        };

        // Si el cobro cubre el precio acordado, es una venta y se emite un boleto por integrante.
        var resultado = monto >= precio
            ? await _cliente.RegistrarGrupoVentaAsync(solicitud)
            : await _cliente.RegistrarGrupoReservaAsync(solicitud);

        if (!resultado.Exito || resultado.Datos is null)
        {
            await _dialogos.MostrarMensajeAsync("No se registró el grupo", Detalle(resultado));
            return;
        }

        var grupo = resultado.Datos;
        var registrado = $"Grupo {grupo.Codigo} registrado: {grupo.Pasajes.Count} pasajes";

        if (grupo.Pasajes.All(pasaje => pasaje.Boleto is null))
        {
            MensajeDeExito = $"{registrado}.";
        }
        else
        {
            var impresion = await _impresion.ImprimirBoletosAsync(grupo.Pasajes);
            MensajeDeExito = impresion == EstadoImpresora.Lista
                ? $"{registrado}; boletos enviados a la impresora."
                : $"{registrado}.";
            await AvisarFallaDeImpresionAsync(impresion);
        }

        LimpiarPasajero();
        PrecioAcordado = string.Empty;
        MontoDelGrupo = string.Empty;
        ReiniciarGrupo();
    }

    [RelayCommand]
    private Task CancelarAsync() =>
        NavegacionDeModulos.IrAsync("panel");

    private Task AvisarFallaDeImpresionAsync(EstadoImpresora impresion) =>
        impresion switch
        {
            EstadoImpresora.Lista => Task.CompletedTask,
            EstadoImpresora.SinPapel => _dialogos.MostrarMensajeAsync(
                "IMP-02",
                "La venta quedó registrada, pero la impresora no tiene papel. " +
                "Cambie el rollo e imprima el boleto desde el detalle del pasaje."),
            _ => _dialogos.MostrarMensajeAsync(
                "IMP-01",
                "La venta quedó registrada, pero la impresora no responde. " +
                "Revise que esté encendida y conectada, e imprima el boleto desde el detalle del pasaje.")
        };

    private void LimpiarPasajero()
    {
        Nombre = string.Empty;
        NumeroDocumento = string.Empty;
        Telefono = string.Empty;
        MenorSinPadres = false;
        NombrePadre = string.Empty;
        NombreMadre = string.Empty;
        MotivoViaje = string.Empty;
        Monto = string.Empty;
        ElegirAsientoDe(0);
    }

    private static decimal? LeerImporte(string? texto)
    {
        var limpio = (texto ?? string.Empty).Replace("S/", string.Empty).Replace(',', '.').Trim();

        return decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out var importe) && importe >= 0
            ? importe
            : null;
    }

    private static string Detalle(ResultadoDeLaApi resultado) =>
        resultado.SinPermiso
            ? "No tiene permiso para esta operación."
            : resultado.Errores.Count > 0
                ? string.Join(Environment.NewLine, resultado.Errores.Select(error => error.Mensaje))
                : "No se pudo completar la operación.";
}
