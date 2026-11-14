using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace MochiSprout.Services.Diagnostics;
public sealed record DetectedUnityInstall(string Version, string EditorPath, bool IsManual = false) { public override string ToString() => Version; }
public sealed class UnityDetectionService
{
    public List<DetectedUnityInstall> Detect()
    {
        var results = new Dictionary<string, DetectedUnityInstall>(StringComparer.OrdinalIgnoreCase);
        foreach (var programFiles in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            var root = Path.Combine(programFiles, "Unity", "Hub", "Editor");
            try { if (Directory.Exists(root)) foreach (var dir in Directory.EnumerateDirectories(root)) Add(Path.Combine(dir, "Editor", "Unity.exe"), Path.GetFileName(dir), results); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        var json = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnityHub", "editors.json");
        try { if (File.Exists(json)) { using var doc = JsonDocument.Parse(File.ReadAllText(json)); Walk(doc.RootElement, null, results); } }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { }
        return results.Values.OrderByDescending(i => Numeric(i.Version)).ThenBy(i => i.EditorPath, StringComparer.OrdinalIgnoreCase).ToList();
    }
    private static void Walk(JsonElement el, string? hint, Dictionary<string, DetectedUnityInstall> output)
    {
        if (el.ValueKind == JsonValueKind.Array) { foreach (var child in el.EnumerateArray()) Walk(child, hint, output); return; }
        if (el.ValueKind != JsonValueKind.Object) return;
        var version = el.TryGetProperty("version", out var ver) && ver.ValueKind == JsonValueKind.String ? ver.GetString() : hint;
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Name is "location" or "path" && prop.Value.ValueKind == JsonValueKind.String)
            {
                var path = prop.Value.GetString(); if (!string.IsNullOrWhiteSpace(path)) { if (Directory.Exists(path)) path = Path.Combine(path, "Editor", "Unity.exe"); Add(path, version, output); }
            }
            else if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) Walk(prop.Value, Regex.IsMatch(prop.Name, @"^\d+\.\d+") ? prop.Name : version, output);
        }
    }
    private static void Add(string path, string? version, Dictionary<string, DetectedUnityInstall> output)
    {
        if (!File.Exists(path)) return;
        version ??= System.Diagnostics.FileVersionInfo.GetVersionInfo(path).ProductVersion?.Split(' ')[0];
        if (!string.IsNullOrWhiteSpace(version)) output[Path.GetFullPath(path)] = new(version, Path.GetFullPath(path));
    }
    private static Version Numeric(string value) { var match = Regex.Match(value, @"^\d+\.\d+\.\d+"); return Version.TryParse(match.Value, out var v) ? v : new Version(0, 0); }
}

