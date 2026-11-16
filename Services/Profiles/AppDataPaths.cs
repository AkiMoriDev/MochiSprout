using System.IO;
namespace MochiSprout.Services.Profiles;

/// <summary>
/// Carpeta de datos de la app: %LocalAppData%\Akimorivt Systems\MochiSprout.
/// Contiene profiles.json, preferences.json y exported-templates.json.
/// </summary>
public static class AppDataPaths
{
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string Root => Path.Combine(LocalAppData, "Akimorivt Systems", "MochiSprout");
    /// <summary>Ubicación anterior (VCC Preset Studio). Se conserva intacta como respaldo.</summary>
    public static string LegacyRoot => Path.Combine(LocalAppData, "VCCPresetStudio");

    /// <summary>
    /// Si la carpeta nueva todavía no tiene datos y existe la anterior, copia su contenido sin sobrescribir nada.
    /// No borra ni mueve la carpeta anterior. Devuelve true si copió algo.
    /// </summary>
    public static bool MigrateLegacyData(string? root = null, string? legacyRoot = null)
    {
        root ??= Root; legacyRoot ??= LegacyRoot;
        if (!Directory.Exists(legacyRoot)) return false;
        if (File.Exists(Path.Combine(root, "profiles.json")) || File.Exists(Path.Combine(root, "preferences.json"))) return false;
        var copied = false;
        foreach (var source in Directory.EnumerateFiles(legacyRoot, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(root, Path.GetRelativePath(legacyRoot, source));
            if (File.Exists(target)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
            copied = true;
        }
        return copied;
    }

    /// <summary>
    /// Copia los presets y el registro de plantillas exportadas de VCC Preset Studio, sin preferencias (esas las elige
    /// el asistente de primer inicio) y sin sobrescribir nada. La carpeta anterior queda intacta.
    /// </summary>
    public static int ImportLegacyPresets(string root, string legacyRoot)
    {
        var copied = 0;
        Directory.CreateDirectory(root);
        foreach (var name in new[] { "profiles.json", "exported-templates.json" })
        {
            var source = Path.Combine(legacyRoot, name);
            var target = Path.Combine(root, name);
            if (!File.Exists(source) || File.Exists(target)) continue;
            File.Copy(source, target);
            copied++;
        }
        return copied;
    }
}
