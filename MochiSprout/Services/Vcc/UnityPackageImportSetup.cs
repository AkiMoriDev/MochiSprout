using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MochiSprout.Models;
namespace MochiSprout.Services.Vcc;
/// <summary>Writes references to original archives; never duplicates .unitypackage files.</summary>
public sealed class UnityPackageImportSetup
{
 public const string EditorRelative="Assets/MochiSprout/Editor";
 public const string StateRelative="ProjectSettings/MochiSproutImportState.json";
 public const string AsmdefName="MochiSprout.UnityPackageImporter";
 // Ubicaciones de versiones anteriores (VCC Preset Studio): se leen para migrar y el importador viejo se reemplaza.
 public const string LegacyRootRelative="Assets/VCCPresetStudio";
 public const string LegacyEditorRelative="Assets/VCCPresetStudio/Editor";
 public const string LegacyStateRelative="ProjectSettings/VCCPresetStudioImportState.json";
 public const string ArchiveRelative="Assets/VCCPresetStudio/Archives~"; // solo compatibilidad con el formato v1
 private static readonly string[] LegacyEditorFiles=["UnityPackageAutoImporter.cs","VCCPresetStudio.UnityPackageImporter.asmdef","import-manifest.json"];
 private static readonly JsonSerializerOptions Options=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
 public sealed record ArchiveEntry(string path,string sha256);
 public sealed record ImportManifest(int schemaVersion,List<ArchiveEntry> packages);
 public int Prepare(string projectRoot,IReadOnlyList<UnityPackageReference>? attachments=null,bool resetState=false,string? inheritedSource=null)
 {
  projectRoot=Path.GetFullPath(projectRoot);
  if(!Directory.Exists(Path.Combine(projectRoot,"ProjectSettings")))throw new InvalidDataException("La carpeta no contiene ProjectSettings de Unity.");
  var source=Path.GetFullPath(inheritedSource??projectRoot);
  var references=new List<string>();
  var sourceManifest=Path.Combine(source,EditorRelative,"import-manifest.json");
  if(!File.Exists(sourceManifest))sourceManifest=Path.Combine(source,LegacyEditorRelative,"import-manifest.json");
  if(File.Exists(sourceManifest))
  {
   var old=JsonSerializer.Deserialize<ImportManifest>(File.ReadAllText(sourceManifest),Options);
   if(old is null||old.schemaVersion is not (1 or 2))throw new InvalidDataException("Versión desconocida del manifiesto de archivos de Unity.");
   foreach(var entry in old.packages)references.Add(old.schemaVersion==1?Path.GetFullPath(Path.Combine(source,entry.path)):entry.path);
  }
  foreach(var relative in new[]{"UnityPackages",ArchiveRelative})
  {
   var folder=Path.Combine(source,relative);
   if(Directory.Exists(folder))foreach(var file in Directory.GetFiles(folder,"*.unitypackage").Order(StringComparer.OrdinalIgnoreCase))if(!references.Any(p=>string.Equals(Path.GetFileName(p),Path.GetFileName(file),StringComparison.OrdinalIgnoreCase)))references.Add(file);
  }
  // New selections supersede inherited links, while distinct originals may share a filename.
  var selectedNames=(attachments??[]).Select(p=>p.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
  references.RemoveAll(path=>selectedNames.Contains(Path.GetFileName(path)));
  references.AddRange((attachments??[]).Select(p=>p.FullPath));
  var entries=new List<ArchiveEntry>();
  foreach(var reference in references.Distinct(StringComparer.OrdinalIgnoreCase))
  {
   if(!Path.IsPathRooted(reference)||!reference.EndsWith(".unitypackage",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("La referencia debe ser una ruta absoluta a un archivo .unitypackage.");
   var path=Path.GetFullPath(reference);
   if(!File.Exists(path))throw new FileNotFoundException("El archivo original ya no está disponible. Volvé a enlazarlo desde el preset.",path);
   entries.Add(new(path,Hash(path)));
  }
  if(entries.Count==0)return 0;
  var editorDir=Path.Combine(projectRoot,EditorRelative);Directory.CreateDirectory(editorDir);
  using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("MochiSprout.UnityPackageAutoImporter")??throw new InvalidOperationException("No se encontró el importador de Unity incluido en la app.");
  using var reader=new StreamReader(resource);
  var importer=reader.ReadToEnd();
  AtomicWrite(Path.Combine(editorDir,"import-manifest.json"),JsonSerializer.Serialize(new ImportManifest(2,entries),Options));
  AtomicWrite(Path.Combine(editorDir,AsmdefName+".asmdef"),"{\"name\":\""+AsmdefName+"\",\"references\":[],\"includePlatforms\":[\"Editor\"],\"overrideReferences\":true,\"precompiledReferences\":[],\"autoReferenced\":false,\"noEngineReferences\":false}");
  AtomicWrite(Path.Combine(editorDir,"UnityPackageAutoImporter.cs"),importer);
  MigrateLegacy(projectRoot,resetState);
  if(resetState)File.Delete(Path.Combine(projectRoot,StateRelative));
  return entries.Count;
 }
 /// <summary>
 /// Quita el importador de VCC Preset Studio (para que no haya dos importando a la vez) y conserva su registro
 /// de importaciones bajo el nombre nuevo. No toca Archives~ ni ningún .unitypackage.
 /// </summary>
 private static void MigrateLegacy(string projectRoot,bool resetState)
 {
  var legacyState=Path.Combine(projectRoot,LegacyStateRelative);var state=Path.Combine(projectRoot,StateRelative);
  if(File.Exists(legacyState))
  {
   if(!resetState&&!File.Exists(state))File.Copy(legacyState,state);
   File.Delete(legacyState);
  }
  var legacyEditor=Path.Combine(projectRoot,LegacyEditorRelative);
  if(!Directory.Exists(legacyEditor))return;
  foreach(var name in LegacyEditorFiles){DeleteIfExists(Path.Combine(legacyEditor,name));DeleteIfExists(Path.Combine(legacyEditor,name+".meta"));}
  DeleteIfEmpty(legacyEditor);
  DeleteIfEmpty(Path.Combine(projectRoot,LegacyRootRelative));
 }
 private static void DeleteIfExists(string file){if(File.Exists(file))File.Delete(file);}
 private static void DeleteIfEmpty(string folder)
 {
  if(!Directory.Exists(folder)||Directory.EnumerateFileSystemEntries(folder).Any())return;
  Directory.Delete(folder);DeleteIfExists(folder.TrimEnd(Path.DirectorySeparatorChar,'/')+".meta");
 }
 private static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();}
 private static void AtomicWrite(string path,string value){var temp=path+".tmp";File.WriteAllText(temp,value,new UTF8Encoding(false));File.Move(temp,path,true);}
}
