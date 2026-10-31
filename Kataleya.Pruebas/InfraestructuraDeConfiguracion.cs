using Kataleya.Aplicacion;
using Kataleya.Aplicacion.Services;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Pruebas;

internal sealed class RepositorioTarifasFalso : IRepositorioTarifas
{
    public List<Tarifa> Tarifas { get; } = [];

    public Task<Tarifa?> BuscarVigenteAsync(DateOnly fecha, Sentido sentido)
    {
        var programada = Tarifas
            .OfType<TarifaProgramada>()
            .Where(tarifa =>
                tarifa.Vigencia.Desde <= fecha &&
                fecha <= tarifa.Vigencia.Hasta &&
                (tarifa.Sentido is null || tarifa.Sentido == sentido))
            .OrderByDescending(tarifa => tarifa.Sentido == sentido)
            .ThenByDescending(tarifa => tarifa.Id)
            .FirstOrDefault();

        if (programada is not null)
        {
            return Task.FromResult<Tarifa?>(programada);
        }

        return Task.FromResult<Tarifa?>(
            Tarifas.OfType<TarifaRegular>().FirstOrDefault(tarifa => tarifa.Sentido == sentido));
    }

    public Task<IReadOnlyList<Tarifa>> ListarAsync() =>
        Task.FromResult<IReadOnlyList<Tarifa>>(Tarifas.OrderBy(tarifa => tarifa.Id).ToList());

    public Task<int> GuardarAsync(Tarifa tarifa)
    {
        if (tarifa.Id == 0)
        {
            var id = Tarifas.Count == 0 ? 1 : Tarifas.Max(item => item.Id) + 1;
            Tarifas.Add(ConId(tarifa, id));
            return Task.FromResult(id);
        }

        var indice = Tarifas.FindIndex(item => item.Id == tarifa.Id);
        if (indice >= 0)
        {
            Tarifas[indice] = tarifa;
        }

        return Task.FromResult(tarifa.Id);
    }

    public Task QuitarAsync(TarifaProgramada tarifa)
    {
        Tarifas.RemoveAll(item => item.Id == tarifa.Id);
        return Task.CompletedTask;
    }

    private static Tarifa ConId(Tarifa tarifa, int id) =>
        tarifa switch
        {
            TarifaRegular regular => Tarifa.Reconstruir(new TarifaGuardada(
                id,
                TipoDeTarifa.Regular,
                null,
                regular.Importe,
                null,
                regular.IdUsuario,
                regular.FijadaPor,
                regular.Sentido)),
            TarifaProgramada programada => Tarifa.Reconstruir(new TarifaGuardada(
                id,
                TipoDeTarifa.Programada,
                programada.Nombre,
                programada.Importe,
                programada.Vigencia,
                programada.IdUsuario,
                programada.FijadaPor,
                programada.Sentido)),
            _ => throw new ArgumentOutOfRangeException(nameof(tarifa))
        };
}

internal sealed class RepositorioParametrosFalso : IRepositorioParametros
{
    private readonly Dictionary<string, ParametroDelNegocio> _parametros =
        new(StringComparer.Ordinal);

    public List<TipoDeVehiculo> Tipos { get; } = [];

    public void Sembrar(string nombre, string valor)
    {
        _parametros[nombre] = new ParametroDelNegocio(nombre, valor);
    }

    public Task<ParametroDelNegocio?> BuscarAsync(string nombre) =>
        Task.FromResult(_parametros.GetValueOrDefault(nombre));

    public Task<IReadOnlyList<ParametroDelNegocio>> ListarAsync() =>
        Task.FromResult<IReadOnlyList<ParametroDelNegocio>>(
            _parametros.Values.OrderBy(parametro => parametro.Nombre, StringComparer.Ordinal).ToList());

    public Task<IReadOnlyList<TipoDeVehiculo>> ListarTiposDeVehiculoAsync() =>
        Task.FromResult<IReadOnlyList<TipoDeVehiculo>>(Tipos.ToList());

    public Task GuardarAsync(ParametroDelNegocio parametro)
    {
        _parametros[parametro.Nombre] = ParametroDelNegocio.Reconstruir(
            new ParametroDelNegocioGuardado(
                parametro.Nombre,
                parametro.Valor,
                parametro.IdUsuario,
                DateTime.UtcNow));

        return Task.CompletedTask;
    }
}

internal sealed class RepositorioPasajesFalso : IRepositorioPasajes
{
    public int Cantidad { get; set; }
    public TimeOnly? HoraConsultada { get; private set; }
    public List<Pasaje> Pasajes { get; } = [];
    public bool AsientoOcupadoResultado { get; set; }

    public Task<int> ContarPasajesDelHorarioAsync(TimeOnly hora)
    {
        HoraConsultada = hora;
        return Task.FromResult(Cantidad);
    }

