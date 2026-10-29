namespace Kataleya.Escritorio.Features.PanelPrincipal.ReporteAnual;

public partial class DialogoDeReporteAnualPage : ContentPage
{
    public DialogoDeReporteAnualPage(DialogoDeReporteAnualViewModel modelo)
    {
        InitializeComponent();
        Modelo = modelo;
        BindingContext = modelo;
    }

    public DialogoDeReporteAnualViewModel Modelo { get; }
}
