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
        // Primera vez con la carpeta nueva: trae los presets de %LocalAppData%\VCCPresetStudio (sin borrar el original).
        try { AppDataPaths.MigrateLegacyData(); }
        catch (Exception ex) { MessageBox.Show($"No se pudieron copiar los datos anteriores desde {AppDataPaths.LegacyRoot}:\n{ex.Message}", "MochiSprout", MessageBoxButton.OK, MessageBoxImage.Warning); }
        var services = new ServiceCollection();
        services.AddSingleton<IProfileStore, ProfileStore>();
        services.AddSingleton<UnityDetectionService>();
        services.AddSingleton<RegisteredCatalogService>();
        services.AddSingleton<ExportedTemplateStore>();
        services.AddSingleton<VccTemplateExporter>();
        services.AddSingleton<PresetController>();
        services.AddSingleton<MainWindow>();
        provider = services.BuildServiceProvider();
        provider.GetRequiredService<MainWindow>().Show();
    }
    protected override void OnExit(ExitEventArgs e) { provider?.Dispose(); base.OnExit(e); }
}