    public Task<long> RegistrarPasajeAsync(Pasaje pasaje)
    {
        var id = Pasajes.Count == 0 ? 1L : Pasajes.Max(p => p.Id) + 1;
        var conId = Pasaje.Reconstruir(new PasajeGuardado(
            id,
            pasaje.UnidadSalidaId,
            pasaje.NumeroAsiento,
            pasaje.NombrePasajero,
            pasaje.TipoDocumento,
            pasaje.NumeroDocumento,
            pasaje.Celular,
            pasaje.Origen,
            pasaje.Destino,
            pasaje.LugarEmbarque,
            pasaje.FechaHoraSalida,
            pasaje.FechaVenta,
            pasaje.Adelanto,
            pasaje.Estado,
            pasaje.IdUsuario,
            pasaje.VendidaPor,
            pasaje.EsMenor,
            pasaje.NombrePadreMadre,
            pasaje.MotivoMenor));
        Pasajes.Add(conId);
        return Task.FromResult(id);
    }

    public Task<Pasaje?> ObtenerPasajeAsync(long id) =>
        Task.FromResult(Pasajes.FirstOrDefault(p => p.Id == id));

    public Task<bool> AsientoEstaOcupadoAsync(long unidadSalidaId, int numeroAsiento) =>
        Task.FromResult(AsientoOcupadoResultado);
}

internal sealed class RepositorioUnidadesSalidaFalso : IRepositorioUnidadesSalida
{
    public List<UnidadSalidaDisponible> Disponibles { get; } = [];
    public UnidadSalida? UnidadPorId { get; set; }
    public int AsientosOcupadosResultado { get; set; }

    public Task<IReadOnlyList<UnidadSalidaDisponible>> ListarDisponiblesAsync(DateOnly fecha) =>
        Task.FromResult<IReadOnlyList<UnidadSalidaDisponible>>(Disponibles);

    public Task<UnidadSalida?> ObtenerAsync(long id) =>
        Task.FromResult(UnidadPorId);

    public Task<int> ContarAsientosOcupadosAsync(long unidadSalidaId) =>
        Task.FromResult(AsientosOcupadosResultado);
}

internal sealed class EscenarioDeConfiguracion
{
    public EscenarioDeConfiguracion()
    {
        Reloj = new RelojFalso(new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc));
        Contexto = new ContextoDeOperacion();
        Bitacora = new RepositorioBitacoraFalso();
        TarifasRepositorio = new RepositorioTarifasFalso();
        ParametrosRepositorio = new RepositorioParametrosFalso();
        PasajesRepositorio = new RepositorioPasajesFalso();
        UnidadesSalidaRepositorio = new RepositorioUnidadesSalidaFalso();

        Administradora = new Usuario(
            1,
            "Administradora",
            "admin",
            string.Empty,
            string.Empty,
            EstadoCuenta.Activa,
            0,
            null,
            new Rol(1, "Administrador"),
            new Sede(1, "Central"));
        Administradora.FijarContrasena("Admin1234");

        Contexto.IdUsuario = Administradora.Id;
        Contexto.Usuario = Administradora;

        ParametrosRepositorio.Tipos.Add(new TipoDeVehiculo("Auto", 4));
        ParametrosRepositorio.Tipos.Add(new TipoDeVehiculo("Combi", 20));

        var servicioBitacora = new ServicioBitacora(Bitacora, Contexto);
        var unidad = new UnidadDeTrabajoFalsa();

        Tarifas = new ServicioTarifas(
            TarifasRepositorio,
            servicioBitacora,
            unidad,
            Reloj,
            Contexto);
        Parametros = new ServicioParametros(
            ParametrosRepositorio,
            servicioBitacora,
            unidad,
            Reloj,
            Contexto);
        Consultas = new ServicioConsultas(PasajesRepositorio);
        Pasajes = new ServicioPasajes(
            PasajesRepositorio,
            UnidadesSalidaRepositorio,
            TarifasRepositorio,
            servicioBitacora,
            unidad,
            Reloj,
            Contexto);
    }

    public RelojFalso Reloj { get; }

    public ContextoDeOperacion Contexto { get; }

    public RepositorioBitacoraFalso Bitacora { get; }

    public RepositorioTarifasFalso TarifasRepositorio { get; }

    public RepositorioParametrosFalso ParametrosRepositorio { get; }

    public RepositorioPasajesFalso PasajesRepositorio { get; }
    public RepositorioUnidadesSalidaFalso UnidadesSalidaRepositorio { get; }

    public Usuario Administradora { get; }

    public ServicioTarifas Tarifas { get; }

    public ServicioParametros Parametros { get; }

    public ServicioConsultas Consultas { get; }
    public ServicioPasajes Pasajes { get; }
}
