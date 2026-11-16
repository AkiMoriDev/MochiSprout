using System.IO;
using CommunityToolkit.Mvvm.Input;
using MochiSprout.Services.Vcc;
namespace MochiSprout.Controllers;
public partial class PresetController
{
 [RelayCommand] private async Task EnableTemplateImportsAsync(VccTemplateInfo? template)
 {
  if(template is null||!template.IsDeletable||IsBusy)return;
  IsBusy=true;
  try {var count=await Task.Run(()=>exporter.EnableTemplateImports(template));Status=count==0?"Esta plantilla no contiene archivos .unitypackage enlazados.":$"Autoimportación activada para {count} archivo(s) de «{template.DisplayName}». Los nuevos proyectos usarán el importador al abrir Unity.";}
  catch(Exception ex){Status=$"No se pudo activar la autoimportación: {ex.Message}";}
  finally{IsBusy=false;}
 }
 public async Task EnableProjectImportsAsync(string folder)
 {
  if(IsBusy)return;
  IsBusy=true;
  try {var count=await Task.Run(()=>new UnityPackageImportSetup().Prepare(folder));Status=count==0?"No se encontraron .unitypackage en este proyecto. Creá uno desde la plantilla reparada o seleccioná la carpeta de un proyecto que conserve UnityPackages.":$"Importador preparado para {count} archivo(s). Si Unity está abierto, esperá la recompilación; si no, abrí el proyecto.";}
  catch(Exception ex){Status=$"No se pudo preparar el proyecto: {ex.Message}";}
  finally{IsBusy=false;}
 }
}

