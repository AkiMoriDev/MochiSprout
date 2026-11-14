using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MochiSprout.Models;
using MochiSprout.Services.Vcc;
namespace MochiSprout.Controllers;
public partial class PresetController
{
 public event EventHandler? EditorRequested;
 public event Action<string>? UtilityRequested;
 private static readonly string[] NavigationPages=["Presets","Crear preset","Paquetes de VCC","Configuración"];
 private AppPreferences savedPreferences=new();
 private bool restoringSources;
 public IReadOnlyList<VccTemplateInfo> BaseTemplates => DefaultTemplates.Concat(Templates).ToList();
 public ObservableCollection<VccTemplateInfo> DefaultTemplates {get;}=[];
 public ObservableCollection<TemplatePackageDetail> TemplateDetails {get;}=[];
 public ICollectionView DefaultTemplateView {get;private set;}=null!;
 public ICollectionView ExportedTemplateView {get;private set;}=null!;
 public ICollectionView CatalogBrowseView {get;private set;}=null!;
 public IReadOnlyList<string> PresetFilters {get;}=["Todos","Mis presets","Predeterminados VCC"];
 public IReadOnlyList<string> ThemeOptions {get;}=["Oscuro"];
 public IReadOnlyList<string> LanguageOptions {get;}=["Español"];
 [ObservableProperty] private string presetFilter="Todos";
 [ObservableProperty] private string browseSearch="";
 [ObservableProperty] private string browseRepository="Todos los repositorios";
 [ObservableProperty] private bool onlyStable=true;
 [ObservableProperty] private CatalogPackage? selectedCatalogPackage;
 [ObservableProperty] private CatalogVersion? inspectedVersion;
 [ObservableProperty] private VccTemplateInfo? selectedBaseTemplate;
 [ObservableProperty] private bool templateDetailsOpen;
 [ObservableProperty] private string templateDetailsTitle="";
 [ObservableProperty] private string vccPathDraft="";
 [ObservableProperty] private bool rememberLastPage=true;
 [ObservableProperty] private bool showPrerelease;
 public string VccConnectionLabel=>File.Exists(Path.Combine(vccRoot,"settings.json"))?"●  VCC conectado":"○  VCC sin configurar";
 public string DefaultCount=>$"{DefaultTemplates.Count} plantillas base detectadas";
 public string CatalogCount=>$"{Packages.Count(p=>p.IsAvailable)} paquetes · {Packages.Where(p=>p.IsAvailable).Select(p=>p.RepositoryId).Distinct().Count()} repositorios";
 public string FilteredCatalogCount=>$"{CatalogBrowseView.Cast<object>().Count()} paquetes visibles";
 public bool ShowOwnPresets=>PresetFilter!="Predeterminados VCC";
 public bool ShowDefaultPresets=>PresetFilter!="Mis presets";
 public string SelectedPackageCount=>$"{catalog.Count(p=>p.IsSelected&&!p.IsSdk)} paquetes seleccionados";
 public IReadOnlyList<CatalogPackage> SelectedPackages=>catalog.Where(p=>p.IsSelected&&!p.IsSdk).ToList();
 public IReadOnlyList<CatalogVersion> InspectorVersions=>SelectedCatalogPackage?.Versions.Where(v=>!OnlyStable||!v.Version.Contains('-')).ToList()??[];
 public string InspectedDescription=>string.IsNullOrWhiteSpace(InspectedVersion?.Description)?"Este repositorio no incluye una descripción para esta versión.":InspectedVersion.Description;
 public string InspectedUnity=>string.IsNullOrWhiteSpace(InspectedVersion?.Unity)?"No declarado":InspectedVersion.Unity;
 public IReadOnlyList<TemplatePackageDetail> InspectedDependencies=>InspectedVersion?.Dependencies.Select(d=>new TemplatePackageDetail(d.Key,d.Value)).ToList()??[];
 private void InitializeNavigation()
 {
  DefaultTemplateView=new ListCollectionView(DefaultTemplates);
  DefaultTemplateView.Filter=o=>o is VccTemplateInfo t&&t.DisplayName.Contains(PresetSearch,StringComparison.OrdinalIgnoreCase);
  ExportedTemplateView=new ListCollectionView(Templates);
  ExportedTemplateView.Filter=o=>o is VccTemplateInfo t&&t.DisplayName.Contains(PresetSearch,StringComparison.OrdinalIgnoreCase);
  CatalogBrowseView=new ListCollectionView(Packages);
  CatalogBrowseView.Filter=o=>o is CatalogPackage p&&p.IsAvailable&&(BrowseRepository=="Todos los repositorios"||p.RepositoryName==BrowseRepository)&&$"{p.DisplayName} {p.Id}".Contains(BrowseSearch,StringComparison.OrdinalIgnoreCase)&&(!OnlyStable||p.Versions.Any(v=>!v.Version.Contains('-')));
  Packages.CollectionChanged+=(_,_)=>{OnPropertyChanged(nameof(CatalogCount));OnPropertyChanged(nameof(FilteredCatalogCount));};
 }
 partial void OnPresetFilterChanged(string value){OnPropertyChanged(nameof(ShowOwnPresets));OnPropertyChanged(nameof(ShowDefaultPresets));}
 partial void OnBrowseSearchChanged(string value)=>RefreshCatalogFilter();
 partial void OnBrowseRepositoryChanged(string value)=>RefreshCatalogFilter();
 partial void OnOnlyStableChanged(bool value){RefreshCatalogFilter();OnPropertyChanged(nameof(InspectorVersions));InspectedVersion=InspectorVersions.FirstOrDefault();}
 private void RefreshCatalogFilter(){CatalogBrowseView?.Refresh();OnPropertyChanged(nameof(FilteredCatalogCount));}
 partial void OnSelectedCatalogPackageChanged(CatalogPackage? value){UpdatePackageInfo();OnPropertyChanged(nameof(InspectorVersions));InspectedVersion=InspectorVersions.FirstOrDefault();}
 partial void OnInspectedVersionChanged(CatalogVersion? value){UpdatePackageInfo();OnPropertyChanged(nameof(InspectedDescription));OnPropertyChanged(nameof(InspectedUnity));OnPropertyChanged(nameof(InspectedDependencies));}
 partial void OnShowPrereleaseChanged(bool value){foreach(var p in catalog)p.IncludePrerelease=value;}
 partial void OnSelectedBaseTemplateChanged(VccTemplateInfo? value)
 {
  if(value is null || restoringSources)return;
  Mode=value.IsAvatar?ProjectMode.Avatar:ProjectMode.World;
  SelectedUnity=Installs.FirstOrDefault(i=>i.Version==value.UnityVersion)??Installs.FirstOrDefault(i=>i.Version.Split('.')[0]==value.UnityVersion?.Split('.')[0])??SelectedUnity;
  UpdateSummary();
 }
 [RelayCommand] private void SetMode(ProjectMode value)=>Mode=value;
 [RelayCommand] private void UseTemplate(VccTemplateInfo? template)
 {
  if(template is null)return;
  PrepareNewPreset();SelectedBaseTemplate=template;
  // A new draft never inherits extra packages, files, name or description.
  Name="";Notes="";
  Mode=template.IsAvatar?ProjectMode.Avatar:ProjectMode.World;
  UpdateSummary();MarkEditorClean();
  EditorRequested?.Invoke(this,EventArgs.Empty);
 }
 [RelayCommand] private void ShowTemplatePackages(VccTemplateInfo? template)
 {
  if(template is null)return;
  try {TemplateDetails.Clear();foreach(var p in VccTemplateBrowser.ReadPackages(template.FolderPath))TemplateDetails.Add(new(p.Id,p.Version));TemplateDetailsTitle=template.DisplayName;TemplateDetailsOpen=true;}
  catch(Exception ex){Status=$"No se pudo leer la plantilla: {ex.Message}";}
 }
 [RelayCommand] private void CloseTemplateDetails()=>TemplateDetailsOpen=false;
 [RelayCommand] private void UseCatalogPackage()
 {
  var p=SelectedCatalogPackage;var version=InspectedVersion;
  if(p is null||p.IsSdk){Status="El SDK se incluye desde la plantilla base Avatar/World.";return;}
  if(!IsEditor)PrepareNewPreset();
  p.IsSelected=true;p.SelectedVersion=version??p.SelectedVersion;
  UpdateSummary();Status=$"{p.DisplayName} agregado al borrador del preset.";
  EditorRequested?.Invoke(this,EventArgs.Empty);
 }
 [RelayCommand] private async Task SaveSettingsAsync()
 {
  if(IsBusy)return;
  if(!File.Exists(Path.Combine(VccPathDraft,"settings.json"))){Status="Seleccioná la carpeta de datos de VCC que contiene settings.json.";return;}
  IsBusy=true;
  try {vccRoot=Path.GetFullPath(VccPathDraft);await SavePreferencesAsync();OnPropertyChanged(nameof(VccRoot));await RefreshSourcesCoreAsync();Status="Configuración guardada.";}
  catch(Exception ex){Status=$"No se pudo guardar la configuración: {ex.Message}";}
  finally{IsBusy=false;}
 }
 [RelayCommand] private void ResetSettings()
 {
  VccPathDraft=vccRoot;RememberLastPage=savedPreferences.RememberLastPage;ShowPrerelease=savedPreferences.ShowPrerelease;
  Status="Cambios de configuración descartados.";
 }
 public async Task SaveSessionAsync()
 {
  // Closing remembers navigation without silently applying unsaved preference toggles.
  if(!File.Exists(PreferencesFile)&&!File.Exists(Path.Combine(vccRoot,"settings.json")))return;
  var remember=RememberLastPage;var preview=ShowPrerelease;
  RememberLastPage=savedPreferences.RememberLastPage;ShowPrerelease=savedPreferences.ShowPrerelease;
  try{await SavePreferencesAsync();}finally{RememberLastPage=remember;ShowPrerelease=preview;}
 }
}


