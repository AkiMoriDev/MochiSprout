using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MochiSprout.Contracts;
using MochiSprout.Controllers;
using MochiSprout.Services.Diagnostics;
using MochiSprout.Services.Profiles;
using MochiSprout.Services.Vcc;
using MochiSprout.Services.Vpm;
namespace MochiSprout;
public partial class App : Application
{
    private ServiceProvider? provider;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Los datos pasaron de "Akimorivt Systems" a "AkiMoriDev Collection": se copian una vez y la carpeta anterior queda.
        try { AppDataPaths.MigratePreviousRoot(); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { }
        // Los presets de VCC Preset Studio se traen desde el asistente de primer inicio, si el usuario lo elige.
        var services = new ServiceCollection();
        services.AddSingleton<IProfileStore, ProfileStore>();
        services.AddSingleton<UnityDetectionService>();
        services.AddSingleton<RegisteredCatalogService>();
        services.AddSingleton<ExportedTemplateStore>();
        services.AddSingleton<VccTemplateExporter>();
        services.AddSingleton<PresetController>();
        services.AddSingleton<MainWindow>();
        provider = services.BuildServiceProvider();
        // Primero se valida si la configuración inicial ya se hizo (solo lee preferences.json, sin buscar VCC ni Unity).
        // Hecha: se abre la app principal. Pendiente: se abre la ventana de configuración inicial y, al terminarla, la app principal.
        var controller = provider.GetRequiredService<PresetController>();
        if (controller.IsSetupCompleted()) { ShowMainWindow(openNewPreset: false); return; }
        var setup = new Views.SetupWindow(controller);
        setup.Completed += (_, createPreset) => { ShowMainWindow(createPreset); setup.Close(); };
        MainWindow = setup;
        setup.Show();
    }
    private void ShowMainWindow(bool openNewPreset)
    {
        var main = provider!.GetRequiredService<MainWindow>();
        main.OpenNewPresetOnLoad = openNewPreset;
        MainWindow = main;
        main.Show();
    }
    protected override void OnExit(ExitEventArgs e) { provider?.Dispose(); base.OnExit(e); }
}
