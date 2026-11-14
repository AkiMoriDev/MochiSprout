using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using UnityEditor;
using MochiSprout.Generated;
class Program
{
 static int checks;
 static string root;
 static string stateFile=>Path.Combine(root,"ProjectSettings/MochiSproutImportState.json");
 static string manifestFile=>Path.Combine(root,"Assets/MochiSprout/Editor/import-manifest.json");
 static readonly string[] Scenarios={"success","failure","cancel","interrupted","tampered","busy","missing","moved","timeout","corrupt"};
 static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
 static void Ticks(int n){for(var i=0;i<n;i++)EditorApplication.Tick();}
 static void Main(string[] args)
 {
  if(args.Length==0)
  {
   foreach(var test in Scenarios)
   {
    var start=new ProcessStartInfo(Environment.ProcessPath){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(test);
    using var process=Process.Start(start);var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(30000)){process.Kill();throw new Exception("Harness timeout");}Console.Write(stdout.GetAwaiter().GetResult());Console.Write(stderr.GetAwaiter().GetResult());if(process.ExitCode!=0)throw new Exception("Failed importer case: "+test);
   }
   Console.WriteLine($"All {Scenarios.Length} importer runtime scenarios passed.");return;
  }
  root=Path.Combine(AppContext.BaseDirectory,"fixtures",Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(Path.Combine(root,"Assets/MochiSprout/Editor"));Directory.CreateDirectory(Path.Combine(root,"Assets/VCCPresetStudio/Archives~"));Directory.CreateDirectory(Path.Combine(root,"ProjectSettings"));
  UnityEngine.Application.dataPath=Path.Combine(root,"Assets");
  var a=Archive("First.unitypackage","first");var b=Archive("Second.unitypackage","second");
  File.WriteAllText(manifestFile,JsonSerializer.Serialize(new{schemaVersion=2,packages=new[]{a,b}}));
  AssetDatabase.ImportAction=p=>AssetDatabase.Complete(Path.GetFileNameWithoutExtension(p));
  if(args[0]=="failure")AssetDatabase.ImportAction=p=>AssetDatabase.Fail(Path.GetFileName(p));
  if(args[0]=="cancel")AssetDatabase.ImportAction=p=>AssetDatabase.Cancel(Path.GetFileName(p));
  if(args[0]=="timeout")AssetDatabase.ImportAction=null;
  if(args[0]=="interrupted")File.WriteAllText(stateFile,JsonSerializer.Serialize(new{records=new[]{new{key=a.sha256,status="importing",error="",attempts=0}}}));
  if(args[0]=="corrupt")File.WriteAllText(stateFile,"{esto no es json");
  if(args[0]=="tampered")File.AppendAllText(a.path,"changed");
  string moved=null;
  if(args[0]=="missing"){moved=Path.Combine(root+"-elsewhere","First.unitypackage");Directory.CreateDirectory(Path.GetDirectoryName(moved));File.Move(a.path,moved);}
  if(args[0]=="moved"){moved=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(a.path)),"First.unitypackage");File.Move(a.path,moved);}
  RuntimeHelpers.RunClassConstructor(typeof(UnityPackageAutoImporter).TypeHandle);
  switch(args[0])
  {
   case "busy":
    EditorApplication.isCompiling=true;EditorApplication.Tick();Check(AssetDatabase.Calls.Count==0,"Waits while Unity compiles");
    EditorApplication.isCompiling=false;EditorApplication.isUpdating=true;EditorApplication.Tick();Check(AssetDatabase.Calls.Count==0,"Waits while Unity updates assets");EditorApplication.isUpdating=false;
    AssetDatabase.ImportAction=null;EditorApplication.Tick();AssetDatabase.Complete("Unrelated");EditorApplication.Tick();
    Check(AssetDatabase.Calls.Count==1&&File.ReadAllText(stateFile).Contains("importing"),"Unrelated import callback cannot mark our archive complete");
    AssetDatabase.Complete("First");AssetDatabase.ImportAction=p=>AssetDatabase.Complete(Path.GetFileNameWithoutExtension(p));Ticks(2);Check(EditorApplication.Locks==0,"Reload locks are released on completion");
    break;
   case "success":
    Ticks(3);
    Check(AssetDatabase.Calls.SequenceEqual(new[]{"First.unitypackage","Second.unitypackage"}),"Archives import sequentially in manifest order");
    Check(ReadStatuses().All(s=>s=="completed"),"Success is recorded from completed callbacks");
    UnityPackageAutoImporter.RetryPending();EditorApplication.Tick();Check(AssetDatabase.Calls.Count==2,"Completed content is not reimported after reloading persisted state");
    Check(EditorApplication.Locks==0&&EditorUtility.Shown.Count==0,"Reload locks balanced and no dialogs on success");
    break;
   case "failure": case "cancel": case "timeout":
    Ticks(args[0]=="timeout"?90:8);
    Check(!ReadStatuses().Contains("completed"),"Failed, cancelled or unconfirmed archive is not marked complete");
    Check(AssetDatabase.Calls.Count==2,"Retries automatically exactly once ("+args[0]+")");
    Check(EditorUtility.Shown.Count(t=>t.Contains("No se pudo importar"))==1,"Shows one dialog with options after the automatic retry");
    var count=AssetDatabase.Calls.Count;Ticks(10);Check(AssetDatabase.Calls.Count==count,"No endless retry loop after giving up");
    Check(EditorApplication.Locks==0,"Reload locks released after failure");
    AssetDatabase.ImportAction=p=>AssetDatabase.Complete(Path.GetFileNameWithoutExtension(p));UnityPackageAutoImporter.RetryPending();Ticks(4);
    Check(ReadStatuses().Count(s=>s=="completed")==2,"Menu retry resumes pending archives and finishes");
    break;
   case "interrupted":
    Ticks(4);
    Check(ReadStatuses().Count(s=>s=="completed")==2&&UnityEngine.Debug.Warnings.Any(w=>w.Contains("interrumpida")),"Interrupted import resumes automatically");
    Check(EditorUtility.Shown.Count==0&&EditorApplication.Locks==0,"Interrupted resume needs no dialog and releases locks");
    break;
   case "corrupt":
    Ticks(4);
    Check(ReadStatuses().Count(s=>s=="completed")==2,"Damaged import record is set aside and import continues");
    Check(Directory.GetFiles(Path.Combine(root,"ProjectSettings"),"*.danado-*").Length==1,"Damaged record is kept as a backup");
    break;
   case "tampered":
    Ticks(3);
    Check(AssetDatabase.Calls.Count==0&&EditorUtility.Shown.Count(t=>t.Contains("cambió"))==1,"Changed archive is not imported silently and the user is asked");
    UnityPackageAutoImporter.RetryPending();EditorUtility.ComplexAnswers.Enqueue(0);Ticks(5);
    Check(ReadStatuses().Count(s=>s=="completed")==2&&File.ReadAllText(manifestFile).Contains(Hash(a.path)),"«Importar igual» updates the manifest and imports the new version");
    break;
   case "missing":
    EditorUtility.ComplexAnswers.Enqueue(0);EditorUtility.Files.Enqueue(moved);Ticks(6);
    Check(EditorUtility.Shown.Any(t=>t.Contains("Falta un archivo")),"Missing archive opens a dialog to locate it");
    Check(ReadStatuses().Count(s=>s=="completed")==2&&File.ReadAllText(manifestFile).Contains("elsewhere"),"Located archive is relinked in the manifest and imported");
    break;
   case "moved":
    Ticks(4);
    Check(EditorUtility.Shown.Count==0&&ReadStatuses().Count(s=>s=="completed")==2,"Archive moved to a nearby folder is found and relinked automatically");
    break;
  }
  Console.WriteLine($"{args[0]}: {checks} checks passed.");
 }

 record ArchiveInfo(string path,string sha256);
 static string Hash(string file)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
 static ArchiveInfo Archive(string name,string data){var folder=Path.Combine(root+"-originals","packs");Directory.CreateDirectory(folder);var full=Path.Combine(folder,name);File.WriteAllText(full,data);return new(full,Hash(full));}
 static List<string> ReadStatuses(){if(!File.Exists(stateFile))return new();using var doc=JsonDocument.Parse(File.ReadAllText(stateFile));return doc.RootElement.GetProperty("records").EnumerateArray().Select(e=>e.GetProperty("status").GetString()).ToList();}
}
