using System.Globalization;
using System.Text.Json;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioParametros
{
    public const string Horarios = "horarios_salida";
    public const string PlazoDeReserva = "plazo_maximo_reserva_dias";
    public const string AvisoPrevio = "aviso_previo_horas";

    private const string OperacionCambioParametro = "Cambio de parámetro";
    private const string OperacionAgregarHorario = "Agregar horario";
    private const string OperacionQuitarHorario = "Quitar horario";

    private static readonly string[] Nombres =
    [
        Horarios,
        PlazoDeReserva,
        AvisoPrevio
    ];

    private static readonly string[] FormatosDeHora = ["H:mm", "HH:mm"];

    private static readonly TimeOnly[] HorariosIniciales =
    [
        new(6, 0),
        new(10, 0),
        new(13, 0),
        new(15, 0),
        new(20, 0)
    ];

    private readonly IRepositorioParametros _parametros;
    private readonly ServicioBitacora _bitacora;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public ServicioParametros(
        IRepositorioParametros parametros,
        ServicioBitacora bitacora,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _parametros = parametros ?? throw new ArgumentNullException(nameof(parametros));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public Task<IReadOnlyList<ParametroDelNegocio>> ListarParametrosAsync() =>
        _parametros.ListarAsync();

    public Task<IReadOnlyList<TipoDeVehiculo>> ObtenerTiposDeVehiculoAsync() =>
        _parametros.ListarTiposDeVehiculoAsync();

    public async Task<TimeSpan> ObtenerAvisoPrevioAsync()
    {
        var parametro = await _parametros.BuscarAsync(AvisoPrevio);

        return int.TryParse(
                   parametro?.Valor,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out var horas) &&
               horas is >= 0 and <= 24
            ? TimeSpan.FromHours(horas)
            : TimeSpan.FromHours(3);
    }

    public async Task<ParametroDelNegocio> GuardarParametroAsync(string nombre, string valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        var nombreNormalizado = nombre.Trim();
        if (!Nombres.Contains(nombreNormalizado, StringComparer.Ordinal))
        {
            throw new ReglaDeNegocioException("Elija un parámetro válido.", "nombre");
        }

        var valorNormalizado = valor?.Trim() ?? string.Empty;

        switch (nombreNormalizado)
        {
            case PlazoDeReserva:
                ValidarEntero(valorNormalizado, 1, 30, "Escriba un plazo de 1 a 30 días.");
                break;
            case AvisoPrevio:
                ValidarEntero(valorNormalizado, 0, 24, "Escriba un aviso de 0 a 24 horas.");
                break;
            case Horarios:
                valorNormalizado = UnirHorarios(LeerHorarios(valorNormalizado));
                break;
        }

        if (await YaSeRegistroAsync())
        {
            return await _parametros.BuscarAsync(nombreNormalizado)
                ?? new ParametroDelNegocio(nombreNormalizado, valorNormalizado);
        }

        var anterior = await _parametros.BuscarAsync(nombreNormalizado);
        var parametro = new ParametroDelNegocio(nombreNormalizado, valorNormalizado);
        parametro.CambiarValor(valorNormalizado, Actor);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _parametros.GuardarAsync(parametro);
            await RegistrarBitacoraAsync(
                OperacionCambioParametro,
                anterior is null ? null : Serializar(anterior),
                Serializar(parametro));
        });

        return parametro;
    }

    public async Task AgregarHorarioAsync(TimeOnly hora)
    {
        if (await YaSeRegistroAsync())
        {
            return;
        }

        var horarios = await ListarHorariosAsync();

        if (horarios.Contains(hora))
        {
            throw new ReglaDeNegocioException("Ese horario ya existe.", "hora");
        }

        horarios.Add(hora);
        await GuardarHorariosAsync(horarios, OperacionAgregarHorario);
    }

    public async Task QuitarHorarioAsync(TimeOnly hora)
    {
        if (await YaSeRegistroAsync())
        {
            return;
        }

        var horarios = await ListarHorariosAsync();

        if (!horarios.Contains(hora))
        {
            throw new ReglaDeNegocioException("Ese horario ya no existe.", "hora");
        }

        if (horarios.Count == 1)
        {
            throw new ReglaDeNegocioException(
                "Debe quedar al menos un horario de salida.",
                "hora");
        }

        horarios.Remove(hora);
        await GuardarHorariosAsync(horarios, OperacionQuitarHorario);
    }

    private Usuario Actor =>
        _contexto.Usuario
        ?? throw new InvalidOperationException("No se encontró el usuario de la sesión.");

    private async Task<bool> YaSeRegistroAsync() =>
        _contexto.ClaveDeOperacion is Guid clave &&
        await _bitacora.ExisteAsync(clave);

    private async Task<List<TimeOnly>> ListarHorariosAsync()
    {
        var parametro = await _parametros.BuscarAsync(Horarios);

        if (parametro is null || string.IsNullOrWhiteSpace(parametro.Valor))
        {
            return [.. HorariosIniciales];
        }

        return LeerHorarios(parametro.Valor);
    }

    private async Task GuardarHorariosAsync(List<TimeOnly> horarios, string operacion)
    {
        horarios.Sort();
        var valor = UnirHorarios(horarios);
        var anterior = await _parametros.BuscarAsync(Horarios);
        var parametro = new ParametroDelNegocio(Horarios, valor);
        parametro.CambiarValor(valor, Actor);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _parametros.GuardarAsync(parametro);
            await RegistrarBitacoraAsync(
                operacion,
                anterior is null ? null : Serializar(anterior),
                Serializar(parametro));
        });
    }

    private static List<TimeOnly> LeerHorarios(string valor)
    {
        var horarios = new List<TimeOnly>();

        foreach (var parte in valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!IntentarLeerHora(parte, out var hora))
            {
                throw new ReglaDeNegocioException(
                    "Escriba la hora en formato de 24 horas, por ejemplo 07:30.",
                    "hora");
            }

            if (!horarios.Contains(hora))
            {
                horarios.Add(hora);
            }
        }

        if (horarios.Count == 0)
        {
            throw new ReglaDeNegocioException(
                "Escriba al menos un horario de salida.",
                "hora");
        }

        horarios.Sort();
        return horarios;
    }

    private static bool IntentarLeerHora(string texto, out TimeOnly hora) =>
        TimeOnly.TryParseExact(
            texto.Trim(),
            FormatosDeHora,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out hora);

    private static string UnirHorarios(IEnumerable<TimeOnly> horarios) =>
        string.Join(
            ",",
            horarios.Select(hora => hora.ToString("HH:mm", CultureInfo.InvariantCulture)));

    private static void ValidarEntero(
        string valor,
        int minimo,
        int maximo,
        string mensaje)
    {
        if (!int.TryParse(
                valor,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var numero) ||
            numero < minimo ||
            numero > maximo)
        {
            throw new ReglaDeNegocioException(mensaje, "valor");
        }
    }

    private async Task RegistrarBitacoraAsync(
        string operacion,
        string? anterior,
        string? nuevo) =>
        await _bitacora.RegistrarAsync(new RegistroDeBitacora(
            0,
            _reloj.Ahora,
            operacion,
            Actor,
            anterior,
            nuevo));

    private static string Serializar(ParametroDelNegocio parametro) =>
        JsonSerializer.Serialize(new
        {
            parametro.Nombre,
            parametro.Valor
        });
}
