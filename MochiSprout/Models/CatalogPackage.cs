using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
namespace MochiSprout.Models;
public sealed record CatalogVersion(string Version, string? Unity, IReadOnlyDictionary<string,string> Dependencies, string Description = "", string Author = "") { public override string ToString() => Version; }
public partial class CatalogPackage : ObservableObject
{
 public required string Id { get; init; }
 public required string DisplayName { get; init; }
 public required string RepositoryId { get; init; }
 public required string RepositoryName { get; init; }
 public required string RepositoryUrl { get; init; }
 public required List<CatalogVersion> Versions { get; init; }
 public bool IsSdk => Id is "com.vrchat.base" or "com.vrchat.avatars" or "com.vrchat.worlds" or "com.vrchat.core.vpm-resolver";
 public bool CanSelect => !IsSdk;
 public bool IsAvailable { get; init; } = true;
 [ObservableProperty] private bool isSelected;
 [ObservableProperty] private CatalogVersion? selectedVersion;
 [ObservableProperty] private bool includePrerelease;
 public IReadOnlyList<CatalogVersion> AvailableVersions => Versions.Where(v=>IncludePrerelease || !v.Version.Contains('-') || v == SelectedVersion).ToList();
 public string LatestVersion => Versions.FirstOrDefault(v=>IncludePrerelease || !v.Version.Contains('-'))?.Version ?? "Sólo preliminares";
 public string LatestStableVersion => Versions.FirstOrDefault(v=>!v.Version.Contains('-'))?.Version ?? LatestVersion;
 public string VersionCount => Versions.Count == 1 ? "1 versión" : $"{Versions.Count} versiones";
 public string SourceLabel => $"{RepositoryName} · {Id}";
 partial void OnIncludePrereleaseChanged(bool value) { OnPropertyChanged(nameof(AvailableVersions)); OnPropertyChanged(nameof(LatestVersion)); }
 partial void OnSelectedVersionChanged(CatalogVersion? value) => OnPropertyChanged(nameof(AvailableVersions));
}
public sealed record CatalogSnapshot(IReadOnlyList<CatalogPackage> Packages,IReadOnlyList<string> Messages);
public sealed record TemplatePackageDetail(string Id,string Version);
public sealed class AppPreferences
{
 public string VccRoot {get;set;} = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VRChatCreatorCompanion");
 public List<MochiSprout.Services.Diagnostics.DetectedUnityInstall> ManualInstalls {get;set;} = [];
 public bool RememberLastPage {get;set;} = true;
 public bool ShowPrerelease {get;set;}
 public string LastPage {get;set;} = "Presets";
}


