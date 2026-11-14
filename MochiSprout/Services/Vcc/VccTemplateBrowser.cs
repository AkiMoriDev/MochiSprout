using System.IO;
using System.Text.Json;

namespace MochiSprout.Services.Vcc;

public sealed record VccTemplateInfo(string FolderName, string DisplayName, string? UnityVersion, int PackageCount, string FolderPath, bool IsDeletable, bool IsAvatar)
{
 public int FileCount => UnityPackageFileNamesPreview.Count + UnityPackageFilesRemainingCount;

    // Sólo se completan para plantillas creadas por la app (ver ProfilesViewModel.RefreshVccTemplates);
    // las predeterminadas de VCC no tienen archivos .unitypackage adjuntos.
    public override string ToString() => DisplayName;
    public string KindLabel => IsAvatar ? "Avatar" : "World";
    public IReadOnlyList<string> UnityPackageFileNamesPreview { get; init; } = [];
    public int UnityPackageFilesRemainingCount { get; init; }
}

/// <summary>
/// Lee las carpetas de plantillas reales que VCC conoce: las predeterminadas que trae de
/// fábrica (VRCTemplates) y las que el usuario fue creando (Templates). Sirve para mostrar
/// en la app lo que efectivamente aparecerá como opción al crear un proyecto nuevo en VCC.
/// </summary>
public static class VccTemplateBrowser
{
    private static string VccRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VRChatCreatorCompanion");

    public static string DefaultTemplatesRoot => Path.Combine(VccRoot, "VRCTemplates");
    public static string CreatedTemplatesRoot => Path.Combine(VccRoot, "Templates");

    public static List<VccTemplateInfo> List(string rootFolder, bool isDeletable)
    {
        var results = new List<VccTemplateInfo>();
        if (!Directory.Exists(rootFolder)) return results;

        foreach (var dir in Directory.EnumerateDirectories(rootFolder))
        {
            // "Base" es un building block interno del SDK (no un punto de partida elegible);
            // VCC no lo ofrece como opción al crear un proyecto nuevo, así que tampoco lo mostramos.
            if (!isDeletable && Path.GetFileName(dir).Equals("Base", StringComparison.OrdinalIgnoreCase))
                continue;

            var packageJsonPath = Path.Combine(dir, "package.json");
            if (!File.Exists(packageJsonPath)) continue;

            var displayName = Path.GetFileName(dir);
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));

                if (doc.RootElement.TryGetProperty("category", out var cat)
                    && cat.ValueKind == JsonValueKind.String
                    && !string.Equals(cat.GetString(), "ProjectTemplate", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (doc.RootElement.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String)
                    displayName = dn.GetString() ?? displayName;
            }
            catch (JsonException)
            {
                continue;
            }

            var packages = ReadPackages(dir);
            var packageIds = packages.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            results.Add(new VccTemplateInfo(
                Path.GetFileName(dir),
                displayName,
                ReadUnityVersion(dir),
                packages.Count,
                dir,
                isDeletable,
                IsAvatar: !packageIds.Contains("com.vrchat.worlds")));
        }

        return [.. results.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)];
    }

    private static string? ReadUnityVersion(string templateDir)
    {
        var path = Path.Combine(templateDir, "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(path)) return null;

        var line = File.ReadLines(path)
            .FirstOrDefault(l => l.StartsWith("m_EditorVersion:", StringComparison.OrdinalIgnoreCase));

        return line?["m_EditorVersion:".Length..].Trim();
    }

    public static List<(string Id, string Version)> ReadPackages(string templateDir)
    {
        var results = new List<(string Id, string Version)>();
        var path = Path.Combine(templateDir, "Packages", "vpm-manifest.json");
        if (!File.Exists(path)) return results;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("dependencies", out var deps) || deps.ValueKind != JsonValueKind.Object)
                return results;

            foreach (var dep in deps.EnumerateObject())
            {
                var version = dep.Value.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString() ?? "*"
                    : "*";
                results.Add((dep.Name, version));
            }
        }
        catch (JsonException)
        {
            // Manifiesto corrupto o con formato inesperado; devolvemos lo que se pudo leer.
        }

        return results;
    }
}


