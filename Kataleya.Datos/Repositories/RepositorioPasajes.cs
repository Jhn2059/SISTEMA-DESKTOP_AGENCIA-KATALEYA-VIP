using System.Text;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using Kataleya.Dominio.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace Kataleya.Datos.Repositories;

public sealed class RepositorioPasajes : IRepositorioPasajes
{
    private const string SeleccionarPasaje = """
        SELECT p.id,
               p.pasajero_nombre,
               p.tipo_documento,
               p.numero_documento,
               p.telefono,
               p.origen,
               p.destino,
               p.lugar_embarque,
               p.fecha_salida,
               p.hora_salida,
               p.unidad_salida_id,
               p.numero_asiento,
               un.numero,
               tv.nombre,
               p.grupo_id,
               g.codigo,
               p.es_responsable,
               p.importe,
               p.estado,
               p.sede_id,
               s.nombre,
               p.emisor_id,
               eu.nombre_usuario,
               p.fecha_hora_registro,
               p.tipo_registro,
               p.hora_emision_real,
               p.numero_talonario,
               p.motivo_anulacion,
               p.nombre_padre,
               p.nombre_madre,
               p.motivo_viaje,
               b.id,
               b.serie,
               b.numero,
               b.fecha_hora_emision,
               b.emisor_id,
               b.reimpresiones,
               CASE
                   WHEN p.grupo_id IS NULL THEN COALESCE((
                       SELECT SUM(CASE WHEN pg.tipo_movimiento = 'Pago' THEN pg.importe ELSE -pg.importe END)
                       FROM pago pg WHERE pg.pasaje_id = p.id), 0)
                   WHEN p.es_responsable THEN COALESCE((
                       SELECT SUM(CASE WHEN pg.tipo_movimiento = 'Pago' THEN pg.importe ELSE -pg.importe END)
                       FROM pago pg WHERE pg.grupo_id = p.grupo_id), 0)
                   ELSE 0
               END
        FROM pasaje p
        INNER JOIN sede s ON s.id = p.sede_id
        INNER JOIN usuario eu ON eu.id = p.emisor_id
        LEFT JOIN unidad_salida un ON un.id = p.unidad_salida_id
        LEFT JOIN tipo_vehiculo tv ON tv.id = un.tipo_vehiculo_id
        LEFT JOIN grupo g ON g.id = p.grupo_id
        LEFT JOIN boleto_viaje b ON b.pasaje_id = p.id
        """;

    private readonly ContextoDeBaseDeDatos _contexto;

