using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Mapeo;
using Kataleya.Contratos.Bitacora;
using Kataleya.Contratos.Cuadre;
using Kataleya.Contratos.Panel;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioReportes
{
    private readonly IRepositorioCuadre _cuadre;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IRepositorioConfiguracion _configuracion;
    private readonly IRepositorioCatalogos _catalogos;
    private readonly ServicioBitacora _bitacora;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public ServicioReportes(
        IRepositorioCuadre cuadre,
        IRepositorioUsuarios usuarios,
        IRepositorioConfiguracion configuracion,
        IRepositorioCatalogos catalogos,
        ServicioBitacora bitacora,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _cuadre = cuadre ?? throw new ArgumentNullException(nameof(cuadre));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        _catalogos = catalogos ?? throw new ArgumentNullException(nameof(catalogos));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public async Task<DatosDelCuadre> GenerarCuadreAsync(FiltroDeCuadre filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var hoy = FechasLocales.Hoy(_reloj.Ahora);
        var desde = filtro.Desde ?? hoy;
        var hasta = filtro.Hasta ?? hoy;

        if (hasta < desde)
        {
            throw new ReglaDeNegocioException(
                "La fecha final no puede ser anterior a la inicial.",
                "fecha");
        }

        var usuario = await ObtenerUsuarioAsync();
        var esAdministrador = usuario.Rol.Nombre == "Administrador";
        var emisorId = esAdministrador ? filtro.EmisorId : usuario.Id;
        var sede = esAdministrador
            ? filtro.SedeId is short sedeId ? await _catalogos.BuscarSedeAsync(sedeId) : null
            : usuario.Sede;

        var detalle = await _cuadre.GenerarAsync(desde, hasta, emisorId, sede?.Id);
        detalle.GeneradoEl = _reloj.Ahora;
        detalle.GeneradoPor = usuario.NombreUsuario;
        detalle.Sede = sede?.Nombre ?? "todas";
        return detalle.ADatos();
    }

    public async Task<DatosDelPanel> ObtenerPanelAsync(DateOnly? fecha)
    {
        var dia = fecha ?? FechasLocales.Hoy(_reloj.Ahora);
        var usuario = await ObtenerUsuarioAsync();
        int? emisorId = usuario.Rol.Nombre == "Administrador" ? null : usuario.Id;
        var resumen = await _cuadre.ObtenerPanelAsync(dia, emisorId);

        await CompletarHorariosAsync(resumen);
        return resumen.ADatos(dia);
    }

    public async Task<IReadOnlyList<DatosDeBitacora>> ConsultarBitacoraAsync(
        Contratos.Bitacora.FiltroDeBitacora filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var entradas = await _bitacora.ConsultarAsync(new Dominio.Entities.FiltroDeBitacora
        {
            UsuarioId = filtro.UsuarioId,
            Desde = filtro.Desde,
            Hasta = filtro.Hasta,
            PasajeId = filtro.PasajeId
        });

        return entradas.Select(e => e.ADatos()).ToList();
    }

    private async Task CompletarHorariosAsync(ResumenDelPanel resumen)
    {
        var parametros = await _configuracion.ListarParametrosAsync();
        var configuracion = parametros.FirstOrDefault(p => p.Nombre == "horarios_salida")?.Valor;
        if (string.IsNullOrWhiteSpace(configuracion))
        {
            return;
        }

        var horas = configuracion
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(texto => TimeOnly.TryParse(texto, out var hora) ? hora : (TimeOnly?)null)
            .Where(hora => hora is not null)
            .Select(hora => hora!.Value)
            .ToHashSet();

        foreach (var fila in resumen.Horarios)
        {
            horas.Add(fila.Hora);
        }

        resumen.Horarios = horas
            .OrderBy(hora => hora)
            .Select(hora => resumen.Horarios.FirstOrDefault(f => f.Hora == hora)
                ?? new FilaDeHorario { Hora = hora })
            .ToList();
    }

    private async Task<Usuario> ObtenerUsuarioAsync() =>
        await _usuarios.BuscarPorIdAsync(_contexto.IdUsuario)
        ?? throw new InvalidOperationException("No se encontró el usuario de la sesión.");
}
