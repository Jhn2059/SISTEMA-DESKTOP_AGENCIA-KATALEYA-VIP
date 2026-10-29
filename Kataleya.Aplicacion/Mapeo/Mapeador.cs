using Kataleya.Contratos.Bitacora;
using Kataleya.Contratos.Configuracion;
using Kataleya.Contratos.Cuadre;
using Kataleya.Contratos.Panel;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.Entities;

namespace Kataleya.Aplicacion.Mapeo;

public static class Mapeador
{
    public static DatosDeTarifa ADatos(this Tarifa tarifa) =>
        new()
        {
            Id = tarifa.Id,
            Tipo = tarifa.Tipo,
            Nombre = tarifa.Nombre,
            Importe = tarifa.Importe,
            VigenciaDesde = tarifa.VigenciaDesde,
            VigenciaHasta = tarifa.VigenciaHasta,
            Sentido = tarifa.Sentido,
            FijadaPor = tarifa.UsuarioNombre,
            FechaHora = tarifa.FechaHora
        };

    public static DatosDeTipoDeVehiculo ADatos(this TipoDeVehiculo tipo) =>
        new()
        {
            Id = tipo.Id,
            Nombre = tipo.Nombre,
            NumeroAsientos = tipo.NumeroAsientos
        };

    public static DatosDeParametro ADatos(this ParametroDelNegocio parametro, string? cambiadoPor = null) =>
        new()
        {
            Nombre = parametro.Nombre,
            Valor = parametro.Valor,
            CambiadoPor = cambiadoPor,
            FechaHora = parametro.FechaHora
        };

    public static DatosDePago ADatos(this Pago pago) =>
        new()
        {
            Id = pago.Id,
            Importe = pago.Importe,
            Medio = pago.Medio,
            TipoMovimiento = pago.TipoMovimiento,
            FechaHora = pago.FechaHora,
            CobradoPor = pago.UsuarioNombre
        };

    public static DatosDeBoleto ADatos(this BoletoDeViaje boleto) =>
        new()
        {
            Id = boleto.Id,
            Serie = boleto.Serie,
            Numero = boleto.Numero,
            FechaHoraEmision = boleto.FechaHoraEmision,
            Reimpresiones = boleto.Reimpresiones
        };

    public static DatosDePasaje ADatos(this Pasaje pasaje) =>
        new()
        {
            Id = pasaje.Id,
            PasajeroNombre = pasaje.PasajeroNombre,
            TipoDocumento = pasaje.TipoDocumento,
            NumeroDocumento = pasaje.NumeroDocumento,
            Telefono = pasaje.Telefono,
            Origen = pasaje.Origen,
            Destino = pasaje.Destino,
            LugarDeEmbarque = pasaje.LugarDeEmbarque,
            FechaSalida = pasaje.FechaSalida,
            HoraSalida = pasaje.HoraSalida,
            UnidadSalidaId = pasaje.UnidadSalidaId,
            Unidad = pasaje.UnidadNombre is null
                ? null
                : $"Unidad {pasaje.UnidadNombre} · asiento {pasaje.NumeroAsiento}",
            NumeroAsiento = pasaje.NumeroAsiento,
            GrupoId = pasaje.GrupoId,
            GrupoCodigo = pasaje.GrupoCodigo,
            EsResponsable = pasaje.EsResponsable,
            Importe = pasaje.Importe,
            Pagado = pasaje.Pagado,
            Saldo = pasaje.Saldo,
            Estado = pasaje.Estado,
            Sede = pasaje.SedeNombre,
            Emisor = pasaje.EmisorNombre,
            FechaHoraRegistro = pasaje.FechaHoraRegistro,
            TipoRegistro = pasaje.TipoRegistro,
            HoraEmisionReal = pasaje.HoraEmisionReal,
            NumeroTalonario = pasaje.NumeroTalonario,
            MotivoAnulacion = pasaje.MotivoAnulacion,
            NombrePadre = pasaje.NombrePadre,
            NombreMadre = pasaje.NombreMadre,
            MotivoViaje = pasaje.MotivoViaje,
            Boleto = pasaje.Boleto?.ADatos(),
            Pagos = pasaje.Pagos.Select(p => p.ADatos()).ToList()
        };

