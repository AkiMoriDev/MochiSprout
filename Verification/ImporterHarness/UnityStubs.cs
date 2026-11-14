using System.Text.Json;
namespace UnityEngine
{
 public static class Application { public static string dataPath; }
 public static class JsonUtility
 {
  private static readonly JsonSerializerOptions Options=new(){IncludeFields=true,WriteIndented=true};
  public static T FromJson<T>(string json)=>JsonSerializer.Deserialize<T>(json,Options);
  public static string ToJson(object value,bool pretty)=>JsonSerializer.Serialize(value,value.GetType(),Options);
 }
 public static class Debug
 {
  public static List<string> Errors=new();public static List<string> Warnings=new();
  public static void Log(object msg)=>Console.WriteLine(msg);
  public static void LogWarning(object msg){Warnings.Add(msg.ToString());Console.WriteLine("WARN: "+msg);}
  public static void LogError(object msg){Errors.Add(msg.ToString());Console.WriteLine("ERROR: "+msg);}
 }
}
namespace UnityEditor
{
 [AttributeUsage(AttributeTargets.Class)] public sealed class InitializeOnLoadAttribute:Attribute{}
 [AttributeUsage(AttributeTargets.Method)] public sealed class MenuItem:Attribute{public MenuItem(string text){}}
 public static class EditorApplication
 {
  public static event Action update;public static event Action quitting;
  public static bool isCompiling,isUpdating,isPlayingOrWillChangePlaymode;
  public static double timeSinceStartup;public static int Locks;
  public static void LockReloadAssemblies()=>Locks++;
  public static void UnlockReloadAssemblies()=>Locks--;
  public static void Tick(){timeSinceStartup+=3;update?.Invoke();}
  public static void Quit()=>quitting?.Invoke();
 }
 public static class AssetDatabase
 {
  public static event Action<string> importPackageCompleted,importPackageCancelled;
  public static event Action<string,string> importPackageFailed;
  public static List<string> Calls=new(); public static Action<string> ImportAction;
  public static void ImportPackage(string path,bool interactive){if(interactive)throw new Exception("Must import non-interactively");Calls.Add(Path.GetFileName(path));ImportAction?.Invoke(path);}
  public static void Complete(string name)=>importPackageCompleted?.Invoke(name);
  public static void Fail(string name)=>importPackageFailed?.Invoke(name,"fixture failure");
  public static void Cancel(string name)=>importPackageCancelled?.Invoke(name);
 }
 /// <summary>Ventanas simuladas: cada llamada consume la próxima respuesta preparada por la prueba.</summary>
 public static class EditorUtility
 {
  public static List<string> Shown=new();
  public static Queue<int> ComplexAnswers=new();public static Queue<bool> Answers=new();public static Queue<string> Files=new();
  public static bool DisplayDialog(string title,string message,string ok){Shown.Add(title);return true;}
  public static bool DisplayDialog(string title,string message,string ok,string cancel){Shown.Add(title);return Answers.Count>0&&Answers.Dequeue();}
  public static int DisplayDialogComplex(string title,string message,string ok,string cancel,string alt){Shown.Add(title);return ComplexAnswers.Count>0?ComplexAnswers.Dequeue():1;}
  public static string OpenFilePanel(string title,string directory,string extension){Shown.Add(title);return Files.Count>0?Files.Dequeue():"";}
 }
}