    public RepositorioPasajes(ContextoDeBaseDeDatos contexto) =>
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));

    public async Task<Pasaje?> BuscarPorIdAsync(long id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"{SeleccionarPasaje} WHERE p.id = @id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = id;

        var pasajes = await LeerPasajesAsync(comando);
        if (pasajes.Count == 0)
        {
            return null;
        }

        var pasaje = pasajes[0];
        pasaje.Pagos = pasaje.GrupoId is null
            ? (await ListarPagosAsync(pasaje.Id)).ToList()
            : (await ListarPagosDelGrupoAsync(pasaje.GrupoId.Value)).ToList();

        return pasaje;
    }

    public async Task<IReadOnlyList<Pasaje>> ConsultarAsync(FiltroDePasajes filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var condiciones = new List<string>();
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand { Connection = conexion, Transaction = _contexto.TransaccionActual };

        if (filtro.Desde is not null)
        {
            condiciones.Add("p.fecha_salida >= @desde");
            comando.Parameters.Add("desde", NpgsqlDbType.Date).Value = filtro.Desde.Value;
        }

        if (filtro.Hasta is not null)
        {
            condiciones.Add("p.fecha_salida <= @hasta");
            comando.Parameters.Add("hasta", NpgsqlDbType.Date).Value = filtro.Hasta.Value;
        }

        if (filtro.Hora is not null)
        {
            condiciones.Add("p.hora_salida = @hora");
            comando.Parameters.Add("hora", NpgsqlDbType.Time).Value = filtro.Hora.Value;
        }

        if (filtro.Estado is not null)
        {
            condiciones.Add("p.estado = @estado");
            comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value = filtro.Estado.Value.ToString();
        }

        if (filtro.Tipo == TipoDePasaje.Individual)
        {
            condiciones.Add("p.grupo_id IS NULL");
        }
        else if (filtro.Tipo == TipoDePasaje.Grupo)
        {
            condiciones.Add("p.grupo_id IS NOT NULL");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Pasajero))
        {
            condiciones.Add("(p.pasajero_nombre ILIKE @pasajero OR p.numero_documento ILIKE @pasajero)");
            comando.Parameters.Add("pasajero", NpgsqlDbType.Text).Value = $"%{filtro.Pasajero.Trim()}%";
        }

        if (filtro.EmisorId is not null)
        {
            condiciones.Add("p.emisor_id = @emisor");
            comando.Parameters.Add("emisor", NpgsqlDbType.Integer).Value = filtro.EmisorId.Value;
        }

        if (filtro.SedeId is not null)
        {
            condiciones.Add("p.sede_id = @sede");
            comando.Parameters.Add("sede", NpgsqlDbType.Smallint).Value = filtro.SedeId.Value;
        }

        var sql = new StringBuilder(SeleccionarPasaje);
        if (condiciones.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", condiciones));
        }

        sql.Append(" ORDER BY p.fecha_salida DESC, p.hora_salida DESC, p.id DESC");
        comando.CommandText = sql.ToString();

        return await LeerPasajesAsync(comando);
    }

    public async Task<long> AgregarAsync(Pasaje pasaje)
    {
        ArgumentNullException.ThrowIfNull(pasaje);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO pasaje
                (pasajero_nombre, tipo_documento, numero_documento, telefono,
                 origen, destino, lugar_embarque, fecha_salida, hora_salida,
                 unidad_salida_id, numero_asiento, grupo_id, es_responsable,
                 importe, estado, sede_id, emisor_id, tipo_registro,
                 hora_emision_real, numero_talonario, motivo_anulacion,
                 nombre_padre, nombre_madre, motivo_viaje)
            VALUES
                (@pasajero_nombre, @tipo_documento, @numero_documento, @telefono,
                 @origen, @destino, @lugar_embarque, @fecha_salida, @hora_salida,
                 @unidad_salida_id, @numero_asiento, @grupo_id, @es_responsable,
                 @importe, @estado, @sede_id, @emisor_id, @tipo_registro,
                 @hora_emision_real, @numero_talonario, @motivo_anulacion,
                 @nombre_padre, @nombre_madre, @motivo_viaje)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        AgregarParametrosDePasaje(comando, pasaje);
        pasaje.Id = (long)(await comando.ExecuteScalarAsync())!;
        return pasaje.Id;
    }

    public async Task ActualizarAsync(Pasaje pasaje)
    {
        ArgumentNullException.ThrowIfNull(pasaje);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            UPDATE pasaje
            SET pasajero_nombre = @pasajero_nombre,
                tipo_documento = @tipo_documento,
                numero_documento = @numero_documento,
                telefono = @telefono,
                origen = @origen,
                destino = @destino,
                lugar_embarque = @lugar_embarque,
                fecha_salida = @fecha_salida,
                hora_salida = @hora_salida,
                unidad_salida_id = @unidad_salida_id,
                numero_asiento = @numero_asiento,
                grupo_id = @grupo_id,
                es_responsable = @es_responsable,
                importe = @importe,
                estado = @estado,
                sede_id = @sede_id,
                emisor_id = @emisor_id,
                tipo_registro = @tipo_registro,
                hora_emision_real = @hora_emision_real,
                numero_talonario = @numero_talonario,
                motivo_anulacion = @motivo_anulacion,
                nombre_padre = @nombre_padre,
                nombre_madre = @nombre_madre,
                motivo_viaje = @motivo_viaje
            WHERE id = @id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };

        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = pasaje.Id;
        AgregarParametrosDePasaje(comando, pasaje);

        await comando.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<Pago>> ListarPagosAsync(long pasajeId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT pg.id, pg.pasaje_id, pg.grupo_id, pg.importe, pg.medio,
                   pg.tipo_movimiento, pg.fecha_hora, pg.usuario_id, u.nombre_usuario
            FROM pago pg
            INNER JOIN usuario u ON u.id = pg.usuario_id
            WHERE pg.pasaje_id = @pasaje_id
            ORDER BY pg.fecha_hora
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("pasaje_id", NpgsqlDbType.Bigint).Value = pasajeId;

        var pagos = new List<Pago>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            pagos.Add(LeerPago(lector));
        }

        return pagos;
    }

    public async Task<decimal> TotalPagadoAsync(long? pasajeId, long? grupoId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT COALESCE(SUM(CASE WHEN tipo_movimiento = 'Pago' THEN importe ELSE -importe END), 0)
            FROM pago
            WHERE (@pasaje_id IS NOT NULL AND pasaje_id = @pasaje_id)
               OR (@grupo_id IS NOT NULL AND grupo_id = @grupo_id)
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("pasaje_id", NpgsqlDbType.Bigint).Value =
            pasajeId is null ? DBNull.Value : pasajeId.Value;
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value =
            grupoId is null ? DBNull.Value : grupoId.Value;

        var resultado = await comando.ExecuteScalarAsync();
        return resultado is decimal valor ? valor : 0m;
    }

    public async Task AgregarPagoAsync(Pago pago)
    {
        ArgumentNullException.ThrowIfNull(pago);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO pago
                (pasaje_id, grupo_id, importe, medio, tipo_movimiento, usuario_id)
            VALUES
                (@pasaje_id, @grupo_id, @importe, @medio, @tipo_movimiento, @usuario_id)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("pasaje_id", NpgsqlDbType.Bigint).Value =
            pago.PasajeId is null ? DBNull.Value : pago.PasajeId.Value;
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value =
            pago.GrupoId is null ? DBNull.Value : pago.GrupoId.Value;
        comando.Parameters.Add("importe", NpgsqlDbType.Numeric).Value = pago.Importe;
        comando.Parameters.Add("medio", NpgsqlDbType.Varchar).Value = pago.Medio.ToString();
        comando.Parameters.Add("tipo_movimiento", NpgsqlDbType.Varchar).Value = pago.TipoMovimiento.ToString();
        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = pago.UsuarioId;

        pago.Id = (long)(await comando.ExecuteScalarAsync())!;
    }

    public async Task<Grupo?> BuscarGrupoAsync(long id)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT g.id, g.codigo, g.telefono, g.precio_acordado, g.origen, g.destino,
                   g.lugar_recojo, g.fecha_salida, g.hora_salida, g.usuario_id,
                   u.nombre_usuario, g.fecha_hora,
                   COALESCE((SELECT SUM(CASE WHEN pg.tipo_movimiento = 'Pago' THEN pg.importe ELSE -pg.importe END)
                             FROM pago pg WHERE pg.grupo_id = g.id), 0)
            FROM grupo g
            INNER JOIN usuario u ON u.id = g.usuario_id
            WHERE g.id = @id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = id;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? LeerGrupo(lector) : null;
    }

    public async Task<long> AgregarGrupoAsync(Grupo grupo)
    {
        ArgumentNullException.ThrowIfNull(grupo);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO grupo
                (codigo, telefono, precio_acordado, origen, destino, lugar_recojo,
                 fecha_salida, hora_salida, usuario_id)
            VALUES
                ('G-' || lpad(nextval('secuencia_codigo_grupo')::text, 5, '0'),
                 @telefono, @precio_acordado, @origen, @destino, @lugar_recojo,
                 @fecha_salida, @hora_salida, @usuario_id)
            RETURNING id, codigo
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("telefono", NpgsqlDbType.Varchar).Value = grupo.Telefono;
        comando.Parameters.Add("precio_acordado", NpgsqlDbType.Numeric).Value = grupo.PrecioAcordado;
        comando.Parameters.Add("origen", NpgsqlDbType.Varchar).Value = grupo.Origen;
        comando.Parameters.Add("destino", NpgsqlDbType.Varchar).Value = grupo.Destino;
        comando.Parameters.Add("lugar_recojo", NpgsqlDbType.Varchar).Value = grupo.LugarDeRecojo;
        comando.Parameters.Add("fecha_salida", NpgsqlDbType.Date).Value = grupo.FechaSalida;
        comando.Parameters.Add("hora_salida", NpgsqlDbType.Time).Value = grupo.HoraSalida;
        comando.Parameters.Add("usuario_id", NpgsqlDbType.Integer).Value = grupo.UsuarioId;

        await using var lector = await comando.ExecuteReaderAsync();
        await lector.ReadAsync();
        grupo.Id = lector.GetInt64(0);
        grupo.Codigo = lector.GetString(1);
        return grupo.Id;
    }

    public async Task ActualizarGrupoAsync(Grupo grupo)
    {
        ArgumentNullException.ThrowIfNull(grupo);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            UPDATE grupo
            SET telefono = @telefono,
                precio_acordado = @precio_acordado,
                origen = @origen,
                destino = @destino,
                lugar_recojo = @lugar_recojo,
                fecha_salida = @fecha_salida,
                hora_salida = @hora_salida
            WHERE id = @id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = grupo.Id;
        comando.Parameters.Add("telefono", NpgsqlDbType.Varchar).Value = grupo.Telefono;
        comando.Parameters.Add("precio_acordado", NpgsqlDbType.Numeric).Value = grupo.PrecioAcordado;
        comando.Parameters.Add("origen", NpgsqlDbType.Varchar).Value = grupo.Origen;
        comando.Parameters.Add("destino", NpgsqlDbType.Varchar).Value = grupo.Destino;
        comando.Parameters.Add("lugar_recojo", NpgsqlDbType.Varchar).Value = grupo.LugarDeRecojo;
        comando.Parameters.Add("fecha_salida", NpgsqlDbType.Date).Value = grupo.FechaSalida;
        comando.Parameters.Add("hora_salida", NpgsqlDbType.Time).Value = grupo.HoraSalida;

        await comando.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<Pasaje>> ListarPasajesDelGrupoAsync(long grupoId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"{SeleccionarPasaje} WHERE p.grupo_id = @grupo_id ORDER BY p.id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value = grupoId;

        return await LeerPasajesAsync(comando);
    }

    public async Task<IReadOnlyList<Pago>> ListarPagosDelGrupoAsync(long grupoId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT pg.id, pg.pasaje_id, pg.grupo_id, pg.importe, pg.medio,
                   pg.tipo_movimiento, pg.fecha_hora, pg.usuario_id, u.nombre_usuario
            FROM pago pg
            INNER JOIN usuario u ON u.id = pg.usuario_id
            WHERE pg.grupo_id = @grupo_id
            ORDER BY pg.fecha_hora
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value = grupoId;

        var pagos = new List<Pago>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            pagos.Add(LeerPago(lector));
        }

        return pagos;
    }

    public async Task<(string Serie, long Numero)> SiguienteBoletoAsync(short sedeId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            UPDATE correlativo_boleto
            SET ultimo_numero = ultimo_numero + 1
            WHERE serie = (SELECT serie FROM correlativo_boleto WHERE sede_id = @sede LIMIT 1)
            RETURNING serie, ultimo_numero
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("sede", NpgsqlDbType.Smallint).Value = sedeId;

        await using var lector = await comando.ExecuteReaderAsync();
        if (!await lector.ReadAsync())
        {
            throw new InvalidOperationException("La sede no tiene una serie de boletos configurada.");
        }

        return (lector.GetString(0), lector.GetInt64(1));
    }

    public async Task AgregarBoletoAsync(BoletoDeViaje boleto)
    {
        ArgumentNullException.ThrowIfNull(boleto);

        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO boleto_viaje
                (pasaje_id, serie, numero, fecha_hora_emision, emisor_id)
            VALUES
                (@pasaje_id, @serie, @numero, @fecha_hora_emision, @emisor_id)
            RETURNING id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("pasaje_id", NpgsqlDbType.Bigint).Value = boleto.PasajeId;
        comando.Parameters.Add("serie", NpgsqlDbType.Varchar).Value = boleto.Serie;
        comando.Parameters.Add("numero", NpgsqlDbType.Bigint).Value = boleto.Numero;
        comando.Parameters.Add("fecha_hora_emision", NpgsqlDbType.TimestampTz).Value = boleto.FechaHoraEmision;
        comando.Parameters.Add("emisor_id", NpgsqlDbType.Integer).Value = boleto.EmisorId;

        boleto.Id = (long)(await comando.ExecuteScalarAsync())!;
    }

    public async Task<BoletoDeViaje?> BuscarBoletoAsync(string serie, long numero)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, pasaje_id, serie, numero, fecha_hora_emision, emisor_id, reimpresiones
            FROM boleto_viaje
            WHERE serie = @serie AND numero = @numero
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("serie", NpgsqlDbType.Varchar).Value = serie;
        comando.Parameters.Add("numero", NpgsqlDbType.Bigint).Value = numero;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? LeerBoleto(lector) : null;
    }

    public async Task<BoletoDeViaje?> BuscarBoletoDelPasajeAsync(long pasajeId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT id, pasaje_id, serie, numero, fecha_hora_emision, emisor_id, reimpresiones
            FROM boleto_viaje
            WHERE pasaje_id = @pasaje_id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("pasaje_id", NpgsqlDbType.Bigint).Value = pasajeId;

        await using var lector = await comando.ExecuteReaderAsync();
        return await lector.ReadAsync() ? LeerBoleto(lector) : null;
    }

    public async Task RegistrarReimpresionAsync(long boletoId)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "UPDATE boleto_viaje SET reimpresiones = reimpresiones + 1 WHERE id = @id",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("id", NpgsqlDbType.Bigint).Value = boletoId;

        await comando.ExecuteNonQueryAsync();
    }

    public async Task<bool> ExisteTalonarioAsync(string numero)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pasaje WHERE numero_talonario = @numero)",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("numero", NpgsqlDbType.Varchar).Value = numero;

        return await comando.ExecuteScalarAsync() is true;
    }

    public async Task<bool> ExisteBoletoFisicoAsync(string serie, long numero)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM boleto_viaje WHERE serie = @serie AND numero = @numero)",
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("serie", NpgsqlDbType.Varchar).Value = serie;
        comando.Parameters.Add("numero", NpgsqlDbType.Bigint).Value = numero;

        return await comando.ExecuteScalarAsync() is true;
    }

    public async Task<IReadOnlyList<Pasaje>> BuscarDelAnioAsync(int anio)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            {SeleccionarPasaje}
            WHERE p.fecha_salida BETWEEN @desde AND @hasta
            ORDER BY p.fecha_salida, p.hora_salida, p.id
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        AgregarParametrosDelAnio(comando, anio);

        var pasajes = await LeerPasajesAsync(comando);
        if (pasajes.Count == 0)
        {
            return pasajes;
        }

        await using var consultaDePagos = new NpgsqlCommand(
            """
            SELECT pg.id, pg.pasaje_id, pg.grupo_id, pg.importe, pg.medio,
                   pg.tipo_movimiento, pg.fecha_hora, pg.usuario_id, u.nombre_usuario
            FROM pago pg
            INNER JOIN usuario u ON u.id = pg.usuario_id
            WHERE pg.pasaje_id = ANY(@pasajes) OR pg.grupo_id = ANY(@grupos)
            ORDER BY pg.fecha_hora
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        consultaDePagos.Parameters.Add("pasajes", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value =
            pasajes.Select(pasaje => pasaje.Id).ToArray();
        consultaDePagos.Parameters.Add("grupos", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value =
            pasajes.Where(pasaje => pasaje.GrupoId is not null).Select(pasaje => pasaje.GrupoId!.Value).Distinct().ToArray();

        var pagos = new List<Pago>();
        await using (var lector = await consultaDePagos.ExecuteReaderAsync())
        {
            while (await lector.ReadAsync())
            {
                pagos.Add(LeerPago(lector));
            }
        }

        // Los pagos de un grupo van en el pasaje del responsable, como en el cuadre.
        foreach (var pasaje in pasajes)
        {
            pasaje.Pagos = pasaje.GrupoId is null
                ? pagos.Where(pago => pago.PasajeId == pasaje.Id).ToList()
                : pasaje.EsResponsable
                    ? pagos.Where(pago => pago.GrupoId == pasaje.GrupoId).ToList()
                    : [];
        }

        return pasajes;
    }

    public async Task<int> EliminarDelAnioAsync(int anio)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            CREATE TEMP TABLE pasajes_depurados ON COMMIT DROP AS
                SELECT id, grupo_id FROM pasaje WHERE fecha_salida BETWEEN @desde AND @hasta;

            DELETE FROM pago
            WHERE pasaje_id IN (SELECT id FROM pasajes_depurados)
               OR grupo_id IN (SELECT grupo_id FROM pasajes_depurados WHERE grupo_id IS NOT NULL);

            DELETE FROM boleto_viaje WHERE pasaje_id IN (SELECT id FROM pasajes_depurados);

            DELETE FROM pasaje WHERE id IN (SELECT id FROM pasajes_depurados);

            DELETE FROM unidad_salida
            WHERE grupo_id IN (SELECT grupo_id FROM pasajes_depurados WHERE grupo_id IS NOT NULL);

            DELETE FROM grupo WHERE id IN (SELECT grupo_id FROM pasajes_depurados WHERE grupo_id IS NOT NULL);

            DELETE FROM unidad_salida u
            WHERE u.grupo_id IS NULL
              AND (u.fecha_hora_salida AT TIME ZONE 'America/Lima')::date BETWEEN @desde AND @hasta
              AND NOT EXISTS (SELECT 1 FROM pasaje p WHERE p.unidad_salida_id = u.id);

            SELECT count(*)::int FROM pasajes_depurados;
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        AgregarParametrosDelAnio(comando, anio);

        return (int)(await comando.ExecuteScalarAsync())!;
    }

    // Pasajes no anulados de las próximas salidas en ese horario (CDU-16 A3).
    public async Task<int> ContarDelHorarioAsync(TimeOnly hora, DateTime desdeEnHoraDePeru)
    {
        var conexion = await _contexto.ObtenerConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM pasaje
            WHERE hora_salida = @hora
              AND fecha_salida + hora_salida >= @desde
              AND estado <> 'Anulado'
            """,
            conexion)
        {
            Transaction = _contexto.TransaccionActual
        };
        comando.Parameters.Add("hora", NpgsqlDbType.Time).Value = hora;
        comando.Parameters.Add("desde", NpgsqlDbType.Timestamp).Value =
            DateTime.SpecifyKind(desdeEnHoraDePeru, DateTimeKind.Unspecified);

        return (int)(await comando.ExecuteScalarAsync())!;
    }

    private static void AgregarParametrosDelAnio(NpgsqlCommand comando, int anio)
    {
        comando.Parameters.Add("desde", NpgsqlDbType.Date).Value = new DateOnly(anio, 1, 1);
        comando.Parameters.Add("hasta", NpgsqlDbType.Date).Value = new DateOnly(anio, 12, 31);
    }

    private static void AgregarParametrosDePasaje(NpgsqlCommand comando, Pasaje pasaje)
    {
        comando.Parameters.Add("pasajero_nombre", NpgsqlDbType.Varchar).Value = pasaje.PasajeroNombre;
        comando.Parameters.Add("tipo_documento", NpgsqlDbType.Varchar).Value = pasaje.TipoDocumento.ToString();
        comando.Parameters.Add("numero_documento", NpgsqlDbType.Varchar).Value = pasaje.NumeroDocumento;
        comando.Parameters.Add("telefono", NpgsqlDbType.Varchar).Value =
            pasaje.Telefono is null ? DBNull.Value : pasaje.Telefono;
        comando.Parameters.Add("origen", NpgsqlDbType.Varchar).Value = pasaje.Origen;
        comando.Parameters.Add("destino", NpgsqlDbType.Varchar).Value = pasaje.Destino;
        comando.Parameters.Add("lugar_embarque", NpgsqlDbType.Varchar).Value = pasaje.LugarDeEmbarque;
        comando.Parameters.Add("fecha_salida", NpgsqlDbType.Date).Value = pasaje.FechaSalida;
        comando.Parameters.Add("hora_salida", NpgsqlDbType.Time).Value = pasaje.HoraSalida;
        comando.Parameters.Add("unidad_salida_id", NpgsqlDbType.Bigint).Value =
            pasaje.UnidadSalidaId is null ? DBNull.Value : pasaje.UnidadSalidaId.Value;
        comando.Parameters.Add("numero_asiento", NpgsqlDbType.Smallint).Value =
            pasaje.NumeroAsiento is null ? DBNull.Value : pasaje.NumeroAsiento.Value;
        comando.Parameters.Add("grupo_id", NpgsqlDbType.Bigint).Value =
            pasaje.GrupoId is null ? DBNull.Value : pasaje.GrupoId.Value;
        comando.Parameters.Add("es_responsable", NpgsqlDbType.Boolean).Value = pasaje.EsResponsable;
        comando.Parameters.Add("importe", NpgsqlDbType.Numeric).Value = pasaje.Importe;
        comando.Parameters.Add("estado", NpgsqlDbType.Varchar).Value = pasaje.Estado.ToString();
        comando.Parameters.Add("sede_id", NpgsqlDbType.Smallint).Value = pasaje.SedeId;
        comando.Parameters.Add("emisor_id", NpgsqlDbType.Integer).Value = pasaje.EmisorId;
        comando.Parameters.Add("tipo_registro", NpgsqlDbType.Varchar).Value = pasaje.TipoRegistro.ToString();
        comando.Parameters.Add("hora_emision_real", NpgsqlDbType.TimestampTz).Value =
            pasaje.HoraEmisionReal is null ? DBNull.Value : pasaje.HoraEmisionReal.Value;
        comando.Parameters.Add("numero_talonario", NpgsqlDbType.Varchar).Value =
            pasaje.NumeroTalonario is null ? DBNull.Value : pasaje.NumeroTalonario;
        comando.Parameters.Add("motivo_anulacion", NpgsqlDbType.Text).Value =
            pasaje.MotivoAnulacion is null ? DBNull.Value : pasaje.MotivoAnulacion;
        comando.Parameters.Add("nombre_padre", NpgsqlDbType.Varchar).Value =
            pasaje.NombrePadre is null ? DBNull.Value : pasaje.NombrePadre;
        comando.Parameters.Add("nombre_madre", NpgsqlDbType.Varchar).Value =
            pasaje.NombreMadre is null ? DBNull.Value : pasaje.NombreMadre;
        comando.Parameters.Add("motivo_viaje", NpgsqlDbType.Text).Value =
            pasaje.MotivoViaje is null ? DBNull.Value : pasaje.MotivoViaje;
    }

    private static async Task<List<Pasaje>> LeerPasajesAsync(NpgsqlCommand comando)
    {
        var pasajes = new List<Pasaje>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            pasajes.Add(LeerPasaje(lector));
        }

        return pasajes;
    }

    private static Pasaje LeerPasaje(NpgsqlDataReader lector)
    {
        var pasaje = new Pasaje
        {
            Id = lector.GetInt64(0),
            PasajeroNombre = lector.GetString(1),
            TipoDocumento = Enum.Parse<TipoDocumento>(lector.GetString(2)),
            NumeroDocumento = lector.GetString(3),
            Telefono = lector.IsDBNull(4) ? null : lector.GetString(4),
            Origen = lector.GetString(5),
            Destino = lector.GetString(6),
            LugarDeEmbarque = lector.GetString(7),
            FechaSalida = lector.GetFieldValue<DateOnly>(8),
            HoraSalida = lector.GetFieldValue<TimeOnly>(9),
            UnidadSalidaId = lector.IsDBNull(10) ? null : lector.GetInt64(10),
            NumeroAsiento = lector.IsDBNull(11) ? null : lector.GetInt16(11),
            UnidadNombre = lector.IsDBNull(12) ? null : lector.GetString(12),
            TipoVehiculoNombre = lector.IsDBNull(13) ? null : lector.GetString(13),
            GrupoId = lector.IsDBNull(14) ? null : lector.GetInt64(14),
            GrupoCodigo = lector.IsDBNull(15) ? null : lector.GetString(15),
            EsResponsable = lector.GetBoolean(16),
            Importe = lector.GetDecimal(17),
            Estado = Enum.Parse<EstadoPasaje>(lector.GetString(18)),
            SedeId = lector.GetInt16(19),
            SedeNombre = lector.GetString(20),
            EmisorId = lector.GetInt32(21),
            EmisorNombre = lector.GetString(22),
            FechaHoraRegistro = lector.GetDateTime(23),
            TipoRegistro = Enum.Parse<TipoRegistroDePasaje>(lector.GetString(24)),
            HoraEmisionReal = lector.IsDBNull(25) ? null : lector.GetDateTime(25),
            NumeroTalonario = lector.IsDBNull(26) ? null : lector.GetString(26),
            MotivoAnulacion = lector.IsDBNull(27) ? null : lector.GetString(27),
            NombrePadre = lector.IsDBNull(28) ? null : lector.GetString(28),
            NombreMadre = lector.IsDBNull(29) ? null : lector.GetString(29),
            MotivoViaje = lector.IsDBNull(30) ? null : lector.GetString(30),
            Pagado = lector.GetDecimal(37)
        };

        if (!lector.IsDBNull(31))
        {
            pasaje.Boleto = new BoletoDeViaje
            {
                Id = lector.GetInt64(31),
                PasajeId = pasaje.Id,
                Serie = lector.GetString(32),
                Numero = lector.GetInt64(33),
                FechaHoraEmision = lector.GetDateTime(34),
                EmisorId = lector.GetInt32(35),
                Reimpresiones = lector.GetInt32(36)
            };
        }

        return pasaje;
    }

    private static Pago LeerPago(NpgsqlDataReader lector) =>
        new()
        {
            Id = lector.GetInt64(0),
            PasajeId = lector.IsDBNull(1) ? null : lector.GetInt64(1),
            GrupoId = lector.IsDBNull(2) ? null : lector.GetInt64(2),
            Importe = lector.GetDecimal(3),
            Medio = Enum.Parse<MedioDePago>(lector.GetString(4)),
            TipoMovimiento = Enum.Parse<TipoDeMovimiento>(lector.GetString(5)),
            FechaHora = lector.GetDateTime(6),
            UsuarioId = lector.GetInt32(7),
            UsuarioNombre = lector.GetString(8)
        };

    private static BoletoDeViaje LeerBoleto(NpgsqlDataReader lector) =>
        new()
        {
            Id = lector.GetInt64(0),
            PasajeId = lector.GetInt64(1),
            Serie = lector.GetString(2),
            Numero = lector.GetInt64(3),
            FechaHoraEmision = lector.GetDateTime(4),
            EmisorId = lector.GetInt32(5),
            Reimpresiones = lector.GetInt32(6)
        };

    private static Grupo LeerGrupo(NpgsqlDataReader lector) =>
        new()
        {
            Id = lector.GetInt64(0),
            Codigo = lector.GetString(1),
            Telefono = lector.GetString(2),
            PrecioAcordado = lector.GetDecimal(3),
            Origen = lector.GetString(4),
            Destino = lector.GetString(5),
            LugarDeRecojo = lector.GetString(6),
            FechaSalida = lector.GetFieldValue<DateOnly>(7),
            HoraSalida = lector.GetFieldValue<TimeOnly>(8),
            UsuarioId = lector.GetInt32(9),
            UsuarioNombre = lector.GetString(10),
            FechaHora = lector.GetDateTime(11),
            Pagado = lector.GetDecimal(12)
        };
}
