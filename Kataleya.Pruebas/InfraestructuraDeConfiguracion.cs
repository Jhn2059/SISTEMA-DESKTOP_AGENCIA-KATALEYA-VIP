using Kataleya.Aplicacion;
using Kataleya.Aplicacion.Services;
using Kataleya.Aplicacion.Time;
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

    public Task<TipoDeVehiculo?> BuscarTipoDeVehiculoAsync(short id) =>
        Task.FromResult(Tipos.FirstOrDefault(tipo => tipo.Id == id));

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
    public RepositorioUnidadesSalidaFalso? Unidades { get; set; }

    public Task<long> RegistrarAsync(Pasaje pasaje)
    {
        var id = Pasajes.Count == 0 ? 1L : Pasajes.Max(p => p.Id) + 1;
        Pasajes.Add(Clonar(pasaje, id, pasaje.Estado));
        return Task.FromResult(id);
    }

    public Task<Pasaje?> BuscarAsync(long id) =>
        Task.FromResult(Pasajes.FirstOrDefault(p => p.Id == id));

    public Task<Pasaje?> BuscarPorAsientoAsync(long unidadDeSalidaId, int numeroAsiento) =>
        Task.FromResult(Pasajes.FirstOrDefault(p =>
            p.UnidadDeSalidaId == unidadDeSalidaId &&
            p.NumeroAsiento == numeroAsiento &&
            p.Estado != EstadoPasaje.Anulado));

    public Task<IReadOnlyList<Pasaje>> ListarAsync(FiltroDePasajes filtro)
    {
        IEnumerable<Pasaje> consulta = Pasajes.Where(p =>
            ZonaHorariaLima.FechaLocal(p.FechaHoraSalida) == filtro.Fecha);

        if (filtro.Hora is not null)
        {
            consulta = consulta.Where(p =>
                TimeOnly.FromDateTime(ZonaHorariaLima.AHoraLocal(p.FechaHoraSalida)) == filtro.Hora);
        }

        if (filtro.Estado is not null)
        {
            consulta = consulta.Where(p => p.Estado == filtro.Estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.NumeroDocumento))
        {
            consulta = consulta.Where(p => p.NumeroDocumento == filtro.NumeroDocumento);
        }

        if (filtro.Sentido is not null && Unidades is not null)
        {
            var ids = Unidades.Unidades
                .Where(u => u.Sentido == filtro.Sentido)
                .Select(u => u.Id)
                .ToHashSet();

            consulta = consulta.Where(p =>
                p.UnidadDeSalidaId is long unidadId && ids.Contains(unidadId));
        }

        return Task.FromResult<IReadOnlyList<Pasaje>>(consulta.ToList());
    }

    public Task<IReadOnlyList<Pasaje>> ListarPorGrupoAsync(long grupoId) =>
        Task.FromResult<IReadOnlyList<Pasaje>>(Pasajes
            .Where(p => p.GrupoId == grupoId)
            .OrderBy(p => p.Id)
            .ToList());

    public Task ActualizarEstadoAsync(long id, EstadoPasaje estado)
    {
        var indice = Pasajes.FindIndex(p => p.Id == id);
        if (indice >= 0)
        {
            Pasajes[indice] = Clonar(Pasajes[indice], id, estado);
        }

        return Task.CompletedTask;
    }

    public Task ActualizarAsync(Pasaje pasaje)
    {
        var indice = Pasajes.FindIndex(p => p.Id == pasaje.Id);
        if (indice >= 0)
        {
            Pasajes[indice] = Clonar(pasaje, pasaje.Id, pasaje.Estado);
        }

        return Task.CompletedTask;
    }

    public Task<int> ContarPasajesDelHorarioAsync(TimeOnly hora)
    {
        HoraConsultada = hora;
        return Task.FromResult(Cantidad);
    }

    public Task<bool> AsientoEstaOcupadoAsync(
        long unidadDeSalidaId,
        int numeroAsiento,
        long? excluirPasajeId = null) =>
        Task.FromResult(
            AsientoOcupadoResultado ||
            Pasajes.Any(p =>
                p.Id != excluirPasajeId &&
                p.UnidadDeSalidaId == unidadDeSalidaId &&
                p.NumeroAsiento == numeroAsiento &&
                p.Estado != EstadoPasaje.Anulado));

    public List<PasajeDeCuadre> AnuladosDeCuadre { get; } = [];

    public List<PasajeDeCuadre> PendientesDeCuadre { get; } = [];

    public Task<IReadOnlyList<PasajeDeCuadre>> ListarAnuladosEnPeriodoAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? emisorId) =>
        Task.FromResult<IReadOnlyList<PasajeDeCuadre>>(AnuladosDeCuadre
            .Where(pasaje =>
                pasaje.FechaDeAnulacion >= desdeUtc &&
                pasaje.FechaDeAnulacion < hastaUtcExclusivo)
            .Where(pasaje => emisorId is null || pasaje.EmisorId == emisorId)
            .OrderBy(pasaje => pasaje.FechaDeAnulacion)
            .ThenBy(pasaje => pasaje.Id)
            .ToList());

    public Task<IReadOnlyList<PasajeDeCuadre>> ListarPendientesDeSaldoAsync(int? emisorId) =>
        Task.FromResult<IReadOnlyList<PasajeDeCuadre>>(PendientesDeCuadre
            .Where(pasaje => emisorId is null || pasaje.EmisorId == emisorId)
            .OrderBy(pasaje => pasaje.FechaHoraSalida)
            .ThenBy(pasaje => pasaje.Id)
            .ToList());

    private static Pasaje Clonar(Pasaje pasaje, long id, EstadoPasaje estado) =>
        Pasaje.Reconstruir(new PasajeGuardado(
            id,
            pasaje.UnidadDeSalidaId,
            pasaje.GrupoId,
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
            pasaje.Importe,
            estado,
            pasaje.TipoRegistro,
            pasaje.EmisorId,
            pasaje.Emisor,
            pasaje.EsMenor,
            pasaje.NombrePadreMadre,
            pasaje.MotivoMenor,
            pasaje.EsResponsable,
            pasaje.CodigoDeGrupo,
            pasaje.MotivoDeAnulacion,
            pasaje.FechaDeAnulacion,
            pasaje.EsDiferido));
}

