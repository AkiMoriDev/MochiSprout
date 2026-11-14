using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MochiSprout.Models;
using MochiSprout.Services.Diagnostics;
using MochiSprout.Services.Profiles;
using MochiSprout.Services.Vcc;
using MochiSprout.Services.Vpm;
namespace MochiSprout.Controllers;

/// <summary>Una instalación de Unity con la decisión de ofrecerla o no al crear presets.</summary>
public sealed partial class UnityChoice : ObservableObject
{
    public UnityChoice(DetectedUnityInstall install, bool enabled) { Install = install; isEnabled = enabled; }
    public DetectedUnityInstall Install { get; }
    public string Version => Install.Version;
    public string EditorPath => Install.EditorPath;
    public string Source => Install.IsManual ? "Agregada a mano" : "Unity Hub";
    [ObservableProperty] private bool isEnabled;
}
public sealed record SetupStepItem(int Index, string Number, string Label, string Sub, bool IsCurrent, bool IsDone);
public sealed record SetupSummaryRow(string Label, string Value, bool Ok, int Step);

/// <summary>Asistente de primer inicio: VCC, Unity, datos anteriores y preferencias. Nada se guarda hasta terminar.</summary>
public partial class PresetController
{
    // ---------- Instalaciones de Unity que se ofrecen ----------
    private HashSet<string> hiddenInstalls = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Todas las instalaciones conocidas. <see cref="Installs"/> tiene solo las que se ofrecen.</summary>
    public ObservableCollection<UnityChoice> AllUnity { get; } = [];
    private bool rebuildingInstalls;

    private List<DetectedUnityInstall> AllManualInstalls() =>
        AllUnity.Select(c => c.Install).Concat(Installs).Where(i => i.IsManual)
            .DistinctBy(i => i.EditorPath, StringComparer.OrdinalIgnoreCase).ToList();

    private void AddUnityChoice(DetectedUnityInstall install)
    {
        var choice = new UnityChoice(install, !hiddenInstalls.Contains(install.EditorPath));
        choice.PropertyChanged += UnityChoiceChanged;
        AllUnity.Add(choice);
    }

    private void RebuildInstalls(IEnumerable<DetectedUnityInstall> all)
    {
        rebuildingInstalls = true;
        try
        {
            foreach (var c in AllUnity) c.PropertyChanged -= UnityChoiceChanged;
            AllUnity.Clear(); Installs.Clear();
            foreach (var i in all) { AddUnityChoice(i); if (!hiddenInstalls.Contains(i.EditorPath)) Installs.Add(i); }
        }
        finally { rebuildingInstalls = false; }
    }

