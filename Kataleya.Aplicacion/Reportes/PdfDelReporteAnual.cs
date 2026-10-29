using System.Globalization;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Kataleya.Aplicacion.Reportes;

// RF-32: los pasajes del año, con sus datos, sus pagos y su historia en la bitácora, en un PDF con contraseña.
internal static class PdfDelReporteAnual
{
    private const double Margen = 40;
    private const string Fuente = "Inter";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public static byte[] Generar(
        int anio,
        IReadOnlyList<Pasaje> pasajes,
        IReadOnlyList<EntradaDeBitacora> historia,
        string generadoPor,
        DateTime ahora,
        string contrasena)
    {
        FuentesDelReporte.Registrar();

        using var documento = new PdfDocument();
        documento.Info.Title = $"Reporte anual de pasajes {anio}";
        documento.Info.Author = "Agencia Kataleya VIP";

        var hoja = new Hoja(documento, anio);
        var cobrado = pasajes.SelectMany(pasaje => pasaje.Pagos).Sum(pago =>
            pago.TipoMovimiento == TipoDeMovimiento.Pago ? pago.Importe : -pago.Importe);

        hoja.Escribir($"REPORTE ANUAL DE PASAJES {anio}", Hoja.Titulo);
        hoja.Escribir("EMPRESA TURISMO VIP KATALEYA SV E.I.R.L. · RUC 20614980002", Hoja.Negrita);
        hoja.Escribir(
            $"Generado el {Fecha(ahora)} por {generadoPor}. Pasajes: {pasajes.Count}. " +
            $"Total cobrado: {Soles(cobrado)}.",
            Hoja.Normal);
        hoja.Escribir(
            "Documento confidencial: contiene datos personales de los pasajeros (Ley N.º 29733).",
            Hoja.Normal);
        hoja.Separador();

        if (pasajes.Count == 0)
        {
            hoja.Escribir($"No hay pasajes con salida en {anio}.", Hoja.Normal);
        }

        var historiaPorPasaje = historia
            .Where(entrada => entrada.PasajeId is not null)
            .ToLookup(entrada => entrada.PasajeId!.Value);

        foreach (var pasaje in pasajes)
        {
            EscribirPasaje(hoja, pasaje, historiaPorPasaje[pasaje.Id]);
        }

        hoja.Terminar();

        documento.SecuritySettings.UserPassword = contrasena;
        documento.SecuritySettings.OwnerPassword = contrasena;
        documento.SecurityHandler.SetEncryptionToV5(true);

        using var flujo = new MemoryStream();
        documento.Save(flujo, false);
        return flujo.ToArray();
    }

    private static void EscribirPasaje(Hoja hoja, Pasaje pasaje, IEnumerable<EntradaDeBitacora> historia)
    {
        var boleto = pasaje.Boleto is null ? "sin boleto" : $"boleto {pasaje.Boleto.NumeroCompleto}";
        var asiento = pasaje.GrupoId is null
            ? $"Unidad {pasaje.UnidadNombre ?? "-"}, asiento {pasaje.NumeroAsiento?.ToString(Cultura) ?? "-"}"
            : $"Grupo {pasaje.GrupoCodigo}{(pasaje.EsResponsable ? " (responsable)" : string.Empty)}";

        hoja.Escribir($"Pasaje {pasaje.Id} · {pasaje.Estado} · {boleto}", Hoja.Negrita);
        hoja.Escribir(
            $"Pasajero: {pasaje.PasajeroNombre} · {pasaje.TipoDocumento} {pasaje.NumeroDocumento}" +
            (string.IsNullOrWhiteSpace(pasaje.Telefono) ? string.Empty : $" · Tel. {pasaje.Telefono}"),
            Hoja.Normal);
        hoja.Escribir(
            $"Viaje: {pasaje.Origen} - {pasaje.Destino} · {pasaje.FechaSalida.ToString("dd/MM/yyyy", Cultura)} " +
            $"{pasaje.HoraSalida.ToString("HH:mm", Cultura)} · Embarque: {pasaje.LugarDeEmbarque} · {asiento}",
            Hoja.Normal);
        hoja.Escribir(
            $"Importe: {Soles(pasaje.Importe)} · Pagado: {Soles(pasaje.Pagado)} · Emisor: {pasaje.EmisorNombre} · " +
            $"Registrado: {Fecha(pasaje.FechaHoraRegistro)}" +
            (pasaje.TipoRegistro == TipoRegistroDePasaje.Diferido
                ? $" · Registro diferido, talonario {pasaje.NumeroTalonario}"
                : string.Empty),
            Hoja.Normal);

        if (!string.IsNullOrWhiteSpace(pasaje.NombrePadre) || !string.IsNullOrWhiteSpace(pasaje.NombreMadre))
        {
            hoja.Escribir(
                $"Menor sin sus padres: padre {pasaje.NombrePadre}, madre {pasaje.NombreMadre}; " +
                $"motivo: {pasaje.MotivoViaje}",
                Hoja.Normal);
        }

        if (!string.IsNullOrWhiteSpace(pasaje.MotivoAnulacion))
        {
            hoja.Escribir($"Motivo de la anulación: {pasaje.MotivoAnulacion}", Hoja.Normal);
        }

        foreach (var pago in pasaje.Pagos)
        {
            hoja.Escribir(
                $"{Fecha(pago.FechaHora)} · {pago.TipoMovimiento} · {pago.Medio} · {Soles(pago.Importe)} · " +
                pago.UsuarioNombre,
                Hoja.Detalle,
                sangria: 14);
        }

        foreach (var entrada in historia)
        {
            var cambio = entrada.ValorAnterior is null
                ? entrada.ValorNuevo
                : $"antes {entrada.ValorAnterior}; después {entrada.ValorNuevo}";

            hoja.Escribir(
                $"{Fecha(entrada.FechaHora)} · {entrada.UsuarioNombre} · {entrada.Operacion}" +
                (string.IsNullOrWhiteSpace(cambio) ? string.Empty : $" · {cambio}"),
                Hoja.Detalle,
                sangria: 14);
        }

        hoja.Separador();
    }

