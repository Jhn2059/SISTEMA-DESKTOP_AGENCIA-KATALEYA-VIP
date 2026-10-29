using System.Globalization;
using Kataleya.Contratos.Configuracion;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.Components.Common;
using Kataleya.Escritorio.UIHelpers;
using Kataleya.Escritorio.UIHelpers.Paginas;
using Microsoft.Maui.Controls.Shapes;

namespace Kataleya.Escritorio.Features.RegistroDePasajes;

public partial class RegistroDePasajesPage : PaginaDeModulo
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string[] HorasPredeterminadas = ["06:00", "10:00", "13:00", "15:00", "20:00"];

    private readonly List<DatosDeUnidad> _unidades = [];
    private List<DatosDeTipoDeVehiculo> _tipos = [];
    private HashSet<short> _asientosOcupados = [];
    private bool _esGrupo;
    private bool _cargado;
    private short _asientoSeleccionado;
    private decimal _tarifaActual;
    private SentidoDelViaje? _sentidoDeLasUnidades;

    public RegistroDePasajesPage()
    {
        OpcionActiva = "registrar-pasaje";
        InitializeComponent();

        FechaSalida.Date = DateTime.Today.AddDays(1);
        SelectorTipoDocumento.SelectedIndex = 0;
        SelectorMedio.SelectedIndex = 0;

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
        LblEmisor.Text = Cliente?.CuentaActual?.NombreUsuario ?? "—";

        if (_cargado)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CampoOrigen.Texto))
        {
            CampoOrigen.Texto = Cliente?.CuentaActual?.Sede ?? string.Empty;
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
            _asientoSeleccionado = 0;
            LblAsiento.Text = "Asiento elegido: —";
            MapaAsientos.Children.Clear();
            LblMensaje.Text = _sentidoDeLasUnidades is null
                ? "Escriba como origen Huancayo o Ica para ver sus unidades."
                : "No hay unidades abiertas para esa salida. Use «Abrir otra unidad».";
        }
    }

    private async Task CargarAsientosAsync()
    {
        if (SelectorUnidad.SelectedIndex < 0 || SelectorUnidad.SelectedIndex >= _unidades.Count)
        {
            return;
        }

        var unidad = _unidades[SelectorUnidad.SelectedIndex];
        var resultado = await Cliente.ObtenerAsientosOcupadosAsync(unidad.Id);

        _asientosOcupados = resultado.Exito && resultado.Datos is not null
            ? resultado.Datos.ToHashSet()
            : [];

        _asientoSeleccionado = 0;
        LblAsiento.Text = "Asiento elegido: —";
        DibujarAsientos(unidad.NumeroAsientos);
    }

    private void DibujarAsientos(short total)
    {
        MapaAsientos.Children.Clear();

        for (short numero = 1; numero <= total; numero++)
        {
            var ocupado = _asientosOcupados.Contains(numero);
            var elegido = numero == _asientoSeleccionado;

            var borde = new Border
            {
                WidthRequest = 52,
                HeightRequest = 38,
                Margin = new Thickness(3),
                StrokeShape = new RoundRectangle { CornerRadius = 6 },
                StrokeThickness = 1
            };

            borde.SetDynamicResource(Border.BackgroundColorProperty,
                elegido ? "Info" : ocupado ? "DeshabilitadoFondo" : "SuperficieActiva");
            borde.SetDynamicResource(Border.StrokeProperty,
                ocupado ? "BordeDeshabilitado" : "Borde");

            var etiqueta = new Label
            {
                Text = numero.ToString(Cultura),
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };

            if (elegido)
            {
                etiqueta.TextColor = Colors.White;
            }
            else if (ocupado)
            {
                etiqueta.SetDynamicResource(Label.TextColorProperty, "DeshabilitadoTexto");
            }

            borde.Content = etiqueta;

            if (!ocupado)
            {
                var gesto = new TapGestureRecognizer { CommandParameter = numero };
                gesto.Tapped += AlElegirAsiento;
                borde.GestureRecognizers.Add(gesto);
            }

            MapaAsientos.Children.Add(borde);
        }
    }

    private void AlElegirAsiento(object? remitente, TappedEventArgs argumentos)
    {
        if (argumentos.Parameter is not short numero)
        {
            return;
        }

        _asientoSeleccionado = numero;
        LblAsiento.Text = $"Asiento elegido: {numero}";
        var total = SelectorUnidad.SelectedIndex >= 0 && SelectorUnidad.SelectedIndex < _unidades.Count
            ? _unidades[SelectorUnidad.SelectedIndex].NumeroAsientos
            : (short)0;
        DibujarAsientos(total);
    }

    private async void AlAbrirUnidad(object? remitente, EventArgs argumentos)
    {
        if (_tipos.Count == 0)
        {
            await DisplayAlertAsync("Sin tipos", "No hay tipos de vehículo configurados.", "Aceptar");
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

        if (!TimeOnly.TryParse(SelectorHora.SelectedItem?.ToString(), Cultura, out var hora))
        {
            return;
        }

        if (SentidoElegido is not SentidoDelViaje sentido)
        {
            await DisplayAlertAsync("Falta el origen", "Escriba como origen Huancayo o Ica.", "Aceptar");
            return;
        }

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

    private async void AlElegirIndividual(object? remitente, TappedEventArgs argumentos) =>
        await AplicarTipoAsync(individual: true);

    private async void AlElegirGrupo(object? remitente, TappedEventArgs argumentos) =>
        await AplicarTipoAsync(individual: false);

    private Task AplicarTipoAsync(bool individual)
    {
        _esGrupo = !individual;
        CampoGrupo.IsVisible = _esGrupo;
        BloquePrecioGrupo.IsVisible = _esGrupo;
        PanelAsientos.IsVisible = !_esGrupo;
        LblAsiento.IsVisible = !_esGrupo;
        SelectorUnidad.IsVisible = !_esGrupo;
        LblUnidad.IsVisible = !_esGrupo;
        BotonAbrirUnidad.IsVisible = !_esGrupo;
        BotonVenta.Text = _esGrupo ? "Registrar venta de grupo" : "Registrar venta";
        BotonReserva.Text = _esGrupo ? "Guardar reserva de grupo" : "Guardar reserva";
        LblMensaje.Text = _esGrupo
            ? "En el grupo, el precio y los pagos se registran en el responsable."
            : string.Empty;

        PintarBoton(BotonIndividual, individual);
        PintarBoton(BotonGrupo, _esGrupo);
        return Task.CompletedTask;
    }

    private static void PintarBoton(Border boton, bool activo)
    {
        boton.BackgroundColor = activo
            ? ServicioDeTema.Color("Info")
            : ServicioDeTema.Color("Superficie");
        boton.Stroke = activo
            ? Brush.Transparent
            : new SolidColorBrush(ServicioDeTema.Color("Borde"));
        boton.StrokeThickness = activo ? 0 : 1;

        if (boton.Content is not HorizontalStackLayout contenido)
        {
            return;
        }

        foreach (var elemento in contenido.Children)
        {
            switch (elemento)
            {
                case Label etiqueta:
                    etiqueta.TextColor = activo ? Colors.White : ServicioDeTema.Color("Texto");
                    break;
                case Microsoft.Maui.Controls.Shapes.Path ruta:
                    ruta.Stroke = activo ? Colors.White : ServicioDeTema.Color("TextoSuave");
                    break;
            }
        }
    }

    private async void AlGuardarReserva(object? remitente, EventArgs argumentos) =>
        await GuardarAsync(esVenta: false);

    private async void AlGuardarVenta(object? remitente, EventArgs argumentos) =>
        await GuardarAsync(esVenta: true);

    private async Task GuardarAsync(bool esVenta)
    {
        Enabled(false);

        try
        {
            if (_esGrupo)
            {
                await GuardarGrupoAsync(esVenta);
            }
            else
            {
                await GuardarIndividualAsync(esVenta);
            }
        }
        finally
        {
            Enabled(true);
        }
    }

    private void Enabled(bool habilitado)
    {
        BotonVenta.IsEnabled = habilitado;
        BotonReserva.IsEnabled = habilitado;
        BotonAbrirUnidad.IsEnabled = habilitado;
    }

    private async Task GuardarIndividualAsync(bool esVenta)
    {
        await ActualizarTarifaAsync();

        if (string.IsNullOrWhiteSpace(CampoNombre.Texto) ||
            string.IsNullOrWhiteSpace(CampoDocumento.Texto) ||
            string.IsNullOrWhiteSpace(CampoOrigen.Texto) ||
            string.IsNullOrWhiteSpace(CampoDestino.Texto) ||
            string.IsNullOrWhiteSpace(CampoEmbarque.Texto))
        {
            await DisplayAlertAsync(
                "Datos incompletos",
                "Complete el nombre, documento, origen, destino y lugar de embarque.",
                "Aceptar");
            return;
        }

        if (SelectorUnidad.SelectedIndex < 0 || _asientoSeleccionado == 0)
        {
            await DisplayAlertAsync(
                "Falta la unidad",
                "Seleccione una unidad y un asiento.",
                "Aceptar");
            return;
        }

        if (!TimeOnly.TryParse(SelectorHora.SelectedItem?.ToString(), Cultura, out var hora))
        {
            await DisplayAlertAsync("Horario inválido", "Seleccione un horario de salida.", "Aceptar");
            return;
        }

        if (!decimal.TryParse(CampoMonto.Texto, NumberStyles.Number, Cultura, out var monto))
        {
            monto = esVenta ? _tarifaActual : 0;
        }

        if (esVenta && Math.Abs(monto - _tarifaActual) > 0.005m)
        {
            await DisplayAlertAsync(
                "Venta incompleta",
                $"Una venta debe cobrarse completa: S/ {_tarifaActual:N2}.",
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
            UnidadSalidaId = _unidades[SelectorUnidad.SelectedIndex].Id,
            NumeroAsiento = _asientoSeleccionado,
            Monto = monto,
            Medio = monto > 0 ? MedioSeleccionado() : null,
            MenorSinPadres = MenorDeEdad.IsChecked,
            NombrePadre = MenorDeEdad.IsChecked ? CampoPadre.Texto : null,
            NombreMadre = MenorDeEdad.IsChecked ? CampoMadre.Texto : null,
            MotivoViaje = CampoMotivo.Texto
        };

        var resultado = esVenta
            ? await Cliente.RegistrarVentaAsync(solicitud)
            : await Cliente.RegistrarReservaAsync(solicitud);

        if (!resultado.Exito || resultado.Datos is null)
        {
            await MostrarErrorAsync(resultado, "No se pudo registrar el pasaje.");
            return;
        }

        var pasaje = resultado.Datos;
        var boleto = pasaje.Boleto?.NumeroCompleto ?? "sin boleto (reserva)";
        await DisplayAlertAsync(
            esVenta ? "Venta registrada" : "Reserva registrada",
            $"Pasaje {pasaje.Id} · {pasaje.Estado} · Boleto {boleto}.",
            "Aceptar");

        LimpiarPasajero();
        await CargarUnidadesAsync();
    }

    private async Task GuardarGrupoAsync(bool esVenta)
    {
        if (!int.TryParse(CampoGrupo.Texto, out var cantidad) || cantidad <= 0)
        {
            await DisplayAlertAsync("Cantidad inválida", "Indique cuántas personas viajan.", "Aceptar");
            return;
        }

        if (!decimal.TryParse(CampoPrecioGrupo.Text, NumberStyles.Number, Cultura, out var precio) || precio <= 0)
        {
            await DisplayAlertAsync("Precio inválido", "Escriba el precio acordado del grupo.", "Aceptar");
            return;
        }

        if (string.IsNullOrWhiteSpace(CampoOrigen.Texto) ||
            string.IsNullOrWhiteSpace(CampoDestino.Texto) ||
            string.IsNullOrWhiteSpace(CampoEmbarque.Texto))
        {
            await DisplayAlertAsync(
                "Datos incompletos",
                "Complete origen, destino y lugar de recojo.",
                "Aceptar");
            return;
        }

        if (!TimeOnly.TryParse(SelectorHora.SelectedItem?.ToString(), Cultura, out var hora))
        {
            await DisplayAlertAsync("Horario inválido", "Seleccione un horario de salida.", "Aceptar");
            return;
        }

        var integrantes = new List<IntegranteDeGrupo>();

        for (var i = 1; i <= cantidad; i++)
        {
            var nombre = await DisplayPromptAsync(
                $"Integrante {i} de {cantidad}",
                "Nombre completo:",
                "Siguiente",
                "Cancelar");

            if (string.IsNullOrWhiteSpace(nombre))
            {
                return;
            }

            var documento = await DisplayPromptAsync(
                $"Integrante {i} de {cantidad}",
                "Número de documento:",
                "Siguiente",
                "Cancelar");

            if (string.IsNullOrWhiteSpace(documento))
            {
                return;
            }

            integrantes.Add(new IntegranteDeGrupo
            {
                Nombre = nombre.Trim(),
                NumeroDocumento = documento.Trim(),
                TipoDocumento = SelectorTipoDocumento.SelectedIndex == 1
                    ? TipoDocumento.Pasaporte
                    : TipoDocumento.DNI,
                EsResponsable = i == 1,
                MotivoViaje = CampoMotivo.Texto
            });
        }

        if (_tipos.Count == 0)
        {
            await DisplayAlertAsync("Sin tipos", "No hay tipos de vehículo configurados.", "Aceptar");
            return;
        }

        var opciones = _tipos.Select(tipo => $"{tipo.Nombre} ({tipo.NumeroAsientos})").ToArray();
        var tipoElegido = await DisplayActionSheetAsync("Tipo de unidad del grupo", "Cancelar", null, opciones);

        if (tipoElegido is null || tipoElegido == "Cancelar")
        {
            return;
        }

        var indiceTipo = Array.IndexOf(opciones, tipoElegido);
        if (indiceTipo < 0)
        {
            return;
        }

        var tipo = _tipos[indiceTipo];
        var unidadesNecesarias = (int)Math.Ceiling(cantidad / (double)tipo.NumeroAsientos);
        var textoUnidades = await DisplayPromptAsync(
            "Unidades del grupo",
            $"El tipo elegido tiene {tipo.NumeroAsientos} asientos. Indique cuántas unidades:",
            "Continuar",
            "Cancelar",
            "1",
            keyboard: Keyboard.Numeric,
            initialValue: unidadesNecesarias.ToString(Cultura));

        if (!int.TryParse(textoUnidades, out var numeroUnidades) || numeroUnidades <= 0)
        {
            return;
        }

        var monto = esVenta
            ? precio
            : decimal.TryParse(CampoMonto.Texto, NumberStyles.Number, Cultura, out var abono)
                ? abono
                : 0;

        var solicitud = new SolicitudDeGrupo
        {
            Telefono = CampoTelefono.Texto,
            PrecioAcordado = precio,
            Origen = CampoOrigen.Texto.Trim(),
            Destino = CampoDestino.Texto.Trim(),
            LugarDeRecojo = CampoEmbarque.Texto.Trim(),
            FechaSalida = DateOnly.FromDateTime(FechaSalida.Date ?? DateTime.Today),
            HoraSalida = hora,
            Integrantes = integrantes,
            Unidades = Enumerable.Range(0, numeroUnidades)
                .Select(_ => new UnidadDelGrupo { TipoVehiculoId = tipo.Id })
                .ToList(),
            Monto = monto,
            Medio = monto > 0 ? MedioSeleccionado() : null
        };

        var resultado = esVenta
            ? await Cliente.RegistrarGrupoVentaAsync(solicitud)
            : await Cliente.RegistrarGrupoReservaAsync(solicitud);

        if (!resultado.Exito || resultado.Datos is null)
        {
            await MostrarErrorAsync(resultado, "No se pudo registrar el grupo.");
            return;
        }

        var grupo = resultado.Datos;
        await DisplayAlertAsync(
            esVenta ? "Venta de grupo registrada" : "Reserva de grupo registrada",
            $"Grupo {grupo.Codigo} · {grupo.Pasajes.Count} integrantes · " +
            $"Pagado S/ {grupo.Pagado:N2} · Saldo S/ {grupo.Saldo:N2}.",
            "Aceptar");

        LimpiarPasajero();
    }

    private MedioDePago MedioSeleccionado() =>
        SelectorMedio.SelectedIndex == 1 ? MedioDePago.Yape : MedioDePago.Efectivo;

    private void LimpiarPasajero()
    {
        CampoNombre.Texto = string.Empty;
        CampoDocumento.Texto = string.Empty;
        CampoTelefono.Texto = string.Empty;
        CampoPadre.Texto = string.Empty;
        CampoMadre.Texto = string.Empty;
        CampoMotivo.Texto = string.Empty;
        CampoGrupo.Texto = string.Empty;
        CampoMonto.Texto = string.Empty;
        CampoPrecioGrupo.Text = string.Empty;
        MenorDeEdad.IsChecked = false;
        _asientoSeleccionado = 0;
    }

    private async Task ActualizarTarifaAsync()
    {
        var fecha = DateOnly.FromDateTime(FechaSalida.Date ?? DateTime.Today);

        if (SentidoElegido is not SentidoDelViaje sentido)
        {
            _tarifaActual = 0;
            LblTarifa.Text = "Sin tarifa vigente";
            return;
        }

        var resultado = await Cliente.ObtenerTarifaVigenteAsync(fecha, sentido);

        if (resultado.Exito && resultado.Datos is not null)
        {
            _tarifaActual = resultado.Datos.Importe;
            LblTarifa.Text = $"S/ {_tarifaActual:N2}";
            return;
        }

        _tarifaActual = 0;
        LblTarifa.Text = "Sin tarifa vigente";
    }

    private async Task MostrarErrorAsync(Kataleya.Dominio.DataObjects.ResultadoDeLaApi resultado, string mensaje)
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
