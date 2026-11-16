using System.IO;
using System.Text.Json;
using MochiSprout.Models;

namespace MochiSprout.Services.Profiles;

/// <summary>
/// Guarda, por fuera de la carpeta de la plantilla (para no ensuciar los proyectos Unity que
/// VCC crea a partir de ella), el ProjectProfile original que generó cada plantilla exportada.
/// Permite reabrir "Crear nuevo perfil" con esos mismos datos para editarla.
/// </summary>
public sealed class ExportedTemplateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private readonly string ConfigFolder;
    public ExportedTemplateStore() : this(AppDataPaths.Root) { }
    public ExportedTemplateStore(string folder) => ConfigFolder = folder;

    private string MappingFile => Path.Combine(ConfigFolder, "exported-templates.json");

    public void SetMapping(string folderName, ProjectProfile profile)
    {
        var mapping = Load();
        mapping[folderName] = profile;
        Save(mapping);
    }

    public ProjectProfile? TryGet(string folderName)
    {
        var mapping = Load();
        return mapping.TryGetValue(folderName, out var profile) ? profile : null;
    }

    /// <summary>Copia de todos los mappings carpeta exportada → preset original.</summary>
    public IReadOnlyDictionary<string, ProjectProfile> All() => Load();

    public void Remove(string folderName)
    {
        var mapping = Load();
        if (mapping.Remove(folderName))
            Save(mapping);
    }

    private Dictionary<string, ProjectProfile> Load()
    {
        if (!File.Exists(MappingFile))
            return new Dictionary<string, ProjectProfile>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(MappingFile);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, ProjectProfile>>(json, JsonOptions);
            return loaded is null
                ? new Dictionary<string, ProjectProfile>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ProjectProfile>(loaded, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, ProjectProfile>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save(Dictionary<string, ProjectProfile> mapping)
    {
        Directory.CreateDirectory(ConfigFolder);
        var tempFile = MappingFile + ".tmp";
        File.WriteAllText(tempFile, JsonSerializer.Serialize(mapping, JsonOptions));
        File.Copy(tempFile, MappingFile, overwrite: true);
        File.Delete(tempFile);
    }
}



