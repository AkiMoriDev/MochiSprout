using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
namespace MochiSprout.Views;

/// <summary>
/// Esqueleto compartido de los modales: ventana propietaria transparente, sin chrome, que cubre solo el área
/// cliente de la ventana dueña, con overlay oscuro y un panel centrado (header 60 / cuerpo / footer 64).
/// </summary>
public static class OverlayHost
{
    public static void Attach(Window window, FrameworkElement overlay, FrameworkElement panel, double maxWidth, double maxHeight, bool fixedHeight, Func<bool>? canDismiss = null)
    {
        void Layout()
        {
            if (window.Width is double.NaN || window.Width <= 0) return;
            panel.Width = Math.Min(maxWidth, Math.Max(320, window.Width * 0.94));
            var height = Math.Min(maxHeight, Math.Max(240, window.Height * 0.94));
            if (fixedHeight) panel.Height = height; else panel.MaxHeight = height;
        }
        window.SourceInitialized += (_, _) => { FitToOwner(window); Layout(); };
        window.Loaded += (_, _) => { FitToOwner(window); Layout(); };
        window.SizeChanged += (_, _) => Layout();
        panel.SizeChanged += (_, _) => { if (panel is System.Windows.Controls.Border { Child: UIElement child }) child.Clip = new RectangleGeometry(new Rect(0, 0, panel.ActualWidth - 2, panel.ActualHeight - 2), 17, 17); };
        overlay.MouseLeftButtonDown += (_, e) => { if (ReferenceEquals(e.OriginalSource, overlay) && (canDismiss?.Invoke() ?? true)) window.Close(); };
        window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && (canDismiss?.Invoke() ?? true)) { e.Handled = true; window.Close(); } };
    }

    /// <summary>Ubica la ventana sobre el contenido renderizado del dueño (sin bordes invisibles ni barra nativa), en coordenadas DPI del dueño.</summary>
    public static void FitToOwner(Window window)
    {
        if (window.Owner?.Content is not FrameworkElement client || client.ActualWidth <= 0 || client.ActualHeight <= 0) return;
        var screen = client.PointToScreen(new Point(0, 0));
        var transform = PresentationSource.FromVisual(client)?.CompositionTarget?.TransformFromDevice;
        var position = transform?.Transform(screen) ?? screen;
        window.Left = position.X; window.Top = position.Y; window.Width = client.ActualWidth; window.Height = client.ActualHeight;
    }
}
