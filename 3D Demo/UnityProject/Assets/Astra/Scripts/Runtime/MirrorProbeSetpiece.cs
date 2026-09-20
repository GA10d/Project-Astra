using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraCabin
{
    /// <summary>A second, physical pressure capsule: delayed imitation gradually becomes a prediction.</summary>
    public sealed class MirrorProbeSetpiece : MonoBehaviour
    {
        public bool Active { get; private set; }
        public float Elapsed { get; private set; }
        public Transform GeneratedRoot { get; private set; }
        public Transform PilotHead { get; private set; }
        public Transform MirrorLever { get; private set; }
        public bool AnticipationPhase { get; private set; }
        public bool MirrorLightsOn { get; private set; }
        public int SampleCount { get { return samples.Count; } }
        public Vector2 AppliedLook { get; private set; }
        public int AppliedWall { get; private set; }
        public float LastAppliedSampleTime { get; private set; }
        public bool MirroredInputPower { get; private set; }

        const int ExteriorLayer = 26;
        const float Delay = .5f;
        const float SampleInterval = 1f / 120f;
        const float PredictionAt = 12f;
        struct InputSample { public float time; public Vector2 look; public int wall; public bool light, power; }
        readonly Queue<InputSample> samples = new Queue<InputSample>();
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Transform> gaugeNeedles = new List<Transform>();
        SetpieceDirector director;
        Transform ship, pilot, leftHand, rightHand, leftUpper, leftLower, rightUpper, rightLower, leverGrip;
        Light warmLamp, watchLamp, rimLamp;
        Material paint, brass, dark, suit, skin, ivory, black, amber, green, red, lampMaterial, glass, textMaterial;
        Font font;
        InputSample delayed;
        float leverAngle, nextEcho, nextSampleAt;
        bool darkened, alertSent;

        public void Initialize(SetpieceDirector owner) { director = owner; }

        public void Show()
        {
            Clear();
            Active = true;
            GeneratedRoot = Group("SETPIECE_08_AnotherYou", null, Vector3.zero);
            paint = HullMaterial("Old Soviet grey-green enamel", new Color(.24f,.29f,.21f), .32f);
            brass = HullMaterial("Oxidised copper frames", new Color(.48f,.25f,.065f), .65f);
            dark = Material("Graphite joints", new Color(.027f,.038f,.032f), .3f, .015f);
            suit = Material("Worn orange pressure suit", new Color(.57f,.21f,.062f), .05f, .025f);
            skin = Material("Unfamiliar familiar face", new Color(.54f,.49f,.37f), .02f, .016f);
            ivory = Material("Faded instrument markings", new Color(.61f,.63f,.48f), .13f, .04f);
            black = Material("Unlit eyes", new Color(.008f,.012f,.008f), 0f, .005f);
            amber = Material("Amber equipment readout", new Color(.96f,.53f,.08f), .05f, .9f);
            green = Material("Green cathode ray phosphor", new Color(.19f,.65f,.35f), .05f, .75f);
            red = Material("Silent red standby", new Color(.72f,.055f,.012f), .1f, .8f);
            lampMaterial = Material("Duplicate cabin filament", new Color(.94f,.71f,.35f), 0, .8f);
            glass = new Material(Shader.Find("Astra/Setpieces/MirrorGlass")) { name = "Duplicate pressure pane / faint reflection" };
            materials.Add(glass);

            ship = Group("PIGEON 04 / matching grey-green pressure capsule", GeneratedRoot, new Vector3(5.45f,1.59f,0));
            ship.localRotation = Quaternion.Euler(0,90,0);
            BuildHull();
            BuildCabin();
            BuildPilot();
            rimLamp = StageLight("External icy rim", ship, new Vector3(-2.6f,3.3f,-2.3f), new Color(.42f,.60f,.68f), 2.1f, 13f);
            warmLamp = StageLight("Duplicate cabin warm light", ship, new Vector3(0,.80f,-.65f), new Color(1f,.74f,.37f), 2.8f, 4.3f);
            watchLamp = StageLight("Duplicate emergency glimmer", ship, new Vector3(-.42f,-.22f,-.85f), new Color(.83f,.095f,.028f), .16f, 2.3f);
            delayed = CaptureInput(0);
            samples.Enqueue(delayed);
            nextSampleAt = SampleInterval;
            AppliedLook = delayed.look; AppliedWall = delayed.wall; MirroredInputPower = delayed.power;
            MirrorLightsOn = delayed.light && delayed.power;
            SetLighting(MirrorLightsOn);
            nextEcho = 14f;
            if (director)
            {
                director.SetAlert("检测到重复船体编号", "目标舱体：CABIN 04\n信号延迟：0.5 秒\n另一名乘员正在模仿你的动作");
                director.PlayCue("echo", .4f);
            }
        }

        void BuildHull()
        {
            // A genuinely open shell: no opaque sphere in front of the second interior.
            const int latitudes=26, longitudes=48;
            var vertices=new List<Vector3>(); var indices=new List<int>();
            for(int y=0;y<=latitudes;y++)
            {
                float latitude=Mathf.PI*y/latitudes;
                float radius=Mathf.Sin(latitude);
                for(int x=0;x<=longitudes;x++)
                {
                    float a=Mathf.PI*2*x/longitudes;
                    vertices.Add(new Vector3(Mathf.Cos(a)*radius*1.76f,Mathf.Cos(latitude)*1.57f,Mathf.Sin(a)*radius*1.32f));
                }
            }
            for(int y=0;y<latitudes;y++) for(int x=0;x<longitudes;x++)
            {
                int a=y*(longitudes+1)+x,b=a+longitudes+1;
                Vector3 center=(vertices[a]+vertices[b]+vertices[a+1]+vertices[b+1])*.25f;
                if(center.z<-.52f && Mathf.Abs(center.x)<1.42f && center.y>-.79f && center.y<1.02f) continue;
                indices.Add(a);indices.Add(a+1);indices.Add(b);indices.Add(a+1);indices.Add(b+1);indices.Add(b);
            }
            Mesh shell=new Mesh { name="Editable cutaway nut pressure hull" };
            shell.SetVertices(vertices);shell.SetTriangles(indices,0);shell.RecalculateNormals();shell.RecalculateBounds();meshes.Add(shell);
            Transform body=Group("Riveted pinecone shell / real window opening",ship,Vector3.zero);
            body.gameObject.AddComponent<MeshFilter>().sharedMesh=shell;
            var shellRenderer=body.gameObject.AddComponent<MeshRenderer>();shellRenderer.sharedMaterial=paint;
            shellRenderer.shadowCastingMode=ShadowCastingMode.Off;
            // Offset armour scales give the hull its nut/pinecone silhouette.
            for(int row=0;row<5;row++) for(int n=0;n<14;n++)
            {
                float y=-1.08f+row*.47f;
                float latitude=Mathf.Acos(Mathf.Clamp(y/1.6f,-1,1));
                float angle=(n+(row%2)*.5f)*Mathf.PI*2/14;
                Vector3 p=new Vector3(Mathf.Cos(angle)*Mathf.Sin(latitude)*1.77f,y,Mathf.Sin(angle)*Mathf.Sin(latitude)*1.33f);
                if(p.z<-.52f && Mathf.Abs(p.x)<1.5f && y>-.85f && y<1.08f) continue;
                Transform scale=Primitive("Overlapping nut armour scale",PrimitiveType.Cube,ship,p,new Vector3(.45f,.44f,.065f),row%3==0?brass:paint);
                scale.localRotation=Quaternion.LookRotation(new Vector3(p.x*.6f,p.y*.14f,p.z).normalized)*Quaternion.Euler(-9,0,0);
                Primitive("Armour rivet",PrimitiveType.Sphere,scale,new Vector3(0,.32f,-.62f),new Vector3(.085f,.085f,.5f),brass);
            }
            Primitive("Upper airlock collar",PrimitiveType.Cylinder,ship,new Vector3(0,1.49f,0),new Vector3(.70f,.10f,.70f),brass);
            Primitive("Lower engine collar",PrimitiveType.Cylinder,ship,new Vector3(0,-1.47f,.08f),new Vector3(.85f,.14f,.85f),dark);
            for(int side=-1;side<=1;side+=2)
            {
                Rod("Outer pressure pipe",ship,new Vector3(side*1.52f,-.8f,-.55f),new Vector3(side*1.52f,.75f,-.55f),.085f,brass);
                Primitive("Side thruster",PrimitiveType.Capsule,ship,new Vector3(side*1.62f,-.55f,.26f),new Vector3(.32f,.39f,.32f),dark);
                Primitive("Navigation lamp",PrimitiveType.Sphere,ship,new Vector3(side*1.55f,.81f,-.62f),Vector3.one*.12f,side<0?red:green);
            }
            // The conspicuously identical window frame remains recognisable against the stars.
            Frame("Pressure window outer enamel",ship,new Vector3(0,.13f,-1.11f),2.97f,1.91f,.16f,.19f,paint);
            Frame("Aged brass glazing gasket",ship,new Vector3(0,.13f,-1.23f),2.71f,1.69f,.065f,.075f,brass);
            Frame("Black inner seal",ship,new Vector3(0,.13f,-1.24f),2.56f,1.54f,.035f,.025f,dark);
            for(int i=-5;i<=5;i++) foreach(int side in new[]{-1,1})
                Primitive("Window bolt",PrimitiveType.Sphere,ship,new Vector3(i*.25f,.13f+side*.85f,-1.25f),new Vector3(.042f,.042f,.021f),brass);
            Primitive("Transparent pressure pane",PrimitiveType.Cube,ship,new Vector3(0,.13f,-1.27f),new Vector3(2.47f,1.45f,.008f),glass);
            Primitive("Duplicate hull identity plaque",PrimitiveType.Cube,ship,new Vector3(0,-.98f,-1.10f),new Vector3(1.25f,.27f,.075f),dark);
            Label("PIGEON  //  CABIN 04",ship,new Vector3(0,-.98f,-1.146f),.13f,new Color(.76f,.73f,.51f));
        }

        void BuildCabin()
        {
            Primitive("Interior rear bulkhead",PrimitiveType.Cube,ship,new Vector3(0,.05f,.73f),new Vector3(2.4f,2.25f,.12f),paint);
            Primitive("Interior low sill",PrimitiveType.Cube,ship,new Vector3(0,-.68f,-.84f),new Vector3(2.48f,.12f,.64f),paint);
            for(int side=-1;side<=1;side+=2)
            {
                Rod("Identical rear cable run",ship,new Vector3(side*.84f,-.91f,.62f),new Vector3(side*.84f,.91f,.62f),.039f,brass);
                Primitive("Rear bulkhead seam",PrimitiveType.Cube,ship,new Vector3(side*.48f,.08f,.645f),new Vector3(.027f,1.91f,.016f),dark);
                for(int y=0;y<4;y++) Primitive("Bulkhead rivet",PrimitiveType.Sphere,ship,new Vector3(side*1.07f,-.75f+y*.48f,.635f),Vector3.one*.042f,brass);
            }
            Primitive("Identical CRT chassis",PrimitiveType.Cube,ship,new Vector3(-.80f,-.02f,.37f),new Vector3(.60f,.54f,.26f),dark);
            Primitive("Identical CRT phosphor",PrimitiveType.Cube,ship,new Vector3(-.80f,.01f,.228f),new Vector3(.46f,.34f,.015f),green);
            Label("ASTRA\nCABIN 04",ship,new Vector3(-.80f,.025f,.211f),.059f,new Color(.035f,.13f,.061f));
            for(int i=0;i<2;i++)
            {
                Transform gauge=Group("Duplicate analogue gauge",ship,new Vector3(.82f,.52f-i*.43f,.33f));
                Transform bezel=Primitive("Gauge brass bezel",PrimitiveType.Cylinder,gauge,Vector3.zero,new Vector3(.28f,.048f,.28f),brass);
                bezel.localRotation=Quaternion.Euler(90,0,0);
                Transform face=Primitive("Gauge ivory dial",PrimitiveType.Cylinder,gauge,new Vector3(0,0,-.049f),new Vector3(.231f,.008f,.231f),ivory);
                face.localRotation=Quaternion.Euler(90,0,0);
                for(int tick=0;tick<8;tick++)
                {
                    float a=(-115+tick*33)*Mathf.Deg2Rad;
                    Rod("Dial increment",gauge,new Vector3(Mathf.Sin(a)*.088f,Mathf.Cos(a)*.088f,-.060f),new Vector3(Mathf.Sin(a)*.104f,Mathf.Cos(a)*.104f,-.060f),.0025f,dark);
                }
                Transform pivot=Group("Moving duplicate gauge needle",gauge,new Vector3(0,0,-.065f));
                Rod("Needle",pivot,Vector3.zero,new Vector3(0,.092f,0),.006f,red);gaugeNeedles.Add(pivot);
            }
            Primitive("Ceiling light cage",PrimitiveType.Cylinder,ship,new Vector3(0,.94f,-.23f),new Vector3(.24f,.045f,.24f),dark);
            Primitive("Ceiling lamp bulb",PrimitiveType.Sphere,ship,new Vector3(0,.90f,-.23f),new Vector3(.18f,.075f,.18f),lampMaterial);
            Primitive("Duplicate lever mount",PrimitiveType.Cube,ship,new Vector3(.65f,-.30f,-.52f),new Vector3(.28f,.40f,.095f),paint);
            MirrorLever=Group("Duplicated power lever / independent joint",ship,new Vector3(.65f,-.36f,-.62f));
            Rod("Lever shaft",MirrorLever,Vector3.zero,new Vector3(0,.28f,0),.022f,brass);
            leverGrip=Primitive("Lever orange grip",PrimitiveType.Cube,MirrorLever,new Vector3(0,.28f,0),new Vector3(.18f,.07f,.07f),suit);
            Label("PWR",ship,new Vector3(.65f,-.56f,-.69f),.055f,new Color(.87f,.69f,.32f));
            Primitive("Standby red diode",PrimitiveType.Sphere,ship,new Vector3(-.92f,-.42f,-.13f),Vector3.one*.045f,red);
        }

        void BuildPilot()
        {
            pilot=Group("Other occupant / articulated orange work suit",ship,new Vector3(-.06f,-.19f,-.01f));
            Primitive("Seat back",PrimitiveType.Cube,pilot,new Vector3(0,-.01f,.24f),new Vector3(.61f,.88f,.16f),dark);
            Primitive("Worn orange torso",PrimitiveType.Capsule,pilot,new Vector3(0,.04f,0),new Vector3(.56f,.35f,.34f),suit);
            Primitive("Harness vertical left",PrimitiveType.Cube,pilot,new Vector3(-.17f,.08f,-.16f),new Vector3(.061f,.55f,.032f),dark);
            Primitive("Harness vertical right",PrimitiveType.Cube,pilot,new Vector3(.17f,.08f,-.16f),new Vector3(.061f,.55f,.032f),dark);
            Primitive("Harness waist strap",PrimitiveType.Cube,pilot,new Vector3(0,-.12f,-.18f),new Vector3(.48f,.061f,.035f),dark);
            Primitive("Harness buckle",PrimitiveType.Cube,pilot,new Vector3(0,-.12f,-.20f),new Vector3(.08f,.073f,.028f),brass);
            Primitive("Suit identity patch",PrimitiveType.Cube,pilot,new Vector3(-.086f,.17f,-.18f),new Vector3(.092f,.065f,.019f),ivory);
            for(int side=-1;side<=1;side+=2)
            {
                Primitive("Bent pressure suit leg",PrimitiveType.Capsule,pilot,new Vector3(side*.15f,-.39f,-.035f),new Vector3(.21f,.25f,.27f),suit);
                Primitive("Heavy work boot",PrimitiveType.Cube,pilot,new Vector3(side*.15f,-.63f,-.10f),new Vector3(.23f,.15f,.36f),dark);
            }
            PilotHead=Group("Duplicate occupant head / half-second memory",pilot,new Vector3(0,.44f,-.01f));
            Primitive("Pale face",PrimitiveType.Sphere,PilotHead,new Vector3(0,.03f,0),new Vector3(.32f,.43f,.30f),skin);
            Primitive("Close-fitting flight cap",PrimitiveType.Sphere,PilotHead,new Vector3(0,.16f,.026f),new Vector3(.335f,.23f,.307f),dark);
            Primitive("Nose",PrimitiveType.Sphere,PilotHead,new Vector3(0,.022f,-.15f),new Vector3(.055f,.079f,.063f),skin);
            for(int side=-1;side<=1;side+=2)
            {
                Primitive("Dark eye",PrimitiveType.Sphere,PilotHead,new Vector3(side*.061f,.075f,-.139f),new Vector3(.034f,.023f,.018f),black);
                Primitive("Eye glint",PrimitiveType.Sphere,PilotHead,new Vector3(side*.060f,.078f,-.149f),Vector3.one*.005f,ivory);
                Primitive("Headset ear pad",PrimitiveType.Sphere,PilotHead,new Vector3(side*.16f,.047f,.008f),new Vector3(.058f,.13f,.115f),dark);
                Rod("Severe eyebrow",PilotHead,new Vector3(side*.04f,.11f,-.139f),new Vector3(side*.085f,.108f,-.129f),.007f,dark);
            }
            Rod("Expressionless mouth",PilotHead,new Vector3(-.045f,-.064f,-.14f),new Vector3(.045f,-.064f,-.14f),.005f,dark);
            Rod("Headset microphone",PilotHead,new Vector3(.173f,.001f,-.018f),new Vector3(.07f,-.059f,-.189f),.010f,black);
            leftUpper=Rod("Left upper sleeve",pilot,Vector3.zero,Vector3.up,.072f,suit);
            leftLower=Rod("Left forearm",pilot,Vector3.zero,Vector3.up,.058f,suit);
            rightUpper=Rod("Right upper sleeve",pilot,Vector3.zero,Vector3.up,.072f,suit);
            rightLower=Rod("Right forearm",pilot,Vector3.zero,Vector3.up,.058f,suit);
            leftHand=Primitive("Left glove / later reaches glass",PrimitiveType.Sphere,pilot,Vector3.zero,new Vector3(.115f,.12f,.065f),skin);
            rightHand=Primitive("Right glove on switch",PrimitiveType.Sphere,pilot,Vector3.zero,new Vector3(.12f,.085f,.10f),skin);
            for(int i=0;i<4;i++) Rod("Left glove fingers",leftHand,new Vector3((i-1.5f)*.19f,.22f,-.04f),new Vector3((i-1.5f)*.22f,.90f,-.04f),.083f,skin);
        }

        InputSample CaptureInput(float time)
        {
            var c=director?director.controller:null; var s=director?director.systems:null;
            return new InputSample {time=time,look=c?c.CurrentLook:Vector2.zero,wall=c?c.CurrentWall:1,light=!s||s.LightsOn,power=!s||s.MainPowerOn};
        }

        void Update()
        {
            if(!Active || (director && director.controller && director.controller.IsPaused)) return;
            float dt=Time.deltaTime; if(dt<=0) return;
            Elapsed+=dt;
            if(font) RefreshFont(font);
            // Sample at most 120 times a second. Very high render rates must not
            // evict input before it has survived the full half-second delay.
            if(Elapsed>=nextSampleAt)
            {
                samples.Enqueue(CaptureInput(Elapsed));
                nextSampleAt=Elapsed+SampleInterval;
            }
            while(samples.Count>0 && samples.Peek().time<=Elapsed-Delay) delayed=samples.Dequeue();
            // Time-based expiry bounds the queue to about 61 samples without
            // dropping the older sample needed by the delayed performance.
            AppliedLook=delayed.look;AppliedWall=delayed.wall;LastAppliedSampleTime=delayed.time;MirroredInputPower=delayed.power;
            AnticipationPhase=Elapsed>=PredictionAt;
            float prediction=Mathf.Max(0,Elapsed-PredictionAt);
            bool targetLights=delayed.light&&delayed.power;
            float targetLever=targetLights?-24:48;
            if(AnticipationPhase)
            {
                // A voluntary action, not a reflection: this switch leads the actual player.
                float reach=Mathf.SmoothStep(0,1,Mathf.Clamp01(prediction/.72f));
                targetLever=Mathf.Lerp(-24,56,reach);
                targetLights=prediction<.57f && targetLights;
                if(!alertSent)
                {
                    alertSent=true;
                    if(director) director.SetAlert("检测到重复船体编号","目标舱体：CABIN 04\n动作时序异常 / 对方的动作开始领先");
                }
                if(!targetLights&&!darkened) { darkened=true; if(director) director.PlayCue("echo",.60f); }
                if(Elapsed>=nextEcho) { nextEcho=Elapsed+11f; if(director) director.PlayCue("echo",.25f); }
            }
            MirrorLightsOn=targetLights;
            SetLighting(MirrorLightsOn);
            leverAngle=Mathf.Lerp(leverAngle,targetLever,1-Mathf.Exp(-dt*8));
            MirrorLever.localRotation=Quaternion.Euler(leverAngle,0,0);

            float wallYaw=Mathf.Clamp(Mathf.DeltaAngle(90,AppliedWall*90),-72,72);
            Quaternion imitated=Quaternion.Euler(AppliedLook.y*17,-AppliedLook.x*23-wallYaw*.68f,Mathf.Sin(Elapsed*.9f)*1.2f);
            if(AnticipationPhase)
            {
                // After pulling the switch, look directly into the other window, ignoring mouse movement.
                Quaternion stare=Quaternion.Euler(-5+Mathf.Sin(prediction*.55f)*1.5f,Mathf.Sin(prediction*.24f)*2.5f,-9+Mathf.Sin(prediction*.38f)*3f);
                imitated=Quaternion.Slerp(imitated,stare,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,2.5f,prediction)));
            }
            PilotHead.localRotation=Quaternion.Slerp(PilotHead.localRotation,imitated,1-Mathf.Exp(-dt*10));
            ship.localPosition=new Vector3(5.45f+Mathf.Sin(Elapsed*.14f)*.027f,1.59f+Mathf.Sin(Elapsed*.30f)*.018f,Mathf.Sin(Elapsed*.21f)*.020f);
            AnimateArms(prediction);
            for(int i=0;i<gaugeNeedles.Count;i++) gaugeNeedles[i].localRotation=Quaternion.Euler(0,0,-35+i*42+Mathf.Sin(Elapsed*1.07f+i)*2.1f);
        }

        void AnimateArms(float prediction)
        {
            Vector3 right=pilot.InverseTransformPoint(leverGrip.position)+new Vector3(-.015f,.004f,-.027f);
            Vector3 left=new Vector3(-.35f,-.19f,-.32f)+new Vector3(AppliedLook.x*.075f,AppliedLook.y*.035f,0);
            if(AnticipationPhase)
            {
                float raise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.3f,4.8f,prediction));
                left=Vector3.Lerp(left,new Vector3(-.43f,.51f,-1.14f),raise);
                leftHand.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-38,-9,raise));
            }
            else leftHand.localRotation=Quaternion.Euler(0,0,-38);
            leftHand.localPosition=left;rightHand.localPosition=right;
            Vector3 leftShoulder=new Vector3(-.25f,.23f,-.01f),rightShoulder=new Vector3(.25f,.23f,-.01f);
            Vector3 leftElbow=Vector3.Lerp(leftShoulder,left,.48f)+new Vector3(-.13f,-.15f,.08f);
            Vector3 rightElbow=Vector3.Lerp(rightShoulder,right,.54f)+new Vector3(.16f,-.10f,.08f);
            PoseRod(leftUpper,leftShoulder,leftElbow,.072f);PoseRod(leftLower,leftElbow,left,.058f);
            PoseRod(rightUpper,rightShoulder,rightElbow,.072f);PoseRod(rightLower,rightElbow,right,.058f);
        }

        void SetLighting(bool on)
        {
            if(warmLamp) warmLamp.intensity=on?2.8f:0;
            if(watchLamp) watchLamp.intensity=on?.10f:.55f;
            if(rimLamp) rimLamp.intensity=on?2.1f:.48f;
            if(lampMaterial) lampMaterial.SetColor("_EmissionColor",on?new Color(.94f,.71f,.35f)*.8f:Color.black);
            if(green) green.SetColor("_EmissionColor",new Color(.19f,.65f,.35f)*(on?.75f:.04f));
        }

        public void Clear()
        {
            Active=false;Elapsed=0;AnticipationPhase=false;MirrorLightsOn=false;darkened=alertSent=false;
            LastAppliedSampleTime=0;AppliedLook=Vector2.zero;AppliedWall=1;MirroredInputPower=false;leverAngle=0;nextSampleAt=0;
            if(GeneratedRoot) {GeneratedRoot.gameObject.SetActive(false);Release(GeneratedRoot.gameObject);}
            GeneratedRoot=ship=pilot=PilotHead=MirrorLever=leverGrip=null;
            leftHand=rightHand=leftUpper=leftLower=rightUpper=rightLower=null;warmLamp=watchLamp=rimLamp=null;
            Font.textureRebuilt-=RefreshFont; font=null;textMaterial=null;
            foreach(var material in materials) if(material) Release(material);
            foreach(var mesh in meshes) if(mesh) Release(mesh);
            materials.Clear();meshes.Clear();samples.Clear();gaugeNeedles.Clear();
        }
        void OnDestroy() {Clear();}
        static void Release(Object value) {if(Application.isPlaying) Destroy(value);else DestroyImmediate(value);}

        Material HullMaterial(string name,Color color,float metallic)
        {
            var material=new Material(Shader.Find("Astra/Setpieces/MirrorHull")){name=name};
            material.SetColor("_Color",color);material.SetFloat("_Metallic",metallic);materials.Add(material);return material;
        }
        Material Material(string name,Color color,float metallic,float emission)
        {
            var material=new Material(Shader.Find("Standard")){name="Mirror / "+name,color=color};
            material.SetFloat("_Metallic",metallic);material.SetFloat("_Glossiness",.26f);
            material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*emission);materials.Add(material);return material;
        }
        static Transform Group(string name,Transform parent,Vector3 position)
        {
            Transform t=new GameObject(name).transform;t.gameObject.layer=ExteriorLayer;t.SetParent(parent,false);t.localPosition=position;return t;
        }
        static Transform Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);go.name=name;go.layer=ExteriorLayer;
            Transform t=go.transform;t.SetParent(parent,false);t.localPosition=position;t.localScale=scale;
            var collider=go.GetComponent<Collider>();if(collider){collider.enabled=false;Release(collider);}
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return t;
        }
        static Transform Rod(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material material)
        {
            var rod=Primitive(name,PrimitiveType.Cylinder,parent,Vector3.zero,Vector3.one,material);PoseRod(rod,a,b,radius);return rod;
        }
        static void PoseRod(Transform rod,Vector3 a,Vector3 b,float radius)
        {
            Vector3 d=b-a;rod.localPosition=(a+b)*.5f;rod.localScale=new Vector3(radius*2,d.magnitude*.5f,radius*2);
            if(d.sqrMagnitude>.000001f)rod.localRotation=Quaternion.FromToRotation(Vector3.up,d.normalized);
        }
        static void Frame(string name,Transform parent,Vector3 center,float width,float height,float border,float depth,Material material)
        {
            foreach(int side in new[]{-1,1})
            {
                Primitive(name+" horizontal",PrimitiveType.Cube,parent,center+Vector3.up*side*(height-border)*.5f,new Vector3(width,border,depth),material);
                Primitive(name+" vertical",PrimitiveType.Cube,parent,center+Vector3.right*side*(width-border)*.5f,new Vector3(border,height-border*2,depth),material);
            }
        }
        static Light StageLight(string name,Transform parent,Vector3 position,Color color,float intensity,float range)
        {
            var light=Group(name,parent,position).gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;
            light.cullingMask=1<<ExteriorLayer;light.shadows=LightShadows.None;return light;
        }
        void Label(string text,Transform parent,Vector3 position,float size,Color color)
        {
            var node=Group(text,parent,position);var tm=node.gameObject.AddComponent<TextMesh>();
            if(!font)
            {
                font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");textMaterial=new Material(Shader.Find("Astra/Setpieces/WorldText")){name="Duplicate capsule stencil"};
                materials.Add(textMaterial);Font.textureRebuilt+=RefreshFont;
            }
            tm.font=font;tm.text=text;tm.fontSize=64;tm.characterSize=size*.16f;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=color;
            font.RequestCharactersInTexture(text,64,FontStyle.Normal);RefreshFont(font);
            var r=tm.GetComponent<Renderer>();r.sharedMaterial=textMaterial;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        void RefreshFont(Font rebuilt) {if(rebuilt&&rebuilt==font&&textMaterial&&rebuilt.material)textMaterial.mainTexture=rebuilt.material.mainTexture;}
    }
}
