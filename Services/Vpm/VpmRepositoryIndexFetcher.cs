using System.Net.Http;
using System.Text.Json;

namespace MochiSprout.Services.Vpm;

public sealed record RemotePackage(string Id, string DisplayName, string Version, string RepositoryId, string RepositoryDisplayName);

public sealed class VpmRepositoryIndexFetcher
{
    private readonly HttpClient _http;

    public VpmRepositoryIndexFetcher(HttpClient http) => _http = http;

    public async Task<List<RemotePackage>> FetchPackagesAsync(
        string repositoryId, string repositoryDisplayName, string repositoryUrl, CancellationToken ct)
    {
        try
        {
            using var stream = await _http.GetStreamAsync(repositoryUrl, ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("packages", out var packagesElement)
                || packagesElement.ValueKind != JsonValueKind.Object)
                return [];

            var results = new List<RemotePackage>();
            foreach (var package in packagesElement.EnumerateObject())
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

                results.Add(new RemotePackage(package.Name, displayName, version, repositoryId, repositoryDisplayName));
            }

            return results;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return [];
        }
    }
}
