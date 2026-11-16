using System.IO;
using System.Text.Json;
using MochiSprout.Contracts;
using MochiSprout.Models;
namespace MochiSprout.Services.Profiles;
public sealed class ProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string folder;
    private readonly SemaphoreSlim gate = new(1, 1);
    public ProfileStore() : this(AppDataPaths.Root) { }
    public ProfileStore(string folder) => this.folder = folder;
    private string FilePath => Path.Combine(folder, "profiles.json");
    public async Task<List<ProjectProfile>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(FilePath)) return [];
        await using var stream = File.OpenRead(FilePath);
        var doc = await JsonSerializer.DeserializeAsync<ProfilesDocument>(stream, Options, ct) ?? throw new InvalidDataException("Archivo de presets vacío.");
        if (doc.SchemaVersion > 2) throw new InvalidDataException("Versión de presets no compatible.");
        return doc.Profiles;
    }
    public async Task SaveAsync(List<ProjectProfile> profiles, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(folder);
            await using (var stream = File.Create(temp)) await JsonSerializer.SerializeAsync(stream, new ProfilesDocument { SchemaVersion = 2, Profiles = profiles }, Options, ct);
            File.Move(temp, FilePath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); gate.Release(); }
    }
}
