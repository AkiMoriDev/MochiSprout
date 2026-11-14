using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MochiSprout.Controllers;
namespace MochiSprout.Views;
public partial class PackageInfoWindow : Window
{
 private readonly DispatcherTimer copyTimer=new(){Interval=TimeSpan.FromSeconds(2)};
 public PackageInfoWindow(PresetController controller)
 {
  InitializeComponent();DataContext=controller;
  Loaded+=(_,_)=>{FitToOwner();CloseButton.Focus();};
  SourceInitialized+=(_,_)=>FitToOwner();
  SizeChanged+=(_,_)=>UpdateLayoutSize();
  DialogContent.SizeChanged+=(_,_)=>DialogContent.Clip=new System.Windows.Media.RectangleGeometry(new Rect(0,0,DialogContent.ActualWidth,DialogContent.ActualHeight),17,17);
  PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};
  copyTimer.Tick+=(_,_)=>{CopyNotice.Text="";copyTimer.Stop();};Closed+=(_,_)=>copyTimer.Stop();
 }
 public void FitToOwner()
 {
  if(Owner?.Content is not FrameworkElement client || client.ActualWidth<=0 || client.ActualHeight<=0)return;
  // The native window dimensions include invisible resize borders and its title bar.
  // The overlay must cover only the rendered client area, in the owner's DPI coordinates.
  var screen=client.PointToScreen(new Point(0,0));
  var transform=PresentationSource.FromVisual(client)?.CompositionTarget?.TransformFromDevice;
  var position=transform?.Transform(screen)??screen;
  Left=position.X;Top=position.Y;Width=client.ActualWidth;Height=client.ActualHeight;
  UpdateLayoutSize();
 }
 public void UpdateLayoutSize()
 {
  if(DialogPanel is null)return;
  DialogPanel.Width=Math.Min(900,Math.Max(320,Width*0.9));DialogPanel.MaxHeight=Math.Min(640,Height*0.9);
  BodyScroll.MaxHeight=Math.Max(100,DialogPanel.MaxHeight-90);
  var narrow=DialogPanel.Width<720;SideColumn.Width=new GridLength(narrow?0:300);Grid.SetColumn(MetadataPanel,narrow?0:1);Grid.SetRow(MetadataPanel,narrow?1:0);
  MetadataPanel.BorderThickness=narrow?new Thickness(0,1,0,0):new Thickness(1,0,0,0);DependencyScroll.MaxHeight=narrow?double.PositiveInfinity:BodyScroll.MaxHeight;
 }
 private void CloseInfo(object sender,RoutedEventArgs e)=>Close();
 private void OverlayClick(object sender,MouseButtonEventArgs e){if(ReferenceEquals(e.OriginalSource,OverlayRoot))Close();}
 private void CopyId(object sender,RoutedEventArgs e)
 {
  if(DataContext is not PresetController c||c.SelectedCatalogPackage is null)return;
  try{Clipboard.SetText(c.SelectedCatalogPackage.Id);CopyNotice.Text="Copiado";copyTimer.Stop();copyTimer.Start();}
  catch(System.Runtime.InteropServices.ExternalException){CopyNotice.Text="No se pudo copiar. Intentá de nuevo.";}
 }
}
