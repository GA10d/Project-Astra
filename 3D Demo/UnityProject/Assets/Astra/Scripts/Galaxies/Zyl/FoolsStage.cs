using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraCabin.Galaxies.Zyl
{
    // All generated objects and material replacements belong to this session, never the scene asset.
    public sealed class FoolsStage : IDisposable
    {
        public const float CloseCut=11f, TearStart=18.2f, BurstStart=20f, WordCut=23.5f, WordReady=28.5f, Duration=33f;
        public Camera Camera {get;private set;}
        public Vector3 Base {get;private set;}
        public Vector3 Contact {get;private set;}
        public Vector3 Tip {get;private set;}
        public Vector3 FirstCameraPosition {get;private set;}
        public Vector3 FinalCameraPosition {get;private set;}
        public int MicrobeCount {get{return microbes.Count;}}
        public float Opening {get;private set;}
        public float TimeSample {get;private set;}
        public Transform Root {get;private set;}
        readonly List<Transform> microbes=new List<Transform>();
        readonly List<Vector3> targets=new List<Vector3>(),scatter=new List<Vector3>();
        readonly List<Material> ownedMaterials=new List<Material>();
        readonly List<Renderer> walls=new List<Renderer>();
        readonly List<Material[]> originalMaterials=new List<Material[]>();
        readonly List<Material> membranes=new List<Material>();
        readonly List<Mesh> meshes=new List<Mesh>();
        Transform shaft,tip,lip,pocket;
        Vector3 axis,right,up,forward,cutRight,cutUp,wordCenter,finalLook;
        float distance;
        readonly float microbeSpeed;
        public float WordReadyTime => BurstStart + (WordReady - BurstStart) / microbeSpeed;
        public FoolsStage(Transform craft,Transform environment,Matrix4x4 authoredToWorld,Camera source,float microbeSpeed=1)
        {
            this.microbeSpeed=microbeSpeed;
            try {Build(craft,environment,authoredToWorld,source);}
            catch{Dispose();throw;}
        }
        Material Material(string name,Color color,float metal=0,float glow=0)
        {
            var m=new Material(Shader.Find("Standard")){name=name,color=color};m.SetFloat("_Metallic",metal);m.SetFloat("_Glossiness",.55f);
            if(glow>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*glow);}ownedMaterials.Add(m);return m;
        }
        Transform Primitive(string name,PrimitiveType type,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.layer=ZylStomachEnvironment.EnvironmentLayer;go.transform.SetParent(Root,false);
            var collider=go.GetComponent<Collider>();if(collider){collider.enabled=false;Release(collider);}
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;return go.transform;
        }
        void Build(Transform craft,Transform environment,Matrix4x4 frame,Camera source)
        {
            if(!craft||!environment||!source)throw new InvalidOperationException("都是傻瓜：飞船、胃内环境或相机不可用。");
            Root=new GameObject("Zyl15 | pole, incision and bacterial STOP").transform;Root.SetParent(environment,true);
            right=frame.MultiplyVector(Vector3.right).normalized;up=frame.MultiplyVector(Vector3.up).normalized;forward=frame.MultiplyVector(Vector3.forward).normalized;
            var shotForward=(right*9+forward*5).normalized;
            var shotRight=Vector3.Cross(up,shotForward).normalized;
            Base=craft.position+up*.38f+shotRight*.85f;
            axis=(up+shotRight*.64f+shotForward*.14f).normalized;
            Contact=FindCeiling(environment,Base,axis);distance=Vector3.Distance(Base,Contact);
            if(distance<6)throw new InvalidOperationException("杆子上方没有找到足够高的胃壁，请检查飞船位置。");
            cutRight=Vector3.Cross(forward,axis).normalized;cutUp=Vector3.Cross(axis,cutRight).normalized;
            FirstCameraPosition=ClearCamera(environment,craft,craft.position+up*1.9f-shotForward*4.5f,up,shotForward);
            FinalCameraPosition=ClearCamera(environment,craft,craft.position+up*2.4f-shotForward*5f,up,shotForward);
            wordCenter=craft.position+up*6.5f+shotForward*4f-shotRight*1.5f;
            finalLook=craft.position+up*4.2f+shotForward*3.5f-shotRight*1.5f;
            var cameraObject=new GameObject("Zyl15 | fixed-position tracking and cut cameras");cameraObject.transform.SetParent(Root,true);
            Camera=cameraObject.AddComponent<Camera>();Camera.CopyFrom(source);Camera.enabled=false;Camera.targetTexture=null;Camera.rect=new Rect(0,0,1,1);
            Camera.cullingMask=1<<ZylStomachEnvironment.EnvironmentLayer;Camera.depth=source.depth+10;Camera.nearClipPlane=.06f;Camera.farClipPlane=Mathf.Max(source.farClipPlane,500);
            Camera.clearFlags=CameraClearFlags.SolidColor;
            var metal=Material("Brushed telescoping pole",new Color(.32f,.35f,.31f),.78f);
            var rim=Material("Pole collar",new Color(.12f,.16f,.14f),.7f);
            shaft=Primitive("Extending pole | craft socket",PrimitiveType.Cylinder,metal);
            tip=Primitive("Rounded probing tip",PrimitiveType.Sphere,metal);tip.localScale=new Vector3(.24f,.24f,.24f);
            var socket=Primitive("Pole base collar",PrimitiveType.Cylinder,rim);socket.position=Base;socket.rotation=Quaternion.FromToRotation(Vector3.up,axis);socket.localScale=new Vector3(.44f,.22f,.44f);
            var shader=Resources.Load<Shader>("Galaxies/Zyl/Fools/FoolsMucosa");if(!shader)throw new InvalidOperationException("都是傻瓜：胃壁裂口Shader缺失。");
            foreach(var renderer in environment.GetComponentsInChildren<Renderer>(true))
            {
                var original=renderer.sharedMaterials;var replacement=(Material[])original.Clone();bool changed=false;
                for(int i=0;i<original.Length;i++)if(original[i]&&original[i].shader&&original[i].shader.name=="Astra/Zyl/BreathingMucosa")
                {
                    var copy=new Material(original[i]);copy.shader=shader;copy.name=original[i].name+" | private Zyl15 incision";
                    copy.SetVector("_CutCenter",Contact);copy.SetVector("_CutNormal",axis);copy.SetVector("_CutRight",cutRight);copy.SetVector("_CutUp",cutUp);
                    membranes.Add(copy);ownedMaterials.Add(copy);replacement[i]=copy;changed=true;
                }
                if(changed){walls.Add(renderer);originalMaterials.Add(original);renderer.sharedMaterials=replacement;}
            }
            if(membranes.Count==0)throw new InvalidOperationException("都是傻瓜：没有找到胃壁材质。");
            lip=new GameObject("Wet irregular tissue lips").transform;lip.SetParent(Root,true);lip.position=Contact-axis*.10f;lip.rotation=Quaternion.LookRotation(axis,cutUp);
            var mesh=BuildLip();meshes.Add(mesh);lip.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;lip.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Material("Wet incision edge",new Color(.26f,.045f,.05f),0,.04f);lip.gameObject.layer=ZylStomachEnvironment.EnvironmentLayer;
            pocket=Primitive("Dark cavity behind split",PrimitiveType.Sphere,Material("Interior depth",new Color(.012f,.004f,.007f)));pocket.position=Contact+axis*.45f;pocket.rotation=lip.rotation;
            BuildMicrobes();
            var lightObject=new GameObject("Zyl15 | soft pole and membrane fill");lightObject.transform.SetParent(Root,true);lightObject.transform.position=Contact-axis*3;
            var light=lightObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.68f,.45f);light.range=17;light.intensity=1.2f;light.cullingMask=1<<ZylStomachEnvironment.EnvironmentLayer;
            Sample(0);
        }
        static Vector3 ClearCamera(Transform environment,Transform craft,Vector3 preferred,Vector3 up,Vector3 forward)
        {
            var proxies=new List<GameObject>();var colliders=new List<MeshCollider>();bool previous=Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces=true;
                foreach(var filter in environment.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!filter.sharedMesh||filter.transform.IsChildOf(craft))continue;
                    var renderer=filter.GetComponent<Renderer>();if(!renderer||!renderer.enabled)continue;
                    var go=new GameObject("Temporary camera visibility query");proxies.Add(go);
                    go.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);go.transform.localScale=filter.transform.lossyScale;
                    var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;colliders.Add(collider);
                }
                var side=Vector3.Cross(up,forward).normalized;
                var candidates=new[]{preferred,craft.position-forward*3.5f+up*2.4f,preferred+up*1.5f,preferred+side*2,preferred-side*2,craft.position-forward*2.8f+up*2.5f};
                foreach(var position in candidates)
                {
                    bool clear=true;
                    foreach(var target in new[]{craft.position+up*.4f,craft.position+up*1.35f})
                    {
                        var delta=target-position;var ray=new Ray(position,delta.normalized);
                        foreach(var collider in colliders){RaycastHit hit;if(collider.Raycast(ray,out hit,delta.magnitude-.08f)){clear=false;break;}}
                        if(!clear)break;
                    }
                    if(clear)return position;
                }
                throw new InvalidOperationException("都是傻瓜：参考机位附近被场景物体遮挡，找不到清晰的飞船视线。");
            }
            finally{Physics.queriesHitBackfaces=previous;foreach(var go in proxies){go.SetActive(false);Release(go);}}
        }
        public static Vector3 FindCeiling(Transform environment,Vector3 origin,Vector3 direction)
        {
            float nearest=float.MaxValue;Vector3 point=Vector3.zero;bool previous=Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces=true;
            try
            {
                foreach(var renderer in environment.GetComponentsInChildren<MeshRenderer>(true))
                {
                    bool mucosa=false;foreach(var m in renderer.sharedMaterials)if(m&&m.shader&&m.shader.name=="Astra/Zyl/BreathingMucosa")mucosa=true;
                    var filter=renderer.GetComponent<MeshFilter>();if(!mucosa||!filter||!filter.sharedMesh)continue;
                    var probe=new GameObject("Temporary ceiling ray query");
                    try
                    {
                        probe.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);probe.transform.localScale=filter.transform.lossyScale;
                        var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                        RaycastHit hit;if(collider.Raycast(new Ray(origin,direction),out hit,250)&&hit.distance>4&&hit.distance<nearest){nearest=hit.distance;point=hit.point;}
                        collider.enabled=false;
                    }
                    finally{probe.SetActive(false);Release(probe);}
                }
            }
            finally{Physics.queriesHitBackfaces=previous;}
            if(nearest==float.MaxValue)throw new InvalidOperationException("杆子方向未命中胃壁顶部；未使用虚构终点。");return point;
        }
        Mesh BuildLip()
        {
            const int count=80;var vertices=new Vector3[count*3];var triangles=new int[count*12];
            for(int n=0;n<count;n++)
            {
                float a=n*Mathf.PI*2/count;float noise=1+.055f*Mathf.Sin(a*7)+.03f*Mathf.Sin(a*13+1);
                for(int ring=0;ring<3;ring++)vertices[n*3+ring]=new Vector3(Mathf.Cos(a)*(1.35f+ring*.2f)*noise,Mathf.Sin(a)*(.46f+ring*.17f)*noise,ring==1?-.16f:.10f);
                for(int ring=0;ring<2;ring++){int i=n*12+ring*6,v=n*3+ring,next=((n+1)%count)*3+ring;triangles[i]=v;triangles[i+1]=next;triangles[i+2]=v+1;triangles[i+3]=next;triangles[i+4]=next+1;triangles[i+5]=v+1;}
            }
            var mesh=new Mesh{name="Irregular split mucosa lip"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh BuildMicrobeMesh()
        {
            const int sides = 12, hemisphereSteps = 3, rings = hemisphereSteps * 2;
            const float radius = .5f;
            var vertices = new Vector3[sides * rings + 2];
            var normals = new Vector3[vertices.Length];
            var indices = new List<int>(sides * rings * 6);
            int bottom = vertices.Length - 1;
            vertices[0] = normals[0] = Vector3.up;
            vertices[bottom] = normals[bottom] = Vector3.down;

            // Keep the stock capsule's radius and height, with smooth normals around the caps.
            for (int ring = 0; ring < rings; ring++)
            {
                bool upper = ring < hemisphereSteps;
                int step = upper ? hemisphereSteps - 1 - ring : ring - hemisphereSteps;
                float latitude = step * Mathf.PI / (2 * hemisphereSteps) * (upper ? 1 : -1);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * 2 * Mathf.PI / sides;
                    int current = 1 + ring * sides + side;
                    int next = 1 + ring * sides + (side + 1) % sides;
                    var normal = new Vector3(Mathf.Cos(angle) * Mathf.Cos(latitude), Mathf.Sin(latitude),
                        Mathf.Sin(angle) * Mathf.Cos(latitude));
                    normals[current] = normal;
                    vertices[current] = radius * (normal + (upper ? Vector3.up : Vector3.down));

                    if (ring == 0)
                    {
                        indices.Add(0); indices.Add(next); indices.Add(current);
                    }
                    if (ring == rings - 1)
                    {
                        indices.Add(current); indices.Add(next); indices.Add(bottom);
                    }
                    else
                    {
                        indices.Add(current); indices.Add(next); indices.Add(current + sides);
                        indices.Add(next); indices.Add(next + sides); indices.Add(current + sides);
                    }
                }
            }

            return new Mesh { name = "Zyl15 | low-poly bacillus capsule", vertices = vertices,
                normals = normals, triangles = indices.ToArray() };
        }

        void BuildMicrobes()
        {
            string[][] letters={new[]{"01111","11000","11000","01110","00011","00011","11110"},new[]{"11111","00100","00100","00100","00100","00100","00100"},new[]{"01110","11011","11011","11011","11011","11011","01110"},new[]{"11110","11011","11011","11110","11000","11000","11000"}};
            var random=new System.Random(1507);
            var mesh=BuildMicrobeMesh();meshes.Add(mesh);
            var palette=new[]{Material("Microbial ivory",new Color(.84f,.91f,.59f),0,.65f),Material("Microbial olive",new Color(.42f,.67f,.29f),0,.8f),Material("Microbial warm",new Color(.89f,.73f,.33f),0,.55f)};
            Quaternion finalRotation=Quaternion.LookRotation(wordCenter-FinalCameraPosition,up);var letterRight=finalRotation*Vector3.right;var letterUp=finalRotation*Vector3.up;
            for(int l=0;l<4;l++)for(int y=0;y<7;y++)for(int x=0;x<5;x++)if(letters[l][y][x]=='1')for(int k=0;k<4;k++)
            {
                int i=microbes.Count;
                var microbe=new GameObject("Bacillus "+i).transform;
                microbe.gameObject.layer=ZylStomachEnvironment.EnvironmentLayer;
                microbe.SetParent(Root,false);
                microbe.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=microbe.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=palette[i%3];
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                microbe.localScale=new Vector3(.10f,.16f,.10f)*(float)(.85+random.NextDouble()*.35);
                microbes.Add(microbe);
                float jx=(float)(random.NextDouble()-.5)*.22f,jy=(float)(random.NextDouble()-.5)*.22f;
                targets.Add(wordCenter+letterRight*((l*6+x-11)*.60f+jx)+letterUp*((3-y)*.60f+jy)+forward*(float)(random.NextDouble()-.5)*.18f);
                scatter.Add(cutRight*(float)(random.NextDouble()-.5)*6+cutUp*(float)(random.NextDouble()-.5)*4-axis*(float)(2+random.NextDouble()*5));
            }
        }
        static float Ease(float a,float b,float t){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));}
        public static float Jab(float t,float start,float strength)
        {
            float u=(t-start)/1.35f;if(u<0||u>1)return 0;return Mathf.Sin(u*Mathf.PI)*strength;
        }
        public void Sample(float t)
        {
            TimeSample=Mathf.Max(0,t);t=TimeSample;
            float swarmTime=t<=BurstStart?t:BurstStart+(t-BurstStart)*microbeSpeed;
            float grown=Mathf.Lerp(.06f,distance-.35f,Ease(0,10.7f,t));
            float impact=Jab(t,12.1f,.32f)+Jab(t,14.4f,.32f)+Jab(t,16.8f,.9f);
            float retract=Mathf.Lerp(0,1.3f,Ease(19,21,t));
            float length=grown+impact-retract;
            Tip=Base+axis*length;shaft.position=(Base+Tip)*.5f;shaft.rotation=Quaternion.FromToRotation(Vector3.up,axis);shaft.localScale=new Vector3(.15f,length*.5f,.15f);tip.position=Tip;
            Opening=Ease(TearStart,20.6f,t);
            foreach(var m in membranes){m.SetFloat("_CutOpen",Opening);m.SetFloat("_Poke",impact);m.SetFloat("_BreathPreviewTime",t);}
            lip.gameObject.SetActive(Opening>.001f);pocket.gameObject.SetActive(Opening>.001f);lip.localScale=new Vector3(1,Mathf.Lerp(.12f,1,Opening),1)*Mathf.Lerp(.01f,1,Opening);pocket.localScale=new Vector3(2.8f,Mathf.Lerp(.02f,1f,Opening),.6f)*Opening;
            if(t<CloseCut){Camera.transform.position=FirstCameraPosition;Camera.transform.rotation=Quaternion.LookRotation(Vector3.Lerp(Base+up*.7f,Tip,Mathf.Lerp(.05f,.92f,Ease(1,9,t)))-FirstCameraPosition,up);Camera.fieldOfView=70;}
            else if(swarmTime<WordCut){Camera.transform.position=Contact-axis*6+cutRight*2+cutUp*.25f;Camera.transform.rotation=Quaternion.LookRotation(Contact-Camera.transform.position,cutUp);Camera.fieldOfView=46;}
            else
            {
                var cloudCenter=Contact-axis*4.5f;
                float track=Ease(WordCut,WordReady-.8f,swarmTime);
                Camera.transform.position=FinalCameraPosition;
                Camera.transform.rotation=Quaternion.Slerp(Quaternion.LookRotation(cloudCenter-FinalCameraPosition,up),
                    Quaternion.LookRotation(finalLook-FinalCameraPosition,up),track);
                Camera.fieldOfView=70;
            }
            for(int i=0;i<microbes.Count;i++)
            {
                var microbe=microbes[i];
                float born=BurstStart+(i%31)*.023f;
                microbe.gameObject.SetActive(swarmTime>=born);
                if(!microbe.gameObject.activeSelf)continue;

                float gatherStart=WordCut+(i%29)*.025f;
                float gatherEnd=WordReady-(i%13)*.047f;
                float cloudTime=Mathf.Min(swarmTime,gatherStart);
                float burst=Ease(born,born+2.1f,cloudTime);
                var cloud=Contact+Vector3.Lerp(-axis*.2f,scatter[i],burst);
                cloud+=cutRight*Mathf.Sin(cloudTime*2+i*.73f)*.18f+cutUp*Mathf.Cos(cloudTime*1.7f+i*.51f)*.16f;

                float u=Ease(gatherStart,gatherEnd,swarmTime);
                float v=1-u;
                var target=targets[i];
                var bank=right*Mathf.Sin(i*.81f)*2.3f+up*(1.35f+Mathf.Cos(i*.63f)*1.8f);
                var first=Vector3.Lerp(cloud,target,.28f)+bank;
                var second=Vector3.Lerp(cloud,target,.76f)+bank*.48f+forward*Mathf.Sin(i*.41f)*1.2f;
                var tangent=v*v*(first-cloud)+2*v*u*(second-first)+u*u*(target-second);
                microbe.position=v*v*v*cloud+3*v*v*u*first+3*v*u*u*second+u*u*u*target;
                microbe.position+=(right*Mathf.Sin(i+swarmTime*2)+up*Mathf.Cos(i*.7f+swarmTime*1.6f))*.026f*u;
                var tumble=Quaternion.AngleAxis(i*47+swarmTime*28,forward)*Quaternion.FromToRotation(Vector3.up,right);
                microbe.rotation=Quaternion.Slerp(tumble,Quaternion.FromToRotation(Vector3.up,tangent),Mathf.Sin(Mathf.PI*u)*.86f);
            }
        }
        public int VisibleMicrobes {get{return microbes.FindAll(m=>m&&m.gameObject.activeSelf).Count;}}
        public void UpdateBreathing(float time){foreach(var material in membranes)material.SetFloat("_BreathPreviewTime",time);}
        public void SampleReturn(float time,float duration,float hold=4.5f)
        {
            Sample(WordReadyTime+hold);
            float follow=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/duration));
            Camera.transform.rotation=Quaternion.LookRotation(Vector3.Lerp(finalLook,Contact,follow)-FinalCameraPosition,up);
            for(int i=0;i<microbes.Count;i++)
            {
                float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp((i%31)/31f*duration*.18f,duration,time));var m=microbes[i];
                var origin=m.position;
                var middle=Vector3.Lerp(origin,Contact,.5f)-axis*2+cutRight*Mathf.Sin(i*1.73f)*1.2f;
                m.position=(1-u)*(1-u)*origin+2*(1-u)*u*middle+u*u*(Contact+axis*.45f);
                var returnRotation=Quaternion.LookRotation((Contact-m.position).sqrMagnitude>.001f?Contact-m.position:axis,up)*Quaternion.Euler(90,0,i*31);
                m.rotation=Quaternion.Slerp(m.rotation,returnRotation,Mathf.SmoothStep(0,1,u*7));
                m.gameObject.SetActive(u<.995f);
            }
        }
        public void Dispose()
        {
            if(Camera)Camera.enabled=false;
            for(int i=0;i<walls.Count;i++)if(walls[i])walls[i].sharedMaterials=originalMaterials[i];walls.Clear();originalMaterials.Clear();
            if(Root){Root.gameObject.SetActive(false);Release(Root.gameObject);Root=null;}
            foreach(var m in ownedMaterials)if(m)Release(m);ownedMaterials.Clear();membranes.Clear();
            foreach(var mesh in meshes)if(mesh)Release(mesh);meshes.Clear();microbes.Clear();
        }
        static void Release(UnityEngine.Object value){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
