using System.Runtime.InteropServices;
using System.Windows;
using MochiSprout.Controllers;
namespace MochiSprout.Views;
/// <summary>
/// Ventana propia de la configuración inicial: se abre antes que la app principal cuando todavía no se configuró.
/// Al terminar avisa con <see cref="Completed"/>; cerrarla sin terminar no guarda nada.
/// </summary>
public partial class SetupWindow : Window
{
    private readonly PresetController controller;
    /// <summary>Se configuró todo. El valor indica si hay que abrir el editor de un preset nuevo.</summary>
    public event EventHandler<bool>? Completed;
    public SetupWindow(PresetController controller)
    {
        InitializeComponent(); this.controller = controller; DataContext = controller;
        Setup.FillWindow();
        controller.SetupClosed += OnSetupClosed;
        Closed += (_, _) => controller.SetupClosed -= OnSetupClosed;
        Closing += (_, e) => { if (controller.IsBusy) e.Cancel = true; };
        StateChanged += (_, _) => UpdateMaximizedFrame();
        Loaded += async (_, _) => { await controller.PrepareSetupAsync(); Setup.FocusPrimary(); };
    }
    private void OnSetupClosed(object? sender, bool createPreset) => Completed?.Invoke(this, createPreset);
    private void UpdateMaximizedFrame()
    {
        if (WindowState != WindowState.Maximized) { WindowRoot.Padding = new Thickness(0); return; }
        var frame = GetSystemMetrics(32) + GetSystemMetrics(92); var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        WindowRoot.Padding = new Thickness(frame / dpi.DpiScaleX, frame / dpi.DpiScaleY, frame / dpi.DpiScaleX, frame / dpi.DpiScaleY);
    }
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    private void MinimizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void ToggleMaximize(object sender, RoutedEventArgs e) { if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this); else SystemCommands.MaximizeWindow(this); }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
