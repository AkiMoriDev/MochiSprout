using System.IO;
using System.Text.Json;
using MochiSprout.Models;
using MochiSprout.Services.Profiles;
using MochiSprout.Services.Vpm;
using MochiSprout.Services.Vcc;
using MochiSprout.Services.Diagnostics;
using MochiSprout.Controllers;
class Program
{
 static int checks;
 static void Save(PresetController c) { Wait(c.SavePresetCommand.ExecuteAsync(null)); Wait(c.ConfirmSaveCommand.ExecuteAsync(null)); }
 static void Wait(Task task) { var dispatcher=System.Windows.Threading.Dispatcher.CurrentDispatcher;if(!task.IsCompleted){var frame=new System.Windows.Threading.DispatcherFrame();task.ContinueWith(_=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));System.Windows.Threading.Dispatcher.PushFrame(frame);}task.GetAwaiter().GetResult(); }
 static T Wait<T>(Task<T> task) {Wait((Task)task);return task.GetAwaiter().GetResult();}
 static void Check(bool pass,string name){if(!pass)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
 [STAThread] static void Main()
 {
  SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
  var root=Path.Combine(AppContext.BaseDirectory,"fixtures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  var vcc=Path.Combine(root,"VCC");Directory.CreateDirectory(Path.Combine(vcc,"Repos"));
  var repo=Path.Combine(vcc,"Repos","registered.json");
  File.WriteAllText(repo,"""{"repo":{"id":"tools","name":"Creator Tools","url":"https://example.invalid/index.json","packages":{"com.example.tool":{"versions":{"2.0.0-beta.1":{"displayName":"Tool beta"},"1.2.0":{"displayName":"Tool","unity":"2022.3","vpmDependencies":{"com.example.dep":"^1.0.0"}},"1.0.0":{"displayName":"Tool"}}}}}}""");
  File.WriteAllText(Path.Combine(vcc,"Repos","stale.json"),"""{"repo":{"id":"removed","name":"Removed","packages":{"com.removed":{"versions":{"1.0.0":{}}}}}}""");
  File.WriteAllText(Path.Combine(vcc,"settings.json"),JsonSerializer.Serialize(new {userRepos=new[]{new{localPath=repo,id="tools",name="Creator Tools",url="https://example.invalid/index.json"}}}));
  var catalog=new RegisteredCatalogService(vcc).Load();
  Check(catalog.Packages.Count==1,"Only registered community repositories, not stale cache");
  Check(catalog.Packages[0].Versions.Count==3,"All cached package versions retained");
  Check(catalog.Packages[0].SelectedVersion?.Version=="1.2.0","Stable version preferred over newer prerelease");
  Check(catalog.Packages[0].Versions.Any(v=>v.Dependencies.ContainsKey("com.example.dep")),"Dependency and Unity metadata retained");
  Check(new RegisteredCatalogService(Path.Combine(root,"absent")).Load().Messages.Count==1,"Missing VCC is actionable");
  File.WriteAllText(Path.Combine(vcc,"Repos","broken.json"),"invalid");
  var legacyData=Path.Combine(root,"LegacyData");Directory.CreateDirectory(legacyData);File.WriteAllText(Path.Combine(legacyData,"profiles.json"),"{}");File.WriteAllText(Path.Combine(legacyData,"exported-templates.json"),"{}");
  var newData=Path.Combine(root,"Akimorivt Systems","MochiSprout");
  Check(AppDataPaths.MigrateLegacyData(newData,legacyData)&&File.Exists(Path.Combine(newData,"profiles.json"))&&File.Exists(Path.Combine(newData,"exported-templates.json"))&&File.Exists(Path.Combine(legacyData,"profiles.json")),"Legacy data is copied to the new folder and kept in place");
  File.WriteAllText(Path.Combine(newData,"profiles.json"),"{\"kept\":true}");Check(!AppDataPaths.MigrateLegacyData(newData,legacyData)&&File.ReadAllText(Path.Combine(newData,"profiles.json")).Contains("kept"),"Migration never overwrites data already in the new folder");
  Check(AppDataPaths.Root.EndsWith(Path.Combine("Akimorivt Systems","MochiSprout")),"Default data folder is Akimorivt Systems/MochiSprout");
  var store=new ProfileStore(Path.Combine(root,"Data"));
  var editor=Path.Combine(root,"Unity.exe");File.WriteAllText(editor,"test fixture");
  var profile=new ProjectProfile{Id="test",DisplayName="Avatar Test",Target=UnityTarget.Other,UnityVersion="6000.0.10f1",UnityEditorPath=editor,Packages=[new(){Id="com.example.tool",Version="1.2.0",RepositoryId="tools"}],Repositories=[new(){Id="tools",DisplayName="Creator Tools",Url="https://example.invalid/index.json"}]};
  Wait(store.SaveAsync([profile]));var loaded=Wait(store.LoadAsync());
  Check(loaded[0].UnityVersion=="6000.0.10f1"&&loaded[0].Target==UnityTarget.Other,"Custom Unity round trip without 2022 coercion");
  Check(loaded[0].Packages[0].RepositoryId=="tools","Package source and exact version round trip");
  var exporter=new VccTemplateExporter(new ExportedTemplateStore(Path.Combine(root,"Mappings")));
  bool blocked=false;try{exporter.ExportTemplate(profile,vcc);}catch(InvalidOperationException){blocked=true;}
  Check(blocked&&!Directory.Exists(Path.Combine(vcc,"Templates","Avatar Test")),"Unsupported base template does not create partial output");
  var baseDir=Path.Combine(vcc,"VRCTemplates","Avatar 2022");Directory.CreateDirectory(Path.Combine(baseDir,"ProjectSettings"));Directory.CreateDirectory(Path.Combine(baseDir,"Packages"));
  File.WriteAllText(Path.Combine(baseDir,"ProjectSettings","ProjectVersion.txt"),"m_EditorVersion: 2022.3.22f1\nm_EditorVersionWithRevision: 2022.3.22f1 (hash)");
  File.WriteAllText(Path.Combine(baseDir,"Packages","vpm-manifest.json"),"""{"dependencies":{"com.vrchat.avatars":{"version":"~3.0.0"}}}""");
  var valid=new ProjectProfile{Id="valid",DisplayName="Valid Avatar",Target=UnityTarget.Unity2022,UnityVersion="2022.3.22f1",UnityEditorPath=editor,Packages=profile.Packages};
  var output=exporter.ExportTemplate(valid,vcc);
  Check(File.Exists(Path.Combine(output,"package.json")),"Template exported via staging");
  Check(File.ReadAllText(Path.Combine(output,"Packages","vpm-manifest.json")).Contains("com.vrchat.avatars"),"Official SDK dependency preserved");
  Check(File.ReadAllText(Path.Combine(output,"ProjectSettings","ProjectVersion.txt")).Contains("hash"),"Original matching Unity revision preserved");
  blocked=false;try{exporter.ExportTemplate(valid,vcc);}catch(InvalidOperationException){blocked=true;}Check(blocked,"Duplicate template cannot overwrite existing template");
  var controller=new PresetController(store,new UnityDetectionService(),new RegisteredCatalogService(vcc),exporter);
  controller.Installs.Add(new("2022.3.22f1",editor,true));controller.NewPresetCommand.Execute(null);controller.Name="Controller test";
  Wait(controller.SavePresetCommand.ExecuteAsync(null));
  Check(controller.SaveReviewOpen&&controller.PendingPreset?.DisplayName=="Controller test"&&controller.Presets.Count==0,"Save opens review without persisting the preset");
  controller.BackToEditorCommand.Execute(null);
  Check(!controller.SaveReviewOpen&&controller.PendingPreset is null&&controller.Name=="Controller test"&&controller.IsEditor,"Returning from review preserves editor contents");
  Save(controller);
  Console.WriteLine($"Controller status: {controller.Status}; Unity={controller.SelectedUnity?.EditorPath}; Count={controller.Presets.Count}; Editor={controller.IsEditor}"); Check(controller.Presets.Count==1&&!controller.IsEditor,"Controller saves preset without exporting");
  controller.EditPresetCommand.Execute(controller.Presets[0]);controller.Name="Updated";Save(controller);
  Check(controller.Presets.Count==1&&controller.Presets[0].DisplayName=="Updated","Editing replaces same preset and keeps its ID");
  File.WriteAllText(Path.Combine(baseDir,"package.json"),"""{"displayName":"Avatar Base","category":"ProjectTemplate"}""");
  var world=Path.Combine(vcc,"VRCTemplates","UdonSharp");Directory.CreateDirectory(Path.Combine(world,"Packages"));Directory.CreateDirectory(Path.Combine(world,"ProjectSettings"));
  File.WriteAllText(Path.Combine(world,"package.json"),"""{"displayName":"UdonSharp","category":"ProjectTemplate"}""");
  File.WriteAllText(Path.Combine(world,"Packages","vpm-manifest.json"),"""{"dependencies":{"com.vrchat.worlds":{"version":"~3.0.0"},"com.vrchat.udonsharp":{"version":"1.1.0"}}}""");
  File.WriteAllText(Path.Combine(world,"ProjectSettings","ProjectVersion.txt"),"m_EditorVersion: 2022.3.22f1");
  var data=Path.Combine(root,"Navigation");
  var nav=new PresetController(new ProfileStore(data),new UnityDetectionService(),new RegisteredCatalogService(vcc),exporter,data);
  Wait(nav.SetVccRootAsync(vcc));nav.Installs.Add(new("2022.3.22f1",editor,true));
  Check(nav.DefaultTemplates.Count==2,"Default templates are detected from VCC folders");
  Check(nav.Templates.Count==1,"Personal VCC templates are listed separately");
  nav.ShowTemplatePackagesCommand.Execute(nav.DefaultTemplates.First(t=>t.FolderName=="UdonSharp"));
  Check(nav.TemplateDetailsOpen&&nav.TemplateDetails.Any(d=>d.Id=="com.vrchat.udonsharp"),"Read-only default template package inspector");
  nav.CloseTemplateDetailsCommand.Execute(null);nav.UseTemplateCommand.Execute(nav.DefaultTemplates.First(t=>t.FolderName=="UdonSharp"));
  Check(nav.Page=="Presets"&&nav.Mode==ProjectMode.World&&nav.SelectedBaseTemplate?.FolderName=="UdonSharp","Use default template opens independent preset draft");
  nav.SelectedUnity=nav.Installs.First(i=>i.EditorPath==editor);nav.Name="Udon custom";Save(nav);
  var udon=nav.Presets.Single();Check(udon.BaseTemplateName=="UdonSharp","Chosen base persists with preset");
  var udonOutput=exporter.ExportTemplate(udon,vcc);Check(File.ReadAllText(Path.Combine(udonOutput,"Packages","vpm-manifest.json")).Contains("com.vrchat.udonsharp"),"Selected UdonSharp base is used at export");
  nav.NavigateCommand.Execute("Paquetes de VCC");nav.BrowseSearch="missing-package";
  Check(!nav.CatalogBrowseView.Cast<object>().Any()&&nav.PackageView.Cast<object>().Any(),"Catalog filter is independent of preset package filter");
  nav.BrowseSearch="";nav.SelectedCatalogPackage=nav.Packages.First(p=>p.Id=="com.example.tool");
  nav.OnlyStable=true;Check(nav.InspectorVersions.All(v=>!v.Version.Contains('-')),"Stable-only inspector versions");
  nav.OnlyStable=false;Check(nav.InspectorVersions.Any(v=>v.Version.Contains('-')),"Prerelease inspector toggle");
  var inspected=nav.InspectorVersions.First(v=>v.Version=="1.0.0");nav.InspectedVersion=inspected;
  nav.UseCatalogPackageCommand.Execute(null);Check(nav.Page=="Presets"&&nav.SelectedPackages.Single().SelectedVersion?.Version=="1.0.0","Use catalog package copies inspected version into draft");
  nav.NavigateCommand.Execute("Configuración");Check(nav.IsEditor,"Navigating away preserves unsaved draft");
  nav.VccPathDraft=vcc;nav.ShowPrerelease=true;nav.RememberLastPage=false;Wait(nav.SaveSettingsCommand.ExecuteAsync(null));
  var savedPrefs=JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(Path.Combine(data,"preferences.json")))!;
  Check(savedPrefs.ShowPrerelease&&!savedPrefs.RememberLastPage,"Preferences persist with explicit save");
  nav.ShowPrerelease=false;nav.ResetSettingsCommand.Execute(null);Check(nav.ShowPrerelease,"Reset settings restores saved state");
  Check(File.ReadAllText(Path.Combine(world,"package.json")).Contains("UdonSharp"),"Official base remains intact after exporting copy");
  nav.BrowseRepository=null!;Wait(nav.RefreshSourcesCommand.ExecuteAsync(null));
  Check(nav.CatalogBrowseView.Cast<object>().Any(),"Refreshing sources restores a valid catalog filter");
  nav.UseTemplateCommand.Execute(nav.Templates.First(t=>t.FolderName=="Valid Avatar"));
  nav.SelectedUnity=nav.Installs.First(i=>i.EditorPath==editor);nav.Name="Derived personal";Save(nav);
  var personal=nav.Presets.First(p=>p.DisplayName=="Derived personal");
  Check(personal.BaseTemplateIsUser&&personal.BaseTemplateName=="Valid Avatar","Personal VCC base source persists separately from official bases");
  Directory.CreateDirectory(Path.Combine(output,"Assets"));File.WriteAllText(Path.Combine(output,"Assets","source.txt"),"original asset");
  var derived=exporter.ExportTemplate(personal,vcc);
  Check(File.ReadAllText(Path.Combine(derived,"Assets","source.txt"))=="original asset","Using a personal base preserves its assets in the exported copy");
  controller.NewPresetCommand.Execute(null);controller.Name="Snapshot";controller.Notes="Review notes";
  Wait(controller.SavePresetCommand.ExecuteAsync(null));
  Check(controller.PendingPreset?.Notes=="Review notes"&&controller.PendingPreset.UnityEditorPath==editor,"Review contains description and full Unity path");
  controller.Name="Changed after review";Wait(controller.ConfirmSaveCommand.ExecuteAsync(null));
  Check(controller.Presets.Any(p=>p.DisplayName=="Snapshot")&&!controller.SaveReviewOpen,"Confirmation saves exactly the reviewed snapshot and closes review");
  nav.UseTemplateCommand.Execute(nav.DefaultTemplates.First(t=>t.FolderName=="UdonSharp"));nav.SelectedUnity=nav.Installs.First(i=>i.EditorPath==editor);nav.Name="Review details";
  var tool=nav.Packages.First(p=>p.Id=="com.example.tool");tool.IsSelected=true;tool.SelectedVersion=tool.Versions.First(v=>v.Version=="1.2.0");
  var attachment=Path.Combine(root,"Extras.unitypackage");File.WriteAllText(attachment,"fixture");nav.AddFiles([attachment]);
  Wait(nav.SavePresetCommand.ExecuteAsync(null));
  Check(nav.ReviewPackages.Single().Id=="com.example.tool"&&nav.ReviewPackages.Single().Version=="1.2.0"&&nav.ReviewPackages.Single().Repository=="Creator Tools","Review shows package IDs, versions and repositories");
  Check(nav.ReviewBasePackages.Any(p=>p.Id=="com.vrchat.udonsharp")&&nav.PendingPreset?.UnityPackages.Single().FullPath==attachment,"Review includes base dependencies and attachment paths");
  File.Delete(attachment);Wait(nav.ConfirmSaveCommand.ExecuteAsync(null));
  Check(nav.SaveReviewOpen&&nav.ReviewSaveError.Contains("archivo")&&!nav.Presets.Any(p=>p.DisplayName=="Review details"),"Missing attachment prevents confirmation and keeps review open");
  nav.BackToEditorCommand.Execute(null);
  var setup=new UnityPackageImportSetup();
  var legacy=Path.Combine(root,"LegacyTemplate");Directory.CreateDirectory(Path.Combine(legacy,"ProjectSettings"));Directory.CreateDirectory(Path.Combine(legacy,"UnityPackages"));
  File.WriteAllText(Path.Combine(legacy,"UnityPackages","Legacy.unitypackage"),"legacy archive fixture");
  Check(setup.Prepare(legacy)==1,"Repairs a previously exported UnityPackages folder");
  var manifestFile=Path.Combine(legacy,UnityPackageImportSetup.EditorRelative,"import-manifest.json");
  var importManifest=JsonSerializer.Deserialize<UnityPackageImportSetup.ImportManifest>(File.ReadAllText(manifestFile))!;
  Check(File.Exists(Path.Combine(legacy,importManifest.packages.Single().path))&&importManifest.packages.Single().sha256.Length==64,"Manifest references existing originals with SHA-256 fingerprints");
  Check(File.Exists(Path.Combine(legacy,UnityPackageImportSetup.EditorRelative,"UnityPackageAutoImporter.cs"))&&File.Exists(Path.Combine(legacy,UnityPackageImportSetup.EditorRelative,UnityPackageImportSetup.AsmdefName+".asmdef")),"Installs editor importer in isolated Editor assembly");
  File.WriteAllText(Path.Combine(legacy,UnityPackageImportSetup.StateRelative),"existing state");
  setup.Prepare(legacy);Check(File.ReadAllText(Path.Combine(legacy,UnityPackageImportSetup.StateRelative))=="existing state","Repair preserves an existing project's import state");
  setup.Prepare(legacy,resetState:true);Check(!File.Exists(Path.Combine(legacy,UnityPackageImportSetup.StateRelative)),"New template copies reset inherited import state");
  var oldProject=Path.Combine(root,"OldPresetStudioProject");var oldEditor=Path.Combine(oldProject,UnityPackageImportSetup.LegacyEditorRelative);Directory.CreateDirectory(oldEditor);Directory.CreateDirectory(Path.Combine(oldProject,"ProjectSettings"));
  var oldArchive=Path.Combine(root,"OldLinked.unitypackage");File.WriteAllText(oldArchive,"old linked");
  File.WriteAllText(Path.Combine(oldEditor,"import-manifest.json"),JsonSerializer.Serialize(new UnityPackageImportSetup.ImportManifest(2,[new(oldArchive,"x")])));
  foreach(var f in new[]{"UnityPackageAutoImporter.cs","UnityPackageAutoImporter.cs.meta","VCCPresetStudio.UnityPackageImporter.asmdef"})File.WriteAllText(Path.Combine(oldEditor,f),"old");
  File.WriteAllText(Path.Combine(oldProject,UnityPackageImportSetup.LegacyStateRelative),"old state");
  Check(setup.Prepare(oldProject)==1&&File.Exists(Path.Combine(oldProject,UnityPackageImportSetup.EditorRelative,"UnityPackageAutoImporter.cs")),"Old VCC Preset Studio project gets the MochiSprout importer with its linked files");
  Check(!Directory.Exists(Path.Combine(oldProject,UnityPackageImportSetup.LegacyRootRelative)),"Old importer is removed so only one importer runs");
  Check(File.ReadAllText(Path.Combine(oldProject,UnityPackageImportSetup.StateRelative))=="old state"&&!File.Exists(Path.Combine(oldProject,UnityPackageImportSetup.LegacyStateRelative)),"Old import record is kept under the new name");
  var first=Path.Combine(root,"OrderedB.unitypackage");var second=Path.Combine(root,"OrderedA.unitypackage");File.WriteAllText(first,"b");File.WriteAllText(second,"a");
  var target=Path.Combine(root,"Ordered");Directory.CreateDirectory(Path.Combine(target,"ProjectSettings"));
  setup.Prepare(target,[new(){FileName=Path.GetFileName(first),FullPath=first},new(){FileName=Path.GetFileName(second),FullPath=second}]);
  var ordered=JsonSerializer.Deserialize<UnityPackageImportSetup.ImportManifest>(File.ReadAllText(Path.Combine(target,UnityPackageImportSetup.EditorRelative,"import-manifest.json")))!;
  Check(ordered.packages[0].path.EndsWith("OrderedB.unitypackage")&&ordered.packages[1].path.EndsWith("OrderedA.unitypackage"),"Manifest preserves the chosen package import order");
  var auto=new ProjectProfile{Id="auto",DisplayName="Auto Import",Target=UnityTarget.Unity2022,UnityVersion="2022.3.22f1",UnityPackages=[new(){FileName="OrderedB.unitypackage",FullPath=first}]};
  var autoOutput=exporter.ExportTemplate(auto,vcc);
  Check(File.Exists(Path.Combine(autoOutput,UnityPackageImportSetup.EditorRelative,"import-manifest.json")),"New exports include importer and manifest automatically");
  var before=File.ReadAllText(manifestFile);setup.Prepare(legacy);Check(File.ReadAllText(manifestFile)==before,"Repairing a template repeatedly is idempotent");
  Check(importManifest.schemaVersion==2&&Path.IsPathRooted(importManifest.packages.Single().path),"New manifests use absolute file references");
  Check(!Directory.Exists(Path.Combine(target,UnityPackageImportSetup.ArchiveRelative)),"Preparing references does not copy archives into the project");
  Check(!Directory.GetFiles(autoOutput,"*.unitypackage",SearchOption.AllDirectories).Any(),"Exported templates contain no duplicated unitypackage archives");
  var requests=0;string? titleAtRequest=null;
  nav.EditorRequested+=(_,_)=>{requests++;titleAtRequest=nav.EditorTitle;};nav.NewPresetCommand.Execute(null);
  Check(requests==1&&titleAtRequest=="Crear preset"&&nav.Page=="Presets","Create requests an editor modal while home remains visible");
  nav.EditPresetCommand.Execute(nav.Presets.First());
  Check(requests==2&&titleAtRequest=="Editar preset"&&nav.Page=="Presets","Edit requests the same modal after its fields are populated");
  string? utility=null;nav.UtilityRequested+=section=>utility=section;nav.NavigateCommand.Execute("Configuración");
  Check(utility=="Configuración"&&nav.Page=="Presets","Sidebar opens utilities without replacing the home section");
  var app=new System.Windows.Application{ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown};
  app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary{Source=new Uri("/MochiSprout;component/Assets/Themes/AppTheme.xaml",UriKind.Relative)});
  var reviewWindow=new MochiSprout.Views.SaveReviewWindow(nav);reviewWindow.ApplyTemplate();
  Check(reviewWindow.Content is System.Windows.Controls.Grid&&reviewWindow.DataContext==nav,"Review modal loads its XAML and dark theme resources");
  reviewWindow.Close();
  var editorView=new MochiSprout.Views.EditorView{DataContext=nav};Check(editorView.Content is System.Windows.Controls.Grid,"Shared create/edit view loads with dark resources");
  var home=new MochiSprout.Views.PresetsView{DataContext=nav};
  home.Measure(new System.Windows.Size(1400,850));home.Arrange(new System.Windows.Rect(0,0,1400,850));home.UpdateLayout();
  var own=(System.Windows.Controls.ItemsControl)home.FindName("OwnCards");
  Check(own.Items.Count==nav.Presets.Count+1 && own.Items[^1] is MochiSprout.Views.NewPresetTile,"Create card is the last home grid item");
  var grid=new MochiSprout.Views.AdaptiveCardPanel();for(int n=0;n<4;n++)grid.Children.Add(new System.Windows.Controls.Border{Height=170});
  foreach(var pair in new[]{(Width:1000d,Columns:3),(Width:600d,Columns:2),(Width:260d,Columns:1)}){grid.Measure(new System.Windows.Size(pair.Width,double.PositiveInfinity));grid.Arrange(new System.Windows.Rect(0,0,pair.Width,grid.DesiredSize.Height));var slot=System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot((System.Windows.FrameworkElement)grid.Children[0]);Check(Math.Abs(slot.Width-(pair.Width-16*(pair.Columns-1))/pair.Columns)<1,"Responsive cards at width "+pair.Width);}
  var image=new System.Windows.Media.Imaging.RenderTargetBitmap(1400,850,96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(home);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(AppContext.BaseDirectory,"MochiSprout-preview.png")))png.Save(file);
  nav.CancelEditCommand.Execute(null);
  var main=new MochiSprout.MainWindow(nav);Check(main.Title=="MochiSprout","Main window loads the MochiSprout layout");
  var mainContent=(System.Windows.FrameworkElement)main.Content;mainContent.Measure(new System.Windows.Size(1440,900));mainContent.Arrange(new System.Windows.Rect(0,0,1440,900));mainContent.UpdateLayout();var screen=new System.Windows.Media.Imaging.RenderTargetBitmap(1440,900,96,96,System.Windows.Media.PixelFormats.Pbgra32);screen.Render(mainContent);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(screen));using(var previewFile=File.Create(Path.Combine(AppContext.BaseDirectory,"MochiSprout-full.png")))encoder.Save(previewFile);

  nav.NewPresetCommand.Execute(null);
  Check(!nav.HasUnsavedEditorChanges&&nav.Name==""&&nav.Notes==""&&nav.EditorPackageCount==0&&nav.EditorFileCount==0,"New modal starts with a clean empty draft");
  nav.Name="Mochi base";Check(nav.HasUnsavedEditorChanges,"Name changes mark the draft dirty");nav.Name="";Check(!nav.HasUnsavedEditorChanges,"Reverting a field restores the clean baseline");
  var selectable=nav.Packages.First(p=>!p.IsSdk);selectable.IsSelected=true;nav.OnlySelectedPackages=true;
  Check(nav.PackageView.Cast<MochiSprout.Models.CatalogPackage>().Count()==1&&nav.EditorPackageCount==1,"Selected-only filter and live counter follow selections");
  selectable.IsSelected=false;Check(!nav.PackageView.Cast<object>().Any(),"Deselecting the last filtered row updates the list");
  nav.OnlySelectedPackages=false;
  var attachedFile=Path.Combine(root,"modal.unitypackage");File.WriteAllText(attachedFile,"fixture");nav.AddFiles([attachedFile]);Check(nav.HasUnsavedEditorChanges&&nav.EditorFileCount==1,"Linked files update dirty state and counters");nav.Attachments.Clear();Check(!nav.HasUnsavedEditorChanges,"Removing a newly added reference restores the clean baseline");
  nav.EditPresetCommand.Execute(nav.Presets.First());Check(!nav.HasUnsavedEditorChanges&&nav.EditorModalTitle.Contains(nav.Name)&&nav.EditorSaveLabel=="Guardar cambios","Edit mode loads a clean snapshot and named header");
  nav.NewPresetCommand.Execute(null);nav.Name="Mi avatar base";nav.Notes="Configuración para mi próximo proyecto.";selectable.IsSelected=true;
  var modal=new MochiSprout.Views.EditorView{DataContext=nav};modal.Measure(new System.Windows.Size(1080,744));modal.Arrange(new System.Windows.Rect(0,0,1080,744));modal.UpdateLayout();
  void CaptureModal(string name){System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);modal.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1080,744,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(modal);var enc=new System.Windows.Media.Imaging.PngBitmapEncoder();enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(AppContext.BaseDirectory,name));enc.Save(stream);}
  CaptureModal("MochiSprout-editor-paquetes.png");
  var tabs=(System.Windows.Controls.TabControl)modal.FindName("EditorTabs");tabs.SelectedIndex=1;modal.UpdateLayout();CaptureModal("MochiSprout-editor-archivos.png");
  var header=(System.Windows.Controls.StackPanel)((System.Windows.Controls.TabItem)tabs.Items[0]).Header;var countBadge=(System.Windows.Controls.Border)header.Children[1];var countText=(System.Windows.Controls.TextBlock)countBadge.Child;
  Check(countText.Text=="1"&&countBadge.ActualWidth>0,"Tab badge renders the live package counter");
  Check(tabs.Items.Count==2,"Editor exposes native package and file tabs");
  var longRepository="https://docs.hai-vr.dev/repositories/community-tools-with-a-long-name";
  nav.Repositories.Add(longRepository);
  for(int n=0;n<18;n++){var layoutVersion=new MochiSprout.Models.CatalogVersion("2026.10.29",null,new Dictionary<string,string>());nav.Packages.Add(new MochiSprout.Models.CatalogPackage{Id="com.layout."+n,DisplayName="Animator As Code V1 - Modular Avatar functions - extended community package "+n,RepositoryId="layout",RepositoryName=longRepository,RepositoryUrl="",Versions=[layoutVersion],SelectedVersion=layoutVersion});}
  nav.Repository=longRepository;tabs.SelectedIndex=0;
  IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject parent){for(int n=0;n<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);n++){var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,n);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
  foreach(var modalWidth in new[]{1080d,900d,760d,684d}){
   modal.Measure(new System.Windows.Size(modalWidth,744));modal.Arrange(new System.Windows.Rect(0,0,modalWidth,744));modal.UpdateLayout();System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);modal.UpdateLayout();
   var repoSelector=(System.Windows.Controls.ComboBox)modal.FindName("RepositorySelect");var selectorOrigin=repoSelector.TransformToAncestor(modal).Transform(new System.Windows.Point());
   Check(repoSelector.ActualWidth>80&&selectorOrigin.X+repoSelector.ActualWidth<=modalWidth-20,"Repository filter stays inside modal at "+modalWidth);
   var packageRows=(System.Windows.Controls.ListBox)modal.FindName("PackageList");var versions=Descendants(packageRows).OfType<System.Windows.Controls.ComboBox>().ToList();
   Check(versions.Count>0&&versions.All(v=>v.ActualWidth>=100&&v.TransformToAncestor(modal).Transform(new System.Windows.Point()).X+v.ActualWidth<=modalWidth-20),"Long package rows keep version selectors visible at "+modalWidth);
   var listScroll=Descendants(packageRows).OfType<System.Windows.Controls.ScrollViewer>().First();Check(listScroll.ScrollableWidth<1,"Package list has no horizontal overflow at "+modalWidth);
  }
  modal.Measure(new System.Windows.Size(1080,744));modal.Arrange(new System.Windows.Rect(0,0,1080,744));modal.UpdateLayout();
  CaptureModal("MochiSprout-editor-corregido.png");
  var variantData=Path.Combine(root,"Variants");var variants=new PresetController(new ProfileStore(variantData),new UnityDetectionService(),new RegisteredCatalogService(vcc),exporter,variantData);
  variants.Installs.Add(new("2022.3.22f1",editor,true));variants.NewPresetCommand.Execute(null);
  Check(!variants.SelectProjectVariantCommand.CanExecute("Avatar-2019"),"2019 options are disabled without an accessible installation");
  var editor2019=Path.Combine(root,"Unity2019.exe");File.WriteAllText(editor2019,"fixture");variants.Installs.Add(new("2019.4.31f1",editor2019,true));
  foreach(var option in new[]{"Avatar-2022","Avatar-2019","World-2022","World-2019"}){Check(variants.SelectProjectVariantCommand.CanExecute(option),"Variant is available: "+option);variants.SelectProjectVariantCommand.Execute(option);Check(variants.SelectedProjectVariant==option,"Combined button selects matching type and Unity: "+option);}
  variants.SelectedUnity=variants.Installs.First(i=>i.Version.StartsWith("2022."));Check(variants.SelectedProjectVariant=="World-2022","Exact Unity selection keeps combined buttons synchronized");
  variants.Name="Mi avatar base";variants.SelectProjectVariantCommand.Execute("Avatar-2022");modal.DataContext=variants;tabs.SelectedIndex=0;modal.UpdateLayout();CaptureModal("MochiSprout-cuatro-opciones.png");
  nav.BrowseSearch="";nav.BrowseRepository="Todos los repositorios";nav.OnlyStable=true;
  var catalogModal=new MochiSprout.Views.CatalogView{DataContext=nav};catalogModal.Measure(new System.Windows.Size(1080,744));catalogModal.Arrange(new System.Windows.Rect(0,0,1080,744));catalogModal.UpdateLayout();System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);catalogModal.UpdateLayout();
  var catalogList=(System.Windows.Controls.ListBox)catalogModal.FindName("CatalogTable");var rowButtons=Descendants(catalogList).OfType<System.Windows.Controls.Button>().ToList();
  Check(rowButtons.Any(b=>Equals(b.Content,"Usar en preset"))&&rowButtons.Any(b=>Equals(b.Content,"Info")),"Catalog rows expose use and info actions");
  var useButton=rowButtons.First(b=>Equals(b.Content,"Usar en preset")&&b.IsEnabled);var chosenPackage=(MochiSprout.Models.CatalogPackage)useButton.DataContext;useButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
  Check(chosenPackage.IsSelected&&nav.IsEditor&&chosenPackage.SelectedVersion?.Version==chosenPackage.LatestStableVersion,"Use action adds the row's stable version to the preset editor");
  var infoModal=new MochiSprout.Views.PackageInfoWindow(nav);Check(infoModal.DataContext==nav&&infoModal.Content is System.Windows.Controls.Grid,"Package info modal loads descriptions, versions and dependencies");
  var catalogBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1080,744,96,96,System.Windows.Media.PixelFormats.Pbgra32);catalogBitmap.Render(catalogModal);var catalogEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();catalogEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(catalogBitmap));using(var catalogFile=File.Create(Path.Combine(AppContext.BaseDirectory,"MochiSprout-catalogo-modal.png")))catalogEncoder.Save(catalogFile);
  var exampleVersion=new MochiSprout.Models.CatalogVersion("0.3.1","2019.4",new Dictionary<string,string>{{"com.anatawa12.custom-localization-for-editor-extension","^1.0.0"},{"com.vrchat.avatars","~3.2.0-beta.1"}},"Set of Anatawa12's Small Avatar Optimization Utilities","anatawa12");
  var newerExample=new MochiSprout.Models.CatalogVersion("1.0.0","2022.3",new Dictionary<string,string>(),"Nueva versión","anatawa12");
  var examplePackage=new MochiSprout.Models.CatalogPackage{Id="com.anatawa12.avatar-optimizer",DisplayName="Anatawa12's AvatarOptimizer",RepositoryId="anatawa12",RepositoryName="anatawa12",RepositoryUrl="",Versions=[exampleVersion,newerExample]};
  variants.Packages.Add(new MochiSprout.Models.CatalogPackage{Id="com.anatawa12.custom-localization-for-editor-extension",DisplayName="Localization",RepositoryId="anatawa12",RepositoryName="anatawa12",RepositoryUrl="",Versions=[new("1.0.0",null,new Dictionary<string,string>(),"","anatawa12")]});
  variants.SelectedCatalogPackage=examplePackage;
  Check(variants.InspectedAuthor=="anatawa12"&&variants.InfoDependencies.Count==2,"Package info exposes author and version-specific dependencies");
  Check(variants.InfoDependencies.Any(d=>d.Context=="Del mismo autor")&&variants.InfoDependencies.Any(d=>d.Context.Contains("SDK de VRChat")),"Dependency context distinguishes shared author and included SDK");
  var designedInfo=new MochiSprout.Views.PackageInfoWindow(variants){Width=1000,Height=740};designedInfo.UpdateLayoutSize();var infoRoot=(System.Windows.FrameworkElement)designedInfo.Content;infoRoot.Measure(new System.Windows.Size(1000,740));infoRoot.Arrange(new System.Windows.Rect(0,0,1000,740));infoRoot.UpdateLayout();System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);infoRoot.UpdateLayout();
  var infoBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1000,740,96,96,System.Windows.Media.PixelFormats.Pbgra32);infoBitmap.Render(infoRoot);var infoEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();infoEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(infoBitmap));using(var infoFile=File.Create(Path.Combine(AppContext.BaseDirectory,"MochiSprout-info-paquete.png")))infoEncoder.Save(infoFile);
  variants.InspectedVersion=newerExample;Check(variants.InspectedUnity=="2022.3"&&variants.InfoDependencies.Count==0&&variants.InspectedDescription=="Nueva versión","Changing version updates Unity, dependencies and description");
  designedInfo.Width=700;designedInfo.UpdateLayoutSize();var metadata=(System.Windows.Controls.Border)designedInfo.FindName("MetadataPanel");Check(System.Windows.Controls.Grid.GetRow(metadata)==1&&System.Windows.Controls.Grid.GetColumn(metadata)==0,"Narrow package info stacks metadata below dependencies");
  designedInfo.Width=1000;designedInfo.UpdateLayoutSize();Check(System.Windows.Controls.Grid.GetRow(metadata)==0&&System.Windows.Controls.Grid.GetColumn(metadata)==1,"Wide package info restores the two-column layout");
  File.WriteAllText(repo,File.ReadAllText(repo).Replace("\"displayName\":\"Tool\",\"unity\"","\"displayName\":\"Tool\",\"author\":{\"name\":\"Example Author\"},\"unity\""));
  Check(new RegisteredCatalogService(vcc).Load().Packages.SelectMany(p=>p.Versions).Any(v=>v.Author=="Example Author"),"Registered catalog reads package author metadata");
  Console.WriteLine($"{checks} checks passed. Fixtures: {root}");
 }
}












