using System.Text.Json;

namespace MochiSprout.Services.Vpm;

/// <summary>
/// Los repositorios VPM suelen listar las versiones de un paquete con la más nueva primero,
/// pero eso no está garantizado. Esto elige la versión "estable" más alta comparando el
/// núcleo numérico (Major.Minor.Patch) y prefiriendo versiones sin sufijo de pre-release.
/// </summary>
public static class VpmVersionHelper
{
    public static (string Version, JsonElement Data)? SelectLatest(JsonElement versionsObject)
    {
        JsonProperty? best = null;
        Version? bestParsed = null;
        var bestIsPrerelease = true;

        foreach (var entry in versionsObject.EnumerateObject())
        {
            var (core, isPrerelease) = SplitCore(entry.Name);
            if (!Version.TryParse(core, out var parsed))
                continue;

            var better = best is null
                || parsed > bestParsed
                || (parsed == bestParsed && bestIsPrerelease && !isPrerelease);

            if (better)
            {
                best = entry;
                bestParsed = parsed;
                bestIsPrerelease = isPrerelease;
            }
        }

        if (best is { } found)
            return (found.Name, found.Value);

        var fallback = versionsObject.EnumerateObject().FirstOrDefault();
        return fallback.Value.ValueKind == JsonValueKind.Undefined ? null : (fallback.Name, fallback.Value);
    }

    private static (string Core, bool IsPrerelease) SplitCore(string version)
    {
        var dashIndex = version.IndexOf('-');
        var plusIndex = version.IndexOf('+');
        var cutIndex = dashIndex >= 0 ? dashIndex : plusIndex;
        var isPrerelease = dashIndex >= 0;

        var core = cutIndex >= 0 ? version[..cutIndex] : version;
        if (!core.Contains('.'))
            core += ".0";

        return (core, isPrerelease);
    }
}