    private static string Fecha(DateTime fechaHora) =>
        FechasLocales.EnHoraDePeru(fechaHora).ToString("dd/MM/yyyy HH:mm", Cultura);

    private static string Soles(decimal importe) =>
        $"S/ {importe.ToString("N2", Cultura)}";

    private sealed class Hoja
    {
        public static readonly XFont Titulo = new(Fuente, 15, XFontStyleEx.Bold);
        public static readonly XFont Negrita = new(Fuente, 9.5, XFontStyleEx.Bold);
        public static readonly XFont Normal = new(Fuente, 9);
        public static readonly XFont Detalle = new(Fuente, 8);

        private static readonly XFont Pie = new(Fuente, 7.5);

        private readonly PdfDocument _documento;
        private readonly int _anio;
        private PdfPage _pagina = null!;
        private XGraphics? _graficos;
        private double _y;
        private int _numero;

        public Hoja(PdfDocument documento, int anio)
        {
            _documento = documento;
            _anio = anio;
            NuevaPagina();
        }

        private double Ancho => _pagina.Width.Point - 2 * Margen;

        private double Limite => _pagina.Height.Point - Margen - 14;

        public void Escribir(string texto, XFont fuente, double sangria = 0)
        {
            var alto = fuente.GetHeight() * 1.15;

            foreach (var linea in Partir(texto, fuente, Ancho - sangria))
            {
                if (_y + alto > Limite)
                {
                    NuevaPagina();
                }

                _graficos!.DrawString(linea, fuente, XBrushes.Black, new XPoint(Margen + sangria, _y + fuente.Size));
                _y += alto;
            }
        }

        public void Separador()
        {
            _y += 3;

            if (_y + 6 > Limite)
            {
                NuevaPagina();
                return;
            }

            _graficos!.DrawLine(XPens.LightGray, Margen, _y, Margen + Ancho, _y);
            _y += 6;
        }

        public void Terminar()
        {
            EscribirPie();
            _graficos?.Dispose();
            _graficos = null;
        }

        private void NuevaPagina()
        {
            if (_graficos is not null)
            {
                EscribirPie();
                _graficos.Dispose();
            }

            _pagina = _documento.AddPage();
            _pagina.Size = PageSize.A4;
            _graficos = XGraphics.FromPdfPage(_pagina);
            _y = Margen;
            _numero++;
        }

        private void EscribirPie() =>
            _graficos!.DrawString(
                $"Reporte anual de pasajes {_anio} · Página {_numero}",
                Pie,
                XBrushes.Gray,
                new XPoint(Margen, _pagina.Height.Point - Margen + 6));

        private List<string> Partir(string texto, XFont fuente, double ancho)
        {
            var lineas = new List<string>();
            var actual = string.Empty;

            foreach (var palabra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var propuesta = actual.Length == 0 ? palabra : $"{actual} {palabra}";

                if (actual.Length > 0 && _graficos!.MeasureString(propuesta, fuente).Width > ancho)
                {
                    lineas.Add(actual);
                    actual = palabra;
                }
                else
                {
                    actual = propuesta;
                }

                while (_graficos!.MeasureString(actual, fuente).Width > ancho && actual.Length > 1)
                {
                    var corte = actual.Length - 1;
                    while (corte > 1 && _graficos.MeasureString(actual[..corte], fuente).Width > ancho)
                    {
                        corte--;
                    }

                    lineas.Add(actual[..corte]);
                    actual = actual[corte..];
                }
            }

            if (actual.Length > 0 || lineas.Count == 0)
            {
                lineas.Add(actual);
            }

            return lineas;
        }
    }
}
