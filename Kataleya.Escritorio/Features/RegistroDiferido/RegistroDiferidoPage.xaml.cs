using System.Globalization;
using Kataleya.Contratos.Configuracion;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.Components.Common;
using Kataleya.Escritorio.UIHelpers.Paginas;

namespace Kataleya.Escritorio.Features.RegistroDiferido;

public partial class RegistroDiferidoPage : PaginaDeModulo
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string[] HorasPredeterminadas = ["06:00", "10:00", "13:00", "15:00", "20:00"];
    private static readonly string[] FormatosDeEmision =
    [
        "dd/MM/yyyy HH:mm",
        "d/M/yyyy H:mm",
        "yyyy-MM-dd HH:mm",
        "dd/MM/yyyy H:mm",
        "d/M/yyyy HH:mm"
    ];

    private readonly List<DatosDeUnidad> _unidades = [];
    private List<DatosDeTipoDeVehiculo> _tipos = [];
    private HashSet<short> _asientosOcupados = [];
    private bool _cargado;
    private decimal _tarifaActual;
    private SentidoDelViaje? _sentidoDeLasUnidades;

    public RegistroDiferidoPage()
    {
        OpcionActiva = "registro-diferido";
        InitializeComponent();

        FechaSalida.Date = DateTime.Today.AddDays(1);
        SelectorTipoDocumento.SelectedIndex = 0;
        SelectorMedio.SelectedIndex = 0;
        CampoOrigen.Texto = "Ica";
        CampoHoraEmision.Texto = DateTime.Now.ToString("dd/MM/yyyy HH:mm", Cultura);

        FechaSalida.DateSelected += async (_, _) =>
        {
            await CargarUnidadesAsync();
            await ActualizarTarifaAsync();
        };
        SelectorHora.SelectedIndexChanged += async (_, _) => await CargarUnidadesAsync();
        SelectorUnidad.SelectedIndexChanged += async (_, _) => await CargarAsientosAsync();
        CampoOrigen.PropertyChanged += async (_, argumentos) =>
        {
            if (argumentos.PropertyName == nameof(CampoDeTexto.Texto) &&
                SentidoElegido != _sentidoDeLasUnidades)
            {
                await CargarUnidadesAsync();
                await ActualizarTarifaAsync();
            }
        };
    }

    private SentidoDelViaje? SentidoElegido => Ruta.SentidoSegunElOrigen(CampoOrigen.Texto);

    protected override async void AlAparecer()
    {
        if (_cargado)
        {
            return;
        }

        _cargado = true;
        await CargarCatalogosAsync();
    }

    private async Task CargarCatalogosAsync()
    {
        var tipos = await Cliente.ObtenerTiposDeVehiculoAsync();
        if (tipos.Exito && tipos.Datos is not null)
        {
            _tipos = tipos.Datos;
        }

        var parametros = await Cliente.ObtenerParametrosAsync();
        var horas = HorasPredeterminadas;

        if (parametros.Exito && parametros.Datos is not null)
        {
            var parametro = parametros.Datos.FirstOrDefault(p =>
                p.Nombre.Equals("horarios_salida", StringComparison.OrdinalIgnoreCase));

            if (parametro is not null)
            {
                var separadas = parametro.Valor
                    .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(valor => TimeOnly.TryParse(valor, Cultura, out var hora) ? hora.ToString("HH:mm") : null)
                    .Where(valor => valor is not null)
                    .Cast<string>()
                    .ToArray();

                if (separadas.Length > 0)
                {
                    horas = separadas;
                }
            }
        }

        SelectorHora.ItemsSource = horas.ToList();
        SelectorHora.SelectedIndex = 0;

        await CargarUnidadesAsync();
        await ActualizarTarifaAsync();
    }

    private async Task CargarUnidadesAsync()
    {
        if (!_cargado || SelectorHora.SelectedItem is null)
        {
            return;
        }

        var fecha = DateOnly.FromDateTime(FechaSalida.Date ?? DateTime.Today);
        if (!TimeOnly.TryParse(SelectorHora.SelectedItem.ToString(), Cultura, out var hora))
        {
            return;
        }

        _sentidoDeLasUnidades = SentidoElegido;
        _unidades.Clear();

        if (_sentidoDeLasUnidades is SentidoDelViaje sentido)
        {
            var resultado = await Cliente.ObtenerUnidadesAsync(fecha, hora, sentido);

            if (resultado.Exito && resultado.Datos is not null)
            {
                _unidades.AddRange(resultado.Datos);
            }
        }

        SelectorUnidad.ItemsSource = _unidades
            .Select(unidad =>
                $"{unidad.Numero} · {unidad.TipoVehiculo} · {unidad.NumeroAsientos} asientos")
            .ToList();

        if (_unidades.Count > 0)
        {
            SelectorUnidad.SelectedIndex = 0;
        }
        else
        {
            _asientosOcupados.Clear();
            CampoAsiento.Texto = string.Empty;
        }
    }

    private async Task CargarAsientosAsync()
    {
        if (SelectorUnidad.SelectedIndex < 0 || SelectorUnidad.SelectedIndex >= _unidades.Count)
        {
            _asientosOcupados = [];
            return;
        }

        var unidad = _unidades[SelectorUnidad.SelectedIndex];
        var resultado = await Cliente.ObtenerAsientosOcupadosAsync(unidad.Id);

        _asientosOcupados = resultado.Exito && resultado.Datos is not null
            ? resultado.Datos.ToHashSet()
            : [];
    }

    private async void AlAbrirUnidad(object? remitente, EventArgs argumentos)
    {
        if (_tipos.Count == 0)
        {
            await DisplayAlertAsync("Sin tipos", "No hay tipos de vehículo configurados.", "Aceptar");
            return;
        }

        if (!TimeOnly.TryParse(SelectorHora.SelectedItem?.ToString(), Cultura, out var hora))
        {
            await DisplayAlertAsync("Sin horario", "Seleccione un horario de salida.", "Aceptar");
            return;
        }

        if (SentidoElegido is not SentidoDelViaje sentido)
        {
            await DisplayAlertAsync("Falta el origen", "Escriba como origen Huancayo o Ica.", "Aceptar");
            return;
        }

        var opciones = _tipos.Select(tipo => $"{tipo.Nombre} ({tipo.NumeroAsientos})").ToArray();
        var elegido = await DisplayActionSheetAsync("Tipo de vehículo", "Cancelar", null, opciones);

        if (elegido is null || elegido == "Cancelar")
        {
            return;
        }

        var indice = Array.IndexOf(opciones, elegido);
        if (indice < 0)
        {
            return;
        }

        BotonAbrirUnidad.IsEnabled = false;

        try
        {
            var resultado = await Cliente.AbrirUnidadAsync(new SolicitudDeUnidad
            {
                TipoVehiculoId = _tipos[indice].Id,
                Fecha = DateOnly.FromDateTime(FechaSalida.Date ?? DateTime.Today),
                Hora = hora,
                Sentido = sentido
            });

            if (!resultado.Exito)
            {
                await MostrarErrorAsync(resultado, "No se pudo abrir la unidad.");
                return;
            }

            await CargarUnidadesAsync();

            if (resultado.Datos is not null)
            {
                var posicion = _unidades.FindIndex(unidad => unidad.Id == resultado.Datos.Id);
                if (posicion >= 0)
                {
                    SelectorUnidad.SelectedIndex = posicion;
                }
            }
        }
        finally
        {
            BotonAbrirUnidad.IsEnabled = true;
        }
    }

    private async Task ActualizarTarifaAsync()
    {
        var fecha = DateOnly.FromDateTime(FechaSalida.Date ?? DateTime.Today);

        if (SentidoElegido is not SentidoDelViaje sentido)
        {
            _tarifaActual = 0;
            LblImporte.Text = "Sin tarifa vigente";
            return;
        }

        var resultado = await Cliente.ObtenerTarifaVigenteAsync(fecha, sentido);

        if (resultado.Exito && resultado.Datos is not null)
        {
            _tarifaActual = resultado.Datos.Importe;
            LblImporte.Text = $"S/ {_tarifaActual:N2}";
            return;
        }

        _tarifaActual = 0;
        LblImporte.Text = "Sin tarifa vigente";
    }

    private async void AlRegistrar(object? remitente, EventArgs argumentos)
    {
        BotonRegistrar.IsEnabled = false;

        try
        {
            await RegistrarAsync();
        }
        finally
        {
            BotonRegistrar.IsEnabled = true;
        }
    }

    private async Task RegistrarAsync()
    {
        if (string.IsNullOrWhiteSpace(CampoTalonario.Texto) ||
            string.IsNullOrWhiteSpace(CampoNombre.Texto) ||
            string.IsNullOrWhiteSpace(CampoDocumento.Texto) ||
            string.IsNullOrWhiteSpace(CampoOrigen.Texto) ||
            string.IsNullOrWhiteSpace(CampoDestino.Texto) ||
            string.IsNullOrWhiteSpace(CampoEmbarque.Texto))
        {
            await DisplayAlertAsync(
                "Datos incompletos",
                "Complete el talonario, nombre, documento, origen, destino y lugar de embarque.",
                "Aceptar");
            return;
        }

        if (!DateTime.TryParseExact(
                CampoHoraEmision.Texto.Trim(),
                FormatosDeEmision,
                Cultura,
                DateTimeStyles.AssumeLocal,
                out var emision))
        {
            await DisplayAlertAsync(
                "Emisión inválida",
                "Escriba la fecha y hora de emisión con el formato dd/MM/yyyy HH:mm.",
                "Aceptar");
            return;
        }

        if (emision > DateTime.Now.AddMinutes(1))
        {
            await DisplayAlertAsync(
                "Emisión inválida",
                "La fecha de emisión no puede ser posterior al momento actual.",
                "Aceptar");
            return;
        }

        if (!TimeOnly.TryParse(SelectorHora.SelectedItem?.ToString(), Cultura, out var hora))
        {
            await DisplayAlertAsync("Horario inválido", "Seleccione un horario de salida.", "Aceptar");
            return;
        }

        if (SelectorUnidad.SelectedIndex < 0 || SelectorUnidad.SelectedIndex >= _unidades.Count)
        {
            await DisplayAlertAsync(
                "Falta la unidad",
                "Seleccione una unidad disponible o abra una nueva.",
                "Aceptar");
            return;
        }

        if (!short.TryParse(CampoAsiento.Texto, out var asiento) || asiento <= 0)
        {
            await DisplayAlertAsync("Asiento inválido", "Escriba el número de asiento.", "Aceptar");
            return;
        }

        var unidad = _unidades[SelectorUnidad.SelectedIndex];

        if (asiento > unidad.NumeroAsientos)
        {
            await DisplayAlertAsync(
                "Asiento inválido",
                $"La unidad tiene {unidad.NumeroAsientos} asientos.",
                "Aceptar");
            return;
        }

        if (_asientosOcupados.Contains(asiento))
        {
            await DisplayAlertAsync(
                "Asiento ocupado",
                $"El asiento {asiento} ya está ocupado en la unidad {unidad.Numero}.",
                "Aceptar");
            return;
        }

        await ActualizarTarifaAsync();

        if (_tarifaActual <= 0)
        {
            await DisplayAlertAsync(
                "Sin tarifa",
                "No hay una tarifa vigente para la fecha y sentido del viaje.",
                "Aceptar");
            return;
        }

        var solicitud = new SolicitudDePasaje
        {
            PasajeroNombre = CampoNombre.Texto.Trim(),
            TipoDocumento = SelectorTipoDocumento.SelectedIndex == 1
                ? TipoDocumento.Pasaporte
                : TipoDocumento.DNI,
            NumeroDocumento = CampoDocumento.Texto.Trim(),
            Telefono = CampoTelefono.Texto,
            Origen = CampoOrigen.Texto.Trim(),
            Destino = CampoDestino.Texto.Trim(),
            LugarDeEmbarque = CampoEmbarque.Texto.Trim(),
            FechaSalida = DateOnly.FromDateTime(FechaSalida.Date ?? DateTime.Today),
            HoraSalida = hora,
            UnidadSalidaId = unidad.Id,
            NumeroAsiento = asiento,
            Monto = _tarifaActual,
            Medio = SelectorMedio.SelectedIndex == 1 ? MedioDePago.Yape : MedioDePago.Efectivo,
            NumeroTalonario = CampoTalonario.Texto.Trim(),
            HoraEmisionReal = emision
        };

        var resultado = await Cliente.RegistrarDiferidoAsync(solicitud);

        if (!resultado.Exito || resultado.Datos is null)
        {
            await MostrarErrorAsync(resultado, "No se pudo registrar el pasaje diferido.");
            return;
        }

        var pasaje = resultado.Datos;
        await DisplayAlertAsync(
            "Diferido registrado",
            $"Pasaje {pasaje.Id} · Talonario {solicitud.NumeroTalonario} · " +
            $"Pagado S/ {pasaje.Pagado:N2} · Estado {pasaje.Estado}.",
            "Aceptar");

        LimpiarFormulario();
        await CargarUnidadesAsync();
    }

    private void LimpiarFormulario()
    {
        CampoTalonario.Texto = string.Empty;
        CampoNombre.Texto = string.Empty;
        CampoDocumento.Texto = string.Empty;
        CampoTelefono.Texto = string.Empty;
        CampoAsiento.Texto = string.Empty;
        CampoHoraEmision.Texto = DateTime.Now.ToString("dd/MM/yyyy HH:mm", Cultura);
    }

    private async Task MostrarErrorAsync(ResultadoDeLaApi resultado, string mensaje)
    {
        var detalle = resultado.SinPermiso
            ? "No tiene permiso para esta operación."
            : resultado.Errores.Count > 0
                ? string.Join(Environment.NewLine, resultado.Errores.Select(error => error.Mensaje))
                : mensaje;

        await DisplayAlertAsync("Operación no completada", detalle, "Aceptar");
    }

    private async void AlCancelar(object? remitente, EventArgs argumentos) =>
        await NavegacionDeModulos.IrAsync("panel");
}
