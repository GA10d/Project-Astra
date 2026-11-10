using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AstraCabin;
public static class ZylStomachSceneBuilder {
 const string Root="Assets/Astra/Galaxies/Zyl/Stomach/Natural";
 const string ScenePath="Assets/Astra/Scenes/Zyl/StomachRefuge.unity";
 static Material NaturalMaterial(string name,float gloss,float bump){
  string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(name=="Fluid"&&m&&m.shader.name=="Astra/Zyl/DigestiveWater")return m;
  if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.shader=Shader.Find(name=="Mucosa"?"Astra/Zyl/BreathingMucosa":"Standard (Specular setup)");if(name=="Mucosa"){m.SetFloat("_BreathAmplitude",.6f);m.SetFloat("_BreathPeriod",4.5f);m.SetFloat("_BreathPreviewTime",-1);}m.color=Color.white;m.SetColor("_SpecColor",new Color(.055f,.055f,.055f));m.SetFloat("_Metallic",name=="Fluid"?.08f:0);m.SetFloat("_Glossiness",gloss);
  foreach(string suffix in new[]{"_Albedo","_Normal"}){string p=Root+"/"+name+suffix+".png";var ti=(TextureImporter)AssetImporter.GetAtPath(p);ti.maxTextureSize=2048;ti.wrapMode=TextureWrapMode.Repeat;ti.filterMode=FilterMode.Trilinear;ti.anisoLevel=4;ti.textureType=suffix=="_Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;ti.sRGBTexture=suffix!="_Normal";ti.SaveAndReimport();m.SetTexture(suffix=="_Normal"?"_BumpMap":"_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(p));}
  m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",bump);EditorUtility.SetDirty(m);return m;
 }
 static void Lamp(string name,Vector3 p,Color color,float intensity,float range){var o=new GameObject(name);o.transform.position=p;var l=o.AddComponent<Light>();l.color=color;l.intensity=intensity;l.range=range;l.renderMode=LightRenderMode.ForcePixel;if(name.Contains("courtyard")){l.shadows=LightShadows.Soft;l.shadowStrength=.45f;l.shadowBias=.04f;}}
 [MenuItem("Astra/Zyl/Rebuild natural stomach scene")]
 public static void Build(){
  AssetDatabase.Refresh();var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
  foreach(var o in scene.GetRootGameObjects())if(!o.name.StartsWith("05 |")&&!o.name.StartsWith("06 |")&&!o.GetComponent<Camera>()&&!o.GetComponent<AstraCabin.Galaxies.Zyl.StomachEnvironmentSettings>()&&!o.GetComponent<AstraCabin.Galaxies.Zyl.StomachMihuoerAnchor>()&&!o.GetComponent<AstraCabin.Galaxies.Zyl.StomachMihuoerShipAnchor>())Object.DestroyImmediate(o);
  var materials=new Dictionary<string,Material>();foreach(string n in new[]{"Mucosa","Bark","Stone","Humus","Leaf","Fluid"})materials[n]=NaturalMaterial(n,n=="Fluid"?.68f:n=="Mucosa"?.52f:.22f,n=="Mucosa"?.15f:n=="Fluid"?.12f:n=="Bark"?.4f:.25f);
  string fbx=Root+"/Stomach_Natural.fbx";var imp=(ModelImporter)AssetImporter.GetAtPath(fbx);imp.importCameras=false;imp.importLights=false;imp.importAnimation=false;imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.isReadable=false;imp.SaveAndReimport();
  var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));model.name="Natural stomach | Blender authored | +Z depth";
  foreach(var renderer in model.GetComponentsInChildren<Renderer>()){string key=renderer.name.Split('_')[0];renderer.sharedMaterial=materials[key];renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;}
  // Match the original authored cavity's measured width exactly at +30%.
  var oldVault=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Astra/Galaxies/Zyl/Stomach/Mesh0.asset");var walls=model.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Mucosa_Cavity")).ToArray();var wb=walls[0].bounds;foreach(var wall in walls)wb.Encapsulate(wall.bounds);if(oldVault)model.transform.localScale=new Vector3(oldVault.bounds.size.x*1.3f*1.3f/wb.size.x,1,1);
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.19f,.16f,.17f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.105f,.052f,.059f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.005f;RenderSettings.skybox=null;
  Lamp("Soft warm light | courtyard",new Vector3(-5,15,-1),new Color(1,.78f,.55f),1.8f,52);
  Lamp("Soft tissue rim | courtyard",new Vector3(15,12,16),new Color(.84f,.43f,.48f),1.4f,65);
  Lamp("Neutral natural fill",new Vector3(-12,5,23),new Color(.69f,.71f,.65f),1.5f,38);
  for(int z=-240;z<370;z+=45){float side=((z+240)/45)%2==0?-1:1;Lamp("Soft warm depth pool "+z,new Vector3(side*12,23,z),new Color(.86f,.65f,.48f),1.25f,62);Lamp("Soft mauve tissue bounce "+z,new Vector3(-side*24,18,z+15),new Color(.58f,.38f,.49f),.65f,68);}
  var cam=Object.FindObjectOfType<Camera>();cam.transform.position=new Vector3(0,15,-27);cam.transform.LookAt(new Vector3(0,7,30));cam.fieldOfView=60;cam.farClipPlane=1800;cam.backgroundColor=RenderSettings.fogColor;cam.clearFlags=CameraClearFlags.SolidColor;
  var presentation=cam.GetComponent<CabinPresentation>();if(presentation)presentation.sceneLook=CabinSceneLook.Pixel;
  EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
  var output=Path.GetFullPath("../"); // captures are written beside the temporary project instead
  output=Path.GetFullPath("../");
  Capture(cam,Path.Combine(output,"natural-overview.png"));
  cam.transform.position=new Vector3(18,17,-23);cam.transform.LookAt(new Vector3(0,8,45));Capture(cam,Path.Combine(output,"natural-ring.png"));
  cam.transform.position=new Vector3(0,3,3);cam.transform.LookAt(new Vector3(0,4,45));Capture(cam,Path.Combine(output,"natural-courtyard.png"));
  cam.transform.position=new Vector3(0,17,48);cam.transform.LookAt(new Vector3(0,12,160));Capture(cam,Path.Combine(output,"natural-depth.png"));
  cam.transform.position=new Vector3(0,25,116);cam.transform.LookAt(new Vector3(0,20,235));Capture(cam,Path.Combine(output,"natural-mountains.png"));
  cam.transform.position=new Vector3(0,5,-210);cam.transform.LookAt(new Vector3(0,6,-275));Capture(cam,Path.Combine(output,"natural-rear.png"));
  cam.transform.position=new Vector3(0,450,55);cam.transform.rotation=Quaternion.Euler(90,0,0);cam.orthographic=true;cam.orthographicSize=360;cam.aspect=16f/9;bool fog=RenderSettings.fog;RenderSettings.fog=false;Capture(cam,Path.Combine(output,"natural-layout.png"));RenderSettings.fog=fog;
  cam.orthographic=false;cam.transform.position=new Vector3(27,14,25);cam.transform.LookAt(new Vector3(43,19,80));
  var flesh=materials["Mucosa"];if(ShaderUtil.ShaderHasError(flesh.shader))throw new System.Exception("Breathing shader compilation failed");
  flesh.SetFloat("_BreathPreviewTime",0);Capture(cam,Path.Combine(output,"breath-phase0.png"));
  flesh.SetFloat("_BreathPreviewTime",1.125f);Capture(cam,Path.Combine(output,"breath-phase1.png"));
  flesh.SetFloat("_BreathPreviewTime",-1);EditorUtility.SetDirty(flesh);AssetDatabase.SaveAssets();
  var rs=model.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
  int missing=rs.Count(r=>!r.sharedMaterial||!r.sharedMaterial.shader||!r.sharedMaterial.shader.isSupported);int artificial=scene.GetRootGameObjects().Count(o=>o.name.StartsWith("04 |")||o.name.StartsWith("03 |")||o.name.StartsWith("01 |"));
  var missingScripts=Object.FindObjectsOfType<Transform>().Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
  File.WriteAllText(Path.Combine(output,"natural-validation.json"),"{\"renderers\":"+rs.Length+",\"missingMaterials\":"+missing+",\"missingScripts\":"+missingScripts+",\"oldEnvironmentRoots\":"+artificial+",\"boundsMin\":\""+b.min+"\",\"boundsMax\":\""+b.max+"\",\"suitPreserved\":"+scene.GetRootGameObjects().Any(o=>o.name.StartsWith("05 |")).ToString().ToLower()+"}");
  if(missing>0||missingScripts>0||artificial>0)throw new System.Exception("Natural scene validation failed");
 }
 static void Capture(Camera c,string path){var rt=new RenderTexture(1920,1080,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());c.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);}
}




