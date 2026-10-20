namespace Kataleya.Escritorio.Paginas.Dialogos;

public partial class DialogoDeContrasenaPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _resultado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DialogoDeContrasenaPage(
        string nombreUsuario,
        IReadOnlyDictionary<string, string>? errores = null)
    {
        InitializeComponent();
        EtiquetaUsuario.Text = $"Cuenta: {nombreUsuario}";

        if (errores is not null &&
            errores.TryGetValue("contrasenaNueva", out var mensaje))
        {
            ErrorContrasena.Text = mensaje;
            ErrorContrasena.IsVisible = true;
        }
    }

    public Task<string?> Resultado => _resultado.Task;

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _resultado.TrySetResult(null);
    }

    private async void Cancelar_Clicked(object? sender, EventArgs e)
    {
        _resultado.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    private async void Aceptar_Clicked(object? sender, EventArgs e)
    {
        var contrasena = EntradaContrasena.Text ?? string.Empty;

        if (contrasena.Length < 8)
        {
            ErrorContrasena.Text = "La contraseña debe tener al menos 8 caracteres.";
            ErrorContrasena.IsVisible = true;
            return;
        }

        ErrorContrasena.IsVisible = false;
        _resultado.TrySetResult(contrasena);
        await Navigation.PopModalAsync();
    }
}
