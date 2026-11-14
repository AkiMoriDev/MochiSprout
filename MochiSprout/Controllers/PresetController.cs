using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MochiSprout.Contracts;
using MochiSprout.Models;
using MochiSprout.Services.Diagnostics;
using MochiSprout.Services.Vcc;
using MochiSprout.Services.Vpm;
namespace MochiSprout.Controllers;
/// <summary>Coordinates use cases; views only forward input and render bindable state.</summary>
public partial class PresetController : ObservableObject
{
    private readonly IProfileStore store;
    private readonly UnityDetectionService unity;
    private readonly VccTemplateExporter exporter;
    private string? editingId;
    private readonly List<CatalogPackage> catalog = [];
    private string vccRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VRChatCreatorCompanion");
    private readonly string PreferencesFile;
    public ObservableCollection<ProjectProfile> Presets { get; } = [];
    public ObservableCollection<DetectedUnityInstall> Installs { get; } = [];
    public ObservableCollection<CatalogPackage> Packages { get; } = [];
    public ObservableCollection<UnityPackageReference> Attachments { get; } = [];
    public ObservableCollection<string> Repositories { get; } = ["Todos los repositorios"];
    public ObservableCollection<VccTemplateInfo> Templates { get; } = [];
    public ICollectionView PackageView { get; }
    public ICollectionView PresetView { get; }
    public IReadOnlyList<ProjectMode> Modes { get; } = [ProjectMode.Avatar, ProjectMode.World];
    [ObservableProperty] private string page = "Presets";
    [ObservableProperty] private string status = "Listo para crear tu próximo proyecto.";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ConfirmSaveCommand))] private bool isBusy;
    [ObservableProperty] private bool isEditor;
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string notes = "";
    [ObservableProperty] private ProjectMode mode;
    [ObservableProperty] private DetectedUnityInstall? selectedUnity;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private string presetSearch = "";
    [ObservableProperty] private string repository = "Todos los repositorios";
    [ObservableProperty] private string catalogStatus = "Todavía no se cargó el catálogo.";
    [ObservableProperty] private string validation = "Elegí una instalación de Unity.";
    public string EditorTitle => editingId is null ? "Crear preset" : "Editar preset";
    public string Summary => $"{SelectedUnity?.Version ?? "Sin Unity"} · {Mode} · {catalog.Count(p => p.IsSelected)} paquetes · {Attachments.Count} archivos";
    public string VccRoot => vccRoot;
    public string DataRoot => Path.GetDirectoryName(PreferencesFile)!;
    public string PresetCount => $"{Presets.Count} presets guardados";
    partial void OnSearchChanged(string value) => PackageView.Refresh();
    partial void OnPresetSearchChanged(string value) { RefreshPresetFilter(); DefaultTemplateView.Refresh(); ExportedTemplateView.Refresh(); }
    partial void OnRepositoryChanged(string value) => PackageView.Refresh();
    partial void OnSelectedUnityChanged(DetectedUnityInstall? value) => UpdateSummary();
    partial void OnModeChanged(ProjectMode value)
    {
        if (SelectedBaseTemplate is not null && SelectedBaseTemplate.IsAvatar != (value == ProjectMode.Avatar))
            SelectedBaseTemplate = DefaultTemplates.FirstOrDefault(t => t.IsAvatar == (value == ProjectMode.Avatar));
        UpdateSummary();
    }
    public PresetController(IProfileStore store, UnityDetectionService unity, RegisteredCatalogService _, VccTemplateExporter exporter, string? dataFolder = null)
    {
        this.store = store; this.unity = unity; this.exporter = exporter;
        PreferencesFile = Path.Combine(dataFolder ?? MochiSprout.Services.Profiles.AppDataPaths.Root, "preferences.json");
        PackageView = CollectionViewSource.GetDefaultView(Packages);
        PackageView.Filter = o => o is CatalogPackage p && !p.IsSdk && (!OnlySelectedPackages || p.IsSelected) && (Repository == "Todos los repositorios" || p.RepositoryName == Repository) && ($"{p.DisplayName} {p.Id}".Contains(Search, StringComparison.OrdinalIgnoreCase));
        PresetView = CollectionViewSource.GetDefaultView(Presets);
        PresetView.Filter = o => o is ProjectProfile p && PresetMatches(p);
        InitializeNavigation();
        Attachments.CollectionChanged += (_, _) => UpdateSummary();
        Installs.CollectionChanged += (_, _) => { UpdateProjectVariants(); NotifyEnvironment(); };
        Presets.CollectionChanged += (_, _) => { NotifyPresetCounts(); PresetStamp++; };
        Packages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CatalogEnvTitle));
    }
    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            if (File.Exists(PreferencesFile))
            {
                var prefs = JsonSerializer.Deserialize<AppPreferences>(await File.ReadAllTextAsync(PreferencesFile));
                if (prefs is not null)
                {
                    savedPreferences = prefs; vccRoot = prefs.VccRoot;
                    RememberLastPage = prefs.RememberLastPage; ShowPrerelease = prefs.ShowPrerelease;
                    Page = "Presets";
                    foreach (var i in prefs.ManualInstalls) if (File.Exists(i.EditorPath)) Installs.Add(i);
                }
            }
            foreach (var p in await store.LoadAsync()) Presets.Add(p);
            OnPropertyChanged(nameof(PresetCount)); OnPropertyChanged(nameof(VccRoot));
            await RefreshSourcesCoreAsync();
            VccPathDraft = vccRoot;
            Page = "Presets";
        }
        catch (Exception ex) { Status = $"No se pudo cargar: {ex.Message}"; }
        finally { IsBusy = false; }
    }
    [RelayCommand] private void Navigate(string destination)
    {
        if (IsBusy || !NavigationPages.Contains(destination)) return;
        if (destination == "Crear preset") { if (!IsEditor) NewPreset(); else EditorRequested?.Invoke(this, EventArgs.Empty); }
        else if (destination != "Presets") UtilityRequested?.Invoke(destination);
    }
    [RelayCommand]
    private void NewPreset()
    {
        PrepareNewPreset();
        EditorRequested?.Invoke(this, EventArgs.Empty);
    }
    private void PrepareNewPreset()
    {
        editingId = null; Name = ""; Notes = ""; Mode = ProjectMode.Avatar;
        foreach (var p in catalog) { p.IsSelected = false; p.SelectedVersion = p.Versions.FirstOrDefault(); }
        OnlySelectedPackages = false; EditorMessage = ""; Attachments.Clear(); SelectedUnity = Installs.FirstOrDefault(); Search = ""; Repository = Repositories[0];
        Page = "Presets"; IsEditor = true;
        SelectedBaseTemplate = DefaultTemplates.FirstOrDefault(t => t.IsAvatar);
        OnPropertyChanged(nameof(EditorTitle)); UpdateSummary(); MarkEditorClean();
    }
    [RelayCommand]
    private void EditPreset(ProjectProfile? profile)
    {
        if (profile is null) return;
        PrepareNewPreset(); editingId = profile.Id; Name = profile.DisplayName; Notes = profile.Notes ?? ""; Mode = profile.Mode;
        SelectedBaseTemplate = BaseTemplates.FirstOrDefault(t => t.FolderName == profile.BaseTemplateName && t.IsDeletable == profile.BaseTemplateIsUser)
            ?? DefaultTemplates.FirstOrDefault(t => t.IsAvatar == (profile.Mode == ProjectMode.Avatar) && t.UnityVersion?.Split('.')[0] == profile.UnityVersion?.Split('.')[0]);
        SelectedUnity = Installs.FirstOrDefault(i => string.Equals(i.EditorPath, profile.UnityEditorPath, StringComparison.OrdinalIgnoreCase));
        if (SelectedUnity is null && profile.UnityVersion is not null)
        {
            SelectedUnity = new(profile.UnityVersion, profile.UnityEditorPath ?? "", true); Installs.Add(SelectedUnity);
        }
        foreach (var entry in profile.Packages)
        {
            var pkg = catalog.FirstOrDefault(p => p.Id == entry.Id && (entry.RepositoryId is null || p.RepositoryId == entry.RepositoryId));
            if (pkg is null)
            {
                var repo = profile.Repositories.FirstOrDefault(r => r.Id == entry.RepositoryId);
                pkg = new CatalogPackage { Id = entry.Id, DisplayName = entry.Id + " (fuera del catálogo)", IsAvailable = false, RepositoryId = entry.RepositoryId ?? "", RepositoryName = repo?.DisplayName ?? "No disponible", RepositoryUrl = repo?.Url ?? "", Versions = [new(entry.Version ?? "*", null, new Dictionary<string, string>())] };
                AddPackage(pkg);
            }
            pkg.IsSelected = true;
            var savedVersion = pkg.Versions.FirstOrDefault(v => v.Version == entry.Version);
            if (savedVersion is null) { savedVersion = new(entry.Version ?? "*", null, new Dictionary<string, string>()); pkg.Versions.Add(savedVersion); }
            pkg.SelectedVersion = savedVersion;
        }
        foreach (var file in profile.UnityPackages) Attachments.Add(file);
        OnPropertyChanged(nameof(EditorTitle)); UpdateSummary(); MarkEditorClean();
        EditorRequested?.Invoke(this, EventArgs.Empty);
    }
    [RelayCommand] private void CancelEdit() { IsEditor = false; Page = "Presets"; Status = "Edición cerrada. Los cambios sin guardar se descartaron."; }
    [RelayCommand]
    private async Task RefreshSourcesAsync()
    {
        if (IsBusy) return; IsBusy = true;
        try { await RefreshSourcesCoreAsync(); } catch (Exception ex) { Status = $"No se pudo actualizar: {ex.Message}"; } finally { IsBusy = false; }
    }
    private async Task RefreshSourcesCoreAsync()
    {
        var selected = catalog.Where(p => p.IsSelected).ToDictionary(p => p.RepositoryId + "|" + p.Id, p => p.SelectedVersion);
        var previous = SelectedUnity?.EditorPath;
        var baseSelection = SelectedBaseTemplate?.FolderName;
        var baseWasUser = SelectedBaseTemplate?.IsDeletable == true;
        var detection = Task.Run(() => unity.Detect());
        var loading = Task.Run(() => new RegisteredCatalogService(vccRoot).Load());
        await Task.WhenAll(detection, loading);
        var manual = Installs.Where(i => i.IsManual).ToList(); Installs.Clear();
        foreach (var i in await detection) Installs.Add(i);
        foreach (var i in manual) if (!Installs.Any(x => string.Equals(x.EditorPath, i.EditorPath, StringComparison.OrdinalIgnoreCase))) Installs.Add(i);
        SelectedUnity = Installs.FirstOrDefault(i => i.EditorPath == previous) ?? Installs.FirstOrDefault();
        foreach (var p in catalog) p.PropertyChanged -= PackageChanged;
        var old = catalog.ToList(); catalog.Clear(); Packages.Clear(); Repositories.Clear(); Repositories.Add("Todos los repositorios");
        var result = await loading;
        foreach (var p in result.Packages)
        {
            if (selected.TryGetValue(p.RepositoryId + "|" + p.Id, out var version)) { p.IsSelected = true; p.SelectedVersion = p.Versions.FirstOrDefault(v => v.Version == version?.Version) ?? version; }
            if (p.SelectedVersion is not null && !p.Versions.Contains(p.SelectedVersion)) p.Versions.Add(p.SelectedVersion);
            AddPackage(p);
        }
        foreach (var p in old.Where(p => p.IsSelected && !catalog.Any(c => c.Id == p.Id && c.RepositoryId == p.RepositoryId))) AddPackage(p);
        Repository = Repositories[0]; BrowseRepository = Repositories[0];
        CatalogStatus = $"{result.Packages.Count} paquetes · {Math.Max(0, Repositories.Count - 1)} repositorios. " + string.Join(" · ", result.Messages);
        restoringSources = true;
        Templates.Clear();
        foreach (var t in await Task.Run(() => VccTemplateBrowser.List(Path.Combine(vccRoot, "Templates"), true))) Templates.Add(t);
        DefaultTemplates.Clear();
        foreach (var t in await Task.Run(() => VccTemplateBrowser.List(Path.Combine(vccRoot, "VRCTemplates"), false))) DefaultTemplates.Add(t);
        SelectedBaseTemplate = BaseTemplates.FirstOrDefault(t => t.FolderName == baseSelection && t.IsDeletable == baseWasUser);
        restoringSources = false; OnPropertyChanged(nameof(BaseTemplates));
        OnPropertyChanged(nameof(VccConnectionLabel)); OnPropertyChanged(nameof(DefaultCount)); CatalogBrowseView.Refresh();
        SelectedCatalogPackage = Packages.FirstOrDefault(p => p.IsAvailable);
        Status = "Fuentes locales actualizadas."; UpdateSummary();
        NotifyEnvironment(); RefreshExported();
    }
    private void AddPackage(CatalogPackage p) { catalog.Add(p); Packages.Add(p); p.IncludePrerelease = ShowPrerelease; p.PropertyChanged += PackageChanged; if (!Repositories.Contains(p.RepositoryName)) InsertRepositorySorted(p.RepositoryName); }
    // «Todos los repositorios» queda primero; el resto, de la A a la Z sin distinguir mayúsculas ni signos iniciales (_pi_ va en la P).
    private static string RepositorySortKey(string name) => new string(name.SkipWhile(c => !char.IsLetterOrDigit(c)).ToArray());
    private void InsertRepositorySorted(string name)
    {
        var index = 1;
        while (index < Repositories.Count && string.Compare(RepositorySortKey(Repositories[index]), RepositorySortKey(name), StringComparison.CurrentCultureIgnoreCase) <= 0) index++;
        Repositories.Insert(index, name);
    }
    private void PackageChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName is nameof(CatalogPackage.IsSelected) or nameof(CatalogPackage.SelectedVersion)) { UpdateSummary(); if (OnlySelectedPackages && e.PropertyName == nameof(CatalogPackage.IsSelected)) PackageView.Refresh(); } }
    private void UpdateSummary()
    {
        UpdateProjectVariants(); OnPropertyChanged(nameof(Summary)); OnPropertyChanged(nameof(EditorPackageCount)); OnPropertyChanged(nameof(EditorFileCount)); OnPropertyChanged(nameof(EditorFooterSummary)); OnPropertyChanged(nameof(SelectedFilterLabel));
        OnPropertyChanged(nameof(SelectedPackageCount)); OnPropertyChanged(nameof(SelectedPackages));
        var issues = new List<string>();
        if (SelectedUnity is null || !File.Exists(SelectedUnity.EditorPath)) issues.Add("Elegí un Unity.exe instalado y accesible.");
        else if (!SelectedUnity.Version.StartsWith("2019.") && !SelectedUnity.Version.StartsWith("2022.")) issues.Add("Versión personalizada: podés guardar el preset; exportar requiere una plantilla base compatible.");
        foreach (var p in catalog.Where(p => p.IsSelected))
        {
            if (p.SelectedVersion?.Unity is { Length: > 0 } min && SelectedUnity is not null && Version.TryParse(min, out var required) && TryUnityVersion(SelectedUnity.Version, out var actual) && actual < required) issues.Add($"{p.DisplayName} requiere Unity {min} o superior.");
            foreach (var d in p.SelectedVersion?.Dependencies ?? new Dictionary<string, string>())
            {
                if (IsSdkId(d.Key)) continue;
                if (!catalog.Any(c => c.Id == d.Key && c.IsSelected)) issues.Add($"{p.DisplayName} necesita {d.Key} {d.Value}.");
            }
        }
        RebuildEditorIssues(issues);
        OnPropertyChanged(nameof(Unity2022Choice)); OnPropertyChanged(nameof(Unity2019Choice));
        Validation = issues.Count == 0 ? "Selección lista. VCC resolverá los rangos y la compatibilidad final al crear el proyecto." : string.Join(Environment.NewLine, issues.Distinct());
    }
    private static bool TryUnityVersion(string value, out Version parsed) { var core = new string(value.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()); return Version.TryParse(core, out parsed!); }
    private static bool IsSdkId(string id) => id is "com.vrchat.base" or "com.vrchat.avatars" or "com.vrchat.worlds" or "com.vrchat.core.vpm-resolver";
    [RelayCommand]
    private async Task SavePresetAsync()
    {
        if (IsBusy || SaveReviewOpen) return;
        if (string.IsNullOrWhiteSpace(Name)) { Status = "Ingresá un nombre para el preset."; return; }
        if (SelectedUnity is null || !File.Exists(SelectedUnity.EditorPath)) { Status = "Seleccioná una instalación válida de Unity."; return; }
        var chosen = catalog.Where(p => p.IsSelected && !p.IsSdk).ToList();
        if (chosen.GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) { Status = "Elegí una sola fuente por paquete: hay identificadores repetidos."; return; }
        if (Attachments.Any(f => !File.Exists(f.FullPath))) { Status = "Hay archivos .unitypackage que ya no existen."; return; }
        var profile = new ProjectProfile
        {
            Id = editingId ?? Guid.NewGuid().ToString("N"),
            DisplayName = Name.Trim(),
            Notes = Notes,
            Mode = Mode,
            Target = SelectedUnity.Version.StartsWith("2019.") ? UnityTarget.Unity2019Legacy : SelectedUnity.Version.StartsWith("2022.") ? UnityTarget.Unity2022 : UnityTarget.Other,
            UnityVersion = SelectedUnity.Version,
            BaseTemplateName = SelectedBaseTemplate?.FolderName,
            BaseTemplateIsUser = SelectedBaseTemplate?.IsDeletable == true,
            UnityEditorPath = SelectedUnity.EditorPath,
            Packages = chosen.Select(p => new PackageEntry { Id = p.Id, Version = p.SelectedVersion?.Version, RepositoryId = p.RepositoryId }).ToList(),
            Repositories = chosen.GroupBy(p => p.RepositoryId).Select(g => new RepositoryEntry { Id = g.Key, DisplayName = g.First().RepositoryName, Url = g.First().RepositoryUrl }).ToList(),
            UnityPackages = [.. Attachments]
        };
        await PrepareSaveReviewAsync(profile, chosen);
    }
    [RelayCommand(CanExecute = nameof(CanConfirmSave))]
    private async Task ConfirmSaveAsync()
    {
        var profile = PendingPreset;
        if (profile is null || IsBusy || !SaveReviewOpen) return;
        if (!File.Exists(profile.UnityEditorPath)) { ReviewSaveError = "El editor de Unity ya no está disponible. Volvé a editar el preset."; return; }
        if (profile.UnityPackages.Any(f => !File.Exists(f.FullPath))) { ReviewSaveError = "Un archivo adicional ya no está disponible. Volvé a editar el preset."; return; }
        IsBusy = true;
        ReviewSaveError = "";
        try
        {
            var updated = Presets.Where(p => p.Id != profile.Id).Append(profile).ToList(); await store.SaveAsync(updated);
            var existing = Presets.FirstOrDefault(p => p.Id == profile.Id); if (existing is not null) Presets.Remove(existing); Presets.Add(profile);
            OnPropertyChanged(nameof(PresetCount)); IsEditor = false; Page = "Presets"; Status = $"Preset «{profile.DisplayName}» guardado. Exportalo a VCC cuando quieras.";
            SaveReviewOpen = false;
            PendingPreset = null;
        }
        catch (Exception ex) { ReviewSaveError = $"No se pudo guardar: {ex.Message}"; Status = ReviewSaveError; }
        finally { IsBusy = false; }
    }
    [RelayCommand]
    private async Task ExportPresetAsync(ProjectProfile? profile)
    {
        if (profile is null || IsBusy) return; IsBusy = true;
        try
        {
            foreach (var entry in profile.Packages)
            {
                var pkg = catalog.FirstOrDefault(p => p.Id == entry.Id && (entry.RepositoryId is null || p.RepositoryId == entry.RepositoryId));
                var version = pkg?.Versions.FirstOrDefault(v => v.Version == entry.Version);
                if (version is null || pkg?.IsAvailable != true) throw new InvalidOperationException($"{entry.Id} {entry.Version} no está disponible en el catálogo local. Actualizá VCC y revisá el preset.");
                if (!string.IsNullOrWhiteSpace(version.Unity) && Version.TryParse(version.Unity, out var required) && TryUnityVersion(profile.UnityVersion ?? "", out var actual) && actual < required) throw new InvalidOperationException($"{entry.Id} requiere Unity {version.Unity} o superior.");
                foreach (var dep in version.Dependencies) if (!IsSdkId(dep.Key) && !profile.Packages.Any(p => p.Id == dep.Key)) throw new InvalidOperationException($"Agregá la dependencia {dep.Key} {dep.Value} al preset.");
            }
            var path = await Task.Run(() => exporter.ExportTemplate(profile, vccRoot)); Status = $"Exportado a {path}"; Templates.Clear(); foreach (var t in VccTemplateBrowser.List(Path.Combine(vccRoot, "Templates"), true)) Templates.Add(t);
        }
        catch (Exception ex) { Status = $"Exportación pendiente: {ex.Message}"; }
        finally { IsBusy = false; }
    }
    public async Task AddManualUnityAsync(string path)
    {
        var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
        var version = info.ProductVersion?.Split(' ').FirstOrDefault();
        if (string.IsNullOrWhiteSpace(version)) throw new InvalidOperationException("El ejecutable no informa su versión de Unity.");
        var install = Installs.FirstOrDefault(i => string.Equals(i.EditorPath, path, StringComparison.OrdinalIgnoreCase)) ?? new DetectedUnityInstall(version, path, true);
        if (!Installs.Contains(install)) Installs.Add(install); SelectedUnity = install; await SavePreferencesAsync();
    }
    public void AddFiles(IEnumerable<string> files) { foreach (var file in files) if (!Attachments.Any(f => string.Equals(f.FullPath, file, StringComparison.OrdinalIgnoreCase))) Attachments.Add(new() { FileName = Path.GetFileName(file), FullPath = file }); }
    [RelayCommand] private void RemoveFile(UnityPackageReference? file) { if (file is not null) Attachments.Remove(file); }
    public async Task SetVccRootAsync(string path) { if (!File.Exists(Path.Combine(path, "settings.json"))) throw new InvalidOperationException("La carpeta debe contener settings.json de VCC."); vccRoot = path; OnPropertyChanged(nameof(VccRoot)); await SavePreferencesAsync(); await RefreshSourcesAsync(); }
    private async Task SavePreferencesAsync()
    {
        Directory.CreateDirectory(DataRoot);
        savedPreferences.VccRoot = vccRoot;
        savedPreferences.ManualInstalls = Installs.Where(i => i.IsManual && File.Exists(i.EditorPath)).ToList();
        savedPreferences.RememberLastPage = RememberLastPage;
        savedPreferences.ShowPrerelease = ShowPrerelease;
        savedPreferences.LastPage = Page;
        var temp = PreferencesFile + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(savedPreferences, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, PreferencesFile, true);
    }
    public async Task ImportAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path); var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<ProfilesDocument>(json, opts) ?? throw new InvalidDataException("Archivo de presets inválido.");
        if (doc.SchemaVersion > 2) throw new InvalidDataException("Esta versión del archivo todavía no es compatible.");
        var updated = Presets.ToList(); foreach (var profile in doc.Profiles) if (!updated.Any(p => p.Id == profile.Id)) updated.Add(profile);
        await store.SaveAsync(updated); Presets.Clear(); foreach (var profile in updated) Presets.Add(profile); OnPropertyChanged(nameof(PresetCount)); Status = "Presets importados; verificá las rutas de Unity y los archivos adicionales.";
    }
}





