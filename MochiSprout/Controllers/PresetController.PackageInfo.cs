namespace MochiSprout.Controllers;
public sealed record PackageInfoDependency(string Id,string Version,string Context);
public partial class PresetController
{
 public string InspectedAuthor=>string.IsNullOrWhiteSpace(InspectedVersion?.Author)?"No declarado":InspectedVersion.Author;
 public string InfoDependencyTitle=>$"DEPENDENCIAS · {InspectedDependencies.Count}";
 public string InfoVersionsLabel=>$"{InspectorVersions.Count} versiones disponibles";
 public IReadOnlyList<PackageInfoDependency> InfoDependencies=>InspectedDependencies.Select(d=>new PackageInfoDependency(d.Id,d.Version,
  d.Id is "com.vrchat.avatars" or "com.vrchat.worlds" or "com.vrchat.base" or "com.vrchat.core.vpm-resolver"?"SDK de VRChat · ya incluido en la plantilla":
  !string.IsNullOrWhiteSpace(InspectedVersion?.Author)&&Packages.Any(p=>p.Id==d.Id&&p.Versions.Any(v=>string.Equals(v.Author,InspectedVersion.Author,StringComparison.OrdinalIgnoreCase)))?"Del mismo autor":"Dependencia del paquete")).ToList();
 private void UpdatePackageInfo(){OnPropertyChanged(nameof(InspectedAuthor));OnPropertyChanged(nameof(InfoDependencyTitle));OnPropertyChanged(nameof(InfoDependencies));OnPropertyChanged(nameof(InfoVersionsLabel));}
}
