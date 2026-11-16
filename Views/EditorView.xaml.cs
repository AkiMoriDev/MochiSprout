using System.IO;
using System.Windows;
using System.Windows.Controls;
using MochiSprout.Controllers;
using MochiSprout.Models;
namespace MochiSprout.Views;
public partial class EditorView : UserControl
{
 public EditorView(){InitializeComponent();SizeChanged+=(_,_)=>{Clip=new System.Windows.Media.RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight),17,17);InformationColumn.Width=new GridLength(ActualWidth<980?260:320);};}
 public void FocusName(){NameInput.Focus();NameInput.CaretIndex=NameInput.Text.Length;}
 public void ResetEditor(){EditorTabs.SelectedIndex=0;}
 private void AddPackages(object sender,RoutedEventArgs e)=>(Window.GetWindow(this) as MainWindow ?? Application.Current.MainWindow as MainWindow)?.AddPackages(sender,e);
 private void CloseEditor(object sender,RoutedEventArgs e)=>(Window.GetWindow(this) as MainWindow ?? Application.Current.MainWindow as MainWindow)?.RequestCloseEditor();
 private void ShowAll(object sender,RoutedEventArgs e){if(DataContext is PresetController c)c.OnlySelectedPackages=false;}
 private void ShowSelected(object sender,RoutedEventArgs e){if(DataContext is PresetController c)c.OnlySelectedPackages=true;}
 private async void SaveDraft(object sender,RoutedEventArgs e){if(DataContext is not PresetController c||c.IsBusy)return;c.EditorMessage="";await c.SavePresetCommand.ExecuteAsync(null);if(!c.SaveReviewOpen&&c.IsEditor)c.EditorMessage=c.Status;}
 // ⓘ por fila: abre la ficha del paquete sin tocar la selección del borrador.
 private void ShowPackageInfo(object sender,RoutedEventArgs e)
 {
  if(DataContext is not PresetController c||(sender as FrameworkElement)?.DataContext is not CatalogPackage package)return;
  c.SelectedCatalogPackage=package;
  if(package.SelectedVersion is not null&&c.InspectorVersions.Contains(package.SelectedVersion))c.InspectedVersion=package.SelectedVersion;
  var window=new PackageInfoWindow(c);var owner=Window.GetWindow(this);if(owner is not null)window.Owner=owner;
  window.FitToOwner();
  try{window.ShowDialog();}finally{if(sender is UIElement opener&&opener.IsVisible)opener.Focus();}
 }
 // Arrastrar y soltar .unitypackage (AllowDrop): solo se aceptan archivos con esa extensión.
 private static string[] DroppedPackages(DragEventArgs e)=>e.Data.GetDataPresent(DataFormats.FileDrop)&&e.Data.GetData(DataFormats.FileDrop) is string[] files?files.Where(f=>string.Equals(Path.GetExtension(f),".unitypackage",StringComparison.OrdinalIgnoreCase)&&File.Exists(f)).ToArray():[];
 private void FilesDragOver(object sender,DragEventArgs e){var ok=DroppedPackages(e).Length>0;e.Effects=ok?DragDropEffects.Link:DragDropEffects.None;DropZone.Tag=ok?"over":null;e.Handled=true;}
 private void FilesDragLeave(object sender,DragEventArgs e)=>DropZone.Tag=null;
 private void FilesDrop(object sender,DragEventArgs e)
 {
  DropZone.Tag=null;e.Handled=true;
  if(DataContext is not PresetController c)return;
  var files=DroppedPackages(e);
  if(files.Length==0){c.EditorMessage="Solo se pueden enlazar archivos .unitypackage existentes.";return;}
  c.AddFiles(files);c.EditorMessage="";
 }
}
