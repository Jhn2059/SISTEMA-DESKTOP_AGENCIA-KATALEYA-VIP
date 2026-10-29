using System.Globalization;
using Kataleya.Contratos.Configuracion;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;
using Kataleya.Escritorio.UIHelpers;
using Microsoft.Maui.Controls.Shapes;

namespace Kataleya.Escritorio.Features.GestionDeUsuarios;

public partial class GestionDeUsuariosPage
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string[] HorariosPredeterminados = ["06:00", "10:00", "13:00", "15:00", "20:00"];

    private List<DatosDeTarifa> _tarifasConfig = [];
    private List<DatosDeTipoDeVehiculo> _tiposConfig = [];
    private List<string> _horariosConfig = [];
    private bool _cargandoConfiguracion;

    private async Task CargarConfiguracionAsync()
    {
        if (_cargandoConfiguracion)
        {
            return;
        }

        _cargandoConfiguracion = true;

        try
        {
            var tarifas = await _cliente.ObtenerTarifasAsync();
            if (!tarifas.Exito || tarifas.Datos is null)
            {
                MostrarErrorConfig(tarifas, "No se pudieron cargar las tarifas.");
            }
            else
            {
                _tarifasConfig = tarifas.Datos;
                AplicarTarifasRegulares();
                RenderizarTarifasProgramadas();
            }

            var parametros = await _cliente.ObtenerParametrosAsync();
            if (!parametros.Exito || parametros.Datos is null)
            {
                MostrarErrorConfig(parametros, "No se pudieron cargar los parámetros.");
            }
            else
            {
                AplicarParametros(parametros.Datos);
            }

            var tipos = await _cliente.ObtenerTiposDeVehiculoAsync();
            if (!tipos.Exito || tipos.Datos is null)
            {
                MostrarErrorConfig(tipos, "No se pudieron cargar los tipos de vehículo.");
            }
            else
            {
                _tiposConfig = tipos.Datos;
                RenderizarTiposDeVehiculo();
            }
        }
        catch
        {
            MostrarMensajeConfig("No se pudo cargar la configuración del sistema.", esError: true);
        }
        finally
        {
            _cargandoConfiguracion = false;
        }
    }

    private void AplicarTarifasRegulares()
    {
        var ida = _tarifasConfig.FirstOrDefault(t =>
            t.Tipo == TipoDeTarifa.Regular && t.Sentido == SentidoDelViaje.Ida);
        var vuelta = _tarifasConfig.FirstOrDefault(t =>
            t.Tipo == TipoDeTarifa.Regular && t.Sentido == SentidoDelViaje.Vuelta);

        EntradaTarifaIda.Text = ida is null ? string.Empty : ida.Importe.ToString("N2", Cultura);
        EntradaTarifaVuelta.Text = vuelta is null ? string.Empty : vuelta.Importe.ToString("N2", Cultura);
    }

    private void RenderizarTarifasProgramadas()
    {
        ListaTarifasProgramadas.Children.Clear();

        var programadas = _tarifasConfig
            .Where(t => t.Tipo == TipoDeTarifa.Programada)
            .OrderBy(t => t.VigenciaDesde)
            .ThenBy(t => t.Nombre)
            .ToList();

        if (programadas.Count == 0)
        {
            ListaTarifasProgramadas.Children.Add(new Label
            {
                Text = "No hay tarifas programadas.",
                FontSize = 13,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 18)
            }.ConColor("TextoSuave"));

            return;
        }

        foreach (var tarifa in programadas)
        {
            ListaTarifasProgramadas.Children.Add(CrearFilaDeTarifa(tarifa));
        }
    }

    private Border CrearFilaDeTarifa(DatosDeTarifa tarifa)
    {
        var fila = new Border
        {
            Padding = new Thickness(10, 0),
            HeightRequest = 40,
            StrokeThickness = 0,
            BackgroundColor = Colors.Transparent
        };
        fila.SetDynamicResource(Border.BackgroundColorProperty, "Superficie");

        var rejilla = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1.3, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(0.9, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(0.9, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(0.9, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.1, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        rejilla.Children.Add(TextoDeTabla(tarifa.Nombre, 0));
        rejilla.Children.Add(TextoDeTabla($"S/ {tarifa.Importe:N2}", 1, TextAlignment.End));
        rejilla.Children.Add(TextoDeTabla(tarifa.VigenciaDesde?.ToString("dd/MM/yyyy", Cultura) ?? "—", 2, TextAlignment.Center));
        rejilla.Children.Add(TextoDeTabla(tarifa.VigenciaHasta?.ToString("dd/MM/yyyy", Cultura) ?? "—", 3, TextAlignment.Center));
        rejilla.Children.Add(TextoDeTabla(tarifa.Sentido.ToString(), 4, TextAlignment.Center));

        var acciones = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        var editar = new Button
        {
            Text = "Editar",
            FontSize = 11,
            HeightRequest = 28,
            Padding = new Thickness(10, 0)
        };
        AplicarEstilo(editar, "BotonSecundario");
        editar.Clicked += async (_, _) => await EditarTarifaProgramadaAsync(tarifa);

        var quitar = new Button
        {
            Text = "Quitar",
            FontSize = 11,
            HeightRequest = 28,
            Padding = new Thickness(10, 0)
        };
        AplicarEstilo(quitar, "BotonPeligro");
        quitar.Clicked += async (_, _) => await EliminarTarifaProgramadaAsync(tarifa);

        acciones.Children.Add(editar);
        acciones.Children.Add(quitar);
        Grid.SetColumn(acciones, 5);
        rejilla.Children.Add(acciones);

        fila.Content = rejilla;
        return fila;
    }

    private static Label TextoDeTabla(string texto, int columna, TextAlignment alineacion = TextAlignment.Start)
    {
        var etiqueta = new Label
        {
            Text = texto,
            FontSize = 12,
            LineBreakMode = LineBreakMode.TailTruncation,
            HorizontalTextAlignment = alineacion,
            VerticalTextAlignment = TextAlignment.Center
        };

        Grid.SetColumn(etiqueta, columna);
        return etiqueta;
    }

    private void AplicarParametros(List<DatosDeParametro> parametros)
    {
        string? Obtener(string nombre) => parametros
            .FirstOrDefault(p => p.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase))
            ?.Valor;

        EntradaPlazoReserva.Text = Obtener("plazo_maximo_reserva_dias") ?? "7";
        EntradaAvisoPrevio.Text = Obtener("aviso_previo_horas") ?? "3";

        var horarios = Obtener("horarios_salida");
        _horariosConfig = string.IsNullOrWhiteSpace(horarios)
            ? [.. HorariosPredeterminados]
            : horarios
                .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(valor => TimeOnly.TryParse(valor, Cultura, out var hora) ? hora.ToString("HH:mm") : null)
                .Where(valor => valor is not null)
                .Cast<string>()
                .Distinct()
                .OrderBy(valor => valor)
                .ToList();

        if (_horariosConfig.Count == 0)
        {
            _horariosConfig = [.. HorariosPredeterminados];
        }

        RenderizarHorarios();
    }

    private void RenderizarHorarios()
    {
        ListaHorarios.Children.Clear();

        foreach (var hora in _horariosConfig)
        {
            ListaHorarios.Children.Add(CrearChipDeHorario(hora));
        }
    }

    private Border CrearChipDeHorario(string hora)
    {
        var chip = new Border
        {
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(12, 4),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 14 }
        };
        chip.SetDynamicResource(Border.BackgroundColorProperty, "Superficie");
        chip.SetDynamicResource(Border.StrokeProperty, "Borde");

        var contenido = new HorizontalStackLayout { Spacing = 6 };
        contenido.Children.Add(new Label
        {
            Text = hora,
            FontSize = 13,
            VerticalTextAlignment = TextAlignment.Center
        });

        var quitar = new Button
        {
            Text = "×",
            FontSize = 12,
            WidthRequest = 22,
            HeightRequest = 22,
            Padding = 0,
            BackgroundColor = Colors.Transparent
        };
        quitar.TextColor = ServicioDeTema.Color("TextoSuave");
        quitar.Clicked += async (_, _) => await QuitarHorarioAsync(hora);
        contenido.Children.Add(quitar);

        chip.Content = contenido;
        return chip;
    }

    private void RenderizarTiposDeVehiculo()
    {
        ListaTiposVehiculo.Children.Clear();

        foreach (var tipo in _tiposConfig)
        {
            ListaTiposVehiculo.Children.Add(CrearChipDeTipo(tipo));
        }
    }

    private Border CrearChipDeTipo(DatosDeTipoDeVehiculo tipo)
    {
        var chip = new Border
        {
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(12, 4),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 14 }
        };
        chip.SetDynamicResource(Border.BackgroundColorProperty, "Superficie");
        chip.SetDynamicResource(Border.StrokeProperty, "Borde");

        var contenido = new HorizontalStackLayout { Spacing = 6 };
        contenido.Children.Add(new Label
        {
            Text = $"{tipo.Nombre} · {tipo.NumeroAsientos}",
            FontSize = 13,
            VerticalTextAlignment = TextAlignment.Center
        });

        var quitar = new Button
        {
            Text = "×",
            FontSize = 12,
            WidthRequest = 22,
            HeightRequest = 22,
            Padding = 0,
            BackgroundColor = Colors.Transparent
        };
        quitar.TextColor = ServicioDeTema.Color("TextoSuave");
        quitar.Clicked += async (_, _) => await EliminarTipoAsync(tipo);
        contenido.Children.Add(quitar);

        chip.Content = contenido;
        return chip;
    }

    private async void AlGuardarTarifaIda(object? remitente, EventArgs argumentos) =>
        await GuardarTarifaRegularAsync(SentidoDelViaje.Ida, EntradaTarifaIda, BotonGuardarTarifaIda);

    private async void AlGuardarTarifaVuelta(object? remitente, EventArgs argumentos) =>
        await GuardarTarifaRegularAsync(SentidoDelViaje.Vuelta, EntradaTarifaVuelta, BotonGuardarTarifaVuelta);

    private async Task GuardarTarifaRegularAsync(
        SentidoDelViaje sentido,
        Entry entrada,
        Button boton)
    {
        if (!TryParseDecimal(entrada.Text, out var importe) || importe <= 0)
        {
            MostrarMensajeConfig("Escriba un importe mayor que cero.", esError: true);
            return;
        }

        var actual = _tarifasConfig.FirstOrDefault(t =>
            t.Tipo == TipoDeTarifa.Regular && t.Sentido == sentido);

        var nombre = actual?.Nombre ?? sentido switch
        {
            SentidoDelViaje.Ida => "Tarifa regular Huancayo - Ica",
            SentidoDelViaje.Vuelta => "Tarifa regular Ica - Huancayo",
            _ => "Tarifa regular"
        };

        boton.IsEnabled = false;

        try
        {
            var resultado = await _cliente.GuardarTarifaAsync(actual?.Id, new SolicitudDeTarifa
            {
                Tipo = TipoDeTarifa.Regular,
                Nombre = nombre,
                Importe = importe,
                Sentido = sentido
            });

            if (!resultado.Exito)
            {
                MostrarErrorConfig(resultado, "No se pudo guardar la tarifa regular.");
                return;
            }

            MostrarMensajeConfig($"Tarifa regular de {sentido} actualizada a S/ {importe:N2}.");
            await RecargarTarifasAsync();
        }
        finally
        {
            boton.IsEnabled = true;
        }
    }

    private async void AlAgregarTarifa(object? remitente, EventArgs argumentos) =>
        await EditarTarifaProgramadaAsync(null);

    private async Task EditarTarifaProgramadaAsync(DatosDeTarifa? tarifa)
    {
        var nombre = await DisplayPromptAsync(
            tarifa is null ? "Nueva tarifa programada" : "Editar tarifa programada",
            "Nombre de la tarifa:",
            "Continuar",
            "Cancelar",
            "Fiestas Patrias",
            initialValue: tarifa?.Nombre ?? string.Empty);

        if (nombre is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            MostrarMensajeConfig("El nombre de la tarifa es obligatorio.", esError: true);
            return;
        }

        var importeTexto = await DisplayPromptAsync(
            "Importe",
            "Importe por pasaje:",
            "Continuar",
            "Cancelar",
            "S/ 0.00",
            keyboard: Keyboard.Numeric,
            initialValue: tarifa is null ? "0.00" : tarifa.Importe.ToString("N2", Cultura));

        if (importeTexto is null)
        {
            return;
        }

        if (!TryParseDecimal(importeTexto, out var importe) || importe <= 0)
        {
            MostrarMensajeConfig("Escriba un importe mayor que cero.", esError: true);
            return;
        }

        var desdeTexto = await DisplayPromptAsync(
            "Vigencia",
            "Fecha desde (dd/MM/yyyy):",
            "Continuar",
            "Cancelar",
            "dd/MM/yyyy",
            initialValue: (tarifa?.VigenciaDesde ?? DateOnly.FromDateTime(DateTime.Today))
                .ToString("dd/MM/yyyy", Cultura));

        if (desdeTexto is null)
        {
            return;
        }

        if (!DateOnly.TryParseExact(
                desdeTexto.Trim(),
                "dd/MM/yyyy",
                Cultura,
                DateTimeStyles.None,
                out var desde))
        {
            MostrarMensajeConfig("La fecha desde no tiene el formato dd/MM/yyyy.", esError: true);
            return;
        }

        var hastaTexto = await DisplayPromptAsync(
            "Vigencia",
            "Fecha hasta (dd/MM/yyyy):",
            "Continuar",
            "Cancelar",
            "dd/MM/yyyy",
            initialValue: (tarifa?.VigenciaHasta ?? desde.AddDays(7))
                .ToString("dd/MM/yyyy", Cultura));

        if (hastaTexto is null)
        {
            return;
        }

        if (!DateOnly.TryParseExact(
                hastaTexto.Trim(),
                "dd/MM/yyyy",
                Cultura,
                DateTimeStyles.None,
                out var hasta))
        {
            MostrarMensajeConfig("La fecha hasta no tiene el formato dd/MM/yyyy.", esError: true);
            return;
        }

        if (hasta < desde)
        {
            MostrarMensajeConfig("La fecha hasta no puede ser anterior a la fecha desde.", esError: true);
            return;
        }

        var opciones = new[] { "Ida", "Vuelta", "Ambos" };
        var sentidoElegido = await DisplayActionSheetAsync(
            "Sentido del viaje",
            "Cancelar",
            null,
            opciones);

        if (sentidoElegido is null || sentidoElegido == "Cancelar")
        {
            return;
        }

        var sentido = sentidoElegido switch
        {
            "Ida" => SentidoDelViaje.Ida,
            "Vuelta" => SentidoDelViaje.Vuelta,
            _ => SentidoDelViaje.Ambos
        };

        var resultado = await _cliente.GuardarTarifaAsync(tarifa?.Id, new SolicitudDeTarifa
        {
            Tipo = TipoDeTarifa.Programada,
            Nombre = nombre.Trim(),
            Importe = importe,
            VigenciaDesde = desde,
            VigenciaHasta = hasta,
            Sentido = sentido
        });

        if (!resultado.Exito)
        {
            MostrarErrorConfig(resultado, "No se pudo guardar la tarifa programada.");
            return;
        }

        MostrarMensajeConfig($"Tarifa «{nombre.Trim()}» guardada.");
        await RecargarTarifasAsync();
    }

    private async Task EliminarTarifaProgramadaAsync(DatosDeTarifa tarifa)
    {
        var confirmar = await DisplayAlertAsync(
            "Quitar tarifa",
            $"¿Eliminar la tarifa «{tarifa.Nombre}»?",
            "Quitar",
            "Cancelar");

        if (!confirmar)
        {
            return;
        }

        var resultado = await _cliente.EliminarTarifaAsync(tarifa.Id);
        if (!resultado.Exito)
        {
            MostrarErrorConfig(resultado, "No se pudo eliminar la tarifa.");
            return;
        }

        MostrarMensajeConfig("Tarifa eliminada.");
        await RecargarTarifasAsync();
    }

    private async Task RecargarTarifasAsync()
    {
        var resultado = await _cliente.ObtenerTarifasAsync();
        if (!resultado.Exito || resultado.Datos is null)
        {
            MostrarErrorConfig(resultado, "No se pudieron actualizar las tarifas.");
            return;
        }

        _tarifasConfig = resultado.Datos;
        AplicarTarifasRegulares();
        RenderizarTarifasProgramadas();
    }

    private async void AlGuardarParametros(object? remitente, EventArgs argumentos)
    {
        if (!int.TryParse(EntradaPlazoReserva.Text, out var plazo) || plazo < 0)
        {
            MostrarMensajeConfig("El plazo de reserva debe ser un número entero igual o mayor que cero.", esError: true);
            return;
        }

        if (!int.TryParse(EntradaAvisoPrevio.Text, out var aviso) || aviso < 0)
        {
            MostrarMensajeConfig("El aviso previo debe ser un número entero igual o mayor que cero.", esError: true);
            return;
        }

        BotonGuardarParametros.IsEnabled = false;

        try
        {
            var resultadoPlazo = await _cliente.GuardarParametroAsync(
                "plazo_maximo_reserva_dias",
                plazo.ToString(Cultura));

            if (!resultadoPlazo.Exito)
            {
                MostrarErrorConfig(resultadoPlazo, "No se pudo guardar el plazo de reserva.");
                return;
            }

            var resultadoAviso = await _cliente.GuardarParametroAsync(
                "aviso_previo_horas",
                aviso.ToString(Cultura));

            if (!resultadoAviso.Exito)
            {
                MostrarErrorConfig(resultadoAviso, "No se pudo guardar el aviso previo.");
                return;
            }

            MostrarMensajeConfig("Parámetros del negocio guardados.");
        }
        finally
        {
            BotonGuardarParametros.IsEnabled = true;
        }
    }

    private async void AlAgregarHorario(object? remitente, EventArgs argumentos)
    {
        var texto = await DisplayPromptAsync(
            "Nuevo horario",
            "Hora de salida (HH:mm):",
            "Agregar",
            "Cancelar",
            "hh:mm");

        if (texto is null)
        {
            return;
        }

        if (!TimeOnly.TryParse(texto.Trim(), Cultura, out var hora))
        {
            MostrarMensajeConfig("Escriba una hora válida en formato HH:mm.", esError: true);
            return;
        }

        var normalizada = hora.ToString("HH:mm");

        if (_horariosConfig.Contains(normalizada))
        {
            MostrarMensajeConfig($"El horario {normalizada} ya existe.", esError: true);
            return;
        }

        var horarios = _horariosConfig.Append(normalizada).OrderBy(valor => valor).ToList();
        await GuardarHorariosAsync(horarios);

        if (_horariosConfig.Contains(normalizada))
        {
            EntradaNuevoHorario.Text = string.Empty;
        }
    }

    private async Task QuitarHorarioAsync(string hora)
    {
        if (_horariosConfig.Count <= 1)
        {
            MostrarMensajeConfig("Debe quedar al menos un horario de salida.", esError: true);
            return;
        }

        var horarios = _horariosConfig.Where(valor => valor != hora).ToList();
        await GuardarHorariosAsync(horarios);
    }

    private async Task GuardarHorariosAsync(List<string> horarios)
    {
        var resultado = await _cliente.GuardarParametroAsync(
            "horarios_salida",
            string.Join(",", horarios));

        if (!resultado.Exito)
        {
            MostrarErrorConfig(resultado, "No se pudieron guardar los horarios.");
            return;
        }

        _horariosConfig = horarios;
        RenderizarHorarios();
        MostrarMensajeConfig("Horarios de salida guardados.");
    }

    private async void AlAgregarTipo(object? remitente, EventArgs argumentos)
    {
        if (string.IsNullOrWhiteSpace(EntradaNuevoTipo.Text))
        {
            MostrarMensajeConfig("Escriba el nombre del tipo de vehículo.", esError: true);
            return;
        }

        if (!short.TryParse(EntradaNuevosAsientos.Text, out var asientos) || asientos <= 0)
        {
            MostrarMensajeConfig("Indique un número de asientos mayor que cero.", esError: true);
            return;
        }

        BotonAgregarTipo.IsEnabled = false;

        try
        {
            var resultado = await _cliente.GuardarTipoDeVehiculoAsync(null, new SolicitudDeTipoDeVehiculo
            {
                Nombre = EntradaNuevoTipo.Text.Trim(),
                NumeroAsientos = asientos
            });

            if (!resultado.Exito)
            {
                MostrarErrorConfig(resultado, "No se pudo guardar el tipo de vehículo.");
                return;
            }

            EntradaNuevoTipo.Text = string.Empty;
            EntradaNuevosAsientos.Text = string.Empty;
            MostrarMensajeConfig("Tipo de vehículo guardado.");
            await RecargarTiposAsync();
        }
        finally
        {
            BotonAgregarTipo.IsEnabled = true;
        }
    }

    private async Task EliminarTipoAsync(DatosDeTipoDeVehiculo tipo)
    {
        var confirmar = await DisplayAlertAsync(
            "Quitar tipo de vehículo",
            $"¿Eliminar «{tipo.Nombre} · {tipo.NumeroAsientos}»?",
            "Quitar",
            "Cancelar");

        if (!confirmar)
        {
            return;
        }

        var resultado = await _cliente.EliminarTipoDeVehiculoAsync(tipo.Id);
        if (!resultado.Exito)
        {
            MostrarErrorConfig(resultado, "No se pudo eliminar el tipo de vehículo.");
            return;
        }

        MostrarMensajeConfig("Tipo de vehículo eliminado.");
        await RecargarTiposAsync();
    }

    private async Task RecargarTiposAsync()
    {
        var resultado = await _cliente.ObtenerTiposDeVehiculoAsync();
        if (!resultado.Exito || resultado.Datos is null)
        {
            MostrarErrorConfig(resultado, "No se pudieron actualizar los tipos de vehículo.");
            return;
        }

        _tiposConfig = resultado.Datos;
        RenderizarTiposDeVehiculo();
    }

    private void MostrarMensajeConfig(string mensaje, bool esError = false)
    {
        LblMensajeConfig.Text = mensaje;
        LblMensajeConfig.SetDynamicResource(
            Label.TextColorProperty,
            esError ? "Peligro" : "Exito");
        LblMensajeConfig.IsVisible = true;
    }

    private void MostrarErrorConfig(ResultadoDeLaApi resultado, string mensaje)
    {
        var detalle = resultado.SinPermiso
            ? "No tiene permiso para esta operación."
            : resultado.Errores.Count > 0
                ? string.Join(Environment.NewLine, resultado.Errores.Select(error => error.Mensaje))
                : mensaje;

        MostrarMensajeConfig(detalle, esError: true);
    }

    private static bool TryParseDecimal(string? texto, out decimal valor)
    {
        var limpio = (texto ?? string.Empty)
            .Replace("S/", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return decimal.TryParse(limpio, NumberStyles.Number, Cultura, out valor);
    }

    private static void AplicarEstilo(VisualElement elemento, string clave)
    {
        if (Application.Current?.Resources.TryGetValue(clave, out var recurso) == true &&
            recurso is Style estilo)
        {
            elemento.Style = estilo;
        }
    }
}

internal static class EtiquetaDeConfiguracionExtensions
{
    public static Label ConColor(this Label etiqueta, string clave)
    {
        etiqueta.SetDynamicResource(Label.TextColorProperty, clave);
        return etiqueta;
    }
}