internal sealed class RepositorioUnidadesSalidaFalso : IRepositorioUnidadesSalida
{
    public List<UnidadDeSalida> Unidades { get; } = [];
    public RepositorioParametrosFalso? Tipos { get; set; }
    public RepositorioPasajesFalso? Pasajes { get; set; }

    public void Sembrar(UnidadDeSalida unidad)
    {
        var id = Unidades.Count == 0 ? 1L : Unidades.Max(u => u.Id) + 1;
        Unidades.Add(Clonar(unidad, id));
    }

    public Task<IReadOnlyList<UnidadDeSalidaDisponible>> ListarPorSalidaAsync(
        Sentido sentido,
        DateTime fechaHoraSalida)
    {
        var disponibles = Unidades
            .Where(u => u.Sentido == sentido && u.FechaHoraSalida == fechaHoraSalida)
            .Select(Mapear)
            .ToList();

        return Task.FromResult<IReadOnlyList<UnidadDeSalidaDisponible>>(disponibles);
    }

    public Task<UnidadDeSalida?> BuscarAsync(long id) =>
        Task.FromResult(Unidades.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<UnidadDeSalida>> ListarPorGrupoAsync(long grupoId) =>
        Task.FromResult<IReadOnlyList<UnidadDeSalida>>(Unidades
            .Where(u => u.GrupoId == grupoId)
            .OrderBy(u => u.Numero)
            .ToList());

    public Task<int> SiguienteNumeroAsync(Sentido sentido, DateTime fechaHoraSalida)
    {
        var numero = Unidades
            .Where(u => u.Sentido == sentido && u.FechaHoraSalida == fechaHoraSalida)
            .Select(u => u.Numero)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return Task.FromResult(numero);
    }

    public Task<long> RegistrarAsync(UnidadDeSalida unidad)
    {
        var id = Unidades.Count == 0 ? 1L : Unidades.Max(u => u.Id) + 1;
        Unidades.Add(Clonar(unidad, id));
        return Task.FromResult(id);
    }

    public Task<int> ContarAsientosOcupadosAsync(long unidadDeSalidaId) =>
        Task.FromResult(Pasajes?.Pasajes.Count(p =>
            p.UnidadDeSalidaId == unidadDeSalidaId &&
            p.Estado != EstadoPasaje.Anulado) ?? 0);

    private UnidadDeSalidaDisponible Mapear(UnidadDeSalida unidad)
    {
        var tipo = Tipos?.Tipos.FirstOrDefault(t => t.Id == unidad.TipoVehiculoId);
        var ocupados = Pasajes?.Pasajes
            .Where(p => p.UnidadDeSalidaId == unidad.Id && p.Estado != EstadoPasaje.Anulado)
            .Select(p => p.NumeroAsiento)
            .OfType<int>()
            .ToList() ?? [];

        return new UnidadDeSalidaDisponible(
            unidad.Id,
            unidad.Numero,
            unidad.GrupoId,
            unidad.Sentido
                ?? throw new InvalidOperationException(
                    "Una unidad individual debe tener sentido."),
            unidad.TipoVehiculoId,
            tipo?.Nombre ?? "Unidad",
            tipo?.CantidadDeAsientos ?? 40,
            ocupados,
            unidad.FechaHoraSalida,
            unidad.Estado,
            unidad.AbiertaPor);
    }

    private static UnidadDeSalida Clonar(UnidadDeSalida unidad, long id) =>
        UnidadDeSalida.Reconstruir(new UnidadDeSalidaGuardada(
            id,
            unidad.Numero,
            unidad.GrupoId,
            unidad.Sentido,
            unidad.TipoVehiculoId,
            unidad.FechaHoraSalida,
            unidad.Estado,
            unidad.AbiertaPorId,
            unidad.AbiertaPor));
}

internal sealed class RepositorioPagosFalso : IRepositorioPagos
{
    public List<Pago> Pagos { get; } = [];

