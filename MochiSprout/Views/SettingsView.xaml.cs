using System.Windows;
using System.Windows.Controls;
namespace MochiSprout.Views;
public partial class SettingsView : UserControl
{
 public SettingsView()
 {
  InitializeComponent();
  // Ventana angosta: la navegación pasa arriba, en fila.
  SizeChanged+=(_,_)=>{var narrow=ActualWidth<720;NavColumn.Width=new GridLength(narrow?0:220);Grid.SetColumn(NavHost,0);Grid.SetColumnSpan(NavHost,narrow?2:1);NavHost.Visibility=Visibility.Visible;
   if(narrow){NavItems.Orientation=Orientation.Horizontal;NavHost.BorderThickness=new Thickness(0,0,0,1);NavHost.VerticalAlignment=VerticalAlignment.Top;Grid.SetColumn(MainScroll,0);Grid.SetColumnSpan(MainScroll,2);MainScroll.Margin=new Thickness(0,62,0,0);}
   else{NavItems.Orientation=Orientation.Vertical;NavHost.BorderThickness=new Thickness(0,0,1,0);NavHost.VerticalAlignment=VerticalAlignment.Stretch;Grid.SetColumn(MainScroll,1);Grid.SetColumnSpan(MainScroll,1);MainScroll.Margin=new Thickness(0);}};
 }
 private static MainWindow? Main=>Application.Current?.MainWindow as MainWindow;
 private void CloseSettings(object sender,RoutedEventArgs e)=>Window.GetWindow(this)?.Close();
 private void EnableProjectImports(object sender,RoutedEventArgs e)=>Main?.EnableProjectImports(sender,e);
 private void ChooseVcc(object sender,RoutedEventArgs e)=>Main?.ChooseVcc(sender,e);
 private void AddUnity(object sender,RoutedEventArgs e)=>Main?.AddUnity(sender,e);
 private void ImportPresets(object sender,RoutedEventArgs e)=>Main?.ImportPresets(sender,e);
 private void BackupPresets(object sender,RoutedEventArgs e)=>Main?.BackupPresets(sender,e);
 private void OpenDataFolder(object sender,RoutedEventArgs e)=>Main?.OpenDataFolder(sender,e);
}
