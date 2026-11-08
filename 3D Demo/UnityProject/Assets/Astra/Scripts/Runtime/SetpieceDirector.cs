using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraCabin
{
    /// <summary>One active vignette at a time. Zero restores the cabin, never personal documents.</summary>
    public sealed class SetpieceDirector : MonoBehaviour
    {
        public bool allowDebugHotkeys = true;
        public bool discoverExteriorAutomatically = true;
        public CabinController controller;
        public CabinSystems systems;
        public CabinComputer computer;
        public DistantPlanet planet;
        public LoopingStarfield stars;
        // Scene references retain shaders and transparent variants in standalone builds.
        public Material[] retainedMaterials;
        public const int EncounterMode = 12;
        public const int StomachTalkMode=13;
        public const int PrintingRequestMode=14;
        public const int FoolsMode=15;
        public const int EscapeMode=16;
        public const int FarewellMode=17;
        public Galaxies.Zyl.FarewellSetpiece farewell;
        public Galaxies.Zyl.EscapeSetpiece escape;
        public Galaxies.Zyl.FoolsSetpiece fools;
        public Galaxies.Zyl.MihuoerPrintingSetpiece printingRequest;
        public Galaxies.Zyl.StomachTalkSetpiece stomachTalk;
        public Galaxies.Zyl.MihuoerEncounter encounter;
        public const int PortalMode = 11;
        public PortalSetpiece portal;
        public const int BlockedEngineMode = 10;
        public static bool RequiresRepair(int mode) { return mode == 3 || mode == BlockedEngineMode; }
        public bool EngineBlocked { get { return ActiveMode == BlockedEngineMode || (computer && computer.Navigation && computer.Navigation.Session != null && computer.Navigation.Session.Engine.EngineBlocked); } }
        public int ActiveMode { get; private set; }
        public float ModeTime { get; private set; }
        public bool CrisisActive { get; private set; }
        public bool HasHammer { get; private set; }
        public int RepairedCount { get; private set; }
        public float ShakeAmount { get; private set; }
        public string AlertTitle { get; private set; }
        public GameObject AlertRoot { get; private set; }
        public Transform HammerRack { get; private set; }
        public CabinInteractable[] RepairPoints { get; private set; } = new CabinInteractable[0];
        public bool automationMode;
        public bool RepairBusy { get { return repairing; } }
        public ExplosionBattleSetpieces combat;
        public VisitorStationSetpieces visitors;
        public CrimsonEyeSetpiece crimson;
        public StarWhaleSetpiece whales;
        public MirrorProbeSetpiece mirror;
        public SortingStationSetpiece sorting;
        public SetpieceAudio eventAudio;

        readonly List<Material> materials=new List<Material>();
        readonly List<GameObject> crackArt=new List<GameObject>();
        readonly List<GameObject> patches=new List<GameObject>();
        GameObject repairRoot,heldHammer;
        Font font; GUIStyle heading,body,small;
        Material steel,handle,amber,dark,red,white,worldText,promptPaper,promptRed;
        bool initialized,alarmStarted,repairing;
        readonly bool[] repaired=new bool[3];
        float shakeRemaining,shakeDuration,shakeStrength;
        Galaxies.Zyl.ZylPresentationParameters zylParameters=new Galaxies.Zyl.ZylPresentationParameters();
        bool ZylRepair { get { return ActiveMode==BlockedEngineMode && computer.Navigation && computer.Navigation.ActiveGalaxyId=="zyl"; } }
        string alertDetail="";
        Vector3 originalPlanetPosition,originalPlanetScale;
        Color originalCameraBackground;

        void Start() { Initialize(); }
        void Initialize()
        {
            if(initialized) return;
            initialized=true;
            if(!controller) controller=GetComponent<CabinController>();
            if(!systems) systems=GetComponent<CabinSystems>();
            if(!computer) computer=GetComponent<CabinComputer>();
            if(!planet && discoverExteriorAutomatically) planet=FindObjectOfType<DistantPlanet>();
            if(!stars && discoverExteriorAutomatically) stars=FindObjectOfType<LoopingStarfield>();
            encounter=GetComponent<Galaxies.Zyl.MihuoerEncounter>()??gameObject.AddComponent<Galaxies.Zyl.MihuoerEncounter>(); encounter.Initialize(this);
            portal=GetComponent<PortalSetpiece>()??gameObject.AddComponent<PortalSetpiece>(); portal.Initialize(this);
            stomachTalk=GetComponent<Galaxies.Zyl.StomachTalkSetpiece>()??gameObject.AddComponent<Galaxies.Zyl.StomachTalkSetpiece>();stomachTalk.Initialize(this);
            farewell=GetComponent<Galaxies.Zyl.FarewellSetpiece>()??gameObject.AddComponent<Galaxies.Zyl.FarewellSetpiece>();farewell.Initialize(this);
            escape=GetComponent<Galaxies.Zyl.EscapeSetpiece>()??gameObject.AddComponent<Galaxies.Zyl.EscapeSetpiece>();escape.Initialize(this);
            fools=GetComponent<Galaxies.Zyl.FoolsSetpiece>()??gameObject.AddComponent<Galaxies.Zyl.FoolsSetpiece>();fools.Initialize(this);
            printingRequest=GetComponent<Galaxies.Zyl.MihuoerPrintingSetpiece>()??gameObject.AddComponent<Galaxies.Zyl.MihuoerPrintingSetpiece>();printingRequest.Initialize(this);
            combat=GetComponent<ExplosionBattleSetpieces>()??gameObject.AddComponent<ExplosionBattleSetpieces>();
            visitors=GetComponent<VisitorStationSetpieces>()??gameObject.AddComponent<VisitorStationSetpieces>();
            crimson=GetComponent<CrimsonEyeSetpiece>()??gameObject.AddComponent<CrimsonEyeSetpiece>();
            whales=GetComponent<StarWhaleSetpiece>()??gameObject.AddComponent<StarWhaleSetpiece>();
            mirror=GetComponent<MirrorProbeSetpiece>()??gameObject.AddComponent<MirrorProbeSetpiece>();
            sorting=GetComponent<SortingStationSetpiece>()??gameObject.AddComponent<SortingStationSetpiece>();
            eventAudio=GetComponent<SetpieceAudio>()??gameObject.AddComponent<SetpieceAudio>();
            combat.Initialize(this); visitors.Initialize(this); crimson.Initialize(this);
            whales.Initialize(this); mirror.Initialize(this); sorting.Initialize(this);
            originalPlanetPosition=planet?planet.transform.position:new Vector3(20,-6.8f,-7); originalPlanetScale=planet?planet.transform.localScale:Vector3.one*19;
            originalCameraBackground=controller.viewCamera.backgroundColor;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","SimHei","Arial"},48);
            worldText=new Material(Shader.Find("Astra/Setpieces/WorldText")); materials.Add(worldText);
            Font.textureRebuilt+=RefreshFontAtlas;
            steel=Mat("Repair steel",new Color(.29f,.34f,.32f),false,.6f);
            handle=Mat("Hammer grip",new Color(.42f,.11f,.045f),false,.1f);
            amber=Mat("Amber warning",new Color(1f,.43f,.04f),true);
            red=Mat("Emergency red",new Color(.85f,.055f,.02f),true);
            dark=Mat("Dark hardware",new Color(.025f,.055f,.047f),true);
            promptPaper=Mat("Terminal prompt paper",new Color(.69f,.70f,.65f),true);
            promptRed=Mat("Terminal prompt red",new Color(.43f,.13f,.10f),true);
            white=Mat("Warning trim",new Color(.68f,.77f,.64f),true);
        }
        void Update()
        {
            if(!initialized) return;
            if(!Galaxies.Zyl.ZylScreenCurtain.BlocksInput && allowDebugHotkeys && !automationMode && !controller.automationMode && !controller.ComputerOpen && !controller.IsPaused)
            {
                for(int i=0;i<=9;i++)
                    if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0+i)) || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0+i)))
                    {
                        if(computer.Navigation && computer.Navigation.PresentationPending)
                            controller.ShowNotice("请先在导航模式完成当前事件，再使用演出调试快捷键");
                        else TryDebugMode(i);
                        break;
                    }
            }
            if(controller.IsPaused) return;
            ModeTime+=Time.deltaTime;
            if(RequiresRepair(ActiveMode) && CrisisActive && !alarmStarted && ModeTime>=(ZylRepair?zylParameters.repairAlarmDelay:3f))
            {
                alarmStarted=true; systems.SetDamageState(true,true); eventAudio.SetAlarm(true);
                Shake(ZylRepair?zylParameters.repairImpactStrength:.95f,ZylRepair?zylParameters.repairImpactDuration:1.35f); PlayCue("impact",.85f);
                SetAlert("舱体遭遇撞击","应急电源已启动\n前往 B 墙取下维修锤\n修复 D 墙裂痕：0 / 3");
            }
            shakeRemaining=Mathf.Max(0,shakeRemaining-Time.deltaTime);
            ShakeAmount=shakeDuration>0?shakeStrength*Mathf.Pow(shakeRemaining/shakeDuration,1.2f):0;
            float farewellShake = farewell ? farewell.CinematicShake : 0;
            ShakeAmount = Mathf.Max(ShakeAmount, farewellShake);
            float t = farewellShake > 0 ? farewell.Elapsed : Time.time;
            controller.cinematicEuler=new Vector3(Mathf.Sin(t*41),Mathf.Sin(t*33+1)*.75f,Mathf.Sin(t*29+.5f)*.55f)*ShakeAmount*4.4f;
            controller.cinematicOffset=new Vector3(Mathf.Sin(t*37),Mathf.Sin(t*47+2),Mathf.Sin(t*31)*.35f)*ShakeAmount*.027f;
        }
        void LateUpdate()
        {
            // The physical CRT overlay uses the same main/emergency circuit as the desktop.
            if(initialized && AlertRoot && AlertRoot.activeSelf!=systems.ScreenHasPower)
                AlertRoot.SetActive(systems.ScreenHasPower);
        }
        public bool TryDebugMode(int mode) { return allowDebugHotkeys && TriggerMode(mode); }
        public bool TriggerMode(int mode)
        {
            if(mode<0 || mode>FarewellMode) return false;
            Initialize();
            Quaternion previousPlanetRotation=planet?planet.transform.rotation:Quaternion.identity;
            ClearCurrent();
            ActiveMode=mode; ModeTime=0;
            zylParameters=Galaxies.Zyl.ZylPresentationParameters.Load(mode==BlockedEngineMode && computer.Navigation && computer.Navigation.ActiveGalaxyId=="zyl");
            if(mode==0)
            {
                controller.SnapToWall(0); controller.ShowNotice("全部演出已复位 / DEFAULT SCENE"); return true;
            }
            // Events change the world, not the player's chosen wall, mouse look,
            // active turn or window approach. Only damage requires leaving the
            // close-up so the player can reach the repair targets.
            if(!RequiresRepair(mode) && planet) planet.gameObject.SetActive(false);
            switch(mode)
            {
                case 1:
                    combat.PlanetRotation=previousPlanetRotation;
                    combat.ShowPlanetExplosion(originalPlanetPosition,originalPlanetScale.x*.5f,planet?planet.GetComponent<Renderer>().sharedMaterial:null);
                    break;
                case 2:
                    SetAlert("不明飞行物靠近","近距目标已停泊于观察窗外\n检测到生命体 / 对方正在招手");
                    visitors.ShowVisitor(); PlayCue("alert",.55f); break;
                case 3:
                case BlockedEngineMode:
                    controller.ExitWindowView();
                    controller.observationAllowed=false; CrisisActive=true; systems.SetDamageState(true,false);
                    CreateRepairProps(); Shake(1f,1.1f); PlayCue("impact",1);
                    controller.ShowNotice("遭遇撞击 / 主电路已隔离"); break;
                case 4: crimson.Show(); break;
                case 5: combat.ShowBattle(); break;
                case 6: visitors.ShowStation(); break;
                case 7: whales.Show(); break;
                case 8: mirror.Show(); break;
                case 9: sorting.Show(); break;
                case PortalMode: portal.Show(); break;
                case EncounterMode: encounter.Show(); break;
                case StomachTalkMode: stomachTalk.Show();break;
                case PrintingRequestMode: printingRequest.Show();break;
                case FoolsMode: fools.Show();break;
                case EscapeMode: escape.Show();break;
                case FarewellMode: farewell.Show();break;
            }
            return true;
        }
        public void ResetToDefault() { TriggerMode(0); }
        void ClearCurrent()
        {
            StopAllCoroutines();
            if(computer.IsOpen) computer.Close();
            controller.SetPaused(false); controller.observationAllowed=true;
            farewell.Clear();escape.Clear();fools.Clear();printingRequest.Clear();stomachTalk.Clear();encounter.Clear(); portal.Clear(); combat.Clear(); visitors.Clear(); crimson.Clear(); whales.Clear(); mirror.Clear(); sorting.Clear(); eventAudio.StopAll();
            if(repairRoot) { repairRoot.SetActive(false); Destroy(repairRoot); }
            if(heldHammer) { heldHammer.SetActive(false); Destroy(heldHammer); }
            if(AlertRoot) { AlertRoot.SetActive(false); Destroy(AlertRoot); }
            repairRoot=null; heldHammer=null; HammerRack=null; AlertRoot=null;
            RepairPoints=new CabinInteractable[0]; crackArt.Clear(); patches.Clear();
            for(int i=0;i<repaired.Length;i++) repaired[i]=false;
            CrisisActive=false; HasHammer=false; RepairedCount=0; alarmStarted=false; repairing=false;
            AlertTitle=""; alertDetail=""; computer.ClearEventAlert();
            shakeRemaining=shakeDuration=shakeStrength=ShakeAmount=0;
            controller.cinematicOffset=controller.cinematicEuler=Vector3.zero;
            systems.SetDamageState(false,false);
            systems.SetMainPower(true); systems.SetLights(true); systems.SetAir(true); systems.SetPrinter(false); systems.SetRadio(false);
            foreach(var item in FindObjectsOfType<CabinInteractable>()) item.ResetToInitialState();
            if(planet){planet.gameObject.SetActive(true); planet.transform.position=originalPlanetPosition; planet.transform.localScale=originalPlanetScale;planet.SetElapsedForTest(0);planet.animate=true;}
            if(stars){stars.gameObject.SetActive(true);stars.animate=true;}
            controller.viewCamera.backgroundColor=originalCameraBackground;
            var exterior=GetComponent<Galaxies.Zyl.ZylStomachEnvironment>(); if(exterior)exterior.ApplyExterior();
        }
        public void Shake(float intensity,float seconds)
        {
            shakeStrength=Mathf.Max(ShakeAmount,Mathf.Clamp01(intensity));
            shakeDuration=shakeRemaining=Mathf.Clamp(seconds,.05f,5f);
        }
        public void PlayCue(string name,float volume=1f) { if(eventAudio) eventAudio.Play(name,Mathf.Clamp01(volume)); }

        void CreateRepairProps()
        {
            repairRoot=new GameObject("EVENT_Repair | three hull breaches and service hammer");
            HammerRack=new GameObject("ACT_ServiceHammer").transform; HammerRack.SetParent(repairRoot.transform);
            HammerRack.position=new Vector3(-1.05f,1.51f,.42f);
            MakeHammer(HammerRack,1);
            Box("Service tool magnetic bracket",HammerRack,new Vector3(-.052f,.08f,0),new Vector3(.045f,.44f,.26f),dark);
            Text("SERVICE HAMMER",HammerRack,new Vector3(.033f,-.29f,-.16f),Quaternion.Euler(0,-90,0),.028f,new Color(.95f,.76f,.23f));
            var pickup=HammerRack.gameObject.AddComponent<CabinInteractable>(); pickup.title="维修锤 / SERVICE HAMMER"; pickup.description="取下锤子，修复 D 墙三处裂痕";
            pickup.kind=InteractionKind.Button; pickup.toggleState=false; pickup.travel=0; pickup.outlineShader=Shader.Find("Astra/Outline");
            var pickupCollider=HammerRack.gameObject.AddComponent<BoxCollider>(); pickupCollider.center=new Vector3(.02f,.06f,0); pickupCollider.size=new Vector3(.12f,.49f,.31f);
            pickup.onChanged.AddListener(value=>TakeHammer());
            // Independent repair targets in front of the hull, outside the clear window.
            Vector3[] positions={new Vector3(1.25f,1.98f,.71f),new Vector3(1.07f,1.49f,-1.09f),new Vector3(1.05f,.63f,.65f)};
            RepairPoints=new CabinInteractable[3];
            for(int i=0;i<3;i++)
            {
                int index=i; var node=new GameObject("ACT_HullCrack_"+(i+1)); node.transform.SetParent(repairRoot.transform);
                node.transform.position=positions[i];
                var art=new GameObject("Hull split "+(i+1)); art.transform.SetParent(node.transform,false); crackArt.Add(art);
                float[] ys={-.13f,-.07f,-.035f,.035f,.085f,.15f};
                float[] zs={-.10f,-.04f,-.065f,.045f,.012f,.12f};
                for(int n=1;n<ys.Length;n++)
                {
                    Vector3 a=new Vector3(0,ys[n-1],zs[n-1]),b=new Vector3(0,ys[n],zs[n]);
                    Rod("Open fracture",art.transform,a,b,.012f,dark);
                    Rod("Pressure leak glow",art.transform,a+Vector3.left*.005f,b+Vector3.left*.005f,.0035f,amber);
                    if(n==2 || n==4) Rod("Branch fracture",art.transform,b,b+new Vector3(0,.06f,-.11f),.007f,dark);
                }
                var plate=Box("New bolted repair plate",node.transform,new Vector3(-.008f,0,0),new Vector3(.015f,.36f,.36f),steel);
                foreach(float y in new[]{-.145f,.145f}) foreach(float z in new[]{-.145f,.145f})
                    Sphere("Patch rivet",plate.transform,new Vector3(-.6f,y/.36f,z/.36f),Vector3.one*.055f,white);
                plate.SetActive(false); patches.Add(plate);
                var frame=Box("Repair outline source",node.transform,new Vector3(-.015f,0,0),new Vector3(.018f,.36f,.36f),amber);
                frame.GetComponent<Renderer>().enabled=false;
                float markerY=i==0?-.23f:.23f;
                var indicator=Sphere("Leak warning lamp",node.transform,new Vector3(-.045f,markerY,0),Vector3.one*.035f,amber);
                Text("0"+(i+1),node.transform,new Vector3(-.034f,markerY+.04f,.06f),Quaternion.Euler(0,90,0),.045f,new Color(1,.48f,.05f));
                var item=node.AddComponent<CabinInteractable>(); item.title="舱体裂痕 "+(i+1)+" / HULL BREACH";
                item.description="持维修锤点击修复"; item.kind=InteractionKind.Button; item.travel=0; item.toggleState=false;
                item.outlineShader=Shader.Find("Astra/Outline"); item.highlightRenderers=new[]{frame.GetComponent<Renderer>()};
                var box=node.AddComponent<BoxCollider>(); box.size=new Vector3(.09f,.39f,.39f);
                item.onChanged.AddListener(value=>RepairCrack(index)); RepairPoints[i]=item;
            }
        }
        public bool TakeHammer()
        {
            if(!CrisisActive || HasHammer) return false;
            HasHammer=true;
            if(HammerRack) HammerRack.gameObject.SetActive(false);
            heldHammer=new GameObject("Held repair hammer | animated");
            heldHammer.transform.SetParent(controller.viewCamera.transform,false);
            heldHammer.transform.localPosition=new Vector3(.24f,-.23f,.56f);
            heldHammer.transform.localRotation=Quaternion.Euler(0,0,-18);
            MakeHammer(heldHammer.transform,.60f);
            PlayCue("hammer",.4f); controller.ShowNotice("维修锤已拿起 / 前往 D 墙点击三处裂痕");
            return true;
        }
        public bool RepairCrack(int index)
        {
            if(!CrisisActive || index<0 || index>=3 || repaired[index] || repairing) return false;
            if(!HasHammer) { controller.ShowNotice("需要维修锤 / 先到 B 墙取下锤子"); PlayCue("alert",.25f); return false; }
            StartCoroutine(RepairStrike(index)); return true;
        }
        IEnumerator RepairStrike(int index)
        {
            repairing=true;
            Quaternion rest=heldHammer.transform.localRotation; Vector3 restPosition=heldHammer.transform.localPosition;
            float elapsed=0; bool struck=false;
            float strikeDuration=ZylRepair?zylParameters.repairStrikeDuration:.48f;
            while(elapsed<strikeDuration)
            {
                elapsed+=Time.deltaTime; float u=Mathf.Clamp01(elapsed/strikeDuration);
                float swing=Mathf.Sin(u*Mathf.PI);
                heldHammer.transform.localRotation=rest*Quaternion.Euler(-72*swing,0,14*swing);
                heldHammer.transform.localPosition=restPosition+new Vector3(-.045f,.04f,.11f)*swing;
                if(u>.48f && !struck) { struck=true; PlayCue("hammer",.85f); Shake(.07f,.14f); }
                yield return null;
            }
            heldHammer.transform.SetLocalPositionAndRotation(restPosition,rest);
            repaired[index]=true; RepairedCount++; crackArt[index].SetActive(false); patches[index].SetActive(true);
            RepairPoints[index].SetHover(false); RepairPoints[index].GetComponent<Collider>().enabled=false; RepairPoints[index].enabled=false;
            repairing=false;
            SetAlert("舱体遭遇撞击","应急电源维持中\n修复 D 墙裂痕："+RepairedCount+" / 3");
            if(RepairedCount==3)
            {
                CrisisActive=false; HasHammer=false; systems.SetDamageState(false,false); systems.SetMainPower(true); systems.SetLights(true);
                controller.observationAllowed=true; eventAudio.SetAlarm(false);
                foreach(var item in FindObjectsOfType<CabinInteractable>()) if(item.name=="ACT_PowerLever" || item.name=="ACT_LightSwitch") item.ResetToInitialState();
                if(heldHammer) { heldHammer.SetActive(false); Destroy(heldHammer); heldHammer=null; }
                if(HammerRack) { HammerRack.gameObject.SetActive(true); HammerRack.GetComponent<CabinInteractable>().enabled=false; }
                if(ActiveMode == BlockedEngineMode)
                    SetAlert("舱体未完全修复","三处裂痕已封闭\n主电源恢复\n引擎被未知物质堵塞");
                else SetAlert("舱体修复完成","三处裂痕已封闭\n主电源恢复 / 危机解除"); PlayCue("success",.65f);
            }
        }
        void MakeHammer(Transform parent,float scale)
        {
            var art=new GameObject("Hammer geometry").transform; art.SetParent(parent,false); art.localScale=Vector3.one*scale;
            Box("Worn grip",art,new Vector3(0,-.06f,0),new Vector3(.050f,.30f,.05f),handle);
            for(int i=0;i<7;i++) Box("Grip band",art,new Vector3(0,-.18f+i*.032f,0),new Vector3(.054f,.008f,.054f),dark);
            Box("Steel shaft",art,new Vector3(0,.10f,0),new Vector3(.024f,.19f,.026f),steel);
            Box("Steel striking head",art,new Vector3(0,.205f,0),new Vector3(.09f,.08f,.25f),steel);
            Box("Yellow safety marking",art,new Vector3(.047f,.205f,0),new Vector3(.004f,.044f,.08f),amber);
        }
        public void SetAlert(string title,string detail)
        {
            AlertTitle=title; alertDetail=detail; computer.SetEventAlert(title,detail);
            if(AlertRoot) { AlertRoot.SetActive(false); Destroy(AlertRoot); }
            AlertRoot=new GameObject("Terminal alert | "+title);
            AlertRoot.transform.position=new Vector3(0,1.58f,1.165f);
            Box("Popup panel",AlertRoot.transform,Vector3.zero,new Vector3(1.37f,.61f,.012f),promptPaper);
            Box("Red title bar",AlertRoot.transform,new Vector3(0,.23f,-.008f),new Vector3(1.34f,.115f,.012f),promptRed);
            Text(title,AlertRoot.transform,new Vector3(-.61f,.27f,-.022f),Quaternion.identity,.065f,new Color(1,.91f,.75f));
            if(ActiveMode == BlockedEngineMode && RepairedCount == 3)
            {
                Text("三处裂痕已封闭\n主电源恢复",AlertRoot.transform,new Vector3(-.59f,.08f,-.021f),Quaternion.identity,.042f,new Color(.08f,.13f,.11f));
                Text("引擎被未知物质堵塞",AlertRoot.transform,new Vector3(-.59f,-.025f,-.021f),Quaternion.identity,.042f,new Color(.43f,.13f,.10f));
            }
            else Text(detail,AlertRoot.transform,new Vector3(-.59f,.08f,-.021f),Quaternion.identity,.042f,new Color(.08f,.13f,.11f));
            AlertRoot.SetActive(systems.ScreenHasPower);
        }
        Material Mat(string name,Color color,bool unlit,float metallic=0)
        {
            var material=new Material(Shader.Find(unlit?"Unlit/Color":"Standard")){name=name,color=color};
            if(!unlit) { material.SetFloat("_Metallic",metallic); material.SetFloat("_Glossiness",.28f); }
            materials.Add(material); return material;
        }
        GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var ob=GameObject.CreatePrimitive(type); ob.name=name; ob.transform.SetParent(parent,false);
            ob.transform.localPosition=position; ob.transform.localScale=scale;
            var collider=ob.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
            var renderer=ob.GetComponent<Renderer>(); renderer.sharedMaterial=material;
            return ob;
        }
        GameObject Box(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        { return Primitive(name,PrimitiveType.Cube,parent,position,scale,material); }
        GameObject Sphere(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        { return Primitive(name,PrimitiveType.Sphere,parent,position,scale,material); }
        void Rod(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material material)
        {
            var ob=Primitive(name,PrimitiveType.Cylinder,parent,(a+b)*.5f,new Vector3(radius*2,(b-a).magnitude*.5f,radius*2),material);
            ob.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
        }
        void Text(string value,Transform parent,Vector3 position,Quaternion rotation,float size,Color color)
        {
            var ob=new GameObject("Label | "+value); ob.transform.SetParent(parent,false); ob.transform.localPosition=position; ob.transform.localRotation=rotation;
            var text=ob.AddComponent<TextMesh>(); text.font=font; text.fontSize=64; text.characterSize=size*.16f;
            text.anchor=TextAnchor.UpperLeft; text.alignment=TextAlignment.Left; text.text=value; text.color=color;
            font.RequestCharactersInTexture(value,64,FontStyle.Normal);
            worldText.mainTexture=font.material.mainTexture;
            ob.GetComponent<Renderer>().sharedMaterial=worldText;
        }
        void RefreshFontAtlas(Font updated) { if(updated==font && worldText) worldText.mainTexture=font.material.mainTexture; }
        void OnGUI()
        {
            if (farewell && farewell.DepartureActive)
            {
                if (!Galaxies.Zyl.ZylScreenCurtain.IsOpaque) farewell.DrawDepartureTitle(font);
                return;
            }
            if((farewell&&farewell.Active&&!farewell.Complete&&farewell.Phase!=Galaxies.Zyl.FarewellSetpiece.Step.Error)||(escape&&escape.Active&&!escape.Complete&&escape.Phase!=Galaxies.Zyl.EscapeSetpiece.Step.Error)||Galaxies.Zyl.ZylScreenCurtain.IsOpaque || (fools&&fools.Active&&!fools.Complete&&fools.Phase!=Galaxies.Zyl.FoolsSetpiece.Step.Error))return;
            if(!initialized) return;
            if(controller.ComputerOpen) return;
            if(small==null)
            {
                small=new GUIStyle(GUI.skin.label){font=font,fontSize=15,alignment=TextAnchor.MiddleCenter};
                small.normal.textColor=new Color(.75f,.79f,.65f);
                heading=new GUIStyle(small){fontSize=21,alignment=TextAnchor.MiddleLeft};
                heading.normal.textColor=new Color(1,.71f,.35f);
                body=new GUIStyle(small){fontSize=16,alignment=TextAnchor.UpperLeft,wordWrap=true};
            }
            var old=GUI.matrix; float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            try {
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1)); float width=Screen.width/scale;
            if(allowDebugHotkeys) GUI.Label(new Rect(width*.5f-410,28,820,27),"[1] 爆星  [2] 访客  [3] 撞击  [4] 赤域  [5] 战场  [6] 补给  [7] 鲸群  [8] 镜像  [9] 分拣  [0] 复位",small);
            if(ActiveMode!=0)
            {
                Color oldColor=GUI.color; GUI.color=new Color(.018f,.025f,.025f,.87f);
                GUI.DrawTexture(new Rect(28,110,385,CrisisActive?119:79),Texture2D.whiteTexture); GUI.color=oldColor;
                string[] names={"","行星碎裂","不明飞行物靠近","舱体遭遇撞击","赤红星域","交战区","星际补给站","星海鲸群","另一艘你","耗材分拣站","未知撞击 / 引擎堵塞","误入星际传送门","初遇米霍尔","肚中会谈","泰德野里_米霍尔需要打印构件","泰德野里_都是傻瓜","泰德野里_逃出生天","泰德野里_别离"};
                GUI.Label(new Rect(43,116,350,31),ActiveMode < names.Length ? names[ActiveMode] : "舱内演出",heading);
                string text=CrisisActive?(ModeTime<3?"电力中断…":HasHammer?"已持维修锤 / 修复 D 墙裂痕："+RepairedCount+" / 3":"前往 B 墙取下维修锤\n然后点击 D 墙三处黄色高亮裂痕"):ActiveMode==7?"小鲸鱼正在穿舱 / Q、E 环顾四周":ActiveMode==8?"移动鼠标、拨动电源，观察对面…":ActiveMode==9?"转向 D 墙观察 / 点击窗户可凑近":(allowDebugHotkeys?"[0] 随时恢复默认场景":"完成事件后返回导航继续");
                GUI.Label(new Rect(43,153,352,65),text,body);
            }
            } finally { GUI.matrix=old; }
            DrawEngineWarning();
        }
        void DrawEngineWarning()
        {
            if(!EngineBlocked || (ActiveMode == BlockedEngineMode && RepairedCount < 3)) return;
            var matrix=GUI.matrix;var color=GUI.color;
            float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float y=CrisisActive?239:205;
            GUI.color=new Color(.035f,.015f,.01f,.94f);GUI.DrawTexture(new Rect(28,y,385,64),Texture2D.whiteTexture);GUI.color=color;
            var style=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true,alignment=TextAnchor.MiddleLeft,richText=false};
            style.normal.textColor=new Color(1,.36f,.12f);
            GUI.Label(new Rect(43,y+8,355,48),"引擎堵塞，飞船失去动力",style);
            GUI.matrix=matrix;GUI.color=color;
        }
        void OnDestroy()
        {
            Font.textureRebuilt-=RefreshFontAtlas;
            if(initialized) { if(repairRoot) Destroy(repairRoot); if(heldHammer) Destroy(heldHammer); if(AlertRoot) Destroy(AlertRoot); }
            foreach(var material in materials) if(material) Destroy(material);
            if(font) Destroy(font);
        }
    }
}