    public Task<long> RegistrarAsync(Pago pago)
    {
        var id = Pagos.Count == 0 ? 1L : Pagos.Max(p => p.Id) + 1;
        Pagos.Add(Pago.Reconstruir(new PagoGuardado(
            id,
            pago.PasajeId,
            pago.GrupoId,
            pago.Monto,
            pago.MedioDePago,
            pago.Tipo,
            pago.FechaPago,
            pago.CobradorId,
            pago.Cobrador)));

        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<Pago>> ListarPorPasajeAsync(long pasajeId) =>
        Task.FromResult<IReadOnlyList<Pago>>(Pagos
            .Where(p => p.PasajeId == pasajeId)
            .OrderBy(p => p.FechaPago)
            .ThenBy(p => p.Id)
            .ToList());

    public Task<IReadOnlyList<Pago>> ListarPorGrupoAsync(long grupoId) =>
        Task.FromResult<IReadOnlyList<Pago>>(Pagos
            .Where(p => p.GrupoId == grupoId)
            .OrderBy(p => p.FechaPago)
            .ThenBy(p => p.Id)
            .ToList());

    public List<MovimientoDeCuadre> MovimientosDeCuadre { get; } = [];

    public Task<IReadOnlyList<MovimientoDeCuadre>> ListarMovimientosDeCuadreAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? emisorId) =>
        Task.FromResult<IReadOnlyList<MovimientoDeCuadre>>(MovimientosDeCuadre
            .Where(movimiento =>
                movimiento.FechaPago >= desdeUtc &&
                movimiento.FechaPago < hastaUtcExclusivo)
            .Where(movimiento => emisorId is null || movimiento.EmisorId == emisorId)
            .OrderBy(movimiento => movimiento.FechaPago)
            .ThenBy(movimiento => movimiento.PagoId)
            .ToList());
}

internal sealed class RepositorioGruposFalso : IRepositorioGrupos
{
    public List<Grupo> Grupos { get; } = [];

