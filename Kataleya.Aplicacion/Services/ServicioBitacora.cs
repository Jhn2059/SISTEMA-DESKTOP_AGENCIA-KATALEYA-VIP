using Kataleya.Aplicacion;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Time;
using Kataleya.Dominio.DataObjects;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioBitacora
{
    private readonly IRepositorioBitacora _repositorio;
    private readonly ContextoDeOperacion _contexto;

    public ServicioBitacora(
        IRepositorioBitacora repositorio,
        ContextoDeOperacion contexto)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public Task RegistrarAsync(RegistroDeBitacora registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        if (registro.ClaveDeOperacion is null && _contexto.ClaveDeOperacion is not null)
        {
            registro.FijarClaveDeOperacion(_contexto.ClaveDeOperacion);
        }

        return _repositorio.AgregarAsync(registro);
    }

    public Task<bool> ExisteAsync(Guid clave) =>
        _repositorio.ExisteClaveAsync(clave);

    public Task<IReadOnlyList<RegistroDeBitacora>> ConsultarAsync(
        ConsultaDeBitacora consulta)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (consulta.Desde > consulta.Hasta)
        {
            throw new ReglaDeNegocioException(
                "La fecha 'Desde' no puede ser posterior a 'Hasta'.",
                "desde");
        }

        var usuario = _contexto.Usuario
            ?? throw new InvalidOperationException("No hay un usuario autenticado.");
        var usuarioId = string.Equals(
            usuario.Rol.Nombre,
            "Administrador",
            StringComparison.OrdinalIgnoreCase)
            ? (int?)null
            : usuario.Id;
        var desdeUtc = ZonaHorariaLima.ALocalAUtc(
            consulta.Desde.ToDateTime(TimeOnly.MinValue));
        var hastaUtc = ZonaHorariaLima.ALocalAUtc(
            consulta.Hasta.AddDays(1).ToDateTime(TimeOnly.MinValue));

        return _repositorio.ListarAsync(desdeUtc, hastaUtc, usuarioId);
    }
}
