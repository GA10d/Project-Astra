using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace AstraCabin.Editor
{
 public static class CountdownResourcesCheck
 {
  [InitializeOnLoadMethod] static void OnLoad(){EditorApplication.delayCall+=CheckAfterImport;} static void CheckAfterImport(){if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=CheckAfterImport;return;}if(SessionState.GetBool("Zyl.CountdownResourcesCheck.2",false))return;SessionState.SetBool("Zyl.CountdownResourcesCheck.2",true);try{Run();}catch(Exception e){Debug.LogError(e.Message);}}
  [MenuItem("Astra/Zyl/校验米霍尔倒计时资源")]
  public static void Run()
  {
   string key="Galaxies/Zyl/Countdown/",root="Assets/Astra/Resources/"+key;
   var importer=AssetImporter.GetAtPath(root+"CountdownWoundMask.png") as TextureImporter; if(importer!=null&&importer.textureShape!=TextureImporterShape.Texture2D){importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;importer.SaveAndReimport();Debug.Log("已修复米霍尔破口遮罩：Cubemap → Texture2D，保留GUID。");}
   string result="";
   Check<Shader>(root,key,"SubdermalCountdown.shader",ref result);
   Check<Texture2D>(root,key,"CountdownWoundMask.png",ref result);
   Check<Mesh>(root,key,"MihuoerCountdownSkinMesh.asset",ref result);
   var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--countdown-report");if(i>=0&&i+1<args.Length)File.WriteAllText(args[i+1],result);
   if(result.Contains("FAIL"))throw new InvalidOperationException(result);
   Debug.Log("米霍尔倒计时资源校验通过：\n"+result);
  }
  static void Check<T>(string root,string key,string name,ref string result)where T:UnityEngine.Object
  {
   var imported=AssetDatabase.LoadAssetAtPath<T>(root+name);var loaded=Resources.Load<T>(key+Path.GetFileNameWithoutExtension(name));
   result+=(loaded?"PASS ":"FAIL ")+name+" | AssetDatabase="+(imported?imported.ToString():"null")+" | Resources="+(loaded?loaded.ToString():"null")+"\n";
  }
 }
}