    public Task<string> SiguienteCodigoAsync()
    {
        var siguiente = Grupos.Count + 1;
        return Task.FromResult($"G{siguiente:0000}");
    }

    public Task<long> RegistrarAsync(Grupo grupo)
    {
        var id = Grupos.Count == 0 ? 1L : Grupos.Max(g => g.Id) + 1;
        Grupos.Add(Clonar(grupo, id));
        return Task.FromResult(id);
    }

    public Task<Grupo?> BuscarPorClaveDeOperacionAsync(Guid clave) =>
        Task.FromResult(Grupos.FirstOrDefault(g => g.ClaveDeOperacion == clave));

    private static Grupo Clonar(Grupo grupo, long id) =>
        Grupo.Reconstruir(new GrupoGuardado(
            id,
            grupo.Codigo,
            grupo.TelefonoContacto,
            grupo.PrecioAcordado,
            grupo.Origen,
            grupo.Destino,
            grupo.LugarDeRecojo,
            grupo.FechaHoraSalida,
            grupo.FechaRegistro,
            grupo.CreadoPorId,
            grupo.CreadoPor,
            grupo.ClaveDeOperacion));
}

internal sealed class RepositorioBoletosFalso : IRepositorioBoletos
{
    private readonly Dictionary<string, int> _correlativos =
        new(StringComparer.OrdinalIgnoreCase);

    public List<BoletoDeViaje> Boletos { get; } = [];

    public Task<BoletoDeViaje> EmitirAsync(
        long pasajeId,
        string serie,
        DateTime fechaEmision,
        int emitidoPorId,
        string emitidoPor)
    {
        var existente = Boletos.FirstOrDefault(b => b.PasajeId == pasajeId);
        if (existente is not null)
        {
            return Task.FromResult(existente);
        }

        var correlativo = _correlativos.GetValueOrDefault(serie) + 1;
        _correlativos[serie] = correlativo;

        return RegistrarAsync(
            pasajeId,
            serie,
            correlativo,
            fechaEmision,
            emitidoPorId,
            emitidoPor);
    }

    public Task<BoletoDeViaje> RegistrarAsync(
        long pasajeId,
        string serie,
        int correlativo,
        DateTime fechaEmision,
        int emitidoPorId,
        string emitidoPor)
    {
        var existente = Boletos.FirstOrDefault(b => b.PasajeId == pasajeId);
        if (existente is not null)
        {
            return Task.FromResult(existente);
        }

        var boleto = BoletoDeViaje.Reconstruir(new BoletoGuardado(
            Boletos.Count + 1L,
            pasajeId,
            serie,
            correlativo,
            fechaEmision,
            emitidoPorId,
            emitidoPor));

        Boletos.Add(boleto);
        return Task.FromResult(boleto);
    }

    public Task<BoletoDeViaje?> BuscarPorPasajeAsync(long pasajeId) =>
        Task.FromResult(Boletos.FirstOrDefault(b => b.PasajeId == pasajeId));

    public Task<BoletoDeViaje?> BuscarPorNumeroAsync(string serie, int correlativo) =>
        Task.FromResult(Boletos.FirstOrDefault(b =>
            string.Equals(b.Numero.Serie, serie, StringComparison.OrdinalIgnoreCase) &&
            b.Numero.Correlativo == correlativo));
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
        PagosRepositorio = new RepositorioPagosFalso();
        GruposRepositorio = new RepositorioGruposFalso();
        BoletosRepositorio = new RepositorioBoletosFalso();

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

