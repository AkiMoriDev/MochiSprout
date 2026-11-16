using System.Windows;
using MochiSprout.Controllers;
namespace MochiSprout.Views;
/// <summary>Anfitrión superpuesto para Configuración: mismo esqueleto que los demás modales.</summary>
public partial class UtilityWindow:Window
{
 public UtilityWindow(PresetController controller,string title,FrameworkElement content)
 {
  InitializeComponent();DataContext=controller;Title=title;Body.Content=content;
  OverlayHost.Attach(this,OverlayRoot,DialogPanel,1080,744,fixedHeight:true,canDismiss:()=>!controller.IsBusy);
  Closing+=(_,e)=>{if(controller.IsBusy)e.Cancel=true;};
  Loaded+=(_,_)=>content.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First));
 }
}
