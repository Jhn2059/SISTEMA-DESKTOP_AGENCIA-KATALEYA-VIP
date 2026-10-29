using System.Runtime.InteropServices;

namespace Kataleya.Escritorio.Servicios.Impresion;

// Envía los comandos ESC/POS a la cola de impresión de Windows (winspool) sin pasar por el controlador.
internal static class ColaDeImpresion
{
    private const int ImpresorasLocales = 0x2;
    private const int ImpresorasConectadas = 0x4;
    private const int ControlEliminarTrabajo = 5;

    private const uint ImpresoraSinPapel = 0x10 | 0x8 | 0x40;
    private const uint ImpresoraSinRespuesta = 0x2 | 0x80 | 0x1000 | 0x100000 | 0x400000 | 0x800000;
    private const uint AtributoFueraDeLinea = 0x400;

    private const uint TrabajoSinPapel = 0x40;
    private const uint TrabajoConError = 0x2 | 0x20 | 0x200 | 0x400;
    private const uint TrabajoTerminado = 0x80 | 0x1000;

    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(250);

    public static IReadOnlyList<string> ListarImpresoras()
    {
        const int tipos = ImpresorasLocales | ImpresorasConectadas;

        EnumPrinters(tipos, null, 4, IntPtr.Zero, 0, out var necesarios, out _);
        if (necesarios <= 0)
        {
            return [];
        }

        var memoria = Marshal.AllocHGlobal(necesarios);
        try
        {
            if (!EnumPrinters(tipos, null, 4, memoria, necesarios, out _, out var cantidad))
            {
                return [];
            }

            var tamano = Marshal.SizeOf<InfoDeImpresora4>();
            var nombres = new List<string>();

            for (var i = 0; i < cantidad; i++)
            {
                var info = Marshal.PtrToStructure<InfoDeImpresora4>(memoria + i * tamano);
                var nombre = Marshal.PtrToStringUni(info.NombreImpresora);

                if (!string.IsNullOrWhiteSpace(nombre))
                {
                    nombres.Add(nombre);
                }
            }

            return nombres;
        }
        finally
        {
            Marshal.FreeHGlobal(memoria);
        }
    }

    public static EstadoImpresora ConsultarEstado(string impresora)
    {
        if (!OpenPrinter(impresora, out var manejador, IntPtr.Zero))
        {
            return EstadoImpresora.SinRespuesta;
        }

        try
        {
            return LeerEstado(manejador);
        }
        finally
        {
            ClosePrinter(manejador);
        }
    }

    public static async Task<EstadoImpresora> EnviarAsync(string impresora, string documento, byte[] datos)
    {
        if (!OpenPrinter(impresora, out var manejador, IntPtr.Zero))
        {
            return EstadoImpresora.SinRespuesta;
        }

        try
        {
            var estado = LeerEstado(manejador);
            if (estado != EstadoImpresora.Lista)
            {
                return estado;
            }

            var trabajo = Escribir(manejador, documento, datos);
            if (trabajo == 0)
            {
                return EstadoImpresora.SinRespuesta;
            }

            return await EsperarTrabajoAsync(manejador, trabajo);
        }
        finally
        {
            ClosePrinter(manejador);
        }
    }

    private static int Escribir(IntPtr manejador, string documento, byte[] datos)
    {
        var info = new InfoDeDocumento
        {
            NombreDocumento = documento,
            ArchivoDeSalida = null,
            TipoDeDatos = "RAW"
        };

        var trabajo = StartDocPrinter(manejador, 1, ref info);
        if (trabajo == 0)
        {
            return 0;
        }

        var escrito = false;

        try
        {
            if (StartPagePrinter(manejador))
            {
                escrito = WritePrinter(manejador, datos, datos.Length, out var cantidad) &&
                          cantidad == datos.Length;
                EndPagePrinter(manejador);
            }
        }
        finally
        {
            EndDocPrinter(manejador);
        }

        if (!escrito)
        {
            SetJob(manejador, trabajo, 0, IntPtr.Zero, ControlEliminarTrabajo);
            return 0;
        }

        return trabajo;
    }

    private static async Task<EstadoImpresora> EsperarTrabajoAsync(IntPtr manejador, int trabajo)
    {
        var limite = DateTime.UtcNow + EsperaMaxima;

        while (DateTime.UtcNow < limite)
        {
            var estado = LeerEstadoDelTrabajo(manejador, trabajo);

            if (estado is null || (estado & TrabajoTerminado) != 0)
            {
                return EstadoImpresora.Lista;
            }

            if ((estado & TrabajoSinPapel) != 0)
            {
                SetJob(manejador, trabajo, 0, IntPtr.Zero, ControlEliminarTrabajo);
                return EstadoImpresora.SinPapel;
            }

            if ((estado & TrabajoConError) != 0)
            {
                SetJob(manejador, trabajo, 0, IntPtr.Zero, ControlEliminarTrabajo);
                return EstadoImpresora.SinRespuesta;
            }

            await Task.Delay(Intervalo);
        }

        // Si sigue en la cola, se quita para que no salga impreso más tarde, cuando ya se avisó del fallo.
        SetJob(manejador, trabajo, 0, IntPtr.Zero, ControlEliminarTrabajo);
        var actual = LeerEstado(manejador);
        return actual == EstadoImpresora.Lista ? EstadoImpresora.SinRespuesta : actual;
    }

