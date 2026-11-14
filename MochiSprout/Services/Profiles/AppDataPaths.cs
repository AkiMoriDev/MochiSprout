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
}
