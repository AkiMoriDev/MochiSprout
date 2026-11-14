using System.IO;
using CommunityToolkit.Mvvm.Input;
using MochiSprout.Models;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
namespace MochiSprout.Controllers;
public partial class PresetController
{
 [ObservableProperty] private bool onlySelectedPackages;
 [ObservableProperty] private string editorMessage = "";
 private string editorBaseline = "";
 public string SelectedProjectVariant => $"{Mode}-{SelectedUnity?.Version.Split('.')[0]}";
 private bool CanSelectProjectVariant(string? variant)=>variant is not null && variant.Split('-') is ["Avatar" or "World", "2019" or "2022"] parts && Installs.Any(i=>i.Version.StartsWith(parts[1]+".")&&File.Exists(i.EditorPath));
 [RelayCommand(CanExecute=nameof(CanSelectProjectVariant))]
 private void SelectProjectVariant(string? variant)
 {
  if(!CanSelectProjectVariant(variant))return;
  var parts=variant!.Split('-');var mode=parts[0]=="Avatar"?ProjectMode.Avatar:ProjectMode.World;var year=parts[1];
  var install=SelectedUnity is not null && SelectedUnity.Version.StartsWith(year+".") && File.Exists(SelectedUnity.EditorPath)?SelectedUnity:Installs.Where(i=>i.Version.StartsWith(year+".")&&File.Exists(i.EditorPath)).OrderByDescending(i=>TryUnityVersion(i.Version,out var v)?v:new Version()).First();
  Mode=mode;
  if(SelectedBaseTemplate is null || SelectedBaseTemplate.IsAvatar!=(mode==ProjectMode.Avatar) || SelectedBaseTemplate.UnityVersion?.Split('.')[0]!=year)
   SelectedBaseTemplate=DefaultTemplates.FirstOrDefault(t=>t.IsAvatar==(mode==ProjectMode.Avatar)&&t.UnityVersion?.Split('.')[0]==year);
  SelectedUnity=install;EditorMessage="";UpdateSummary();
 }
 private void UpdateProjectVariants(){OnPropertyChanged(nameof(SelectedProjectVariant));SelectProjectVariantCommand.NotifyCanExecuteChanged();}
 public string EditorModalTitle => editingId is null ? "Crear preset" : $"Editar “{Name}”";
 public string EditorSaveLabel => editingId is null ? "Crear preset" : "Guardar cambios";
 public int EditorPackageCount => catalog.Count(p=>p.IsSelected&&!p.IsSdk);
 public int EditorFileCount => Attachments.Count;
 public string SelectedFilterLabel => $"Seleccionados ({EditorPackageCount})";
 public string EditorFooterSummary => $"{EditorPackageCount} paquetes · {EditorFileCount} archivos";
 public bool HasUnsavedEditorChanges => IsEditor && editorBaseline != EditorFingerprint();
 public void MarkEditorClean(){editorBaseline=EditorFingerprint();OnPropertyChanged(nameof(EditorModalTitle));OnPropertyChanged(nameof(EditorSaveLabel));}
 private string EditorFingerprint()=>JsonSerializer.Serialize(new {
  Name,Notes,Mode,Unity=SelectedUnity?.EditorPath,UnityVersion=SelectedUnity?.Version,Base=SelectedBaseTemplate?.FolderPath,
  Packages=catalog.Where(p=>p.IsSelected&&!p.IsSdk).OrderBy(p=>p.RepositoryId).ThenBy(p=>p.Id).Select(p=>new{p.Id,p.RepositoryId,Version=p.SelectedVersion?.Version}).ToArray(),
  Files=Attachments.Select(f=>f.FullPath).ToArray()
 });
 partial void OnOnlySelectedPackagesChanged(bool value)=>PackageView.Refresh();
 partial void OnNameChanged(string value){OnPropertyChanged(nameof(EditorModalTitle));if(IsEditor)UpdateSummary();}
}
