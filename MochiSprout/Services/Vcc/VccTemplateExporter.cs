using System.IO;
using System.Text.Json;
using MochiSprout.Models;
using MochiSprout.Services.Profiles;

namespace MochiSprout.Services.Vcc;

/// <summary>
/// Genera una carpeta de plantilla dentro de %LocalAppData%\VRChatCreatorCompanion\Templates
/// a partir de un perfil guardado, para que aparezca como opción al crear un proyecto nuevo
/// directamente desde VRChat Creator Companion.
///
/// Parte de las plantillas oficiales que VCC ya trae en VRCTemplates\ (Avatar, Avatar 2019,
/// World, World 2019) porque esas ya incluyen un ProjectSettings/Packages válidos para cada
/// generación del SDK; sólo se les fusionan los paquetes elegidos y se ajusta la versión de
/// Unity declarada.
/// </summary>
public sealed class VccTemplateExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ExportedTemplateStore _templateStore;

    public VccTemplateExporter(ExportedTemplateStore templateStore) => _templateStore = templateStore;

    /// <summary>Registro de plantillas exportadas (carpeta → preset) que usa la biblioteca para marcar «En VCC».</summary>
    public ExportedTemplateStore TemplateStore => _templateStore;

    /// <summary>Carpeta de destino que usaría <see cref="ExportTemplate"/> para este preset.</summary>
    public static string DestinationFor(ProjectProfile profile, string vccRoot) =>
        Path.Combine(vccRoot, "Templates", SanitizeFolderName(profile.DisplayName));

    /// <summary>Busca la base compatible igual que la exportación, sin escribir nada. Lanza la misma explicación si falta.</summary>
    public static string ResolveBaseFolder(ProjectProfile profile, string vccRoot)
    {
        var templatesRoot = Path.Combine(vccRoot, "Templates");
        var baseRoot = profile.BaseTemplateIsUser ? templatesRoot : Path.Combine(vccRoot, "VRCTemplates");
        var modeName = profile.Mode == ProjectMode.World ? "World" : "Avatar";
        var major = profile.UnityVersion?.Split('.')[0] ?? throw new InvalidOperationException("El preset no tiene versión de Unity.");
        var sourceDir = Directory.Exists(baseRoot) ? Directory.EnumerateDirectories(baseRoot)
            .Where(d => Path.GetFileName(d).StartsWith(modeName, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(d => ReadDeclaredVersion(Path.Combine(d, "ProjectSettings", "ProjectVersion.txt"))?.Split('.')[0] == major) : null;
        if (!string.IsNullOrWhiteSpace(profile.BaseTemplateName))
        {
            if (profile.BaseTemplateName != Path.GetFileName(profile.BaseTemplateName) || profile.BaseTemplateName is "." or "..")
                throw new InvalidOperationException("Nombre de plantilla base inválido.");
            var selected = VccTemplateBrowser.List(baseRoot, profile.BaseTemplateIsUser).FirstOrDefault(t => t.FolderName == profile.BaseTemplateName);
            if (selected is null || selected.UnityVersion?.Split('.')[0] != major || selected.IsAvatar != (profile.Mode == ProjectMode.Avatar))
                throw new InvalidOperationException("La plantilla base elegida no coincide con el tipo o la generación de Unity del preset.");
            sourceDir = selected.FolderPath;
        }
        return sourceDir ?? throw new InvalidOperationException($"No hay plantilla base {modeName} compatible con Unity {major} en VCC. El preset permanece guardado.");
    }

    private static string VccRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VRChatCreatorCompanion");




    public string ExportTemplate(ProjectProfile profile, string? vccRoot = null)
    {
        var root = vccRoot ?? VccRoot;
        var templatesRoot = Path.Combine(root, "Templates");
        var baseRoot = profile.BaseTemplateIsUser ? templatesRoot : Path.Combine(root, "VRCTemplates");
        var modeName = profile.Mode == ProjectMode.World ? "World" : "Avatar";
        var major = profile.UnityVersion?.Split('.')[0] ?? throw new InvalidOperationException("El preset no tiene versión de Unity.");
        var sourceDir = Directory.Exists(baseRoot) ? Directory.EnumerateDirectories(baseRoot)
            .Where(d => Path.GetFileName(d).StartsWith(modeName, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(d => ReadDeclaredVersion(Path.Combine(d, "ProjectSettings", "ProjectVersion.txt"))?.Split('.')[0] == major) : null;
        if (!string.IsNullOrWhiteSpace(profile.BaseTemplateName))
        {
            if (profile.BaseTemplateName != Path.GetFileName(profile.BaseTemplateName) || profile.BaseTemplateName is "." or "..")
                throw new InvalidOperationException("Nombre de plantilla base inválido.");
            var selected = VccTemplateBrowser.List(baseRoot, profile.BaseTemplateIsUser).FirstOrDefault(t => t.FolderName == profile.BaseTemplateName);
            if (selected is null || selected.UnityVersion?.Split('.')[0] != major || selected.IsAvatar != (profile.Mode == ProjectMode.Avatar))
                throw new InvalidOperationException("La plantilla base elegida no coincide con el tipo o la generación de Unity del preset.");
            sourceDir = selected.FolderPath;
        }
        if (sourceDir is null) throw new InvalidOperationException($"No hay plantilla base {modeName} compatible con Unity {major} en VCC. El preset permanece guardado.");
        if (profile.UnityPackages.Any(p => !File.Exists(p.FullPath))) throw new FileNotFoundException("Falta un archivo .unitypackage del preset.");
        var destName = SanitizeFolderName(profile.DisplayName);
        var destDir = Path.Combine(templatesRoot, destName);
        if (Directory.Exists(destDir)) throw new InvalidOperationException($"Ya existe la plantilla '{destName}'. Cambiá el nombre del preset para exportar otra copia.");
        Directory.CreateDirectory(templatesRoot);
        var staging = Path.Combine(root, "MochiSproutStaging", Guid.NewGuid().ToString("N"));
        var committed = false;
        try
        {
            CopyDirectory(sourceDir, staging);
            UpdatePackageJson(staging, profile, destName);
            MergeVpmManifest(staging, profile);
            UpdateProjectVersion(staging, profile.UnityVersion);
            new UnityPackageImportSetup().Prepare(staging, profile.UnityPackages, resetState: true, inheritedSource: sourceDir);
            Directory.Move(staging, destDir);
            committed = true;
            _templateStore.SetMapping(destName, profile);
            return destDir;
        }
        finally
        {
            if (!committed && Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }
    public int EnableTemplateImports(VccTemplateInfo template)
    {
        if (!template.IsDeletable) throw new InvalidOperationException("Las plantillas predeterminadas son de solo lectura.");
        return new UnityPackageImportSetup().Prepare(template.FolderPath, _templateStore.TryGet(template.FolderName)?.UnityPackages);
    }
    internal static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string([.. name.Select(c => invalid.Contains(c) ? '_' : c)]).Trim();
        return sanitized.Length == 0 || sanitized is "." or ".." ? "Preset VCC" : sanitized;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
            if (!file.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);

        foreach (var folder in Directory.EnumerateDirectories(source))
            CopyDirectory(folder, Path.Combine(destination, Path.GetFileName(folder)));
    }

    private static void UpdatePackageJson(string destDir, ProjectProfile profile, string destName)
    {
        var path = Path.Combine(destDir, "package.json");
        var slug = new string([.. destName.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')]);

        var packageJson = new
        {
            name = $"com.akimorivt.mochisprout.{slug}",
            displayName = profile.DisplayName,
            description = $"Plantilla generada por MochiSprout ({profile.UnityDisplayLabel}, {profile.Mode}).",
            version = "1.0.0",
            category = "ProjectTemplate",
            dependencies = new { }
        };

        File.WriteAllText(path, JsonSerializer.Serialize(packageJson, JsonOptions));
    }

    private static void MergeVpmManifest(string destDir, ProjectProfile profile)
    {
        var path = Path.Combine(destDir, "Packages", "vpm-manifest.json");
        var dependencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(path))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("dependencies", out var depsEl) && depsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var dep in depsEl.EnumerateObject())
                {
                    var version = dep.Value.TryGetProperty("version", out var v) ? v.GetString() ?? "*" : "*";
                    dependencies[dep.Name] = version;
                }
            }
        }

        foreach (var package in profile.Packages)
        {
            if (!string.IsNullOrWhiteSpace(package.Version))
            {
                // Versión conocida (resuelta desde el catálogo de paquetes): siempre manda.
                dependencies[package.Id] = package.Version;
            }
            else if (!dependencies.ContainsKey(package.Id))
            {
                // Sin versión conocida y sin nada previo (ej. de la plantilla base): "*" como último recurso.
                dependencies[package.Id] = "*";
            }
            // Si ya había un rango para este Id (ej. com.vrchat.base "~3.10.x" de la plantilla
            // base) y no tenemos una versión propia, lo dejamos intacto en vez de pisarlo.
        }

        var manifest = new
        {
            dependencies = dependencies.ToDictionary(d => d.Key, d => new { version = d.Value })
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private static void UpdateProjectVersion(string destDir, string? unityVersion)
    {
        if (string.IsNullOrWhiteSpace(unityVersion)) return;

        var path = Path.Combine(destDir, "ProjectSettings", "ProjectVersion.txt");

        // La plantilla base ya trae el hash de revisión real (el changeset exacto de ese
        // build de Unity) para su propia versión. Si el perfil pide esa misma versión, no
        // hay que tocar el archivo: perderíamos ese hash real a cambio de uno inventado.
        // Sólo lo reescribimos cuando el usuario eligió una versión distinta a la de la
        // plantilla, y en ese caso omitimos la línea de revisión en vez de inventar un hash:
        // Unity Hub matchea proyectos contra editores instalados por m_EditorVersion, no por
        // el hash, así que no hace falta y evita mostrar un valor falso.
        var declaredVersion = ReadDeclaredVersion(path);
        if (declaredVersion is not null && declaredVersion.Equals(unityVersion, StringComparison.OrdinalIgnoreCase))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"m_EditorVersion: {unityVersion}{Environment.NewLine}");
    }

    private static string? ReadDeclaredVersion(string path)
    {
        if (!File.Exists(path)) return null;

        var line = File.ReadLines(path)
            .FirstOrDefault(l => l.StartsWith("m_EditorVersion:", StringComparison.OrdinalIgnoreCase));

        return line?["m_EditorVersion:".Length..].Trim();
    }

}




