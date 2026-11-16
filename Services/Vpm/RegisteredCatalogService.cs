using System.IO;
using System.Text.Json;
using MochiSprout.Models;
namespace MochiSprout.Services.Vpm;
/// <summary>Read-only VCC adapter. Registration comes from settings; cached files alone are not registrations.</summary>
public sealed class RegisteredCatalogService
{
    private readonly string root;
    public RegisteredCatalogService() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VRChatCreatorCompanion")) { }
    public RegisteredCatalogService(string root) => this.root = root;
    public CatalogSnapshot Load()
    {
        var packages = new Dictionary<string, CatalogPackage>(StringComparer.OrdinalIgnoreCase);
        var messages = new List<string>();
        var settings = Path.Combine(root, "settings.json");
        if (!File.Exists(settings)) return new([], ["VCC no encontrado. Abrí Creator Companion o seleccioná su carpeta en Configuración."]);
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(settings));
            if (doc.RootElement.TryGetProperty("userRepos", out var repos) && repos.ValueKind == JsonValueKind.Array)
                foreach (var repo in repos.EnumerateArray())
                {
                    var local = Read(repo, "localPath");
                    if (string.IsNullOrWhiteSpace(local)) { messages.Add($"Sin caché: {Read(repo, "name")}"); continue; }
                    if (!Path.IsPathRooted(local)) local = Path.Combine(root, local);
                    ReadRepository(local, Read(repo, "id"), Read(repo, "name"), Read(repo, "url"), packages, messages);
                }
            // Official repositories are managed by VCC separately from userRepos.
            var cache = Path.Combine(root, "Repos");
            if (Directory.Exists(cache))
                foreach (var file in Directory.EnumerateFiles(cache, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
                {
                    try
                    {
                        using var candidate = JsonDocument.Parse(File.ReadAllText(file)); var r = Unwrap(candidate.RootElement);
                        var id = Read(r, "id"); if (id is "com.vrchat.repos.official" or "com.vrchat.repos.curated") ReadRepository(file, id, Read(r, "name"), Read(r, "url"), packages, messages);
                    }
                    catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { messages.Add($"No se pudo leer {Path.GetFileName(file)}."); }
                }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { messages.Add($"No se pudo leer settings.json: {ex.Message}"); }
        return new(packages.Values.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(), messages);
    }
    private static void ReadRepository(string file, string id, string name, string url, Dictionary<string, CatalogPackage> target, List<string> messages)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file)); var repo = Unwrap(doc.RootElement);
            if (string.IsNullOrWhiteSpace(id)) id = Read(repo, "id");
            if (string.IsNullOrWhiteSpace(name)) name = Read(repo, "name");
            if (string.IsNullOrWhiteSpace(url)) url = Read(repo, "url");
            if (!repo.TryGetProperty("packages", out var list) || list.ValueKind != JsonValueKind.Object) { messages.Add($"Formato de caché no reconocido: {name}"); return; }
            foreach (var pkg in list.EnumerateObject())
            {
                if (!pkg.Value.TryGetProperty("versions", out var versions) || versions.ValueKind != JsonValueKind.Object) continue;
                var choices = new List<CatalogVersion>(); var display = pkg.Name;
                foreach (var v in versions.EnumerateObject())
                {
                    if (v.Value.ValueKind != JsonValueKind.Object) continue;
                    if (choices.Count == 0 && !string.IsNullOrWhiteSpace(Read(v.Value, "displayName"))) display = Read(v.Value, "displayName");
                    var dependencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (v.Value.TryGetProperty("vpmDependencies", out var deps) && deps.ValueKind == JsonValueKind.Object)
                        foreach (var d in deps.EnumerateObject()) if (d.Value.ValueKind == JsonValueKind.String) dependencies[d.Name] = d.Value.GetString() ?? "*";
                    choices.Add(new(v.Name, Read(v.Value, "unity"), dependencies, Read(v.Value, "description"), ReadAuthor(v.Value)));
                }
                choices = choices.OrderBy(v => v.Version.Contains('-')).ThenByDescending(v => ParseVersion(v.Version)).ThenByDescending(v => v.Version, StringComparer.Ordinal).ToList();
                if (choices.Count == 0) continue;
                var key = id + "|" + pkg.Name;
                target.TryAdd(key, new CatalogPackage { Id = pkg.Name, DisplayName = display, RepositoryId = id, RepositoryName = name, RepositoryUrl = url, Versions = choices, SelectedVersion = choices[0] });
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { messages.Add($"Repositorio {name}: {ex.Message}"); }
    }
    private static string ReadAuthor(JsonElement value)
    {
        if(!value.TryGetProperty("author",out var author))return "";
        if(author.ValueKind==JsonValueKind.String)return author.GetString()??"";
        return author.ValueKind==JsonValueKind.Object?Read(author,"name"):"";
    }
    private static Version ParseVersion(string value) => Version.TryParse(value.Split('-', '+')[0], out var v) ? v : new Version(0, 0);
    private static JsonElement Unwrap(JsonElement root) => root.TryGetProperty("repo", out var repo) ? repo : root;
    private static string Read(JsonElement el, string key) => el.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}

