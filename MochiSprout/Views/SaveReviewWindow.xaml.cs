using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MochiSprout.Controllers;
namespace MochiSprout.Views;
public partial class SaveReviewWindow : Window
{
 private readonly PresetController controller;
 public SaveReviewWindow(PresetController controller)
 {
  InitializeComponent();this.controller=controller;DataContext=controller;
  // Esc, X, flecha y clic en el overlay vuelven al editor: el cierre se traduce en BackToEditor.
  OverlayHost.Attach(this,OverlayRoot,DialogPanel,900,744,fixedHeight:true,canDismiss:()=>!controller.IsBusy);
  controller.PropertyChanged+=OnStateChanged;
  Closing+=(_,e)=>{if(controller.IsBusy)e.Cancel=true;};
  Closed+=(_,_)=>{controller.PropertyChanged-=OnStateChanged;if(controller.SaveReviewOpen)controller.BackToEditorCommand.Execute(null);};
  DialogPanel.SizeChanged+=(_,_)=>Stack(DialogPanel.ActualWidth<720);
  Loaded+=(_,_)=>ConfirmButton.Focus();
 }
 private void Stack(bool narrow)
 {
  RightColumn.Width=narrow?new GridLength(0):new GridLength(1,GridUnitType.Star);Gap.Width=new GridLength(narrow?0:14);
  void Place(FrameworkElement e,int row,int col){Grid.SetRow(e,row);Grid.SetColumn(e,col);}
  if(narrow){((Grid)ProjectBox.Parent).RowDefinitions.Clear();for(int i=0;i<4;i++)((Grid)ProjectBox.Parent).RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Place(ProjectBox,0,0);Place(BaseBox,1,0);Place(ExtraBox,2,0);Place(RepoBox,3,0);}
  else{Place(ProjectBox,0,0);Place(BaseBox,0,2);Place(ExtraBox,1,0);Place(RepoBox,1,2);}
 }
 private void OnStateChanged(object? sender,PropertyChangedEventArgs e)
 {
  if(e.PropertyName==nameof(PresetController.SaveReviewOpen)&&!controller.SaveReviewOpen)
   Dispatcher.BeginInvoke(new Action(Close));
 }
}
