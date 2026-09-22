using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using AstraCabin;

public static class CabinBuild
{
    const string Root="Assets/Astra";
    static Dictionary<string,Material> materials;
    static GameObject cabin;

    [MenuItem("Astra/Rebuild cabin scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory(Root+"/Scenes"); Directory.CreateDirectory(Root+"/Materials");
        AssetDatabase.Refresh();
        string modelPath=Root+"/Models/Astra_Cabin.fbx";
        var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if(importer==null) throw new Exception("Missing Blender FBX: "+modelPath);
        importer.importCameras=false; importer.importLights=false; importer.importAnimation=false;
        importer.isReadable=true; importer.preserveHierarchy=true;
        importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        ConfigureSettings(); CreateMaterials();
        cabin=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        cabin.name="Astra Cabin | Blender source";
        // Preserve FBX handedness and readable lettering. Clockwise order is A,D,C,B.
        foreach(var r in cabin.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterials=r.sharedMaterials.Select(m=>ResolveMaterial(m==null?"DarkSteel":m.name)).ToArray();
            r.receiveShadows=true; r.shadowCastingMode=ShadowCastingMode.On;
            if(r.GetComponent<MeshFilter>()!=null && !IsControl(r.transform) && !r.name.StartsWith("GAUGE_")) r.gameObject.AddComponent<MeshCollider>();
        }
        var cameraGo=new GameObject("Player | fixed central viewpoint"); cameraGo.tag="MainCamera";
        cameraGo.transform.position=new Vector3(0,1.28f,0);
        var cam=cameraGo.AddComponent<Camera>(); cam.fieldOfView=74; cam.nearClipPlane=.035f; cam.farClipPlane=150;
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.002f,.004f,.009f);
        cam.allowHDR=true; cam.allowMSAA=true; cameraGo.AddComponent<AudioListener>();
        var grade=cameraGo.AddComponent<CabinPresentation>(); grade.grade=materials["Grade"];
        grade.blurShader=Shader.Find("Astra/ComputerBlur");
        var controls=new GameObject("Cabin systems | editable interactions");
        controls.AddComponent<CabinAudio>(); var systems=controls.AddComponent<CabinSystems>();
        var controller=controls.AddComponent<CabinController>(); controller.viewCamera=cam;
        var computer=controls.AddComponent<CabinComputer>(); computer.controller=controller; computer.systems=systems; computer.presentation=grade;
        controller.wallNames=new[]{"A / 指令终端","D / 观察与交换","C / 生活物资","B / 制造与工具"};
        controls.AddComponent<CabinVerification>();
        controls.AddComponent<CabinScreenshot>();
        var lamps=new List<Light>(); var emergency=new List<Light>();
        for(int wall=0;wall<4;wall++)
        {
            var rotation=Quaternion.Euler(0,wall*90,0);
            Vector3 p=rotation*new Vector3(0,2.23f,1.23f);
            var lamp=MakeLight("L"+wall+" | tungsten cage lamp",p,new Color(1,.95f,.86f),1.3f,3.4f);
            lamp.shadows=LightShadows.Soft; lamp.shadowBias=.018f; lamp.shadowNormalBias=.018f;
            lamps.Add(lamp);
            var spot=MakeLight("L"+wall+" | downlight",p,new Color(1,.96f,.88f),1.05f,3.2f);
            spot.type=LightType.Spot; spot.spotAngle=118; spot.shadows=LightShadows.Soft;
            spot.shadowBias=.025f; spot.shadowNormalBias=.025f;
            spot.transform.rotation=Quaternion.LookRotation(rotation*new Vector3(0,-1,.22f)); lamps.Add(spot);
            emergency.Add(MakeLight("L"+wall+" | emergency floor strip",rotation*new Vector3(0,.18f,1.25f),new Color(1,.09f,.025f),.32f,2.2f));
        }
        lamps.Add(MakeLight("Subtle cool cabin bounce",new Vector3(0,1.45f,0),new Color(.64f,.79f,.91f),.85f,4));
        emergency.Add(MakeLight("Emergency | low red cabin bounce",new Vector3(0,1.6f,0),new Color(.80f,.16f,.075f),.65f,4));
        systems.cabinLights=lamps.ToArray(); systems.emergencyLights=emergency.ToArray();
        systems.cabinLampRenderers=cabin.GetComponentsInChildren<Renderer>().Where(r=>r.sharedMaterials.Any(m=>m.name=="LampWarm")).ToArray();
        systems.screenRenderers=cabin.GetComponentsInChildren<Renderer>().Where(r=>r.sharedMaterials.Any(m=>m.name=="Screen")).ToArray();
        systems.printerHead=Find("PRINT_Head");
        systems.printerMotionAxis=Vector3.back;
        systems.printerLight=MakeLight("Fabricator | internal work light",new Vector3(-1.04f,.86f,-.03f),new Color(1,.76f,.40f),1.2f,1.1f);
        Control("ACT_LightSwitch","舱内照明 / CABIN LIGHTS",InteractionKind.Switch,Vector3.right,32,true,systems.SetLights);
        Control("ACT_PowerLever","主电源 / MAIN BUS",InteractionKind.Lever,Vector3.right,62,true,systems.SetMainPower);
        Control("ACT_PrintButton","打印机 / FABRICATOR",InteractionKind.Button,Vector3.left,.025f,false,systems.SetPrinter);
        Control("ACT_RadioKnob","短波收音机 / SHORTWAVE",InteractionKind.Valve,Vector3.forward,65,false,systems.SetRadio);
        Control("ACT_Hatch","交换舱门 / TRANSFER HATCH",InteractionKind.Hatch,Vector3.forward,85,false,null);
        Control("ACT_Valve","循环阀门 / AIR RECIRCULATION",InteractionKind.Valve,Vector3.right,110,true,systems.SetAir);
        Control("ACT_Cabinet","储物柜 / PERSONAL LOCKER",InteractionKind.Hatch,Vector3.up,-80,false,null);
        Control("SCREEN_Main","船载电脑 / ASTRA OS",InteractionKind.Button,Vector3.forward,0,false,computer.SetOpen);
        Find("SCREEN_Main").GetComponent<CabinInteractable>().toggleState=false;
        CreateWindowObservation(controller);
        CreateGaugeMotion();
        CreateSpace();
        var director=controls.AddComponent<SetpieceDirector>();
        director.controller=controller; director.systems=systems; director.computer=computer;
        director.planet=UnityEngine.Object.FindObjectOfType<DistantPlanet>();
        director.stars=UnityEngine.Object.FindObjectOfType<LoopingStarfield>();
        director.retainedMaterials=new[]{materials["SetpieceFracture"], materials["SetpieceGlow"], materials["SetpieceUnlit"], materials["SetpieceSprite"], materials["SetpieceCanopy"]};
        controls.AddComponent<SetpieceVerification>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"/Scenes/Cabin04.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/Cabin04.unity",true)};
        AssetDatabase.SaveAssets();
        WriteInventory(); Debug.Log("ASTRA_SCENE_READY");
    }

    static bool IsControl(Transform t) { while(t!=null) { if(t.name.StartsWith("ACT_") || t.name=="SCREEN_Main") return true; t=t.parent; } return false; }
    static Transform Find(string name) { return cabin.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==name); }
    static void CreateWindowObservation(CabinController controller)
    {
        var target=new GameObject("OBS_Window"); target.transform.SetParent(cabin.transform,false);
        target.transform.position=new Vector3(1.307f,1.65f,0);
        var collider=target.AddComponent<BoxCollider>(); collider.size=new Vector3(.025f,.85f,1.8f);
        var item=target.AddComponent<CabinInteractable>(); item.kind=InteractionKind.Button;
        item.title="观察窗 / OBSERVATION PORT"; item.description="凑近观察窗外";
        item.movingPart=target.transform; item.travel=0; item.toggleState=false;
        item.outlineShader=Shader.Find("Astra/Outline");
        // The existing pressure-window geometry remains untouched. This disabled
        // thin rim supplies only an independent yellow hover outline; no fake glass
        // or image obscures the actual moving 3D exterior.
        var rim=new GameObject("Window | hover rim only"); rim.transform.SetParent(target.transform,false);
        var mesh=MakeWindowRim(); string path=Root+"/Models/ObservationRim.asset";
        var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old) { EditorUtility.CopySerialized(mesh,old); UnityEngine.Object.DestroyImmediate(mesh); mesh=old; }
        else AssetDatabase.CreateAsset(mesh,path);
        rim.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=rim.AddComponent<MeshRenderer>(); renderer.sharedMaterial=materials["Brass"];
        renderer.enabled=false; renderer.shadowCastingMode=ShadowCastingMode.Off;
        item.highlightRenderers=new Renderer[]{renderer};
        UnityEventTools.AddPersistentListener(item.onChanged,controller.SetWindowView);
    }
    static Mesh MakeWindowRim()
    {
        const int steps=9, count=steps*4;
        var vertices=new List<Vector3>(); var indices=new List<int>();
        for(int depth=0;depth<2;depth++)
            for(int inner=0;inner<2;inner++)
                for(int corner=0;corner<4;corner++)
                    for(int segment=0;segment<steps;segment++)
                    {
                        float radius=.115f-inner*.018f;
                        float angle=(corner*90f+segment*90f/(steps-1))*Mathf.Deg2Rad;
                        float cx=(corner==0||corner==3?1:-1)*(.935f-.115f);
                        float cy=(corner<2?1:-1)*(.46f-.115f);
                        vertices.Add(new Vector3(depth==0?-.009f:.009f,cy+Mathf.Sin(angle)*radius,cx+Mathf.Cos(angle)*radius));
                    }
        for(int i=0;i<count;i++)
        {
            int next=(i+1)%count;
            AddQuad(indices,i,next,count+next,count+i);
            AddQuad(indices,2*count+i,3*count+i,3*count+next,2*count+next);
            AddQuad(indices,i,2*count+i,2*count+next,next);
            AddQuad(indices,count+i,count+next,3*count+next,3*count+i);
        }
        var mesh=new Mesh{name="Observation rim (editable generated mesh)"};
        mesh.SetVertices(vertices); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    static void AddQuad(List<int> triangles,int a,int b,int c,int d)
    { triangles.AddRange(new[]{a,b,c,a,c,d}); }

    static void CreateGaugeMotion()
    {
        // Independent Blender meshes retain their authored pivot at the needle axle.
        int seed=1943;
        foreach(var needle in cabin.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("GAUGE_")))
        {
            var motion=needle.gameObject.AddComponent<CabinGaugeMotion>();
            Vector3 normal=needle.name.StartsWith("GAUGE_A_")?Vector3.forward:needle.name.StartsWith("GAUGE_B_")?Vector3.left:Vector3.right;
            motion.worldAxis=normal; motion.phaseSeed=seed++;
        }
    }
    static void Control(string name,string title,InteractionKind kind,Vector3 axis,float travel,bool on,UnityAction<bool> changed)
    {
        var t=Find(name); if(t==null) throw new Exception("Missing interaction pivot: "+name);
        var item=t.gameObject.AddComponent<CabinInteractable>(); item.title=title; item.kind=kind;
        item.movingPart=t; item.startsOn=on;
        if(kind==InteractionKind.Button)
        {
            var delta=t.parent ? t.parent.InverseTransformVector(axis*travel) : axis*travel;
            item.motionAxis=delta.normalized; item.travel=delta.magnitude;
        }
        else
        {
            // Rotation axes are axial vectors: a reflected FBX root reverses handedness.
            float handedness=Mathf.Sign(t.localToWorldMatrix.determinant);
            item.motionAxis=(t.InverseTransformVector(axis)*handedness).normalized; item.travel=travel;
        }
        item.outlineShader=Shader.Find("Astra/Outline");
        if(name=="ACT_PrintButton") { item.overrideSound=true; item.sound=CabinSound.Printer; }
        if(name=="ACT_RadioKnob") { item.overrideSound=true; item.sound=CabinSound.Radio; }
        item.duration=kind==InteractionKind.Hatch?.65f:.24f;
        Bounds b=new Bounds(Vector3.zero,Vector3.zero); bool first=true;
        foreach(var mf in t.GetComponentsInChildren<MeshFilter>())
        {
            var meshBounds=mf.sharedMesh.bounds;
            for(int i=0;i<8;i++)
            {
                var p=meshBounds.center+Vector3.Scale(meshBounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                p=t.InverseTransformPoint(mf.transform.TransformPoint(p));
                if(first) { b=new Bounds(p,Vector3.zero); first=false; } else b.Encapsulate(p);
            }
        }
        var collider=t.gameObject.AddComponent<BoxCollider>(); collider.center=b.center; collider.size=b.size+Vector3.one*.025f;
        if(changed!=null) UnityEventTools.AddPersistentListener(item.onChanged,changed);
    }

    static void ConfigureSettings()
    {
        PlayerSettings.companyName="Project Astra"; PlayerSettings.productName="ASTRA - Cabin 04";
        PlayerSettings.defaultScreenWidth=1600; PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed; PlayerSettings.resizableWindow=true;
        PlayerSettings.runInBackground=true; PlayerSettings.colorSpace=ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone,ApiCompatibilityLevel.NET_Standard_2_0);
        QualitySettings.SetQualityLevel(QualitySettings.names.Length-1); QualitySettings.antiAliasing=4;
        QualitySettings.pixelLightCount=8; QualitySettings.shadowDistance=8; QualitySettings.shadows=ShadowQuality.All;
        QualitySettings.shadowResolution=ShadowResolution.High; QualitySettings.vSyncCount=1;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.21f,.27f,.30f);
        RenderSettings.ambientEquatorColor=new Color(.13f,.16f,.15f);
        RenderSettings.ambientGroundColor=new Color(.07f,.06f,.055f);
        RenderSettings.fog=false; RenderSettings.skybox=null;
    }

    static void CreateMaterials()
    {
        materials=new Dictionary<string,Material>();
        PrepareTextureImports();
        Metal("HullPaint",new Color(.29f,.33f,.30f),.12f,.28f,.16f);
        Metal("DarkSteel",new Color(.115f,.125f,.12f),.64f,.32f,.22f);
        Metal("Brass",new Color(.40f,.29f,.14f),.76f,.43f,.26f);
        Metal("RedPaint",new Color(.31f,.065f,.042f),.16f,.30f,.19f);
        Metal("Rubber",new Color(.018f,.022f,.02f),.02f,.12f,.03f);
        Metal("Canvas",new Color(.24f,.205f,.13f),0,.1f,.1f);
        Metal("Paper",new Color(.64f,.57f,.39f),0,.12f,.02f);
        Emissive("LampWarm",new Color(1,.63f,.24f),2.0f);
        Emissive("LampRed",new Color(1,.055f,.01f),1.5f);
        Emissive("LampGreen",new Color(.23f,1,.18f),1.5f);
        var screen=new Material(Shader.Find("Astra/CRT")); Store("Screen",screen);
        var glass=new Material(Shader.Find("Standard")); glass.color=new Color(.17f,.22f,.23f,.045f);
        glass.SetFloat("_Mode",3); glass.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha); glass.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
        glass.SetInt("_ZWrite",0); glass.EnableKeyword("_ALPHAPREMULTIPLY_ON"); glass.renderQueue=3000; glass.SetFloat("_Glossiness",.9f); Store("Glass",glass);
        Store("Grade",new Material(Shader.Find("Astra/CabinGrade")));
        Store("Planet",new Material(Shader.Find("Astra/DistantPlanet")));
        Store("SetpieceFracture",new Material(Shader.Find("Astra/FracturedPlanet")));
        Store("SetpieceGlow",new Material(Shader.Find("Astra/SetpieceGlow")));
        Store("SetpieceUnlit",new Material(Shader.Find("Unlit/Color")));
        Store("SetpieceSprite",new Material(Shader.Find("Sprites/Default")));
        var canopy=new Material(Shader.Find("Standard")); canopy.SetFloat("_Mode",3);
        canopy.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha); canopy.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
        canopy.SetInt("_ZWrite",0); canopy.EnableKeyword("_ALPHABLEND_ON"); canopy.EnableKeyword("_EMISSION"); canopy.renderQueue=3000;
        Store("SetpieceCanopy",canopy);
    }
    static void Store(string name,Material mat)
    {
        mat.name=name; string path=Root+"/Materials/"+name+".mat";
        var old=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(old!=null) { EditorUtility.CopySerialized(mat,old); UnityEngine.Object.DestroyImmediate(mat); mat=old; }
        else AssetDatabase.CreateAsset(mat,path);
        materials[name]=mat;
    }
    static void Metal(string name,Color color,float metallic,float smooth,float wear)
    {
        var m=new Material(Shader.Find("Astra/WornMetal")); m.SetColor("_Color",color);
        m.SetColor("_RustColor",new Color(.15f,.067f,.026f)); m.SetFloat("_Metallic",metallic); m.SetFloat("_Glossiness",smooth); m.SetFloat("_Wear",wear); m.SetFloat("_Scale",2.5f);
        m.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/NavalPaint_Albedo.png"));
        m.SetFloat("_TextureStrength",name=="HullPaint"?.88f:0);
        m.SetFloat("_TextureScale",.90f); m.SetFloat("_DetailScale",3.4f);
        bool metal=name=="HullPaint" || name=="DarkSteel" || name=="Brass" || name=="RedPaint";
        m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/CC0/PaintedMetal012_2K-PNG_NormalGL.png"));
        m.SetTexture("_RoughnessMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/CC0/PaintedMetal012_2K-PNG_Roughness.png"));
        m.SetFloat("_BumpScale",name=="HullPaint"?.28f:.19f); m.SetFloat("_DetailStrength",metal?.78f:0);
        Store(name,m);
    }
    static void PrepareTextureImports()
    {
        foreach(string file in Directory.GetFiles(Root+"/Textures","*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".png")||p.EndsWith(".jpg")))
        {
            var ti=AssetImporter.GetAtPath(file.Replace('\\','/')) as TextureImporter; if(ti==null) continue;
            bool normal=file.Contains("Normal");
            ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            ti.sRGBTexture=!normal && (file.Contains("Albedo") || file.Contains("Color"));
            ti.wrapMode=TextureWrapMode.Repeat; ti.filterMode=FilterMode.Trilinear; ti.anisoLevel=4;
            ti.maxTextureSize=2048; ti.textureCompression=TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
    }
    static void Emissive(string name,Color color,float energy)
    {
        var m=new Material(Shader.Find("Standard")); m.color=color*.55f; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*energy); Store(name,m);
    }
    static Material ResolveMaterial(string name)
    {
        foreach(var pair in materials) if(name==pair.Key || name.StartsWith(pair.Key+".")) return pair.Value;
        Debug.LogWarning("Unmapped Blender material: "+name); return materials["DarkSteel"];
    }
    static Light MakeLight(string name,Vector3 position,Color color,float intensity,float range)
    {
        var go=new GameObject(name); go.transform.position=position; var l=go.AddComponent<Light>();
        l.type=LightType.Point; l.color=color; l.intensity=intensity; l.range=range; l.renderMode=LightRenderMode.ForcePixel; return l;
    }
    static void CreateSpace()
    {
        var stars=new GameObject("Exterior | looping 3D stars"); var field=stars.AddComponent<LoopingStarfield>();
        field.volumeCenter=new Vector3(23,1.5f,0); field.size=new Vector3(34,32,40); field.velocity=new Vector3(0,.014f,.07f); field.starCount=550;
        field.starShader=Shader.Find("Particles/Standard Unlit");
        var planet=GameObject.CreatePrimitive(PrimitiveType.Sphere); planet.name="Exterior | slow rotating planet";
        planet.transform.position=new Vector3(20,-6.8f,-7); planet.transform.localScale=Vector3.one*19;
        planet.GetComponent<Renderer>().sharedMaterial=materials["Planet"]; UnityEngine.Object.DestroyImmediate(planet.GetComponent<Collider>());
        var spin=planet.AddComponent<DistantPlanet>(); spin.degreesPerSecond=1.2f;
    }
    static void CreateMarkings()
    {
        Label("A-01   /   COMMAND",new Vector3(0,2.35f,1.39f),Quaternion.Euler(0,180,0),.052f);
        Label("B-02   /   FABRICATION",new Vector3(1.39f,2.35f,0),Quaternion.Euler(0,270,0),.052f);
        Label("C-03   /   HABITATION",new Vector3(0,2.35f,-1.39f),Quaternion.identity,.052f);
        Label("D-04   /   OBSERVATION",new Vector3(-1.39f,2.35f,0),Quaternion.Euler(0,90,0),.052f);
    }
    static void Label(string text,Vector3 p,Quaternion q,float scale)
    {
        var go=new GameObject("Stencil | "+text); go.transform.position=p; go.transform.rotation=q;
        var tm=go.AddComponent<TextMesh>(); tm.text=text; tm.anchor=TextAnchor.MiddleCenter; tm.alignment=TextAlignment.Center;
        tm.characterSize=scale; tm.fontSize=64; tm.color=new Color(.68f,.65f,.45f);
    }
    static void WriteInventory()
    {
        var lines=new List<string>();
        foreach(var t in cabin.GetComponentsInChildren<Transform>()) if(t.name.StartsWith("ACT_") || t.name.StartsWith("PRINT_") || t.name.StartsWith("SCREEN_"))
            lines.Add(t.name+" world="+t.position.ToString("F3")+" euler="+t.eulerAngles.ToString("F1")+" scale="+t.lossyScale.ToString("F3"));
        lines.Add("Renderers="+cabin.GetComponentsInChildren<Renderer>().Length);
        lines.Add("Triangles="+cabin.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3));
        File.WriteAllLines(Path.GetFullPath("../scene-inventory.txt"),lines);
    }
    [MenuItem("Astra/Build Windows demo")]
    public static void Build()
    {
        BuildTo("../Build");
    }
    // A second output allows material revisions while the earlier preview is running.
    public static void BuildRevision() { BuildTo("../BuildV2"); }
    public static void BuildWindowRevision() { BuildTo("../BuildV3"); }
    public static void BuildPlanetRevision() { BuildTo("../BuildV4"); }
    public static void BuildEventRevision() { BuildTo("../BuildV5"); }
    public static void BuildEncounterRevision() { BuildTo("../BuildV6"); }
    public static void BuildPhasingWhaleRevision() { BuildTo("../BuildV7"); }
    public static void BuildCombatRevision()
    {
        const string musicPath = Root + "/Resources/Computer/CombatMusic.wav";
        AssetDatabase.ImportAsset(musicPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(musicPath) as AudioImporter;
        if (!importer) throw new Exception("Missing combat BGM: " + musicPath);
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = .75f;
        settings.preloadAudioData = false;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = true;
        importer.SaveAndReimport();
        if (!AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath)) throw new Exception("Combat BGM failed to decode.");
        BuildTo("../BuildV8");
    }
    static void BuildTo(string directory)
    {
        CreateScene(); Directory.CreateDirectory(directory);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{Root+"/Scenes/Cabin04.unity"}, locationPathName=directory+"/Astra Cabin.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed: "+report.summary.result);
        Debug.Log("ASTRA_BUILD_SUCCEEDED "+report.summary.totalSize);
    }
}
