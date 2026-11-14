using System.Windows;
namespace MochiSprout.Views;
/// <summary>Confirmación con el mismo esqueleto de modal. La opción por defecto (Enter/Esc) es la segura.</summary>
public partial class ConfirmWindow : Window
{
    private bool accepted;
    public ConfirmWindow()
    {
        InitializeComponent();
        OverlayHost.Attach(this, OverlayRoot, DialogPanel, 440, 640, fixedHeight: false);
        Loaded += (_, _) => CancelButton.Focus();
    }
    public static bool Ask(Window owner, string title, string message, string accept, string cancel, bool danger = false)
    {
        var dialog = new ConfirmWindow { Owner = owner };
        dialog.TitleText.Text = title; dialog.MessageText.Text = message;
        dialog.AcceptButton.Content = accept; dialog.CancelButton.Content = cancel;
        if (!danger) dialog.AcceptButton.Style = (Style)dialog.FindResource("BtnPrimary");
        OverlayHost.FitToOwner(dialog);
        dialog.ShowDialog();
        return dialog.accepted;
    }
    private void Accept(object sender, RoutedEventArgs e) { accepted = true; Close(); }
    private void Cancel(object sender, RoutedEventArgs e) => Close();
}
