using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MochiSprout.Controllers;
namespace MochiSprout.Views;
/// <summary>Asistente de primer inicio. La lógica vive en el controlador; acá solo hay diálogos nativos y layout.</summary>
public partial class SetupView : UserControl
{
    public SetupView()
    {
        InitializeComponent();
        // Ventana angosta: los pasos pasan arriba y las tarjetas de bienvenida se apilan.
        SizeChanged += (_, _) =>
        {
            var narrow = ActualWidth < 860;
            RailColumn.Width = narrow ? new GridLength(0) : new GridLength(260);
            Grid.SetColumnSpan(Rail, narrow ? 2 : 1);
            Rail.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            WelcomeTiles.Columns = ActualWidth < 980 ? 1 : 3;
        };
    }
    private PresetController? Controller => DataContext as PresetController;
    private Window Owner => Window.GetWindow(this) ?? Application.Current.MainWindow;

    private async void PickVcc(object sender, RoutedEventArgs e)
    {
        if (Controller is not { } c) return;
        var dialog = new OpenFolderDialog { Title = "Carpeta de datos de VCC que contiene settings.json", InitialDirectory = c.SetupVccPath };
        if (dialog.ShowDialog(Owner) == true) await c.ScanVccAsync(dialog.FolderName);
    }

    private void AddUnity(object sender, RoutedEventArgs e)
    {
        if (Controller is not { } c) return;
        var dialog = new OpenFileDialog { Title = "Elegí Unity.exe", Filter = "Unity Editor|Unity.exe" };
        if (dialog.ShowDialog(Owner) == true) c.SetupAddUnity(dialog.FileName);
    }

    private async void ImportBackup(object sender, RoutedEventArgs e)
    {
        if (Controller is not { } c) return;
        var dialog = new OpenFileDialog { Title = "Importar respaldo de presets", Filter = "Presets JSON|*.json" };
        if (dialog.ShowDialog(Owner) == true) await c.SetupImportBackupAsync(dialog.FileName);
    }

    private void OpenLink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { if (Controller is { } c) c.SetupMessage = $"No se pudo abrir el enlace: {ex.Message}"; }
        e.Handled = true;
    }

    /// <summary>
    /// Cuando el asistente es el contenido de su propia ventana: la tarjeta ocupa todo,
    /// sin margen, sombra ni esquinas, para que no quede una ventana dentro de otra.
    /// </summary>
    public void FillWindow()
    {
        Background = Card.Background;
        Frame.Margin = new Thickness(0);
        Card.MaxWidth = double.PositiveInfinity; Card.MinHeight = 0;
        Card.VerticalAlignment = VerticalAlignment.Stretch;
        Card.BorderThickness = new Thickness(0); Card.CornerRadius = new CornerRadius(0); Card.Effect = null;
        Rail.CornerRadius = new CornerRadius(0);
    }

    public void FocusPrimary() => Dispatcher.BeginInvoke(new Action(() => NextButton.Focus()));
}