    public static DatosDeUnidad ADatos(this UnidadDeSalida unidad) =>
        new()
        {
            Id = unidad.Id,
            Numero = unidad.Numero,
            TipoVehiculo = unidad.TipoVehiculoNombre,
            NumeroAsientos = unidad.NumeroAsientos,
            Descripcion = unidad.Descripcion,
            FechaHoraSalida = unidad.FechaHoraSalida,
            GrupoId = unidad.GrupoId,
            AsientosOcupados = unidad.AsientosOcupados
        };

    public static DatosDeGrupo ADatos(this Grupo grupo) =>
        new()
        {
            Id = grupo.Id,
            Codigo = grupo.Codigo,
            Telefono = grupo.Telefono,
            PrecioAcordado = grupo.PrecioAcordado,
            Pagado = grupo.Pagado,
            Saldo = Math.Max(0, grupo.PrecioAcordado - grupo.Pagado),
            Origen = grupo.Origen,
            Destino = grupo.Destino,
            LugarDeRecojo = grupo.LugarDeRecojo,
            FechaSalida = grupo.FechaSalida,
            HoraSalida = grupo.HoraSalida,
            Unidades = grupo.Unidades.Select(u => u.ADatos()).ToList()
        };

    public static DatosDeBitacora ADatos(this EntradaDeBitacora entrada) =>
        new()
        {
            Id = entrada.Id,
            FechaHora = entrada.FechaHora,
            Usuario = entrada.UsuarioNombre,
            Operacion = entrada.Operacion,
            PasajeId = entrada.PasajeId,
            ValorAnterior = entrada.ValorAnterior,
            ValorNuevo = entrada.ValorNuevo
        };

    public static DatosDeFilaDeCuadre ADatos(this FilaDeCuadre fila) =>
        new()
        {
            EmisorId = fila.EmisorId,
            Emisor = fila.EmisorNombre,
            Pasajes = fila.Pasajes,
            Efectivo = fila.Efectivo,
            Yape = fila.Yape,
            AdelantosPorCobrar = fila.AdelantosPorCobrar,
            Anulados = fila.Anulados,
            ImporteAnulado = fila.ImporteAnulado,
            TotalNeto = fila.TotalNeto
        };

    public static DatosDePasajeDelCuadre ADatos(this PasajeDelCuadre pasaje) =>
        new()
        {
            Id = pasaje.Id,
            Pasajero = pasaje.PasajeroNombre,
            Documento = pasaje.NumeroDocumento,
            Emisor = pasaje.EmisorNombre,
            Estado = pasaje.Estado,
            Importe = pasaje.Importe,
            Pagado = pasaje.Pagado,
            Boleto = pasaje.Boleto
        };

    public static DatosDelCuadre ADatos(this DetalleDelCuadre detalle) =>
        new()
        {
            Filas = detalle.Filas.Select(f => f.ADatos()).ToList(),
            Pasajes = detalle.Pasajes.Select(p => p.ADatos()).ToList(),
            GeneradoEl = detalle.GeneradoEl,
            GeneradoPor = detalle.GeneradoPor,
            Sede = detalle.Sede
        };

    public static DatosDelPanel ADatos(this ResumenDelPanel resumen, DateOnly fecha) =>
        new()
        {
            Fecha = fecha,
            ReservasRegistradas = resumen.ReservasRegistradas,
            Ventas = resumen.Ventas,
            CobradoEnEfectivo = resumen.CobradoEnEfectivo,
            CobradoPorYape = resumen.CobradoPorYape,
            AdelantosPorCobrar = resumen.AdelantosPorCobrar,
            Horarios = resumen.Horarios.Select(h => new DatosDeHorario
            {
                Hora = h.Hora.ToString("HH:mm"),
                Pasajes = h.Pasajes,
                Pagados = h.Pagados,
                Reservados = h.Reservados,
                ConAdelanto = h.ConAdelanto
            }).ToList()
        };
}
