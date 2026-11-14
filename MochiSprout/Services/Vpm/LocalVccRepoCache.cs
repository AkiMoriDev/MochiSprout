using System.IO;
using System.Text.Json;

namespace MochiSprout.Services.Vpm;

public sealed record CachedRepo(string Id, string Name, string Url, List<RemotePackage> Packages);

/// <summary>
/// VCC guarda una copia local del manifiesto completo de cada repositorio que conoce en
/// %LocalAppData%\VRChatCreatorCompanion\Repos\*.json. Leer esa caché es más confiable y
/// completo que reconstruir la lista de paquetes vía HTTP: cubre todos los repositorios
/// que el usuario ya tiene registrados (no sólo los predeterminados de esta app) y funciona
/// sin conexión.
/// </summary>
public static class LocalVccRepoCache
{
    public static List<CachedRepo> Load()
    {
        var repoDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VRChatCreatorCompanion", "Repos");

        if (!Directory.Exists(repoDir)) return [];

        // Cada vez que VCC refresca un repo guarda un archivo nuevo con hash distinto;
        // nos quedamos sólo con el más reciente por Id para no duplicar ni usar datos viejos.
        var latestById = new Dictionary<string, (DateTime Modified, CachedRepo Repo)>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(repoDir, "*.json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (!doc.RootElement.TryGetProperty("repo", out var repoEl)) continue;
                if (!repoEl.TryGetProperty("id", out var idEl)) continue;

                var id = idEl.GetString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                var modified = File.GetLastWriteTimeUtc(file);
                if (latestById.TryGetValue(id, out var existing) && existing.Modified >= modified)
                    continue;

                var name = repoEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? id : id;
                var url = repoEl.TryGetProperty("url", out var urlEl) ? urlEl.GetString() ?? "" : "";

                var packages = new List<RemotePackage>();
                if (repoEl.TryGetProperty("packages", out var packagesEl) && packagesEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var package in packagesEl.EnumerateObject())
                    {
                        var displayName = package.Name;
                        var version = "";

                        if (package.Value.TryGetProperty("versions", out var versions) && versions.ValueKind == JsonValueKind.Object
                            && VpmVersionHelper.SelectLatest(versions) is { } latest)
                        {
                            version = latest.Version;
                            if (latest.Data.ValueKind == JsonValueKind.Object
                                && latest.Data.TryGetProperty("displayName", out var dn)
                                && dn.ValueKind == JsonValueKind.String)
                            {
                                displayName = dn.GetString() ?? package.Name;
                            }
                        }

                        packages.Add(new RemotePackage(package.Name, displayName, version, id, name));
                    }
                }

                latestById[id] = (modified, new CachedRepo(id, name, url, packages));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                // Archivo corrupto, en uso, o con formato inesperado; se ignora ese repo puntual.
            }
        }

        return [.. latestById.Values.Select(v => v.Repo)];
    }
}
