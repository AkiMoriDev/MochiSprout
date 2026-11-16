using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MochiSprout.Models;
using MochiSprout.Services.Vcc;
namespace MochiSprout.Controllers;
public sealed record ReviewPackage(string DisplayName,string Id,string Version,string Repository);
public partial class PresetController
{
 public event EventHandler? SaveReviewRequested;
 [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ConfirmSaveCommand))] private ProjectProfile? pendingPreset;
 [ObservableProperty] private bool saveReviewOpen;
 [ObservableProperty] private string reviewValidation="";
 [ObservableProperty] private string reviewBaseName="";
 [ObservableProperty] private string reviewBaseSource="";
 [ObservableProperty] private string reviewSaveError="";
 public ObservableCollection<ReviewPackage> ReviewPackages {get;}=[];
 public ObservableCollection<TemplatePackageDetail> ReviewBasePackages {get;}=[];
 private bool CanConfirmSave()=>PendingPreset is not null && SaveReviewOpen && !IsBusy;
 private async Task PrepareSaveReviewAsync(ProjectProfile profile,IReadOnlyList<CatalogPackage> chosen)
 {
  IsBusy=true;
  try
  {
   ReviewPackages.Clear();
   foreach(var package in chosen)ReviewPackages.Add(new(package.DisplayName,package.Id,package.SelectedVersion?.Version??"*",package.RepositoryName));
   ReviewValidation=Validation;ReviewSaveError="";
   var template=SelectedBaseTemplate??DefaultTemplates.FirstOrDefault(t=>t.IsAvatar==(profile.Mode==ProjectMode.Avatar)&&t.UnityVersion?.Split('.')[0]==profile.UnityVersion?.Split('.')[0]&&t.FolderName.StartsWith(profile.Mode.ToString(),StringComparison.OrdinalIgnoreCase));
   ReviewBaseName=template?.DisplayName??"Automática (no se detectó una base local compatible)";
   ReviewBaseSource=template is null?"Se requiere una base compatible para exportar a VCC.":template.IsDeletable?"Plantilla personal de VCC":"Predeterminada de VCC · solo lectura";
   ReviewBasePackages.Clear();
   if(template is not null)
   {
    var packages=await Task.Run(()=>VccTemplateBrowser.ReadPackages(template.FolderPath));
    foreach(var package in packages)ReviewBasePackages.Add(new(package.Id,package.Version));
   }
   PendingPreset=profile;SaveReviewOpen=true;
  }
  catch(Exception ex){Status=$"No se pudo preparar el resumen: {ex.Message}";return;}
  finally{IsBusy=false;}
  ConfirmSaveCommand.NotifyCanExecuteChanged();
  SaveReviewRequested?.Invoke(this,EventArgs.Empty);
 }
 [RelayCommand] private void BackToEditor()
 {
  if(IsBusy)return;
  SaveReviewOpen=false;PendingPreset=null;ReviewSaveError="";
  Status="Revisá el preset y volvé a guardar cuando esté listo.";
 }
}