    private async void UnityChoiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (rebuildingInstalls || e.PropertyName != nameof(UnityChoice.IsEnabled) || sender is not UnityChoice choice) return;
        if (choice.IsEnabled) hiddenInstalls.Remove(choice.EditorPath); else hiddenInstalls.Add(choice.EditorPath);
        var previous = SelectedUnity?.EditorPath;
        RebuildInstalls(AllUnity.Select(c => c.Install).ToList());
        SelectedUnity = Installs.FirstOrDefault(i => i.EditorPath == previous) ?? Installs.FirstOrDefault();
        UpdateSummary();
        try { await SavePreferencesAsync(); Status = choice.IsEnabled ? $"Unity {choice.Version} vuelve a ofrecerse en los presets." : $"Unity {choice.Version} ya no se ofrece en los presets."; }
        catch (Exception ex) { Status = $"No se pudo guardar la preferencia: {ex.Message}"; }
    }

    // ---------- Estado del asistente ----------
    private static readonly (string Label, string Sub, string Title, string Lead)[] SetupSteps =
    [
        ("Bienvenida", "Qué vamos a preparar", "Te damos la bienvenida a MochiSprout", "Antes de empezar revisamos tres cosas en esta computadora para que tus presets funcionen desde el primer proyecto."),
        ("VRChat Creator Companion", "Carpeta de datos de VCC", "Buscamos VRChat Creator Companion", "MochiSprout lee de VCC tus repositorios de paquetes y las plantillas oficiales, y ahí guarda las plantillas que exportes."),
        ("Unity", "Instalaciones del editor", "Elegí qué versiones de Unity usar", "Cada preset usa una instalación exacta. Desmarcá las que no quieras ofrecer al crear presets."),
        ("Tus datos", "Presets y respaldos", "Tus presets y respaldos", "Si ya usaste la versión anterior o tenés un respaldo, lo podés traer ahora."),
        ("Preferencias", "Cómo trabaja el editor", "Un par de preferencias", "Se pueden cambiar en cualquier momento desde Configuración."),
        ("Listo", "Resumen", "Listo para crear tu primer preset", "Esto es lo que quedó configurado."),
    ];

    public event EventHandler? SetupRequested;
    public event EventHandler<bool>? SetupClosed;   // true: abrir el editor de un preset nuevo

    [ObservableProperty] private bool needsSetup;
    [ObservableProperty] private bool isSetup;
    [ObservableProperty] private int setupStep;
    [ObservableProperty] private bool setupScanning;
    [ObservableProperty] private string setupVccPath = "";
    [ObservableProperty] private bool setupVccFound;
    [ObservableProperty] private bool setupVccPicked;
    [ObservableProperty] private int setupRepoCount;
    [ObservableProperty] private int setupPackageCount;
    [ObservableProperty] private int setupTemplateCount;
    [ObservableProperty] private bool setupHasLegacy;
    [ObservableProperty] private int setupLegacyCount;
    [ObservableProperty] private bool setupImportLegacy = true;
    [ObservableProperty] private string setupBackupText = "Importar combina por ID y no duplica presets que ya tengas.";
    [ObservableProperty] private bool setupPrerelease;
    [ObservableProperty] private string setupMessage = "";
    public ObservableCollection<UnityChoice> SetupUnity { get; } = [];
    public ObservableCollection<SetupStepItem> SetupStepItems { get; } = [];
    public ObservableCollection<SetupSummaryRow> SetupSummary { get; } = [];

    private string? LegacyRoot => string.Equals(Path.GetFullPath(DataRoot), Path.GetFullPath(AppDataPaths.Root), StringComparison.OrdinalIgnoreCase) ? AppDataPaths.LegacyRoot : null;
    private int scanToken;

    public string SetupTitle => SetupSteps[SetupStep].Title;
    public string SetupLead => SetupSteps[SetupStep].Lead;
    public string SetupCounter => $"Paso {SetupStep + 1} de {SetupSteps.Length}";
    public double SetupProgress => (SetupStep + 1) / (double)SetupSteps.Length;
    public string SetupProgressLabel => $"{SetupStep + 1} de {SetupSteps.Length} pasos";
    public bool SetupCanBack => SetupStep is > 0 and < 5;
    public bool SetupShowSkip => SetupStep < 5;
    public bool SetupIsLast => SetupStep == 5;
    public int SetupEnabledUnityCount => SetupUnity.Count(u => u.IsEnabled);
    public bool SetupHas2022 => SetupUnity.Any(u => u.IsEnabled && u.Version.StartsWith("2022."));
    public bool SetupHas2019 => SetupUnity.Any(u => u.IsEnabled && u.Version.StartsWith("2019."));
    public string Setup2022Text => SetupHas2022 ? "Disponibles para Avatar y World." : "Agregá una instalación de Unity 2022 para crear estos presets.";
    public string Setup2019Text => SetupHas2019 ? "Disponibles para Avatar y World." : "Opcional. Solo hace falta para proyectos que todavía usan 2019.";
    public bool SetupShowVccFound => SetupVccFound && !SetupScanning;
    public bool SetupShowVccMissing => !SetupVccFound && !SetupScanning;
    public string SetupVccHeadline => SetupVccPicked ? "VCC encontrado en la carpeta que elegiste" : "VCC encontrado";
    public string SetupLegacyText => $"Encontramos {SetupLegacyCount} {(SetupLegacyCount == 1 ? "preset" : "presets")} y el registro de plantillas exportadas en %LocalAppData%\\VCCPresetStudio. Se copian; la carpeta anterior queda como respaldo.";
    public bool SetupNextIsPrimary => !((SetupStep == 1 && !SetupVccFound) || (SetupStep == 2 && SetupEnabledUnityCount == 0));
    public string SetupNextLabel => SetupStep switch
    {
        0 => "Empezar",
        1 => SetupVccFound ? "Continuar" : "Seguir sin VCC",
        2 => SetupEnabledUnityCount > 0 ? "Continuar" : "Seguir sin Unity",
        5 => "Crear mi primer preset",
        _ => "Continuar"
    };
    public string SetupFootNote => SetupMessage.Length > 0 ? SetupMessage : SetupStep switch
    {
        0 => "Nada se guarda hasta que termines.",
        1 when !SetupVccFound && !SetupScanning => "Sin VCC no se puede leer el catálogo ni exportar plantillas.",
        2 when !SetupScanning => SetupEnabledUnityCount switch { 0 => "Sin Unity no vas a poder guardar presets.", 1 => "1 instalación seleccionada", var n => $"{n} instalaciones seleccionadas" },
        _ => ""
    };

    partial void OnSetupStepChanged(int value) { SetupMessage = ""; NotifySetup(); }
    partial void OnSetupScanningChanged(bool value) => NotifySetup();
    partial void OnSetupVccFoundChanged(bool value) => NotifySetup();
    partial void OnSetupVccPickedChanged(bool value) => NotifySetup();
    partial void OnSetupMessageChanged(string value) => OnPropertyChanged(nameof(SetupFootNote));
    partial void OnSetupImportLegacyChanged(bool value) => RebuildSetupSummary();
    partial void OnSetupPrereleaseChanged(bool value) => RebuildSetupSummary();
    partial void OnSetupLegacyCountChanged(int value) => OnPropertyChanged(nameof(SetupLegacyText));

    private void NotifySetup()
    {
        foreach (var name in new[] { nameof(SetupTitle), nameof(SetupLead), nameof(SetupCounter), nameof(SetupProgress), nameof(SetupProgressLabel), nameof(SetupCanBack), nameof(SetupShowSkip), nameof(SetupIsLast), nameof(SetupNextIsPrimary), nameof(SetupNextLabel), nameof(SetupFootNote), nameof(SetupVccHeadline), nameof(SetupShowVccFound), nameof(SetupShowVccMissing), nameof(SetupEnabledUnityCount), nameof(SetupHas2022), nameof(SetupHas2019), nameof(Setup2022Text), nameof(Setup2019Text) })
            OnPropertyChanged(name);
        SetupStepItems.Clear();
        for (var i = 0; i < SetupSteps.Length; i++) SetupStepItems.Add(new(i, (i + 1).ToString(), SetupSteps[i].Label, SetupSteps[i].Sub, i == SetupStep, i < SetupStep));
        RebuildSetupSummary();
    }

    private void RebuildSetupSummary()
    {
        SetupSummary.Clear();
        var unity = SetupUnity.Where(u => u.IsEnabled).Select(u => u.Version).ToList();
        SetupSummary.Add(new("VCC", SetupVccFound ? SetupVccPath : "Sin configurar · podés elegirlo en Configuración", SetupVccFound, 1));
        SetupSummary.Add(new("Unity", unity.Count == 0 ? "Ninguna seleccionada" : string.Join(" · ", unity), unity.Count > 0, 2));
        SetupSummary.Add(new("Tus datos", SetupHasLegacy && SetupImportLegacy ? $"{SetupLegacyCount} {(SetupLegacyCount == 1 ? "preset traído" : "presets traídos")} de VCC Preset Studio" : Presets.Count > 0 ? $"{Presets.Count} {(Presets.Count == 1 ? "preset" : "presets")} en tu biblioteca" : "Empezás con la biblioteca vacía", true, 3));
        SetupSummary.Add(new("Preferencias", "Tema oscuro · Español · " + (SetupPrerelease ? "con versiones preliminares" : "solo versiones estables"), true, 4));
    }

    // ---------- Comandos ----------
    [RelayCommand] private void BeginSetup()
    {
        if (IsBusy || IsEditor || IsSetup) return;
        SetupVccPath = vccRoot; SetupVccPicked = false; SetupPrerelease = ShowPrerelease; SetupImportLegacy = true; SetupMessage = "";
        SetupBackupText = "Importar combina por ID y no duplica presets que ya tengas.";
        // Si venís de VCC Preset Studio, se proponen su carpeta de VCC y sus Unity agregados a mano.
        var legacy = LegacyRoot;
        var legacyManual = new List<DetectedUnityInstall>();
        SetupHasLegacy = false; SetupLegacyCount = 0;
        if (legacy is not null && File.Exists(Path.Combine(legacy, "profiles.json")) && !File.Exists(Path.Combine(DataRoot, "profiles.json")))
        {
            try
            {
                var doc = JsonSerializer.Deserialize<ProfilesDocument>(File.ReadAllText(Path.Combine(legacy, "profiles.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                SetupLegacyCount = doc?.Profiles.Count ?? 0;
                SetupHasLegacy = true;
                var legacyPrefsFile = Path.Combine(legacy, "preferences.json");
                if (File.Exists(legacyPrefsFile) && JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(legacyPrefsFile)) is { } old)
                {
                    if (File.Exists(Path.Combine(old.VccRoot, "settings.json"))) SetupVccPath = old.VccRoot;
                    legacyManual.AddRange(old.ManualInstalls.Where(i => File.Exists(i.EditorPath)));
                }
            }
            catch (Exception) { SetupHasLegacy = false; }
        }
        SetupUnity.Clear();
        foreach (var c in AllUnity) AddSetupUnity(c.Install, c.IsEnabled);
        foreach (var i in legacyManual) if (!SetupUnity.Any(u => string.Equals(u.EditorPath, i.EditorPath, StringComparison.OrdinalIgnoreCase))) AddSetupUnity(i, true);
        SetupStep = 0; NotifySetup();
        IsSetup = true;
        SetupRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AddSetupUnity(DetectedUnityInstall install, bool enabled)
    {
        var choice = new UnityChoice(install, enabled);
        choice.PropertyChanged += (_, _) => NotifySetup();
        SetupUnity.Add(choice);
    }

    [RelayCommand] private async Task SetupGoAsync(int step)
    {
        if (SetupScanning) return;
        var target = Math.Clamp(step, 0, SetupSteps.Length - 1);
        var changed = target != SetupStep;
        SetupStep = target;
        if (changed && target == 1) await ScanVccAsync();
        else if (changed && target == 2) await ScanUnityAsync();
    }
    [RelayCommand] private Task SetupNextAsync() => SetupGoAsync(SetupStep + 1);
    [RelayCommand] private Task SetupBackAsync() => SetupGoAsync(SetupStep - 1);
    [RelayCommand] private void SetupSkip() { if (!SetupScanning) SetupStep = 5; }
    [RelayCommand] private async Task SetupRescanAsync()
    {
        if (SetupStep == 1) await ScanVccAsync(); else if (SetupStep == 2) await ScanUnityAsync();
    }

    /// <summary>Lee la carpeta de VCC propuesta: settings.json, repositorios, paquetes en caché y plantillas oficiales.</summary>
    public async Task ScanVccAsync(string? folder = null)
    {
        if (folder is not null) { SetupVccPath = folder; SetupVccPicked = true; }
        var token = ++scanToken; SetupScanning = true;
        var path = SetupVccPath;
        try
        {
            var minimum = Task.Delay(600);
            var scan = Task.Run(() =>
            {
                if (!File.Exists(Path.Combine(path, "settings.json"))) return (false, 0, 0, 0);
                var catalogResult = new RegisteredCatalogService(path).Load();
                var templates = VccTemplateBrowser.List(Path.Combine(path, "VRCTemplates"), false).Count;
                return (true, catalogResult.Packages.Select(p => p.RepositoryId).Distinct().Count(), catalogResult.Packages.Count, templates);
            });
            await Task.WhenAll(minimum, scan);
            if (token != scanToken) return;
            (SetupVccFound, SetupRepoCount, SetupPackageCount, SetupTemplateCount) = scan.Result;
            if (!SetupVccFound && SetupVccPicked) SetupMessage = "Esa carpeta no tiene settings.json de VCC. Elegí la carpeta de datos de VCC.";
        }
        catch (Exception ex) { if (token == scanToken) { SetupVccFound = false; SetupMessage = $"No se pudo leer VCC: {ex.Message}"; } }
        finally { if (token == scanToken) SetupScanning = false; }
    }

    /// <summary>Vuelve a detectar Unity (Unity Hub y Program Files) y conserva las agregadas a mano y lo que ya desmarcaste.</summary>
    public async Task ScanUnityAsync()
    {
        var token = ++scanToken; SetupScanning = true;
        try
        {
            var minimum = Task.Delay(600);
            var detection = Task.Run(() => unity.Detect());
            await Task.WhenAll(minimum, detection);
            if (token != scanToken) return;
            var previous = SetupUnity.ToDictionary(u => u.EditorPath, u => u.IsEnabled, StringComparer.OrdinalIgnoreCase);
            var manual = SetupUnity.Where(u => u.Install.IsManual).Select(u => u.Install).ToList();
            SetupUnity.Clear();
            foreach (var i in detection.Result) AddSetupUnity(i, previous.TryGetValue(i.EditorPath, out var on) ? on : !hiddenInstalls.Contains(i.EditorPath));
            foreach (var i in manual) if (File.Exists(i.EditorPath) && !SetupUnity.Any(u => string.Equals(u.EditorPath, i.EditorPath, StringComparison.OrdinalIgnoreCase))) AddSetupUnity(i, !previous.TryGetValue(i.EditorPath, out var on2) || on2);
        }
        catch (Exception ex) { if (token == scanToken) SetupMessage = $"No se pudo detectar Unity: {ex.Message}"; }
        finally { if (token == scanToken) { SetupScanning = false; NotifySetup(); } }
    }

    public void SetupAddUnity(string path)
    {
        var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).ProductVersion?.Split(' ').FirstOrDefault();
        if (string.IsNullOrWhiteSpace(version)) { SetupMessage = "Ese ejecutable no informa su versión de Unity."; return; }
        var existing = SetupUnity.FirstOrDefault(u => string.Equals(u.EditorPath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { existing.IsEnabled = true; return; }
        AddSetupUnity(new DetectedUnityInstall(version, Path.GetFullPath(path), true), true);
        SetupMessage = ""; NotifySetup();
    }

    public async Task SetupImportBackupAsync(string path)
    {
        var before = Presets.Count;
        try { await ImportAsync(path); var added = Presets.Count - before; SetupBackupText = $"{Path.GetFileName(path)} · {added} {(added == 1 ? "preset nuevo se sumó" : "presets nuevos se sumaron")} a tu biblioteca."; RebuildSetupSummary(); }
        catch (Exception ex) { SetupBackupText = $"No se pudo importar: {ex.Message}"; }
    }

    /// <summary>Aplica todo lo elegido y cierra el asistente. Con <paramref name="createPreset"/> abre el editor de un preset nuevo.</summary>
    [RelayCommand] private async Task SetupFinishAsync(bool createPreset)
    {
        if (IsBusy || !IsSetup) return;
        IsBusy = true;
        try
        {
            if (SetupHasLegacy && SetupImportLegacy && LegacyRoot is { } legacy)
            {
                AppDataPaths.ImportLegacyPresets(DataRoot, legacy);
                Presets.Clear(); foreach (var p in await store.LoadAsync()) Presets.Add(p);
            }
            if (SetupVccFound) { vccRoot = Path.GetFullPath(SetupVccPath); VccPathDraft = vccRoot; OnPropertyChanged(nameof(VccRoot)); }
            hiddenInstalls = new HashSet<string>(SetupUnity.Where(u => !u.IsEnabled).Select(u => u.EditorPath), StringComparer.OrdinalIgnoreCase);
            var all = SetupUnity.Select(u => u.Install).ToList();
            foreach (var c in AllUnity.Where(c => c.Install.IsManual)) if (!all.Any(i => string.Equals(i.EditorPath, c.EditorPath, StringComparison.OrdinalIgnoreCase))) all.Add(c.Install);
            RebuildInstalls(all);
            ShowPrerelease = SetupPrerelease;
            savedPreferences.SetupCompleted = true;
            await SavePreferencesAsync();
            await RefreshSourcesCoreAsync();
            NeedsSetup = false; IsSetup = false;
            Status = "Configuración inicial guardada.";
        }
        catch (Exception ex) { SetupMessage = $"No se pudo guardar: {ex.Message}"; return; }
        finally { IsBusy = false; }
        SetupClosed?.Invoke(this, createPreset);
    }
}
