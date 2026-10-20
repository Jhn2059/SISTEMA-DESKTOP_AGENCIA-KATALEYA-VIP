using System.Windows.Input;

namespace Kataleya.Escritorio.UIHelpers.Behaviors;

public static class CicloDeVidaDePagina
{
    public static readonly BindableProperty AlAparecerProperty =
        BindableProperty.CreateAttached(
            "AlAparecer",
            typeof(ICommand),
            typeof(CicloDeVidaDePagina),
            null,
            propertyChanged: AlCambiarComandoDeAparicion);

    public static readonly BindableProperty AlDesaparecerProperty =
        BindableProperty.CreateAttached(
            "AlDesaparecer",
            typeof(ICommand),
            typeof(CicloDeVidaDePagina),
            null,
            propertyChanged: AlCambiarComandoDeDesaparicion);

    public static ICommand? GetAlAparecer(BindableObject objetivo) =>
        (ICommand?)objetivo.GetValue(AlAparecerProperty);

    public static void SetAlAparecer(BindableObject objetivo, ICommand? valor) =>
        objetivo.SetValue(AlAparecerProperty, valor);

    public static ICommand? GetAlDesaparecer(BindableObject objetivo) =>
        (ICommand?)objetivo.GetValue(AlDesaparecerProperty);

    public static void SetAlDesaparecer(BindableObject objetivo, ICommand? valor) =>
        objetivo.SetValue(AlDesaparecerProperty, valor);

    private static void AlCambiarComandoDeAparicion(
        BindableObject objetivo,
        object? valorAnterior,
        object? valorNuevo)
    {
        if (objetivo is not Page pagina)
        {
            return;
        }

        pagina.Appearing -= EjecutarComandoDeAparicion;
        if (valorNuevo is ICommand)
        {
            pagina.Appearing += EjecutarComandoDeAparicion;
        }
    }

    private static void AlCambiarComandoDeDesaparicion(
        BindableObject objetivo,
        object? valorAnterior,
        object? valorNuevo)
    {
        if (objetivo is not Page pagina)
        {
            return;
        }

        pagina.Disappearing -= EjecutarComandoDeDesaparicion;
        if (valorNuevo is ICommand)
        {
            pagina.Disappearing += EjecutarComandoDeDesaparicion;
        }
    }

    private static void EjecutarComandoDeAparicion(object? remitente, EventArgs argumentos)
    {
        if (remitente is Page pagina &&
            GetAlAparecer(pagina) is { } comando &&
            comando.CanExecute(null))
        {
            comando.Execute(null);
        }
    }

    private static void EjecutarComandoDeDesaparicion(object? remitente, EventArgs argumentos)
    {
        if (remitente is Page pagina &&
            GetAlDesaparecer(pagina) is { } comando &&
            comando.CanExecute(null))
        {
            comando.Execute(null);
        }
    }
}
