using System.Text.Json;
using Kataleya.Aplicacion.Excepciones;
using Kataleya.Aplicacion.Mapeo;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;

namespace Kataleya.Aplicacion.Services;

public sealed class ServicioPasajes
{
    private readonly IRepositorioPasajes _pasajes;
    private readonly IRepositorioUnidades _unidades;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IRepositorioConfiguracion _configuracion;
    private readonly ServicioConfiguracion _servicioConfiguracion;
    private readonly ServicioBitacora _bitacora;
    private readonly RegistradorDeBitacora _registrador;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;
    private readonly ContextoDeOperacion _contexto;

    private Usuario? _emisor;

    public ServicioPasajes(
        IRepositorioPasajes pasajes,
        IRepositorioUnidades unidades,
        IRepositorioUsuarios usuarios,
        IRepositorioConfiguracion configuracion,
        ServicioConfiguracion servicioConfiguracion,
        ServicioBitacora bitacora,
        RegistradorDeBitacora registrador,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj,
        ContextoDeOperacion contexto)
    {
        _pasajes = pasajes ?? throw new ArgumentNullException(nameof(pasajes));
        _unidades = unidades ?? throw new ArgumentNullException(nameof(unidades));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        _servicioConfiguracion = servicioConfiguracion ?? throw new ArgumentNullException(nameof(servicioConfiguracion));
        _bitacora = bitacora ?? throw new ArgumentNullException(nameof(bitacora));
        _registrador = registrador ?? throw new ArgumentNullException(nameof(registrador));
        _unidadDeTrabajo = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public async Task<DatosDePasaje> RegistrarReservaAsync(SolicitudDePasaje solicitud)
    {
        var repetida = await BuscarRepeticionAsync();
        if (repetida is not null)
        {
            return repetida;
        }

        var emisor = await ObtenerEmisorAsync();
        var preparado = await PrepararAsync(solicitud, emisor, permitirPasado: false);
        var monto = solicitud.Monto ?? 0m;

        if (monto < 0 || monto > preparado.Importe)
        {
            throw new ReglaDeNegocioException(
                "El adelanto no puede superar el importe del pasaje.",
                "monto");
        }

        preparado.Pasaje.Estado = CalcularEstado(monto, preparado.Importe);

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _pasajes.AgregarAsync(preparado.Pasaje);

            if (monto > 0)
            {
                await AgregarPagoAsync(
                    preparado.Pasaje.Id,
                    null,
                    monto,
                    solicitud.Medio ?? MedioDePago.Efectivo,
                    emisor);
            }

            await _registrador.RegistrarAsync(
                "Registrar reserva",
                null,
                JsonSerializer.Serialize(new
                {
                    preparado.Pasaje.PasajeroNombre,
                    preparado.Pasaje.NumeroDocumento,
                    preparado.Pasaje.FechaSalida,
                    preparado.Pasaje.HoraSalida,
                    preparado.Pasaje.NumeroAsiento,
                    Monto = monto
                }),
                preparado.Pasaje.Id);
        });

