using System;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;
public static class EncounterAssetBuilder {
 const string Folder="Assets/Astra/Resources/Galaxies/Zyl/Encounter";
 public static void Diagnose(){foreach(string name in new[]{"Mihuoer","Taideler"}){var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+".prefab"));var anim=go.GetComponentInChildren<Animation>();Debug.Log("DIAG root "+name+" child "+anim.transform.localPosition+" rot "+anim.transform.localEulerAngles+" scale "+anim.transform.localScale);foreach(AnimationState state in anim){foreach(var binding in AnimationUtility.GetCurveBindings(state.clip).Where(b=>b.path==""))Debug.Log("DIAG ROOT CURVE "+name+" "+binding.propertyName);anim.Play(state.name);state.time=0;anim.Sample();foreach(var renderer in go.GetComponentsInChildren<Renderer>())Debug.Log("DIAG SAMPLE "+name+" clip="+state.name+" "+renderer.bounds);Debug.Log("DIAG TRANSFORM "+anim.transform.localEulerAngles+" "+anim.transform.localScale);}UnityEngine.Object.DestroyImmediate(go);}}
 public static void Build(){Prepare("Mihuoer_SeatedWave","Mihuoer",true);PrepareTaideler();AssetDatabase.SaveAssets();CabinBuild.BuildDevelopment();}
 static void Prepare(string file,string prefab,bool actor){
 string path=Folder+"/"+file+".fbx";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(ModelImporter)AssetImporter.GetAtPath(path);
 importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;importer.importBlendShapes=true;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.optimizeGameObjects=false;
 var defaults=importer.defaultClipAnimations;if(defaults.Length==0)throw new Exception(file+" has no imported animation");
 var clips=actor?new[]{
  new ModelImporterClipAnimation{name="SeatedWave",takeName=defaults[0].takeName,firstFrame=0,lastFrame=96,loopTime=true,wrapMode=WrapMode.Loop},
  new ModelImporterClipAnimation{name="SeatedTalk",takeName=defaults[0].takeName,firstFrame=97,lastFrame=169,loopTime=true,wrapMode=WrapMode.Loop},
  new ModelImporterClipAnimation{name="Explain",takeName=defaults[0].takeName,firstFrame=170,lastFrame=242,loopTime=true,wrapMode=WrapMode.Loop},
  new ModelImporterClipAnimation{name="Think",takeName=defaults[0].takeName,firstFrame=243,lastFrame=339,loopTime=true,wrapMode=WrapMode.Loop},
  new ModelImporterClipAnimation{name="Concern",takeName=defaults[0].takeName,firstFrame=340,lastFrame=412,loopTime=true,wrapMode=WrapMode.Loop}
 }:new[]{new ModelImporterClipAnimation{name="Flight",takeName=defaults[0].takeName,firstFrame=1,lastFrame=96,loopTime=true,wrapMode=WrapMode.Loop}};
 importer.clipAnimations=clips;importer.SaveAndReimport();string textures=Folder+"/Textures";Directory.CreateDirectory(textures);importer.ExtractTextures(textures);AssetDatabase.Refresh();
 foreach(string png in Directory.GetFiles(textures,"*.png",SearchOption.AllDirectories)){string lower=Path.GetFileName(png).ToLowerInvariant();if(lower.Contains("normal")||lower.Contains("skin_nor")){var normal=AssetImporter.GetAtPath(png.Replace('\\','/')) as TextureImporter;if(normal!=null){normal.textureType=TextureImporterType.NormalMap;normal.sRGBTexture=false;normal.SaveAndReimport();}}}
 importer.SaveAndReimport();
 var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);var model=(GameObject)PrefabUtility.InstantiatePrefab(source);var go=new GameObject(prefab);model.transform.SetParent(go.transform,false);var animation=model.GetComponent<Animation>()??model.AddComponent<Animation>();animation.playAutomatically=false;animation.cullingType=AnimationCullingType.AlwaysAnimate;
 foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(x=>!x.name.StartsWith("__"))){animation.AddClip(clip,clip.name);Debug.Log("ENCOUNTER CLIP "+file+" "+clip.name+" length="+clip.length);}
  if(actor){
  var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/Mihuoer_Albedo.png");if(!texture)throw new Exception("Mihuoer albedo missing");
  string materialPath=Folder+"/MihuoerSurface.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
  if(!material){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,materialPath);}material.mainTexture=texture;material.color=Color.white;material.SetFloat("_Glossiness",.2f);
  foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterial=material;
 }
 foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))Debug.Log("ENCOUNTER BOUNDS "+prefab+" "+renderer.name+" "+renderer.bounds);
 PrefabUtility.SaveAsPrefabAsset(go,Folder+"/"+prefab+".prefab");UnityEngine.Object.DestroyImmediate(go);
 
 }
 static AnimationClip ImportCreature(string file,string clipName,int frames,bool loop){
  string path=Folder+"/"+file+".fbx";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(ModelImporter)AssetImporter.GetAtPath(path);
  importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;importer.importBlendShapes=true;importer.importCameras=false;importer.importLights=false;importer.optimizeGameObjects=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
  var defaults=importer.defaultClipAnimations;if(defaults.Length==0)throw new Exception(file+" missing animation");
  importer.clipAnimations=new[]{new ModelImporterClipAnimation{name=clipName,takeName=defaults[0].takeName,firstFrame=defaults[0].firstFrame,lastFrame=defaults[0].firstFrame+frames,loopTime=loop,wrapMode=loop?WrapMode.Loop:WrapMode.ClampForever}};importer.SaveAndReimport();
  var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c=>c.name==clipName);
  int jaws=AnimationUtility.GetCurveBindings(clip).Count(binding=>binding.propertyName.Contains("Jaw_Open"));if(jaws<5)throw new Exception(clipName+" jaw channels missing: "+jaws);
  Debug.Log("RECESSED CLIP "+clipName+" seconds="+clip.length+" jawChannels="+jaws);return clip;
 }
 static Material CreatureMaterial(string name,string colorTexture,Color color,float roughness,string normalTexture=null){
  const string textureFolder=Folder+"/Textures/TaidelerRecessed/";
  Texture2D albedo=null,normal=null;
  foreach(string file in new[]{colorTexture,normalTexture}.Where(x=>x!=null)){
   string path=textureFolder+file+".png";var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)throw new Exception("Missing texture "+path);
   bool isNormal=file==normalTexture;importer.textureType=isNormal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!isNormal;importer.alphaSource=TextureImporterAlphaSource.None;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
   if(isNormal)normal=AssetDatabase.LoadAssetAtPath<Texture2D>(path);else albedo=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  }
  string materialPath=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!material){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,materialPath);}material.color=color;material.mainTexture=albedo;material.SetFloat("_Metallic",0);material.SetFloat("_Glossiness",1-roughness);if(normal){material.SetTexture("_BumpMap",normal);material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",1);}EditorUtility.SetDirty(material);return material;
 }
 static void PrepareTaideler(){
  var flight=ImportCreature("Taideler_Encounter","Flight",192,true);var opening=ImportCreature("Taideler_OpenTransition","OpenTransition",144,false);var openFlight=ImportCreature("Taideler_OpenFlightLoop","OpenFlight",192,true);
  var body=CreatureMaterial("TaidelerBody","BodyAlbedo",Color.white,.5f,"BodyNormal");var mucosa=CreatureMaterial("TaidelerMucosa","MucosaAlbedo",Color.white,.38f,"MucosaNormal");var lip=CreatureMaterial("TaidelerLip","LipAlbedo",Color.white,.3f,"LipNormal");var inner=CreatureMaterial("TaidelerInner",null,new Color(.042f,.006f,.013f),.62f);var cheek=CreatureMaterial("TaidelerCheek",null,new Color(.42f,.36f,.44f),.65f);
  var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Taideler_Encounter.fbx"));var go=new GameObject("Taideler");model.transform.SetParent(go.transform,false);
  var animation=model.GetComponent<Animation>()??model.AddComponent<Animation>();animation.playAutomatically=false;animation.cullingType=AnimationCullingType.AlwaysAnimate;animation.AddClip(flight,"Flight");animation.AddClip(opening,"OpenTransition");animation.AddClip(openFlight,"OpenFlight");animation.clip=flight;
  foreach(var renderer in go.GetComponentsInChildren<Renderer>(true)){
   if(renderer.name.Contains("continuous"))renderer.sharedMaterials=new[]{mucosa};else if(renderer.name.Contains("rim"))renderer.sharedMaterials=new[]{lip};else if(renderer.name.Contains("Sealed"))renderer.sharedMaterials=new[]{cheek};else renderer.sharedMaterials=new[]{body,inner};
   Debug.Log("RECESSED MESH "+renderer.name+" "+renderer.bounds);
  }
  var bindings=AnimationUtility.GetCurveBindings(opening);float difference=0;
  foreach(var binding in bindings){var a=AnimationUtility.GetEditorCurve(opening,binding);var b=AnimationUtility.GetEditorCurve(openFlight,binding);if(a!=null&&b!=null)difference=Mathf.Max(difference,Mathf.Abs(a.Evaluate(opening.length)-b.Evaluate(0)));}
  Debug.Log("RECESSED TRANSITION ENDPOINT MAX DIFFERENCE "+difference);if(difference>.02f)throw new Exception("Creature transition endpoint mismatch: "+difference);
  PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Taideler.prefab");UnityEngine.Object.DestroyImmediate(go);
 }

}



