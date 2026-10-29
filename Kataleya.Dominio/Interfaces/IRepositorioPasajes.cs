using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;

namespace Kataleya.Dominio.Interfaces;

public interface IRepositorioPasajes
{
    Task<Pasaje?> BuscarPorIdAsync(long id);

    Task<IReadOnlyList<Pasaje>> ConsultarAsync(FiltroDePasajes filtro);

    Task<long> AgregarAsync(Pasaje pasaje);

    Task ActualizarAsync(Pasaje pasaje);

    Task<IReadOnlyList<Pago>> ListarPagosAsync(long pasajeId);

    Task<decimal> TotalPagadoAsync(long? pasajeId, long? grupoId);

    Task AgregarPagoAsync(Pago pago);

    Task<Grupo?> BuscarGrupoAsync(long id);

    Task<long> AgregarGrupoAsync(Grupo grupo);

    Task ActualizarGrupoAsync(Grupo grupo);

    Task<IReadOnlyList<Pasaje>> ListarPasajesDelGrupoAsync(long grupoId);

    Task<IReadOnlyList<Pago>> ListarPagosDelGrupoAsync(long grupoId);

    Task<(string Serie, long Numero)> SiguienteBoletoAsync(short sedeId);

    Task AgregarBoletoAsync(BoletoDeViaje boleto);

    Task<BoletoDeViaje?> BuscarBoletoAsync(string serie, long numero);

    Task<BoletoDeViaje?> BuscarBoletoDelPasajeAsync(long pasajeId);

    Task RegistrarReimpresionAsync(long boletoId);

    Task<bool> ExisteTalonarioAsync(string numero);

    Task<bool> ExisteBoletoFisicoAsync(string serie, long numero);

    Task<IReadOnlyList<Pasaje>> BuscarDelAnioAsync(int anio);

    Task<int> EliminarDelAnioAsync(int anio);
}

public interface IRepositorioUnidades
{
    Task<IReadOnlyList<UnidadDeSalida>> ListarDeLaSalidaAsync(DateTime fechaHora, SentidoDelViaje sentido);

    Task<UnidadDeSalida?> BuscarAsync(long id);

    Task<long> AgregarAsync(UnidadDeSalida unidad);

    Task<IReadOnlyList<UnidadDeSalida>> ListarDelGrupoAsync(long grupoId);

    Task<IReadOnlyList<short>> AsientosOcupadosAsync(long unidadId);
}
