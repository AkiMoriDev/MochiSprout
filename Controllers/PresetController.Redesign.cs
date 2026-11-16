using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MochiSprout.Models;
using MochiSprout.Services.Vcc;
namespace MochiSprout.Controllers;

/// <summary>Ítem de validación visible en el editor o en la exportación. Kind: ok, warn o err.</summary>
public sealed record StatusItem(string Kind, string Text, string? FixPackageId = null, string? FixLabel = null)
{
    public bool HasFix => FixPackageId is not null;
}

/// <summary>Estado de la biblioteca rediseñada: franja de entorno, filtros, marca «En VCC», secciones de Configuración y exportación guiada.</summary>
public partial class PresetController
{
    public event EventHandler? ExportRequested;

    // ---------- Franja de entorno ----------
    public bool VccConnected => File.Exists(Path.Combine(vccRoot, "settings.json"));
    public string VccEnvTitle => VccConnected ? "VCC conectado" : "VCC sin configurar";
    public string VccEnvDetail => VccConnected ? $"settings.json encontrado · {vccRoot}" : $"No se encontró settings.json en {vccRoot}";
    public string UnityEnvTitle => Installs.Count switch { 0 => "Sin instalaciones de Unity", 1 => "1 instalación de Unity", var n => $"{n} instalaciones de Unity" };
    public string UnityEnvDetail => Installs.Count == 0 ? "Agregá Unity.exe en Configuración" : string.Join(" · ", Installs.Select(i => i.Version).Distinct());
    public string CatalogEnvTitle
    {
        get
        {
            var packages = Packages.Count(p => p.IsAvailable);
            var repos = Packages.Where(p => p.IsAvailable).Select(p => p.RepositoryId).Distinct().Count();
            return $"{packages} {(packages == 1 ? "paquete" : "paquetes")} en {repos} {(repos == 1 ? "repositorio" : "repositorios")}";
        }
    }
    private void NotifyEnvironment()
    {
        OnPropertyChanged(nameof(VccConnected)); OnPropertyChanged(nameof(VccEnvTitle)); OnPropertyChanged(nameof(VccEnvDetail));
        OnPropertyChanged(nameof(UnityEnvTitle)); OnPropertyChanged(nameof(UnityEnvDetail)); OnPropertyChanged(nameof(CatalogEnvTitle));
        OnPropertyChanged(nameof(VccConnectionLabel)); OnPropertyChanged(nameof(Unity2022Choice)); OnPropertyChanged(nameof(Unity2019Choice));
    }