        return await ObtenerAsync(preparado.Pasaje.Id);
    }

    public async Task<DatosDePasaje> RegistrarVentaAsync(SolicitudDePasaje solicitud)
    {
        var repetida = await BuscarRepeticionAsync();
        if (repetida is not null)
        {
            return repetida;
        }

        var emisor = await ObtenerEmisorAsync();
        var preparado = await PrepararAsync(solicitud, emisor, permitirPasado: false);
        var monto = solicitud.Monto ?? preparado.Importe;

        if (Math.Abs(monto - preparado.Importe) > 0.005m)
        {
            throw new ReglaDeNegocioException(
                "Una venta debe cobrarse por el importe total del pasaje.",
                "monto");
        }

        preparado.Pasaje.Estado = EstadoPasaje.Pagado;

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _pasajes.AgregarAsync(preparado.Pasaje);
            await AgregarPagoAsync(
                preparado.Pasaje.Id,
                null,
                monto,
                solicitud.Medio ?? MedioDePago.Efectivo,
                emisor);
            await EmitirBoletoAsync(preparado.Pasaje, emisor);

            await _registrador.RegistrarAsync(
                "Registrar venta",
                null,
                JsonSerializer.Serialize(new
                {
                    preparado.Pasaje.PasajeroNombre,
                    preparado.Pasaje.NumeroDocumento,
                    preparado.Pasaje.FechaSalida,
                    preparado.Pasaje.HoraSalida,
                    preparado.Pasaje.NumeroAsiento,
                    Monto = monto
                }),
                preparado.Pasaje.Id);
        });

        return await ObtenerAsync(preparado.Pasaje.Id);
    }

    public async Task<DatosDePasaje> RegistrarDiferidoAsync(SolicitudDePasaje solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var repetida = await BuscarRepeticionAsync();
        if (repetida is not null)
        {
            return repetida;
        }

        if (string.IsNullOrWhiteSpace(solicitud.NumeroTalonario))
        {
            throw new ReglaDeNegocioException(
                "Escriba el número de talonario del boleto físico.",
                "numeroTalonario");
        }

        var talonario = solicitud.NumeroTalonario.Trim();

        if (await _pasajes.ExisteTalonarioAsync(talonario))
        {
            throw new ReglaDeNegocioException(
                "Ese número de talonario ya fue registrado.",
                "numeroTalonario");
        }

        if (solicitud.HoraEmisionReal is null)
        {
            throw new ReglaDeNegocioException(
                "Indique la hora real de emisión del boleto.",
                "horaEmisionReal");
        }

        if (solicitud.HoraEmisionReal.Value > _reloj.Ahora)
        {
            throw new ReglaDeNegocioException(
                "La hora real de emisión no puede estar en el futuro.",
                "horaEmisionReal");
        }

        var emisor = await ObtenerEmisorAsync();
        var preparado = await PrepararAsync(solicitud, emisor, permitirPasado: true);
        var monto = solicitud.Monto ?? preparado.Importe;

        if (Math.Abs(monto - preparado.Importe) > 0.005m)
        {
            throw new ReglaDeNegocioException(
                "El registro diferido debe quedar pagado en su totalidad.",
                "monto");
        }

        preparado.Pasaje.Estado = EstadoPasaje.Pagado;
        preparado.Pasaje.TipoRegistro = TipoRegistroDePasaje.Diferido;
        preparado.Pasaje.NumeroTalonario = talonario;
        preparado.Pasaje.HoraEmisionReal = solicitud.HoraEmisionReal;

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _pasajes.AgregarAsync(preparado.Pasaje);
            await AgregarPagoAsync(
                preparado.Pasaje.Id,
                null,
                monto,
                solicitud.Medio ?? MedioDePago.Efectivo,
                emisor);

            await _registrador.RegistrarAsync(
                "Registrar pasaje diferido",
                null,
                JsonSerializer.Serialize(new
                {
                    preparado.Pasaje.PasajeroNombre,
                    preparado.Pasaje.NumeroDocumento,
                    preparado.Pasaje.FechaSalida,
                    preparado.Pasaje.HoraSalida,
                    Talonario = talonario,
                    preparado.Pasaje.HoraEmisionReal
                }),
                preparado.Pasaje.Id);
        });

        return await ObtenerAsync(preparado.Pasaje.Id);
    }

    public async Task<DatosDeGrupo> RegistrarGrupoAsync(SolicitudDeGrupo solicitud, bool esVenta)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var repetida = await BuscarRepeticionDeGrupoAsync();
        if (repetida is not null)
        {
            return repetida;
        }

        if (string.IsNullOrWhiteSpace(solicitud.Telefono))
        {
            throw new ReglaDeNegocioException("Escriba el teléfono de contacto del grupo.", "telefono");
        }

        if (solicitud.PrecioAcordado <= 0)
        {
            throw new ReglaDeNegocioException(
                "El precio acordado debe ser mayor que cero.",
                "precioAcordado");
        }

        if (solicitud.Integrantes.Count == 0)
        {
            throw new ReglaDeNegocioException(
                "Agregue al menos un integrante al grupo.",
                "integrantes");
        }

        if (solicitud.Integrantes.Count(i => i.EsResponsable) != 1)
        {
            throw new ReglaDeNegocioException(
                "Debe haber un único responsable del grupo.",
                "integrantes");
        }

        if (solicitud.Unidades.Count == 0)
        {
            throw new ReglaDeNegocioException(
                "Agregue al menos una unidad para el grupo.",
                "unidades");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(solicitud.Origen);
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitud.Destino);
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitud.LugarDeRecojo);

        var tipos = new List<TipoDeVehiculo>();
        var capacidad = 0;

        foreach (var item in solicitud.Unidades)
        {
            var tipo = await _configuracion.BuscarTipoDeVehiculoAsync(item.TipoVehiculoId)
                ?? throw new ReglaDeNegocioException(
                    "Una de las unidades tiene un tipo de vehículo inválido.",
                    "unidades");
            tipos.Add(tipo);
            capacidad += tipo.NumeroAsientos;
        }

        if (capacidad < solicitud.Integrantes.Count)
        {
            throw new ReglaDeNegocioException(
                "Las unidades no tienen asientos suficientes para todos los integrantes.",
                "unidades");
        }

        foreach (var integrante in solicitud.Integrantes)
        {
            ValidarIntegrante(integrante);
        }

        if (esVenta && FechasLocales.Combinar(solicitud.FechaSalida, solicitud.HoraSalida) < _reloj.Ahora)
        {
            throw new ReglaDeNegocioException(
                "La salida del grupo no puede estar en el pasado.",
                "fechaSalida");
        }

        var monto = solicitud.Monto ?? (esVenta ? solicitud.PrecioAcordado : 0m);

        if (monto < 0 || monto > solicitud.PrecioAcordado)
        {
            throw new ReglaDeNegocioException(
                "El adelanto no puede superar el precio acordado.",
                "monto");
        }

        if (esVenta && Math.Abs(monto - solicitud.PrecioAcordado) > 0.005m)
        {
            throw new ReglaDeNegocioException(
                "Una venta de grupo debe cobrarse por el precio acordado completo.",
                "monto");
        }

        var emisor = await ObtenerEmisorAsync();
        var estado = CalcularEstado(monto, solicitud.PrecioAcordado);
        Grupo? creado = null;
        long responsableId = 0;

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            creado = new Grupo
            {
                Telefono = solicitud.Telefono.Trim(),
                PrecioAcordado = solicitud.PrecioAcordado,
                Origen = solicitud.Origen.Trim(),
                Destino = solicitud.Destino.Trim(),
                LugarDeRecojo = solicitud.LugarDeRecojo.Trim(),
                FechaSalida = solicitud.FechaSalida,
                HoraSalida = solicitud.HoraSalida,
                UsuarioId = emisor.Id,
                UsuarioNombre = emisor.NombreUsuario,
                FechaHora = _reloj.Ahora
            };

            await _pasajes.AgregarGrupoAsync(creado);

            for (var i = 0; i < tipos.Count; i++)
            {
                var tipo = tipos[i];
                await _unidades.AgregarAsync(new UnidadDeSalida
                {
                    FechaHoraSalida = FechasLocales.Combinar(solicitud.FechaSalida, solicitud.HoraSalida),
                    Numero = $"G{creado.Id}-{i + 1}",
                    TipoVehiculoId = tipo.Id,
                    TipoVehiculoNombre = tipo.Nombre,
                    NumeroAsientos = tipo.NumeroAsientos,
                    GrupoId = creado.Id
                });
            }

            if (monto > 0)
            {
                await AgregarPagoAsync(
                    null,
                    creado.Id,
                    monto,
                    solicitud.Medio ?? MedioDePago.Efectivo,
                    emisor);
            }

            var pasajesCreados = new List<Pasaje>();

            foreach (var integrante in solicitud.Integrantes)
            {
                var pasaje = new Pasaje
                {
                    PasajeroNombre = integrante.Nombre.Trim(),
                    TipoDocumento = integrante.TipoDocumento,
                    NumeroDocumento = integrante.NumeroDocumento.Trim(),
                    Telefono = solicitud.Telefono.Trim(),
                    Origen = creado.Origen,
                    Destino = creado.Destino,
                    LugarDeEmbarque = creado.LugarDeRecojo,
                    FechaSalida = creado.FechaSalida,
                    HoraSalida = creado.HoraSalida,
                    GrupoId = creado.Id,
                    GrupoCodigo = creado.Codigo,
                    EsResponsable = integrante.EsResponsable,
                    Importe = integrante.EsResponsable ? creado.PrecioAcordado : 0m,
                    Estado = estado,
                    SedeId = emisor.Sede.Id,
                    SedeNombre = emisor.Sede.Nombre,
                    EmisorId = emisor.Id,
                    EmisorNombre = emisor.NombreUsuario,
                    FechaHoraRegistro = _reloj.Ahora,
                    TipoRegistro = TipoRegistroDePasaje.Normal,
                    NombrePadre = integrante.NombrePadre?.Trim(),
                    NombreMadre = integrante.NombreMadre?.Trim(),
                    MotivoViaje = integrante.MotivoViaje?.Trim()
                };

                await _pasajes.AgregarAsync(pasaje);
                pasajesCreados.Add(pasaje);

                if (integrante.EsResponsable)
                {
                    responsableId = pasaje.Id;
                }
            }

            if (estado == EstadoPasaje.Pagado)
            {
                foreach (var pasaje in pasajesCreados)
                {
                    await EmitirBoletoAsync(pasaje, emisor);
                }
            }

            await _registrador.RegistrarAsync(
                esVenta ? "Registrar venta de grupo" : "Registrar reserva de grupo",
                null,
                JsonSerializer.Serialize(new
                {
                    creado.Codigo,
                    creado.Telefono,
                    creado.PrecioAcordado,
                    Integrantes = solicitud.Integrantes.Count,
                    Unidades = tipos.Count,
                    Monto = monto
                }),
                responsableId);
        });

        return await ObtenerGrupoAsync(creado?.Id
            ?? throw new InvalidOperationException("No se encontró el grupo recién creado."));
    }

    public async Task<DatosDePasaje> RegistrarCobroAsync(long id, SolicitudDeCobro solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.Importe <= 0)
        {
            throw new ReglaDeNegocioException("El importe del cobro debe ser mayor que cero.", "importe");
        }

        var pasaje = await ObtenerEntidadAsync(id);

        if (pasaje.Estado is EstadoPasaje.Anulado or EstadoPasaje.NoSePresento)
        {
            throw new ReglaDeNegocioException(
                "No se puede cobrar un pasaje anulado o marcado como no presentado.",
                "estado");
        }

        var emisor = await ObtenerEmisorAsync();
        var grupo = pasaje.GrupoId is null ? null : await _pasajes.BuscarGrupoAsync(pasaje.GrupoId.Value);
        var importe = grupo?.PrecioAcordado ?? pasaje.Importe;
        var pagado = await _pasajes.TotalPagadoAsync(grupo is null ? pasaje.Id : null, grupo?.Id);
        var saldo = Math.Max(0, importe - pagado);

        if (solicitud.Importe > saldo)
        {
            throw new ReglaDeNegocioException(
                $"El cobro supera el saldo pendiente de {saldo:0.00}.",
                "importe");
        }

        var nuevoPagado = pagado + solicitud.Importe;
        var nuevoEstado = CalcularEstado(nuevoPagado, importe);
        var estadoAnterior = pasaje.Estado;

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await AgregarPagoAsync(
                grupo is null ? pasaje.Id : null,
                grupo?.Id,
                solicitud.Importe,
                solicitud.Medio,
                emisor);

            if (grupo is null)
            {
                pasaje.Estado = nuevoEstado;
                await _pasajes.ActualizarAsync(pasaje);

                if (nuevoEstado == EstadoPasaje.Pagado)
                {
                    await EmitirBoletoAsync(pasaje, emisor);
                }
            }
            else
            {
                foreach (var miembro in await _pasajes.ListarPasajesDelGrupoAsync(grupo.Id))
                {
                    miembro.Estado = nuevoEstado;
                    await _pasajes.ActualizarAsync(miembro);

                    if (nuevoEstado == EstadoPasaje.Pagado)
                    {
                        await EmitirBoletoAsync(miembro, emisor);
                    }
                }
            }

            await _registrador.RegistrarAsync(
                "Registrar cobro",
                JsonSerializer.Serialize(new { Pagado = pagado, Estado = estadoAnterior.ToString() }),
                JsonSerializer.Serialize(new { Pagado = nuevoPagado, Estado = nuevoEstado.ToString() }),
                pasaje.Id);
        });

        return await ObtenerAsync(pasaje.Id);
    }

    public async Task<RevisionDeAnulacion> RevisarAnulacionAsync(long id)
    {
        var pasaje = await ObtenerEntidadAsync(id);

        if (pasaje.Estado == EstadoPasaje.Anulado)
        {
            return new RevisionDeAnulacion
            {
                Advertencias = { "El pasaje ya está anulado." }
            };
        }

        var pagado = await _pasajes.TotalPagadoAsync(
            pasaje.GrupoId is null ? pasaje.Id : null,
            pasaje.GrupoId);
        var horas = (FechasLocales.Combinar(pasaje.FechaSalida, pasaje.HoraSalida) - _reloj.Ahora).TotalHours;
        var aviso = await ObtenerHorasDeAvisoAsync();
        var seDevuelve = pagado > 0 && horas >= aviso;

        var advertencias = new List<string>();

        if (horas < 0)
        {
            advertencias.Add("La salida ya ocurrió.");
        }
        else
        {
            advertencias.Add($"Faltan {Math.Floor(horas)} horas para la salida.");
        }

        if (pagado <= 0)
        {
            advertencias.Add("El pasaje no tiene adelantos ni pagos registrados.");
        }
        else if (seDevuelve)
        {
            advertencias.Add($"Se devolverá {pagado:0.00} al pasajero.");
        }
        else
        {
            advertencias.Add(
                $"Con menos de {aviso} horas de aviso, el importe de {pagado:0.00} se retiene.");
        }

        return new RevisionDeAnulacion
        {
            Advertencias = advertencias,
            SeDevuelveElAdelanto = seDevuelve,
            ImporteADevolver = seDevuelve ? pagado : 0m,
            HorasParaLaSalida = (int)Math.Floor(horas)
        };
    }

    public async Task<DatosDePasaje> AnularAsync(long id, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ReglaDeNegocioException("Escriba el motivo de la anulación.", "motivo");
        }

        var pasaje = await ObtenerEntidadAsync(id);

        if (pasaje.Estado == EstadoPasaje.Anulado)
        {
            throw new ReglaDeNegocioException("El pasaje ya está anulado.", "estado");
        }

        var emisor = await ObtenerEmisorAsync();
        var grupo = pasaje.GrupoId is null ? null : await _pasajes.BuscarGrupoAsync(pasaje.GrupoId.Value);
        var pagado = await _pasajes.TotalPagadoAsync(grupo is null ? pasaje.Id : null, grupo?.Id);
        var horas = (FechasLocales.Combinar(pasaje.FechaSalida, pasaje.HoraSalida) - _reloj.Ahora).TotalHours;
        var aviso = await ObtenerHorasDeAvisoAsync();
        var seDevuelve = pagado > 0 && horas >= aviso;
        var estadoAnterior = pasaje.Estado;

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            if (seDevuelve)
            {
                await AgregarPagoAsync(
                    grupo is null ? pasaje.Id : null,
                    grupo?.Id,
                    pagado,
                    MedioDePago.Efectivo,
                    emisor,
                    TipoDeMovimiento.Devolucion);
            }

            if (grupo is null)
            {
                pasaje.Estado = EstadoPasaje.Anulado;
                pasaje.MotivoAnulacion = motivo.Trim();
                await _pasajes.ActualizarAsync(pasaje);
            }
            else
            {
                foreach (var miembro in await _pasajes.ListarPasajesDelGrupoAsync(grupo.Id))
                {
                    miembro.Estado = EstadoPasaje.Anulado;
                    miembro.MotivoAnulacion = motivo.Trim();
                    await _pasajes.ActualizarAsync(miembro);
                }
            }

            await _registrador.RegistrarAsync(
                "Anular pasaje",
                JsonSerializer.Serialize(new { Estado = estadoAnterior.ToString(), Pagado = pagado }),
                JsonSerializer.Serialize(new
                {
                    Estado = nameof(EstadoPasaje.Anulado),
                    Motivo = motivo.Trim(),
                    Devuelto = seDevuelve ? pagado : 0m
                }),
                pasaje.Id);
        });

        return await ObtenerAsync(pasaje.Id);
    }

    public async Task<DatosDePasaje> MarcarNoSePresentoAsync(long id)
    {
        var pasaje = await ObtenerEntidadAsync(id);

        if (pasaje.Estado == EstadoPasaje.Anulado)
        {
            throw new ReglaDeNegocioException("No se puede marcar un pasaje anulado.", "estado");
        }

        if (FechasLocales.Combinar(pasaje.FechaSalida, pasaje.HoraSalida) > _reloj.Ahora)
        {
            throw new ReglaDeNegocioException(
                "Solo se puede marcar después de la hora de salida.",
                "salida");
        }

        var emisor = await ObtenerEmisorAsync();

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            if (pasaje.GrupoId is null)
            {
                pasaje.Estado = EstadoPasaje.NoSePresento;
                await _pasajes.ActualizarAsync(pasaje);
            }
            else
            {
                foreach (var miembro in await _pasajes.ListarPasajesDelGrupoAsync(pasaje.GrupoId.Value))
                {
                    miembro.Estado = EstadoPasaje.NoSePresento;
                    await _pasajes.ActualizarAsync(miembro);
                }
            }

            await _registrador.RegistrarAsync(
                "Marcar no presentado",
                null,
                JsonSerializer.Serialize(new
                {
                    Emisor = emisor.NombreUsuario,
                    Estado = nameof(EstadoPasaje.NoSePresento)
                }),
                pasaje.Id);
        });

        return await ObtenerAsync(pasaje.Id);
    }

    public async Task<DatosDePasaje> ModificarAsync(long id, CambiosDelPasaje cambios)
    {
        ArgumentNullException.ThrowIfNull(cambios);

        var pasaje = await ObtenerEntidadAsync(id);

        if (pasaje.GrupoId is not null)
        {
            throw new ReglaDeNegocioException(
                "Los pasajes de grupo se gestionan desde el grupo.",
                "grupo");
        }

        if (pasaje.Estado is EstadoPasaje.Anulado or EstadoPasaje.NoSePresento)
        {
            throw new ReglaDeNegocioException(
                "No se puede modificar un pasaje anulado o no presentado.",
                "estado");
        }

        var emisor = await ObtenerEmisorAsync();
        var anterior = JsonSerializer.Serialize(new
        {
            pasaje.PasajeroNombre,
            pasaje.Telefono,
            pasaje.FechaSalida,
            pasaje.HoraSalida,
            pasaje.UnidadSalidaId,
            pasaje.NumeroAsiento,
            pasaje.LugarDeEmbarque
        });

        if (!string.IsNullOrWhiteSpace(cambios.PasajeroNombre))
        {
            pasaje.PasajeroNombre = cambios.PasajeroNombre.Trim();
        }

        if (cambios.Telefono is not null)
        {
            pasaje.Telefono = string.IsNullOrWhiteSpace(cambios.Telefono)
                ? null
                : cambios.Telefono.Trim();
        }

        if (cambios.LugarDeEmbarque is not null)
        {
            if (string.IsNullOrWhiteSpace(cambios.LugarDeEmbarque))
            {
                throw new ReglaDeNegocioException(
                    "Escriba el lugar de embarque.",
                    "lugarDeEmbarque");
            }

            pasaje.LugarDeEmbarque = cambios.LugarDeEmbarque.Trim();
        }

        var nuevaFecha = cambios.FechaSalida ?? pasaje.FechaSalida;
        var nuevaHora = cambios.HoraSalida ?? pasaje.HoraSalida;
        var nuevaUnidadId = cambios.UnidadSalidaId ?? pasaje.UnidadSalidaId;
        var nuevoAsiento = cambios.NumeroAsiento ?? pasaje.NumeroAsiento;

        if (nuevaUnidadId is null)
        {
            throw new ReglaDeNegocioException(
                "Seleccione una unidad para el pasaje.",
                "unidadSalidaId");
        }

        var unidad = await _unidades.BuscarAsync(nuevaUnidadId.Value)
            ?? throw new ReglaDeNegocioException(
                "La unidad seleccionada ya no existe.",
                "unidadSalidaId");

        if (unidad.FechaHoraSalida != FechasLocales.Combinar(nuevaFecha, nuevaHora))
        {
            throw new ReglaDeNegocioException(
                "La unidad no corresponde a la fecha y hora de salida.",
                "unidadSalidaId");
        }

        if (nuevoAsiento is null || nuevoAsiento < 1 || nuevoAsiento > unidad.NumeroAsientos)
        {
            throw new ReglaDeNegocioException(
                "El asiento elegido no existe en la unidad.",
                "numeroAsiento");
        }

        if (unidad.Id != pasaje.UnidadSalidaId || nuevoAsiento != pasaje.NumeroAsiento)
        {
            var ocupados = await _unidades.AsientosOcupadosAsync(unidad.Id);
            if (ocupados.Contains(nuevoAsiento.Value))
            {
                throw new ReglaDeNegocioException(
                    "El asiento elegido ya está ocupado.",
                    "numeroAsiento");
            }
        }

        pasaje.FechaSalida = nuevaFecha;
        pasaje.HoraSalida = nuevaHora;
        pasaje.UnidadSalidaId = unidad.Id;
        pasaje.UnidadNombre = unidad.Numero;
        pasaje.NumeroAsiento = nuevoAsiento;

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _pasajes.ActualizarAsync(pasaje);
            await _registrador.RegistrarAsync(
                "Modificar pasaje",
                anterior,
                JsonSerializer.Serialize(new
                {
                    pasaje.PasajeroNombre,
                    pasaje.Telefono,
                    pasaje.FechaSalida,
                    pasaje.HoraSalida,
                    pasaje.UnidadSalidaId,
                    pasaje.NumeroAsiento,
                    pasaje.LugarDeEmbarque,
                    Emisor = emisor.NombreUsuario
                }),
                pasaje.Id);
        });

        return await ObtenerAsync(pasaje.Id);
    }

    public async Task<DatosDePasaje> ObtenerAsync(long id) =>
        (await ObtenerEntidadAsync(id)).ADatos();

    public async Task<DatosDePasaje?> BuscarAsync(long id)
    {
        var pasaje = await _pasajes.BuscarPorIdAsync(id);
        return pasaje?.ADatos();
    }

    public async Task<IReadOnlyList<DatosDePasaje>> ConsultarAsync(Contratos.Pasajes.FiltroDePasajes filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var dominio = new Dominio.Entities.FiltroDePasajes
        {
            Desde = filtro.Desde,
            Hasta = filtro.Hasta,
            Hora = filtro.Hora,
            Estado = filtro.Estado,
            Tipo = filtro.Tipo,
            Pasajero = filtro.Pasajero,
            EmisorId = filtro.EmisorId,
            SedeId = filtro.SedeId
        };

        var pasajes = await _pasajes.ConsultarAsync(dominio);
        return pasajes.Select(p => p.ADatos()).ToList();
    }

    public async Task<DatosDeGrupo> ObtenerGrupoAsync(long id)
    {
        var grupo = await _pasajes.BuscarGrupoAsync(id)
            ?? throw new ReglaDeNegocioException("El grupo ya no existe.", "id");

        grupo.Unidades = (await _unidades.ListarDelGrupoAsync(id)).ToList();
        var pasajes = await _pasajes.ListarPasajesDelGrupoAsync(id);
        var pagos = await _pasajes.ListarPagosDelGrupoAsync(id);

        var datos = grupo.ADatos();
        datos.Unidades = grupo.Unidades.Select(u => u.ADatos()).ToList();
        datos.Pasajes = pasajes.Select(p => p.ADatos()).ToList();
        datos.Pagos = pagos.Select(p => p.ADatos()).ToList();
        return datos;
    }

    public async Task<DatosDePasaje> RegistrarReimpresionAsync(string serie, long numero)
    {
        var boleto = await _pasajes.BuscarBoletoAsync(serie, numero)
            ?? throw new ReglaDeNegocioException(
                "No se encontró el boleto indicado.",
                "boleto");

        var emisor = await ObtenerEmisorAsync();

        await _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            await _pasajes.RegistrarReimpresionAsync(boleto.Id);
            await _registrador.RegistrarAsync(
                "Reimprimir boleto",
                null,
                JsonSerializer.Serialize(new { boleto.Serie, boleto.Numero, Emisor = emisor.NombreUsuario }),
                boleto.PasajeId);
        });

        var actualizado = await _pasajes.BuscarBoletoAsync(serie, numero)
            ?? throw new InvalidOperationException("No se encontró el boleto reimpreso.");

        var pasaje = await ObtenerEntidadAsync(actualizado.PasajeId);
        return pasaje.ADatos();
    }

    public async Task RegistrarFallaDeBoletoAsync(string serie, long numero, string descripcion)
    {
        var boleto = await _pasajes.BuscarBoletoAsync(serie, numero)
            ?? throw new ReglaDeNegocioException(
                "No se encontró el boleto indicado.",
                "boleto");

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            throw new ReglaDeNegocioException(
                "Describa la falla del boleto.",
                "descripcion");
        }

        await _registrador.RegistrarAsync(
            "Reportar falla de boleto",
            null,
            JsonSerializer.Serialize(new { boleto.Serie, boleto.Numero, Descripcion = descripcion.Trim() }),
            boleto.PasajeId);
    }

    private async Task<(Pasaje Pasaje, decimal Importe)> PrepararAsync(
        SolicitudDePasaje solicitud,
        Usuario emisor,
        bool permitirPasado)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (string.IsNullOrWhiteSpace(solicitud.PasajeroNombre))
        {
            throw new ReglaDeNegocioException("Escriba el nombre del pasajero.", "pasajeroNombre");
        }

        if (solicitud.PasajeroNombre.Trim().Length > 120)
        {
            throw new ReglaDeNegocioException(
                "El nombre no puede superar los 120 caracteres.",
                "pasajeroNombre");
        }

        if (string.IsNullOrWhiteSpace(solicitud.NumeroDocumento))
        {
            throw new ReglaDeNegocioException("Escriba el número de documento.", "numeroDocumento");
        }

        if (solicitud.NumeroDocumento.Trim().Length > 20)
        {
            throw new ReglaDeNegocioException(
                "El documento no puede superar los 20 caracteres.",
                "numeroDocumento");
        }

        if (string.IsNullOrWhiteSpace(solicitud.Origen) ||
            string.IsNullOrWhiteSpace(solicitud.Destino) ||
            string.IsNullOrWhiteSpace(solicitud.LugarDeEmbarque))
        {
            throw new ReglaDeNegocioException(
                "Complete el origen, el destino y el lugar de embarque.",
                "ruta");
        }

        if (solicitud.MenorSinPadres &&
            (string.IsNullOrWhiteSpace(solicitud.NombrePadre) ||
             string.IsNullOrWhiteSpace(solicitud.NombreMadre) ||
             string.IsNullOrWhiteSpace(solicitud.MotivoViaje)))
        {
            throw new ReglaDeNegocioException(
                "Para un menor sin padres registre el nombre del padre, de la madre y el motivo del viaje.",
                "menor");
        }

        var salida = FechasLocales.Combinar(solicitud.FechaSalida, solicitud.HoraSalida);

        if (!permitirPasado && salida < _reloj.Ahora)
        {
            throw new ReglaDeNegocioException(
                "La fecha y hora de salida no pueden estar en el pasado.",
                "fechaSalida");
        }

        var unidad = await _unidades.BuscarAsync(solicitud.UnidadSalidaId)
            ?? throw new ReglaDeNegocioException(
                "La unidad seleccionada ya no existe.",
                "unidadSalidaId");

        if (unidad.FechaHoraSalida != salida)
        {
            throw new ReglaDeNegocioException(
                "La unidad no corresponde a la fecha y hora de salida.",
                "unidadSalidaId");
        }

        if (solicitud.NumeroAsiento < 1 || solicitud.NumeroAsiento > unidad.NumeroAsientos)
        {
            throw new ReglaDeNegocioException(
                "El asiento elegido no existe en la unidad.",
                "numeroAsiento");
        }

        var ocupados = await _unidades.AsientosOcupadosAsync(unidad.Id);
        if (ocupados.Contains(solicitud.NumeroAsiento))
        {
            throw new ReglaDeNegocioException(
                "El asiento elegido ya está ocupado.",
                "numeroAsiento");
        }

        var sentido = ServicioConfiguracion.DeducirSentido(solicitud.Origen, solicitud.Destino);
        var tarifa = await _servicioConfiguracion.ObtenerTarifaVigenteAsync(solicitud.FechaSalida, sentido);

        var pasaje = new Pasaje
        {
            PasajeroNombre = solicitud.PasajeroNombre.Trim(),
            TipoDocumento = solicitud.TipoDocumento,
            NumeroDocumento = solicitud.NumeroDocumento.Trim(),
            Telefono = string.IsNullOrWhiteSpace(solicitud.Telefono) ? null : solicitud.Telefono.Trim(),
            Origen = solicitud.Origen.Trim(),
            Destino = solicitud.Destino.Trim(),
            LugarDeEmbarque = solicitud.LugarDeEmbarque.Trim(),
            FechaSalida = solicitud.FechaSalida,
            HoraSalida = solicitud.HoraSalida,
            UnidadSalidaId = unidad.Id,
            NumeroAsiento = solicitud.NumeroAsiento,
            Importe = tarifa.Importe,
            SedeId = emisor.Sede.Id,
            SedeNombre = emisor.Sede.Nombre,
            EmisorId = emisor.Id,
            EmisorNombre = emisor.NombreUsuario,
            FechaHoraRegistro = _reloj.Ahora,
            TipoRegistro = TipoRegistroDePasaje.Normal,
            NombrePadre = solicitud.NombrePadre?.Trim(),
            NombreMadre = solicitud.NombreMadre?.Trim(),
            MotivoViaje = solicitud.MotivoViaje?.Trim()
        };

        return (pasaje, tarifa.Importe);
    }

    private static void ValidarIntegrante(IntegranteDeGrupo integrante)
    {
        if (string.IsNullOrWhiteSpace(integrante.Nombre))
        {
            throw new ReglaDeNegocioException(
                "Todos los integrantes deben tener nombre.",
                "integrantes");
        }

        if (string.IsNullOrWhiteSpace(integrante.NumeroDocumento))
        {
            throw new ReglaDeNegocioException(
                "Todos los integrantes deben tener documento.",
                "integrantes");
        }

        if (integrante.MenorSinPadres &&
            (string.IsNullOrWhiteSpace(integrante.NombrePadre) ||
             string.IsNullOrWhiteSpace(integrante.NombreMadre) ||
             string.IsNullOrWhiteSpace(integrante.MotivoViaje)))
        {
            throw new ReglaDeNegocioException(
                "Para un menor sin padres registre el nombre del padre, de la madre y el motivo del viaje.",
                "integrantes");
        }
    }

    private async Task<Pago> AgregarPagoAsync(
        long? pasajeId,
        long? grupoId,
        decimal importe,
        MedioDePago medio,
        Usuario emisor,
        TipoDeMovimiento tipo = TipoDeMovimiento.Pago)
    {
        var pago = new Pago
        {
            PasajeId = pasajeId,
            GrupoId = grupoId,
            Importe = importe,
            Medio = medio,
            TipoMovimiento = tipo,
            FechaHora = _reloj.Ahora,
            UsuarioId = emisor.Id,
            UsuarioNombre = emisor.NombreUsuario
        };

        await _pasajes.AgregarPagoAsync(pago);
        return pago;
    }

    private async Task EmitirBoletoAsync(Pasaje pasaje, Usuario emisor)
    {
        if (pasaje.Boleto is not null)
        {
            return;
        }

        if (await _pasajes.BuscarBoletoDelPasajeAsync(pasaje.Id) is not null)
        {
            return;
        }

        var (serie, numero) = await _pasajes.SiguienteBoletoAsync(pasaje.SedeId);
        var boleto = new BoletoDeViaje
        {
            PasajeId = pasaje.Id,
            Serie = serie,
            Numero = numero,
            FechaHoraEmision = _reloj.Ahora,
            EmisorId = emisor.Id
        };

        await _pasajes.AgregarBoletoAsync(boleto);
        pasaje.Boleto = boleto;
    }

    private async Task<Pasaje> ObtenerEntidadAsync(long id) =>
        await _pasajes.BuscarPorIdAsync(id)
        ?? throw new ReglaDeNegocioException("El pasaje ya no existe.", "id");

    private async Task<Usuario> ObtenerEmisorAsync()
    {
        if (_emisor is not null)
        {
            return _emisor;
        }

        _emisor = await _usuarios.BuscarPorIdAsync(_contexto.IdUsuario)
            ?? throw new InvalidOperationException("No se encontró el usuario de la sesión.");

        return _emisor;
    }

    private async Task<int> ObtenerHorasDeAvisoAsync()
    {
        var parametros = await _configuracion.ListarParametrosAsync();
        var valor = parametros
            .FirstOrDefault(p => p.Nombre == "aviso_previo_horas")
            ?.Valor;

        return int.TryParse(valor, out var horas) ? horas : 3;
    }

    private async Task<DatosDePasaje?> BuscarRepeticionAsync()
    {
        if (_contexto.ClaveDeOperacion is not Guid clave)
        {
            return null;
        }

        var entrada = await _bitacora.BuscarPorClaveAsync(clave);
        if (entrada is null)
        {
            return null;
        }

        return entrada.PasajeId is long id
            ? await BuscarAsync(id)
            : throw new ReglaDeNegocioException(
                "La operación ya fue registrada.",
                "idempotencia");
    }

    private async Task<DatosDeGrupo?> BuscarRepeticionDeGrupoAsync()
    {
        if (_contexto.ClaveDeOperacion is not Guid clave)
        {
            return null;
        }

        var entrada = await _bitacora.BuscarPorClaveAsync(clave);
        if (entrada is null)
        {
            return null;
        }

        if (entrada.PasajeId is long id)
        {
            var pasaje = await _pasajes.BuscarPorIdAsync(id);
            if (pasaje?.GrupoId is long grupoId)
            {
                return await ObtenerGrupoAsync(grupoId);
            }
        }

        throw new ReglaDeNegocioException(
            "La operación ya fue registrada.",
            "idempotencia");
    }

    private static EstadoPasaje CalcularEstado(decimal pagado, decimal importe)
    {
        if (importe > 0 && pagado >= importe)
        {
            return EstadoPasaje.Pagado;
        }

        return pagado > 0
            ? EstadoPasaje.ReservadoConAdelanto
            : EstadoPasaje.Reservado;
    }
}
