using Kataleya.Contratos.Cuentas;

namespace Kataleya.Escritorio.Paginas.Dialogos;

public partial class DialogoDeCuentaPage : ContentPage
{
    private readonly IReadOnlyList<DatosDeRol> _roles;
    private readonly IReadOnlyList<DatosDeSede> _sedes;
    private readonly TaskCompletionSource<SolicitudDeCuenta?> _resultado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DialogoDeCuentaPage(
        IReadOnlyList<DatosDeRol> roles,
        IReadOnlyList<DatosDeSede> sedes,
        IReadOnlyDictionary<string, string>? errores = null,
        SolicitudDeCuenta? anterior = null)
    {
        InitializeComponent();
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        _sedes = sedes ?? throw new ArgumentNullException(nameof(sedes));

        SelectorRol.ItemsSource = _roles.ToList();
        SelectorRol.ItemDisplayBinding = new Binding(nameof(DatosDeRol.Nombre));
        SelectorSede.ItemsSource = _sedes.ToList();
        SelectorSede.ItemDisplayBinding = new Binding(nameof(DatosDeSede.Nombre));

        if (anterior is not null)
        {
            EntradaNombre.Text = anterior.Nombre;
            EntradaNombreUsuario.Text = anterior.NombreUsuario;
            EntradaContrasena.Text = anterior.ContrasenaInicial;
            SelectorRol.SelectedItem = _roles.FirstOrDefault(rol => rol.Id == anterior.IdRol);
            SelectorSede.SelectedItem = _sedes.FirstOrDefault(sede => sede.Id == anterior.IdSede);
        }

        MostrarErrores(errores);
    }

    public Task<SolicitudDeCuenta?> Resultado => _resultado.Task;

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

    private async void Crear_Clicked(object? sender, EventArgs e)
    {
        var errores = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(EntradaNombre.Text))
        {
            errores["nombre"] = "Escriba el nombre completo.";
        }

        if (string.IsNullOrWhiteSpace(EntradaNombreUsuario.Text))
        {
            errores["nombreUsuario"] = "Escriba el nombre de usuario.";
        }

        if (string.IsNullOrEmpty(EntradaContrasena.Text))
        {
            errores["contrasenaInicial"] = "Escriba la contraseña inicial.";
        }
        else if (EntradaContrasena.Text.Length < 8)
        {
            errores["contrasenaInicial"] = "La contraseña debe tener al menos 8 caracteres.";
        }

        var rol = SelectorRol.SelectedItem as DatosDeRol;
        if (rol is null)
        {
            errores["idRol"] = "Seleccione un rol.";
        }

        var sede = SelectorSede.SelectedItem as DatosDeSede;
        if (sede is null)
        {
            errores["idSede"] = "Seleccione una sede.";
        }

        if (errores.Count > 0)
        {
            MostrarErrores(errores);
            return;
        }

        var solicitud = new SolicitudDeCuenta
        {
            Nombre = EntradaNombre.Text.Trim(),
            NombreUsuario = EntradaNombreUsuario.Text.Trim(),
            ContrasenaInicial = EntradaContrasena.Text,
            IdRol = rol!.Id,
            IdSede = sede!.Id
        };

        _resultado.TrySetResult(solicitud);
        await Navigation.PopModalAsync();
    }

    private void MostrarErrores(IReadOnlyDictionary<string, string>? errores)
    {
        MostrarError(ErrorNombre, "nombre", errores);
        MostrarError(ErrorNombreUsuario, "nombreUsuario", errores);
        MostrarError(ErrorContrasena, "contrasenaInicial", errores);
        MostrarError(ErrorRol, "idRol", errores);
        MostrarError(ErrorSede, "idSede", errores);

        var conocidos = new[]
        {
            "nombre", "nombreUsuario", "contrasenaInicial", "idRol", "idSede"
        };

        var generales = errores?
            .Where(error => !conocidos.Contains(error.Key))
            .Select(error => error.Value)
            .Distinct()
            .ToList() ?? new List<string>();

        ErrorGeneral.Text = string.Join(Environment.NewLine, generales);
        ErrorGeneral.IsVisible = generales.Count > 0;
    }

    private static void MostrarError(
        Label etiqueta,
        string campo,
        IReadOnlyDictionary<string, string>? errores)
    {
        etiqueta.Text = errores is not null && errores.TryGetValue(campo, out var mensaje)
            ? mensaje
            : string.Empty;
        etiqueta.IsVisible = !string.IsNullOrWhiteSpace(etiqueta.Text);
    }
}
