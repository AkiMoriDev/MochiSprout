using System.Windows;
using System.Windows.Controls;
using MochiSprout.Controllers;
using MochiSprout.Models;
namespace MochiSprout.Views;
public partial class CatalogView : UserControl
{
 public CatalogView(){InitializeComponent();SizeChanged+=(_,_)=>Clip=new System.Windows.Media.RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight),17,17);}
 private void CloseCatalog(object sender,RoutedEventArgs e)=>(Window.GetWindow(this) as MainWindow)?.ClosePackages(sender,e);
 private void UsePackage(object sender,RoutedEventArgs e){if(DataContext is not PresetController c||c.IsBusy||(sender as FrameworkElement)?.DataContext is not CatalogPackage package)return;c.SelectedCatalogPackage=package;c.InspectedVersion=package.Versions.FirstOrDefault(v=>v.Version==(c.OnlyStable?package.LatestStableVersion:package.LatestVersion));c.UseCatalogPackageCommand.Execute(null);}
 private void ShowPackageInfo(object sender,RoutedEventArgs e){if((sender as FrameworkElement)?.DataContext is CatalogPackage package)OpenInfo(package,sender as FrameworkElement);}
 private void PackageRowClick(object sender,System.Windows.Input.MouseButtonEventArgs e)
 {
  for(var node=e.OriginalSource as DependencyObject;node is not null&&node!=sender;node=System.Windows.Media.VisualTreeHelper.GetParent(node))if(node is System.Windows.Controls.Primitives.ButtonBase)return;
  if((sender as FrameworkElement)?.DataContext is CatalogPackage package)OpenInfo(package,sender as FrameworkElement);
 }
 private void OpenInfo(CatalogPackage package,FrameworkElement? opener)
 {
  if(DataContext is not PresetController c)return;c.SelectedCatalogPackage=package;
  var window=new PackageInfoWindow(c);var owner=Window.GetWindow(this);if(owner is not null)window.Owner=owner;
  window.FitToOwner();
  try{window.ShowDialog();}finally{if(opener?.IsVisible==true)opener.Focus();}
 }
}
