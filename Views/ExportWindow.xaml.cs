using System.Windows;
using MochiSprout.Controllers;
namespace MochiSprout.Views;
/// <summary>Exportación guiada: destino, chequeos previos, progreso y resultado. No sobrescribe plantillas existentes.</summary>
public partial class ExportWindow : Window
{
    private readonly PresetController controller;
    public ExportWindow(PresetController controller)
    {
        InitializeComponent(); this.controller = controller; DataContext = controller;
        OverlayHost.Attach(this, OverlayRoot, DialogPanel, 560, 640, fixedHeight: false, canDismiss: () => !controller.IsBusy);
        Closing += (_, e) => { if (controller.IsBusy) e.Cancel = true; };
        Loaded += (_, _) => CancelButton.Focus();
    }
    private void CloseExport(object sender, RoutedEventArgs e) { if (!controller.IsBusy) Close(); }
}
