using System.Globalization;
using System.Text;
using Kataleya.Contratos.Pasajes;
using Kataleya.Dominio.Enums;

namespace Kataleya.Escritorio.Servicios.Impresion;

// Contenido del boleto: ERS, 3.1.1.4 (Tabla 16 y Tabla 17).
public static class BoletoEnEscPos
{
    private const string RazonSocial = "EMPRESA TURISMO VIP KATALEYA SV E.I.R.L.";
    private const string Ruc = "20614980002";
    private const string Lema = "Uniendo a las familias peruanas";
    private const string Ruta = "Ica - Hyo. - viceversa";
    private const byte PaginaDeCodigosPc850 = 2;

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    private static readonly IReadOnlyDictionary<string, string> Domicilios =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Huancayo"] = "Ancash y Ferrocarril (frente a Ataco), Huancayo",
            ["Ica"] = "Cruz del Sur, Grifo Repsol, Ica"
        };

    private static readonly string[] Leyendas =
    [
        "Todo pasajero que no se presente a la hora indicada perderá su pasaje.",
        "Todo pasajero tendrá derecho a 12 kg de equipaje."
    ];

    public static byte[] Armar(DatosDePasaje pasaje, AnchoDePapel ancho)
    {
        ArgumentNullException.ThrowIfNull(pasaje);

        var boleto = pasaje.Boleto
            ?? throw new InvalidOperationException("Solo se imprime el boleto de un pasaje pagado.");
        var texto = new TextoDelBoleto(ancho);

        texto.Encabezado(pasaje.Sede);
        texto.Separador();
        texto.Centrado("BOLETO DE VIAJE", grande: true);
        texto.Centrado(boleto.NumeroCompleto, negrita: true);
        texto.Separador();
        texto.Fila("Emisión", boleto.FechaHoraEmision.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura));
        texto.Fila("Pasajero", pasaje.PasajeroNombre);
        texto.Fila("Documento", $"{NombreDelDocumento(pasaje.TipoDocumento)} {pasaje.NumeroDocumento}");

        if (!string.IsNullOrWhiteSpace(pasaje.Telefono))
        {
            texto.Fila("Teléfono", pasaje.Telefono);
        }

        texto.Fila("Ruta", $"{pasaje.Origen} - {pasaje.Destino}");
        texto.Fila("Salida", Salida(pasaje));
        texto.Fila("Embarque", pasaje.LugarDeEmbarque);
        texto.Fila("Asiento", Asiento(pasaje));
        texto.Fila("Importe", $"S/ {pasaje.Importe.ToString("N2", Cultura)}", negrita: true);
        texto.Linea(ImporteEnLetras.Convertir(pasaje.Importe));

        var medios = MediosDePago(pasaje);
        if (medios.Length > 0)
        {
            texto.Fila("Pago", medios);
        }

        texto.Fila("Emisor", Emisor(pasaje));
        texto.Separador();

        foreach (var leyenda in Leyendas)
        {
            texto.Linea(leyenda);
        }

        texto.Centrado("ORIGINAL: PASAJERO");
        texto.Final();

        return texto.Bytes();
    }

    public static byte[] ArmarPrueba(string impresora, AnchoDePapel ancho, DateTime ahora)
    {
        var texto = new TextoDelBoleto(ancho);

        texto.Centrado("PRUEBA DE IMPRESIÓN", grande: true);
        texto.Separador();
        texto.Fila("Impresora", impresora);
        texto.Fila("Papel", ancho == AnchoDePapel.Mm58 ? "58 mm" : "80 mm");
        texto.Fila("Fecha", ahora.ToString("dd/MM/yyyy HH:mm", Cultura));
        texto.Separador();
        texto.Linea("Esta etiqueta no usa la numeración de los boletos.");
        texto.Linea("Acentos: á é í ó ú ñ Ñ ¿ ¡");
        texto.Final();

        return texto.Bytes();
    }

    private static string NombreDelDocumento(TipoDocumento tipo) =>
        tipo == TipoDocumento.Pasaporte ? "Pasaporte" : "DNI";

    private static string Salida(DatosDePasaje pasaje)
    {
        var dia = Cultura.TextInfo.ToTitleCase(pasaje.FechaSalida.ToString("dddd", Cultura));
        return $"{dia} {pasaje.FechaSalida.ToString("dd/MM/yyyy", Cultura)} {pasaje.HoraSalida.ToString("HH:mm", Cultura)}";
    }

    private static string Asiento(DatosDePasaje pasaje) =>
        pasaje.GrupoId is not null
            ? $"Grupo {pasaje.GrupoCodigo}"
            : $"Unidad {pasaje.Unidad ?? "-"} - asiento {pasaje.NumeroAsiento?.ToString(Cultura) ?? "-"}";

    private static string MediosDePago(DatosDePasaje pasaje) =>
        string.Join(" y ", pasaje.Pagos
            .Where(pago => pago.TipoMovimiento == TipoDeMovimiento.Pago)
            .Select(pago => pago.Medio == MedioDePago.Yape ? "Yape" : "Efectivo")
            .Distinct());

    // El boleto lo emite quien completó el pago (RF-06); si no hay pagos, quien registró el pasaje.
    private static string Emisor(DatosDePasaje pasaje) =>
        pasaje.Pagos
            .Where(pago => pago.TipoMovimiento == TipoDeMovimiento.Pago)
            .OrderBy(pago => pago.FechaHora)
            .LastOrDefault()?.CobradoPor
        ?? pasaje.Emisor;

    private sealed class TextoDelBoleto
    {
        private static readonly Encoding Codificacion = CrearCodificacion();

        private readonly List<byte> _bytes = [];
        private readonly int _columnas;

        public TextoDelBoleto(AnchoDePapel ancho)
        {
            _columnas = ancho == AnchoDePapel.Mm58 ? 32 : 48;
            Comando(0x1B, 0x40);
            Comando(0x1B, 0x74, PaginaDeCodigosPc850);
        }

        public void Encabezado(string sede)
        {
            Centrado(RazonSocial, negrita: true);
            Centrado($"RUC {Ruc}");

            if (Domicilios.TryGetValue(sede ?? string.Empty, out var domicilio))
            {
                Centrado(domicilio);
            }

            Centrado(Lema);
            Centrado(Ruta);
        }

        public void Centrado(string texto, bool negrita = false, bool grande = false)
        {
            Comando(0x1B, 0x61, 1);
            Escribir(texto, negrita, grande ? _columnas / 2 : _columnas, grande);
            Comando(0x1B, 0x61, 0);
        }

        public void Fila(string etiqueta, string valor, bool negrita = false)
        {
            var prefijo = $"{etiqueta}: ";
            var lineas = Partir(valor, _columnas - prefijo.Length);

            Comando(0x1B, 0x45, 1);
            Agregar(prefijo);
            Comando(0x1B, 0x45, (byte)(negrita ? 1 : 0));
            Agregar(lineas[0]);
            Agregar("\n");

            foreach (var linea in lineas.Skip(1))
            {
                Agregar(new string(' ', prefijo.Length) + linea + "\n");
            }

            Comando(0x1B, 0x45, 0);
        }

        public void Linea(string texto) =>
            Escribir(texto, negrita: false, _columnas, grande: false);

        public void Separador() =>
            Agregar(new string('-', _columnas) + "\n");

        public void Final() =>
            Comando(0x1B, 0x64, 4);

        public byte[] Bytes() =>
            [.. _bytes];

        private void Escribir(string texto, bool negrita, int columnas, bool grande)
        {
            if (negrita)
            {
                Comando(0x1B, 0x45, 1);
            }

            if (grande)
            {
                Comando(0x1D, 0x21, 0x11);
            }

            foreach (var linea in Partir(texto, columnas))
            {
                Agregar(linea + "\n");
            }

            if (grande)
            {
                Comando(0x1D, 0x21, 0x00);
            }

            if (negrita)
            {
                Comando(0x1B, 0x45, 0);
            }
        }

        private static List<string> Partir(string texto, int columnas)
        {
            var lineas = new List<string>();
            var actual = new StringBuilder();

            foreach (var palabra in (texto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var resto = palabra;

                while (resto.Length > columnas)
                {
                    if (actual.Length > 0)
                    {
                        lineas.Add(actual.ToString());
                        actual.Clear();
                    }

                    lineas.Add(resto[..columnas]);
                    resto = resto[columnas..];
                }

                if (actual.Length > 0 && actual.Length + 1 + resto.Length > columnas)
                {
                    lineas.Add(actual.ToString());
                    actual.Clear();
                }

                if (actual.Length > 0)
                {
                    actual.Append(' ');
                }

                actual.Append(resto);
            }

            if (actual.Length > 0 || lineas.Count == 0)
            {
                lineas.Add(actual.ToString());
            }

            return lineas;
        }

        private void Agregar(string texto) =>
            _bytes.AddRange(Codificacion.GetBytes(texto));

        private void Comando(params byte[] bytes) =>
            _bytes.AddRange(bytes);

        private static Encoding CrearCodificacion()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(850, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        }
    }
}
