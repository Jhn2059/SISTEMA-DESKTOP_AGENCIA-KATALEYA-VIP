using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

// CDU-22: la Web API hace la copia semanal cuando la despierta el programador de tareas de Supabase
// o cuando alguien abre el sistema, si pasaron 7 días o más desde la última copia correcta (RNF-17).
public sealed class ServicioCopiaDeDatos
{
    public const int CopiasQueSeConservan = 8;
    private const int DiasEntreCopias = 7;

    private static readonly SemaphoreSlim CopiaEnCurso = new(1, 1);

    private readonly ICopiaDeLaBase _copiaDeLaBase;
    private readonly IAlmacenDeCopias _almacen;
    private readonly IRepositorioCopias _copias;
    private readonly IReloj _reloj;

    public ServicioCopiaDeDatos(
        ICopiaDeLaBase copiaDeLaBase,
        IAlmacenDeCopias almacen,
        IRepositorioCopias copias,
        IReloj reloj)
    {
        _copiaDeLaBase = copiaDeLaBase ?? throw new ArgumentNullException(nameof(copiaDeLaBase));
        _almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
        _copias = copias ?? throw new ArgumentNullException(nameof(copias));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    public bool PuedeCopiar => _almacen.EstaConfigurado;

    public async Task<bool> HacerCopiaSiCorrespondeAsync()
    {
        if (!await CopiaEnCurso.WaitAsync(0))
        {
            return false;
        }

        try
        {
            var ahora = _reloj.Ahora;
            var ultima = await _copias.BuscarUltimaAsync();

            if (ultima is not null &&
                FechasLocales.Hoy(ahora) < FechasLocales.Hoy(ultima.FechaHora).AddDays(DiasEntreCopias))
            {
                return false;
            }

            try
            {
                var copia = await _copiaDeLaBase.ExportarAsync(FechasLocales.EnHoraDePeru(ahora));
                await _almacen.SubirAsync(copia);
                await _almacen.BorrarLasViejasAsync(CopiasQueSeConservan);
                await _copias.AgregarAsync(new CopiaDeRespaldo
                {
                    FechaHora = ahora,
                    Correcta = true,
                    NombreArchivo = copia.NombreArchivo
                });

                return true;
            }
            catch
            {
                await _copias.AgregarAsync(new CopiaDeRespaldo { FechaHora = ahora, Correcta = false });
                throw;
            }
        }
        finally
        {
            CopiaEnCurso.Release();
        }
    }

    public async Task<EstadoDelRespaldo> ObtenerEstadoAsync()
    {
        var ultima = await _copias.BuscarUltimaAsync();
        var fallos = await _copias.ContarFallosSeguidosAsync();

        return new EstadoDelRespaldo
        {
            FechaHoraUltimaCopia = ultima?.FechaHora,
            UltimaCopiaCorrecta = ultima is not null && fallos == 0,
            FallosSeguidos = fallos
        };
    }
}