    private static EstadoImpresora LeerEstado(IntPtr manejador)
    {
        GetPrinter(manejador, 2, IntPtr.Zero, 0, out var necesarios);
        if (necesarios <= 0)
        {
            return EstadoImpresora.SinRespuesta;
        }

        var memoria = Marshal.AllocHGlobal(necesarios);
        try
        {
            if (!GetPrinter(manejador, 2, memoria, necesarios, out _))
            {
                return EstadoImpresora.SinRespuesta;
            }

            var info = Marshal.PtrToStructure<InfoDeImpresora2>(memoria);

            if ((info.Estado & ImpresoraSinPapel) != 0)
            {
                return EstadoImpresora.SinPapel;
            }

            if ((info.Estado & ImpresoraSinRespuesta) != 0 || (info.Atributos & AtributoFueraDeLinea) != 0)
            {
                return EstadoImpresora.SinRespuesta;
            }

            return EstadoImpresora.Lista;
        }
        finally
        {
            Marshal.FreeHGlobal(memoria);
        }
    }

    private static uint? LeerEstadoDelTrabajo(IntPtr manejador, int trabajo)
    {
        GetJob(manejador, trabajo, 1, IntPtr.Zero, 0, out var necesarios);
        if (necesarios <= 0)
        {
            return null;
        }

        var memoria = Marshal.AllocHGlobal(necesarios);
        try
        {
            if (!GetJob(manejador, trabajo, 1, memoria, necesarios, out _))
            {
                return null;
            }

            return Marshal.PtrToStructure<InfoDeTrabajo1>(memoria).Estado;
        }
        finally
        {
            Marshal.FreeHGlobal(memoria);
        }
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string nombre, out IntPtr manejador, IntPtr predeterminados);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr manejador);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int StartDocPrinter(IntPtr manejador, int nivel, ref InfoDeDocumento documento);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr manejador);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr manejador);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr manejador);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr manejador, byte[] datos, int cantidad, out int escritos);

    [DllImport("winspool.drv", EntryPoint = "GetPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetPrinter(IntPtr manejador, int nivel, IntPtr memoria, int tamano, out int necesarios);

    [DllImport("winspool.drv", EntryPoint = "GetJobW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetJob(
        IntPtr manejador,
        int trabajo,
        int nivel,
        IntPtr memoria,
        int tamano,
        out int necesarios);

    [DllImport("winspool.drv", EntryPoint = "SetJobW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetJob(IntPtr manejador, int trabajo, int nivel, IntPtr datos, int comando);

    [DllImport("winspool.drv", EntryPoint = "EnumPrintersW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumPrinters(
        int tipos,
        string? nombre,
        int nivel,
        IntPtr memoria,
        int tamano,
        out int necesarios,
        out int cantidad);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InfoDeDocumento
    {
        public string NombreDocumento;
        public string? ArchivoDeSalida;
        public string TipoDeDatos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InfoDeImpresora4
    {
        public IntPtr NombreImpresora;
        public IntPtr NombreServidor;
        public uint Atributos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InfoDeImpresora2
    {
        public IntPtr NombreServidor;
        public IntPtr NombreImpresora;
        public IntPtr NombreCompartido;
        public IntPtr Puerto;
        public IntPtr Controlador;
        public IntPtr Comentario;
        public IntPtr Ubicacion;
        public IntPtr ModoDeDispositivo;
        public IntPtr ArchivoSeparador;
        public IntPtr Procesador;
        public IntPtr TipoDeDatos;
        public IntPtr Parametros;
        public IntPtr DescriptorDeSeguridad;
        public uint Atributos;
        public uint Prioridad;
        public uint PrioridadPredeterminada;
        public uint HoraDeInicio;
        public uint HoraDeFin;
        public uint Estado;
        public uint Trabajos;
        public uint PaginasPorMinuto;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InfoDeTrabajo1
    {
        public uint Trabajo;
        public IntPtr NombreImpresora;
        public IntPtr NombreEquipo;
        public IntPtr NombreUsuario;
        public IntPtr Documento;
        public IntPtr TipoDeDatos;
        public IntPtr TextoDeEstado;
        public uint Estado;
        public uint Prioridad;
        public uint Posicion;
        public uint PaginasTotales;
        public uint PaginasImpresas;
        public ushort Anio;
        public ushort Mes;
        public ushort DiaDeLaSemana;
        public ushort Dia;
        public ushort Hora;
        public ushort Minuto;
        public ushort Segundo;
        public ushort Milisegundos;
    }
}
