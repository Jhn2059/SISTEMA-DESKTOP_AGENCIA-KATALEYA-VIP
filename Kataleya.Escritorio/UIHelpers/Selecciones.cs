namespace Kataleya.Escritorio.UIHelpers;

// En Windows, un selector con opciones fijas puede recibir su índice antes que las opciones y quedar en
// blanco. Volver a asignarlo cuando la pantalla ya cargó lo muestra, sin cambiar lo que estaba elegido.
public static class Selecciones
{
    public static void Refrescar(Func<int> leer, Action<int> poner)
    {
        ArgumentNullException.ThrowIfNull(leer);
        ArgumentNullException.ThrowIfNull(poner);

        var actual = leer();
        poner(-1);
        poner(actual < 0 ? 0 : actual);
    }
}
