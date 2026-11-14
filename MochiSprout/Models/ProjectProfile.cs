using System.Text.Json.Serialization;

namespace MochiSprout.Models;

public enum UnityTarget
{
    Unity2022,
    Unity2019Legacy, Other
}

public enum ProjectMode
{
    Avatar,
    World
}

public sealed class ProjectProfile
{
    public required string Id { get; init; }
    public required string DisplayName { get; set; }
    public required UnityTarget Target { get; init; }
    public ProjectMode Mode { get; init; } = ProjectMode.Avatar;
    public string? BaseTemplateName { get; init; }
    public bool BaseTemplateIsUser { get; init; }
    public string? UnityVersion { get; init; }
    public string? UnityEditorPath { get; init; }
    public List<RepositoryEntry> Repositories { get; init; } = [];
    public List<PackageEntry> Packages { get; init; } = [];
    public List<UnityPackageReference> UnityPackages { get; init; } = [];
    public string? Notes { get; set; }

    [JsonIgnore]
    public string UnityDisplayLabel =>
        UnityVersion ?? (Target == UnityTarget.Unity2022 ? "Unity 2022" : Target == UnityTarget.Unity2019Legacy ? "Unity 2019 Legacy" : "Unity personalizado");
}

public sealed class UnityPackageReference
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }

    /// <summary>Estado en vivo para la UI: la referencia apunta a un archivo que sigue existiendo.</summary>
    [JsonIgnore]
    public bool Exists => System.IO.File.Exists(FullPath);
}

public sealed class RepositoryEntry
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Url { get; init; }
    public bool Required { get; init; } = true;
}

public sealed class PackageEntry
{
    public required string Id { get; init; }
    public string? Version { get; init; }
    public string? RepositoryId { get; init; }
    public bool EnabledByDefault { get; init; } = true;
}