    // ---------- Biblioteca: filtro por tipo y búsqueda ----------
    [ObservableProperty] private string presetTypeFilter = "Todos";
    public int AvatarPresetCount => Presets.Count(p => p.Mode == ProjectMode.Avatar);
    public int WorldPresetCount => Presets.Count(p => p.Mode == ProjectMode.World);
    public bool HasPresetQuery => !string.IsNullOrWhiteSpace(PresetSearch);
    public string PresetQueryLabel => $"Resultados para «{PresetSearch.Trim()}»";
    public bool NoPresetMatches => (HasPresetQuery || PresetTypeFilter != "Todos") && !PresetView.Cast<object>().Any();
    partial void OnPresetTypeFilterChanged(string value) => RefreshPresetFilter();
    private bool PresetMatches(ProjectProfile p)
    {
        if (PresetTypeFilter == "Avatar" && p.Mode != ProjectMode.Avatar) return false;
        if (PresetTypeFilter == "World" && p.Mode != ProjectMode.World) return false;
        var q = PresetSearch.Trim();
        if (q.Length == 0) return true;
        return p.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
            || (p.Notes ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            || p.Packages.Any(e => e.Id.Contains(q, StringComparison.OrdinalIgnoreCase) || (catalog.FirstOrDefault(c => c.Id == e.Id)?.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
    }
    private void RefreshPresetFilter()
    {
        PresetView.Refresh();
        OnPropertyChanged(nameof(HasPresetQuery)); OnPropertyChanged(nameof(PresetQueryLabel)); OnPropertyChanged(nameof(NoPresetMatches));
    }
    private void NotifyPresetCounts()
    {
        OnPropertyChanged(nameof(PresetCount)); OnPropertyChanged(nameof(AvatarPresetCount)); OnPropertyChanged(nameof(WorldPresetCount)); OnPropertyChanged(nameof(NoPresetMatches));
    }
    [RelayCommand] private void ClearPresetSearch() { PresetSearch = ""; PresetTypeFilter = "Todos"; }
    [RelayCommand] private void SetPresetTypeFilter(string? value) => PresetTypeFilter = value is "Avatar" or "World" ? value : "Todos";

    /// <summary>Nombres legibles de los paquetes adicionales de un preset, para la tarjeta.</summary>
    public string PresetPackageSummary(ProjectProfile profile)
    {
        if (profile.Packages.Count == 0) return "Solo los paquetes de la plantilla base";
        var names = profile.Packages.Select(e => catalog.FirstOrDefault(c => c.Id == e.Id && (e.RepositoryId is null || c.RepositoryId == e.RepositoryId))?.DisplayName ?? e.Id).ToList();
        var shown = string.Join(" · ", names.Take(3));
        return names.Count > 3 ? $"{shown} · +{names.Count - 3}" : shown;
    }
    public int MissingFileCount(ProjectProfile profile) => profile.UnityPackages.Count(f => !File.Exists(f.FullPath));

    // ---------- «En VCC»: el preset tiene una plantilla exportada que sigue existiendo ----------
    private HashSet<string> exportedPresetIds = [];
    [ObservableProperty] private int presetStamp;
    public bool IsExported(ProjectProfile profile) => exportedPresetIds.Contains(profile.Id);
    private void RefreshExported()
    {
        try
        {
            var templates = Path.Combine(vccRoot, "Templates");
            exportedPresetIds = exporter.TemplateStore.All()
                .Where(m => Directory.Exists(Path.Combine(templates, m.Key)))
                .Select(m => m.Value.Id).ToHashSet();
        }
        catch (Exception) { exportedPresetIds = []; }
        PresetStamp++;
    }

    // ---------- Configuración por secciones ----------
    [ObservableProperty] private string settingsSection = "vcc";
    [RelayCommand] private void OpenSettings(string? section)
    {
        if (IsBusy) return;
        SettingsSection = section is "vcc" or "unity" or "data" or "prefs" ? section : "vcc";
        UtilityRequested?.Invoke("Configuración");
    }
    [RelayCommand] private void SetSettingsSection(string? section) => SettingsSection = section is "vcc" or "unity" or "data" or "prefs" ? section : "vcc";

    // ---------- Editor: fichas con versión exacta y validación en vivo ----------
    public string Unity2022Choice => ChoiceLabel("2022");
    public string Unity2019Choice => ChoiceLabel("2019");
    private string ChoiceLabel(string year)
    {
        if (SelectedUnity is not null && SelectedUnity.Version.StartsWith(year + ".") && File.Exists(SelectedUnity.EditorPath)) return SelectedUnity.Version;
        var install = Installs.Where(i => i.Version.StartsWith(year + ".") && File.Exists(i.EditorPath)).OrderByDescending(i => TryUnityVersion(i.Version, out var v) ? v : new Version()).FirstOrDefault();
        return install?.Version ?? $"Sin Unity {year}";
    }
    public string EditorBaseLabel => SelectedBaseTemplate is null ? "No se detectó una base compatible" : $"{SelectedBaseTemplate.DisplayName}{(SelectedBaseTemplate.IsDeletable ? " · personal" : "")}";
    public ObservableCollection<StatusItem> EditorIssues { get; } = [];
    private void RebuildEditorIssues(IReadOnlyList<string> selectionIssues)
    {
        var items = new List<StatusItem>();
        if (string.IsNullOrWhiteSpace(Name)) items.Add(new("warn", "Poné un nombre para guardar el preset."));
        if (SelectedUnity is null || !File.Exists(SelectedUnity.EditorPath)) items.Add(new("err", "Elegí un Unity.exe instalado y accesible."));
        else if (SelectedBaseTemplate is null) items.Add(new("warn", "No hay una base de VCC compatible: podés guardar, pero exportar va a fallar hasta que exista."));
        foreach (var file in Attachments.Where(f => !File.Exists(f.FullPath))) items.Add(new("err", $"No se encuentra {file.FileName}. Quitalo o volvé a enlazarlo."));
        foreach (var p in catalog.Where(p => p.IsSelected && !p.IsSdk))
        {
            if (p.SelectedVersion?.Unity is { Length: > 0 } min && SelectedUnity is not null && Version.TryParse(min, out var required) && TryUnityVersion(SelectedUnity.Version, out var actual) && actual < required)
                items.Add(new("warn", $"{p.DisplayName} requiere Unity {min} o superior."));
            foreach (var d in p.SelectedVersion?.Dependencies ?? new Dictionary<string, string>())
            {
                if (IsSdkId(d.Key) || catalog.Any(c => c.Id == d.Key && c.IsSelected)) continue;
                var available = catalog.FirstOrDefault(c => c.Id == d.Key && c.IsAvailable && !c.IsSdk);
                items.Add(available is null
                    ? new("err", $"{p.DisplayName} necesita {d.Key} {d.Value}, que no está en el catálogo local.")
                    : new("warn", $"{p.DisplayName} necesita {available.DisplayName} ({d.Value}).", available.Id, "Agregar"));
            }
        }
        if (catalog.Where(p => p.IsSelected && !p.IsSdk).GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            items.Add(new("err", "Elegiste el mismo paquete desde dos repositorios. Dejá uno solo."));
        var distinct = items.DistinctBy(i => i.Text).ToList();
        if (distinct.Count == 0) distinct.Add(new("ok", "Todo listo. VCC resuelve los rangos finales al crear el proyecto."));
        EditorIssues.Clear();
        foreach (var item in distinct) EditorIssues.Add(item);
        OnPropertyChanged(nameof(EditorBaseLabel)); OnPropertyChanged(nameof(EditorSubtitle));
    }
    public string EditorSubtitle => $"{Mode} · Unity {SelectedUnity?.Version ?? "sin elegir"} · {(SelectedBaseTemplate?.DisplayName ?? "sin base compatible")}";
    [RelayCommand] private void AddDependency(string? id)
    {
        var package = catalog.FirstOrDefault(c => c.Id == id && c.IsAvailable && !c.IsSdk);
        if (package is null) return;
        package.SelectedVersion ??= package.Versions.FirstOrDefault(v => ShowPrerelease || !v.Version.Contains('-')) ?? package.Versions.FirstOrDefault();
        package.IsSelected = true;
        Status = $"{package.DisplayName} agregado al borrador.";
    }

    // ---------- Exportación guiada ----------
    [ObservableProperty] private ProjectProfile? exportTarget;
    [ObservableProperty] private string exportPhase = "ready";   // ready · run · done · fail
    [ObservableProperty] private string exportDestination = "";
    [ObservableProperty] private string exportBase = "";
    [ObservableProperty] private string exportResult = "";
    public ObservableCollection<StatusItem> ExportChecks { get; } = [];
    public bool ExportCanRun => ExportPhase == "ready" && ExportChecks.All(c => c.Kind != "err");
    public bool ExportIsRunning => ExportPhase == "run";
    public string ExportTitle => ExportTarget is null ? "Exportar a VCC" : $"Exportar «{ExportTarget.DisplayName}» a VCC";
    partial void OnExportPhaseChanged(string value) { OnPropertyChanged(nameof(ExportCanRun)); OnPropertyChanged(nameof(ExportIsRunning)); }
    partial void OnExportTargetChanged(ProjectProfile? value) => OnPropertyChanged(nameof(ExportTitle));

    [RelayCommand] private void RequestExport(ProjectProfile? profile)
    {
        if (profile is null || IsBusy) return;
        ExportTarget = profile; ExportPhase = "ready"; ExportResult = "";
        ExportDestination = VccTemplateExporter.DestinationFor(profile, vccRoot);
        ExportChecks.Clear();
        foreach (var check in ExportPrechecks(profile)) ExportChecks.Add(check);
        OnPropertyChanged(nameof(ExportCanRun));
        ExportRequested?.Invoke(this, EventArgs.Empty);
    }
    private IEnumerable<StatusItem> ExportPrechecks(ProjectProfile profile)
    {
        if (!VccConnected) yield return new("err", "No se encontró settings.json de VCC. Revisá la carpeta en Configuración.");
        string? baseError = null;
        try { ExportBase = Path.GetFileName(VccTemplateExporter.ResolveBaseFolder(profile, vccRoot)); }
        catch (Exception ex) { ExportBase = "Sin base compatible"; baseError = ex.Message; }
        yield return baseError is null ? new("ok", $"Base compatible: {ExportBase} (no se modifica).") : new("err", baseError);
        yield return Directory.Exists(ExportDestination)
            ? new("err", $"Ya existe una plantilla «{Path.GetFileName(ExportDestination)}». Cambiá el nombre del preset para exportar otra copia; no se sobrescribe.")
            : new("ok", "El nombre de la plantilla está libre.");
        var missing = profile.UnityPackages.Where(f => !File.Exists(f.FullPath)).ToList();
        yield return missing.Count == 0
            ? new("ok", profile.UnityPackages.Count == 0 ? "Sin archivos .unitypackage enlazados." : $"{profile.UnityPackages.Count} archivo(s) enlazado(s) accesibles.")
            : new("err", $"Faltan {missing.Count} archivo(s): {string.Join(", ", missing.Select(m => m.FileName))}.");
        var packageErrors = new List<string>();
        foreach (var entry in profile.Packages)
        {
            var pkg = catalog.FirstOrDefault(p => p.Id == entry.Id && (entry.RepositoryId is null || p.RepositoryId == entry.RepositoryId));
            var version = pkg?.Versions.FirstOrDefault(v => v.Version == entry.Version);
            if (version is null || pkg?.IsAvailable != true) { packageErrors.Add($"{entry.Id} {entry.Version} no está en el catálogo local."); continue; }
            if (!string.IsNullOrWhiteSpace(version.Unity) && Version.TryParse(version.Unity, out var required) && TryUnityVersion(profile.UnityVersion ?? "", out var actual) && actual < required) packageErrors.Add($"{entry.Id} requiere Unity {version.Unity} o superior.");
            foreach (var dep in version.Dependencies) if (!IsSdkId(dep.Key) && !profile.Packages.Any(p => p.Id == dep.Key)) packageErrors.Add($"Falta la dependencia {dep.Key} {dep.Value}.");
        }
        if (packageErrors.Count == 0) yield return new("ok", profile.Packages.Count == 0 ? "Sin paquetes adicionales: se usa solo la base." : $"{profile.Packages.Count} paquete(s) disponibles en el catálogo local.");
        else foreach (var e in packageErrors.Distinct()) yield return new("err", e);
    }
    [RelayCommand] private async Task RunExportAsync()
    {
        var profile = ExportTarget;
        if (profile is null || IsBusy || !ExportCanRun) return;
        ExportPhase = "run"; IsBusy = true;
        try
        {
            var path = await Task.Run(() => exporter.ExportTemplate(profile, vccRoot));
            ExportDestination = path; ExportPhase = "done";
            ExportResult = "Plantilla creada. Abrí VCC y elegila al crear un proyecto nuevo.";
            Status = $"Exportado a {path}";
            Templates.Clear(); foreach (var t in VccTemplateBrowser.List(Path.Combine(vccRoot, "Templates"), true)) Templates.Add(t);
            RefreshExported();
        }
        catch (Exception ex) { ExportPhase = "fail"; ExportResult = ex.Message; Status = $"Exportación pendiente: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