        ParametrosRepositorio.Tipos.Add(TipoDeVehiculo.Reconstruir(
            new TipoDeVehiculoGuardado(1, "Auto", 4)));
        ParametrosRepositorio.Tipos.Add(TipoDeVehiculo.Reconstruir(
            new TipoDeVehiculoGuardado(2, "Combi", 20)));

        UnidadesSalidaRepositorio.Tipos = ParametrosRepositorio;
        UnidadesSalidaRepositorio.Pasajes = PasajesRepositorio;
        PasajesRepositorio.Unidades = UnidadesSalidaRepositorio;

        var servicioBitacora = new ServicioBitacora(Bitacora, Contexto);
        BitacoraServicio = servicioBitacora;
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
        Hidratador = new HidratadorDePasajes(PagosRepositorio, BoletosRepositorio);
        Consultas = new ServicioConsultas(PasajesRepositorio, Hidratador);
        Asientos = new ServicioAsientos(
            UnidadesSalidaRepositorio,
            ParametrosRepositorio,
            Parametros,
            servicioBitacora,
            unidad,
            Reloj,
            Contexto);
        Ventas = new ServicioReservasYVentas(
            PasajesRepositorio,
            UnidadesSalidaRepositorio,
            GruposRepositorio,
            PagosRepositorio,
            BoletosRepositorio,
            TarifasRepositorio,
            ParametrosRepositorio,
            Parametros,
            Hidratador,
            servicioBitacora,
            unidad,
            Reloj,
            Contexto);
        Cobros = new ServicioCobros(
            PasajesRepositorio,
            PagosRepositorio,
            BoletosRepositorio,
            Hidratador,
            servicioBitacora,
            unidad,
            Reloj,
            Contexto);
        Gestion = new ServicioGestionDePasajes(
            PasajesRepositorio,
            PagosRepositorio,
            UnidadesSalidaRepositorio,
            ParametrosRepositorio,
            Parametros,
            Hidratador,
            servicioBitacora,
            unidad,
            Reloj,
            Contexto);
        Cuadre = new ServicioCuadre(
            PagosRepositorio,
            PasajesRepositorio,
            Contexto);
    }

    public RelojFalso Reloj { get; }

    public ContextoDeOperacion Contexto { get; }

    public RepositorioBitacoraFalso Bitacora { get; }

    public ServicioBitacora BitacoraServicio { get; }

    public RepositorioTarifasFalso TarifasRepositorio { get; }

    public RepositorioParametrosFalso ParametrosRepositorio { get; }

    public RepositorioPasajesFalso PasajesRepositorio { get; }

    public RepositorioUnidadesSalidaFalso UnidadesSalidaRepositorio { get; }

    public RepositorioPagosFalso PagosRepositorio { get; }

    public RepositorioGruposFalso GruposRepositorio { get; }

    public RepositorioBoletosFalso BoletosRepositorio { get; }

    public Usuario Administradora { get; }

    public ServicioTarifas Tarifas { get; }

    public ServicioParametros Parametros { get; }

    public HidratadorDePasajes Hidratador { get; }

    public ServicioConsultas Consultas { get; }

    public ServicioAsientos Asientos { get; }

    public ServicioReservasYVentas Ventas { get; }

    public ServicioCobros Cobros { get; }

    public ServicioGestionDePasajes Gestion { get; }

    public ServicioCuadre Cuadre { get; }

    public void CambiarActor(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        Contexto.Usuario = usuario;
        Contexto.IdUsuario = usuario.Id;
    }

    public UnidadDeSalida SembrarUnidad(
        Sentido sentido,
        DateTime fechaHoraSalida,
        int numero = 1,
        short tipoVehiculoId = 1)
    {
        var unidad = new UnidadDeSalida(
            numero,
            sentido,
            tipoVehiculoId,
            fechaHoraSalida,
            Administradora);

        UnidadesSalidaRepositorio.Sembrar(unidad);
        return UnidadesSalidaRepositorio.Unidades[^1];
    }
}
