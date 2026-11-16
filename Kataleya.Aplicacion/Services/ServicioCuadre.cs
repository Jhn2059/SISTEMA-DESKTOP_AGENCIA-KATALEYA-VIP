using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Time;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioCuadre
{
    private readonly IRepositorioPagos _pagos;
    private readonly IRepositorioPasajes _pasajes;
    private readonly ContextoDeOperacion _contexto;

    public ServicioCuadre(
        IRepositorioPagos pagos,
        IRepositorioPasajes pasajes,
        ContextoDeOperacion contexto)
    {
        _pagos = pagos ?? throw new ArgumentNullException(nameof(pagos));
        _pasajes = pasajes ?? throw new ArgumentNullException(nameof(pasajes));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public async Task<Cuadre> ObtenerAsync(ConsultaDeCuadre consulta)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (consulta.Desde > consulta.Hasta)
        {
            throw new ReglaDeNegocioException(
                "La fecha 'Desde' no puede ser posterior a 'Hasta'.",
                "desde");
        }

        var emisorId = EmisorPermitido(consulta);
        var desdeUtc = ZonaHorariaLima.ALocalAUtc(
            consulta.Desde.ToDateTime(TimeOnly.MinValue));
        var hastaUtc = ZonaHorariaLima.ALocalAUtc(
            consulta.Hasta.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var movimientos = await _pagos.ListarMovimientosDeCuadreAsync(
            desdeUtc,
            hastaUtc,
            emisorId);
        var anulados = await _pasajes.ListarAnuladosEnPeriodoAsync(
            desdeUtc,
            hastaUtc,
            emisorId);
        var pendientes = await _pasajes.ListarPendientesDeSaldoAsync(emisorId);

        var acumuladores = new Dictionary<int, Acumulador>();

        Acumulador Obtener(int id, string nombre)
        {
            if (!acumuladores.TryGetValue(id, out var acumulador))
            {
                acumulador = new Acumulador(id, nombre);
                acumuladores[id] = acumulador;
            }

            return acumulador;
        }

        foreach (var movimiento in movimientos)
        {
            var acumulador = Obtener(movimiento.EmisorId, movimiento.Emisor);
            acumulador.Movimientos.Add(movimiento);

            if (movimiento.Tipo == TipoDePago.Devolucion)
            {
                acumulador.Devoluciones += movimiento.Monto;
                continue;
            }

            switch (movimiento.MedioDePago)
            {
                case MedioPago.Efectivo:
                    acumulador.CobradoEfectivo += movimiento.Monto;
                    break;
                case MedioPago.Yape:
                    acumulador.CobradoYape += movimiento.Monto;
                    break;
            }
        }

        foreach (var anulado in anulados)
        {
            var acumulador = Obtener(anulado.EmisorId, anulado.Emisor);
            acumulador.PasajesAnulados.Add(anulado);
            acumulador.ImportePasajesAnulados += anulado.Importe;
            acumulador.AdelantosRetenidos += Math.Max(0, anulado.Pagado);
        }

        foreach (var pendiente in pendientes)
        {
            var acumulador = Obtener(pendiente.EmisorId, pendiente.Emisor);
            acumulador.AdelantosPendientes.Add(pendiente);
            acumulador.ImporteAdelantosPendientes += pendiente.Saldo;
        }

        var emisores = acumuladores.Values
            .OrderBy(acumulador => acumulador.Emisor, StringComparer.OrdinalIgnoreCase)
            .ThenBy(acumulador => acumulador.EmisorId)
            .Select(acumulador => acumulador.Construir())
            .ToList();

        var totales = new TotalesDeCuadre(
            emisores.Sum(emisor => emisor.Totales.CobradoEfectivo),
            emisores.Sum(emisor => emisor.Totales.CobradoYape),
            emisores.Sum(emisor => emisor.Totales.Devoluciones),
            emisores.Sum(emisor => emisor.Totales.AdelantosRetenidos),
            emisores.Sum(emisor => emisor.Totales.CantidadPasajesAnulados),
            emisores.Sum(emisor => emisor.Totales.ImportePasajesAnulados),
            emisores.Sum(emisor => emisor.Totales.CantidadAdelantosPendientes),
            emisores.Sum(emisor => emisor.Totales.ImporteAdelantosPendientes));

        return new Cuadre(consulta.Desde, consulta.Hasta, totales, emisores);
    }

    private int? EmisorPermitido(ConsultaDeCuadre consulta)
    {
        var usuario = _contexto.Usuario
            ?? throw new InvalidOperationException(
                "No se encontró el usuario de la sesión.");

        return usuario.Rol.Nombre.Equals("Administrador", StringComparison.OrdinalIgnoreCase)
            ? consulta.EmisorId
            : usuario.Id;
    }

    private sealed class Acumulador
    {
        public Acumulador(int emisorId, string emisor)
        {
            EmisorId = emisorId;
            Emisor = emisor;
        }

        public int EmisorId { get; }

        public string Emisor { get; }

        public List<MovimientoDeCuadre> Movimientos { get; } = [];

        public List<PasajeDeCuadre> PasajesAnulados { get; } = [];

        public List<PasajeDeCuadre> AdelantosPendientes { get; } = [];

        public decimal CobradoEfectivo { get; set; }

        public decimal CobradoYape { get; set; }

        public decimal Devoluciones { get; set; }

        public decimal AdelantosRetenidos { get; set; }

        public decimal ImportePasajesAnulados { get; set; }

        public decimal ImporteAdelantosPendientes { get; set; }

        public CuadrePorEmisor Construir()
        {
            var totales = new TotalesDeCuadre(
                CobradoEfectivo,
                CobradoYape,
                Devoluciones,
                AdelantosRetenidos,
                PasajesAnulados.Count,
                ImportePasajesAnulados,
                AdelantosPendientes.Count,
                ImporteAdelantosPendientes);

            return new CuadrePorEmisor(
                EmisorId,
                Emisor,
                totales,
                Movimientos
                    .OrderBy(movimiento => movimiento.FechaPago)
                    .ThenBy(movimiento => movimiento.PagoId)
                    .ToList(),
                PasajesAnulados
                    .OrderBy(pasaje => pasaje.FechaDeAnulacion)
                    .ThenBy(pasaje => pasaje.Id)
                    .ToList(),
                AdelantosPendientes
                    .OrderBy(pasaje => pasaje.FechaHoraSalida)
                    .ThenBy(pasaje => pasaje.Id)
                    .ToList());
        }
    }
}
