using System.Text.Json;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Reportes;
using Kataleya.Contratos.Reportes;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

// CDU-12: se conservan el año en curso y el anterior; el reporte es del año antepasado (RF-32).
public sealed class ServicioReporteAnual
{
    private readonly IRepositorioPasajes _pasajes;
    private readonly IRepositorioBitacora _historia;
    private readonly IRepositorioReportesAnuales _reportes;
    private readonly RegistradorDeBitacora _bitacora;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    public ServicioReporteAnual(
        IRepositorioPasajes pasajes,
        IRepositorioBitacora historia,
        IRepositorioReportesAnuales reportes,
        RegistradorDeBitacora bitacora,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _pasajes = pasajes ?? throw new ArgumentNullException(nameof(pasajes));
        _historia = historia ?? throw new ArgumentNullException(nameof(historia));
        _reportes = reportes ?? throw new ArgumentNullException(nameof(reportes));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    private int AnioAntepasado => FechasLocales.Hoy(_reloj.Ahora).Year - 2;

    public async Task<bool> HayReportePendienteAsync() =>
        !await _reportes.ExisteDelAnioAsync(AnioAntepasado);

    public async Task<ArchivoProtegido> GenerarAsync(int anio, string contrasenaPdf)
    {
        if (string.IsNullOrEmpty(contrasenaPdf))
        {
            throw new ReglaDeNegocioException("Escriba una contraseña para el PDF.", "contrasenaPdf");
        }

        await ValidarAnioAsync(anio);

        var pasajes = await _pasajes.BuscarDelAnioAsync(anio);
        var historia = await _historia.BuscarHistoriaDeAsync(pasajes.Select(pasaje => pasaje.Id).ToList());
        var usuario = _contexto.Usuario;

        return new ArchivoProtegido
        {
            Nombre = $"Reporte anual {anio} - Kataleya.pdf",
            Contenido = PdfDelReporteAnual.Generar(
                anio,
                pasajes,
                historia,
                usuario.NombreUsuario,
                _reloj.Ahora,
                contrasenaPdf)
        };
    }

    public async Task<DatosDelReporteAnual> DepurarAsync(int anio, string rutaPdf)
    {
        if (string.IsNullOrWhiteSpace(rutaPdf))
        {
            throw new ReglaDeNegocioException(
                "Indique dónde quedó guardado el PDF del reporte.",
                "rutaPdf");
        }

        await ValidarAnioAsync(anio);

        var usuario = _contexto.Usuario;
        var reporte = new ReporteAnual
        {
            Anio = anio,
            UsuarioId = usuario.Id,
            UsuarioNombre = usuario.NombreUsuario,
            RutaPdf = rutaPdf.Trim()
        };

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var pasajes = await _pasajes.BuscarDelAnioAsync(anio);
            await _historia.EliminarHistoriaDeAsync(pasajes.Select(pasaje => pasaje.Id).ToList());
            reporte.CantidadDePasajesDepurados = await _pasajes.EliminarDelAnioAsync(anio);
            await _reportes.AgregarAsync(reporte);

            await _bitacora.RegistrarAsync(
                "Generar reporte anual",
                null,
                JsonSerializer.Serialize(new
                {
                    reporte.Anio,
                    PasajesDepurados = reporte.CantidadDePasajesDepurados,
                    reporte.RutaPdf
                }));
        });

        return new DatosDelReporteAnual
        {
            Anio = reporte.Anio,
            FechaHoraGeneracion = reporte.FechaHoraGeneracion,
            GeneradoPor = reporte.UsuarioNombre,
            CantidadDePasajesDepurados = reporte.CantidadDePasajesDepurados,
            RutaPdf = reporte.RutaPdf
        };
    }

    private async Task ValidarAnioAsync(int anio)
    {
        if (anio > AnioAntepasado)
        {
            throw new ReglaDeNegocioException(
                $"Los pasajes de {anio} todavía se conservan: el reporte es del año {AnioAntepasado} o anterior.",
                "anio");
        }

        if (await _reportes.ExisteDelAnioAsync(anio))
        {
            throw new ReglaDeNegocioException($"El reporte de {anio} ya se generó.", "anio");
        }
    }
}
