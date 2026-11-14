using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using MochiSprout.Controllers;
using MochiSprout.Models;
namespace MochiSprout;
/// <summary>Native window/dialog adapter. All application operations live in the controller.</summary>
public partial class MainWindow : Window
{
 private readonly PresetController controller;
 private bool closeReady;
 private IInputElement? editorOpener;
 private Views.UtilityWindow? utilityWindow;
 private Window DialogOwner => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w=>w.IsActive) ?? this;
 public MainWindow(PresetController controller)
 {
  InitializeComponent();this.controller=controller;DataContext=controller;
  SizeChanged+=(_,_)=>UpdateResponsiveLayout();
  EditorOverlay.SizeChanged+=(_,_)=>UpdateResponsiveLayout();
  StateChanged+=(_,_)=>UpdateMaximizedFrame();
  PreviewKeyDown+=OnPreviewKeyDown;
  controller.PropertyChanged+=EditorVisibilityChanged;
  controller.SaveReviewRequested+=OnSaveReviewRequested;
  controller.ExportRequested+=OnExportRequested;
  controller.EditorRequested+=OnEditorRequested;controller.UtilityRequested+=OnUtilityRequested;
  controller.SetupRequested+=OnSetupRequested;controller.SetupClosed+=OnSetupClosed;
  Closed+=(_,_)=>{controller.SetupRequested-=OnSetupRequested;controller.SetupClosed-=OnSetupClosed;controller.PropertyChanged-=EditorVisibilityChanged;controller.SaveReviewRequested-=OnSaveReviewRequested;controller.ExportRequested-=OnExportRequested;controller.EditorRequested-=OnEditorRequested;controller.UtilityRequested-=OnUtilityRequested;};
  Loaded+=async(_,_)=>{UpdateResponsiveLayout();await controller.InitializeAsync();if(controller.NeedsSetup)controller.BeginSetupCommand.Execute(null);};
  Closing+=async(_,e)=>{if(closeReady)return;e.Cancel=true;if(controller.IsBusy||controller.SaveReviewOpen)return;if(controller.IsEditor&&!CanDiscardEditor())return;try{await controller.SaveSessionAsync();}catch(Exception ex){controller.Status=$"No se pudo recordar la sesión: {ex.Message}";}finally{closeReady=true;Close();}};
 }
 private void OnPreviewKeyDown(object sender,KeyEventArgs e)
 {
  if(e.Key==Key.K&&Keyboard.Modifiers==ModifierKeys.Control&&!ModalOpen){PresetSearchInput.Focus();PresetSearchInput.SelectAll();e.Handled=true;return;}
  if(e.Key!=Key.Escape)return;
  if(controller.IsSetup){e.Handled=true;return;}   // el primer inicio no se descarta con Esc: tiene «Configurar después»
  if(PackagesOverlay.Visibility==Visibility.Visible){ClosePackages(this,new RoutedEventArgs());e.Handled=true;return;}
  if(controller.IsEditor&&!controller.IsBusy&&!controller.SaveReviewOpen){RequestCloseEditor();e.Handled=true;return;}
  if(controller.TemplateDetailsOpen){controller.CloseTemplateDetailsCommand.Execute(null);e.Handled=true;return;}
  if(PresetSearchInput.IsKeyboardFocusWithin&&controller.PresetSearch.Length>0){controller.PresetSearch="";e.Handled=true;}
 }
 private bool ModalOpen=>controller.IsSetup||controller.IsEditor||PackagesOverlay.Visibility==Visibility.Visible||controller.TemplateDetailsOpen;
 private void UpdateTitleActions(){var enabled=!ModalOpen;TitleActions.IsEnabled=enabled;SearchBox.IsEnabled=enabled;}
 private void UpdateResponsiveLayout()
 {
  var w=EditorOverlay.ActualWidth>0?EditorOverlay.ActualWidth:ActualWidth;var h=EditorOverlay.ActualHeight>0?EditorOverlay.ActualHeight:ActualHeight-74;
  var mw=Math.Min(1080,Math.Max(320,w*0.94));var mh=Math.Min(744,Math.Max(300,h*0.94));
  EditorPanel.Width=mw;EditorPanel.Height=mh;PackagesPanel.Width=mw;PackagesPanel.Height=mh;
  var width=RootGrid.ActualWidth>0?RootGrid.ActualWidth:ActualWidth;
  SearchBox.Visibility=width>0&&width<900?Visibility.Collapsed:Visibility.Visible;
  BrandSubtitle.Visibility=width>0&&width<1040?Visibility.Collapsed:Visibility.Visible;
 }
 // With WindowChrome a maximized window extends past the monitor by the resize frame; pad it back in.
 private void UpdateMaximizedFrame()
 {
  if(WindowState!=WindowState.Maximized){WindowRoot.Padding=new Thickness(0);MaximizeGlyph.Width=10;MaximizeGlyph.Height=10;return;}
  var frame=GetSystemMetrics(32)+GetSystemMetrics(92);var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(this);
  WindowRoot.Padding=new Thickness(frame/dpi.DpiScaleX,frame/dpi.DpiScaleY,frame/dpi.DpiScaleX,frame/dpi.DpiScaleY);
  MaximizeGlyph.Width=9;MaximizeGlyph.Height=9;
 }
 [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
 private void MinimizeWindow(object sender,RoutedEventArgs e)=>SystemCommands.MinimizeWindow(this);
 private void ToggleMaximize(object sender,RoutedEventArgs e){if(WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(this);else SystemCommands.MaximizeWindow(this);}
 private void CloseWindow(object sender,RoutedEventArgs e)=>Close();
 private void OnEditorRequested(object? sender,EventArgs e)
 {
  editorOpener=Keyboard.FocusedElement;
  if(utilityWindow?.IsVisible==true)utilityWindow.Close();
  PackagesOverlay.Visibility=Visibility.Collapsed;
  Activate();
  PresetEditor.ResetEditor();
  UpdateTitleActions();
  Dispatcher.BeginInvoke(new Action(()=>PresetEditor.FocusName()));
 }
 private bool CanDiscardEditor()=>!controller.HasUnsavedEditorChanges || Views.ConfirmWindow.Ask(DialogOwner,"Descartar cambios","Tenés cambios sin guardar. ¿Querés descartarlos?","Descartar","Seguir editando",danger:true);
 internal void RequestCloseEditor(){if(controller.IsBusy||controller.SaveReviewOpen||!CanDiscardEditor())return;controller.CancelEditCommand.Execute(null);}
 private void EditorVisibilityChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
 {
  if(e.PropertyName is nameof(PresetController.IsEditor) or nameof(PresetController.TemplateDetailsOpen) or nameof(PresetController.IsSetup))UpdateTitleActions();
  if(e.PropertyName==nameof(PresetController.IsEditor)&&!controller.IsEditor)Dispatcher.BeginInvoke(new Action(()=>{if(editorOpener is UIElement element&&element.IsVisible&&element.IsEnabled)Keyboard.Focus(element);}));
 }
 private void OnUtilityRequested(string section)
 {
  if(section=="Paquetes de VCC"){PackagesOverlay.Visibility=Visibility.Visible;UpdateTitleActions();Dispatcher.BeginInvoke(new Action(()=>PackagesPanel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))));return;}
  var opener=Keyboard.FocusedElement as UIElement;
  utilityWindow=new Views.UtilityWindow(controller,section,new Views.SettingsView()){Owner=DialogOwner};
  try{utilityWindow.ShowDialog();}finally{utilityWindow=null;if(opener?.IsVisible==true)opener.Focus();}
 }
 private void OnSetupRequested(object? sender,EventArgs e)
 {
  if(utilityWindow?.IsVisible==true)utilityWindow.Close();
  PackagesOverlay.Visibility=Visibility.Collapsed;
  UpdateTitleActions();
  Setup.FocusPrimary();
 }
 private void OnSetupClosed(object? sender,bool createPreset){UpdateTitleActions();if(createPreset)controller.NewPresetCommand.Execute(null);}
 internal void ClosePackages(object sender,RoutedEventArgs e){PackagesOverlay.Visibility=Visibility.Collapsed;UpdateTitleActions();}
 private void OnSaveReviewRequested(object? sender,EventArgs e)
 {
  var review = new Views.SaveReviewWindow(controller) { Owner = DialogOwner };
  review.ShowDialog();
 }
 private void OnExportRequested(object? sender,EventArgs e)
 {
  var opener=Keyboard.FocusedElement as UIElement;
  var export=new Views.ExportWindow(controller){Owner=DialogOwner};
  try{export.ShowDialog();}finally{if(opener?.IsVisible==true)opener.Focus();}
 }
 internal async void AddUnity(object sender,RoutedEventArgs e) {var d=new OpenFileDialog{Title="Elegí Unity.exe",Filter="Unity Editor|Unity.exe"};if(d.ShowDialog(DialogOwner)!=true)return;try{await controller.AddManualUnityAsync(d.FileName);}catch(Exception ex){controller.Status=ex.Message;}}
 internal void AddPackages(object sender,RoutedEventArgs e){var d=new OpenFileDialog{Title="Archivos adicionales",Filter="Unity Package|*.unitypackage",Multiselect=true};if(d.ShowDialog(DialogOwner)==true)controller.AddFiles(d.FileNames);}
 internal async void EnableProjectImports(object sender,RoutedEventArgs e) { var d=new OpenFolderDialog{Title="Proyecto Unity existente (carpeta con Assets y ProjectSettings)"}; if(d.ShowDialog(DialogOwner)==true)await controller.EnableProjectImportsAsync(d.FolderName); }
 internal void ChooseVcc(object sender,RoutedEventArgs e){var d=new OpenFolderDialog{Title="Carpeta de datos de VCC que contiene settings.json"};if(d.ShowDialog(DialogOwner)==true)controller.VccPathDraft=d.FolderName;}
 internal async void ImportPresets(object sender,RoutedEventArgs e){var d=new OpenFileDialog{Title="Importar profiles.json",Filter="Presets JSON|*.json"};if(d.ShowDialog(DialogOwner)!=true)return;try{await controller.ImportAsync(d.FileName);}catch(Exception ex){controller.Status=ex.Message;}}
 internal async void BackupPresets(object sender,RoutedEventArgs e){var d=new SaveFileDialog{Title="Respaldar presets",Filter="JSON|*.json",FileName="presets-backup.json"};if(d.ShowDialog(DialogOwner)!=true)return;try{await File.WriteAllTextAsync(d.FileName,JsonSerializer.Serialize(new ProfilesDocument{SchemaVersion=2,Profiles=[..controller.Presets]},new JsonSerializerOptions{WriteIndented=true}));controller.Status="Respaldo guardado.";}catch(Exception ex){controller.Status=ex.Message;}}
 internal void OpenDataFolder(object sender,RoutedEventArgs e){try{Directory.CreateDirectory(controller.DataRoot);Process.Start(new ProcessStartInfo("explorer.exe",$"\"{controller.DataRoot}\""){UseShellExecute=true});}catch(Exception ex){controller.Status=ex.Message;}}
}
