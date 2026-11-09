using System;
using UnityEngine;
namespace AstraCabin
{
    // Runtime attachment keeps existing scenes and ship prefab GUIDs intact.
    [DefaultExecutionOrder(100)]
    public sealed class CabinFabricator : MonoBehaviour
    {
        public bool Focused { get; private set; }
        public bool Printing { get; private set; }
        public int[] Completed = new int[3];
        public int[] Inventory = new int[3];
        public static readonly string[] BlueprintIds={"seal_ring","connection_flange","buffer_gasket"};
        string receiptId, receiptVoyage, receiptPayload;
        int[] receiptCounts;
        public float PrintProgress { get; private set; }
        [Serializable] sealed class Store { public int version=1; public int[] completed,inventory; public int blueprint; public float clock; public bool printing; public string receiptId,receiptVoyage,receiptPayload; public int[] receiptCounts; }
        string SavePath { get { return System.IO.Path.Combine(Array.IndexOf(Environment.GetCommandLineArgs(),"--fabricator-qa")>=0 ? "D:/AI/02_游戏开发/Project-AstraCabin/output/fabricator" : Application.persistentDataPath,"fabricator-v1.json"); } }
        float saveClock;
        bool Save()
        {
            if(!player)return false;
            try {
                var data=new Store{completed=Completed,inventory=Inventory,blueprint=selected,clock=printClock,printing=Printing,receiptId=receiptId,receiptVoyage=receiptVoyage,receiptPayload=receiptPayload,receiptCounts=receiptCounts};
                string temp=SavePath+".tmp";System.IO.File.WriteAllText(temp,JsonUtility.ToJson(data));
                if(System.IO.File.Exists(SavePath))System.IO.File.Replace(temp,SavePath,null);else System.IO.File.Move(temp,SavePath);
                return true;
            } catch(Exception e){Debug.LogWarning("Fabricator save: "+e.Message);return false;}
        }
        void Load()
        {
            try {
                if(!System.IO.File.Exists(SavePath))return;
                var data=JsonUtility.FromJson<Store>(System.IO.File.ReadAllText(SavePath));
                if(data==null||data.version!=1)return;
                receiptId=data.receiptId;receiptVoyage=data.receiptVoyage;receiptPayload=data.receiptPayload;receiptCounts=data.receiptCounts;
                for(int i=0;i<3;i++){Completed[i]=data.completed!=null&&i<data.completed.Length?Mathf.Max(0,data.completed[i]):0;Inventory[i]=data.inventory!=null&&i<data.inventory.Length?Mathf.Max(0,data.inventory[i]):0;}
                if(data.printing){selected=Mathf.Clamp(data.blueprint,0,2);StartPrinting();printClock=Mathf.Clamp(data.clock,0,18);PrintProgress=printClock/18;Save();}
            }catch(Exception e){Debug.LogWarning("Fabricator load: "+e.Message);}
        }
        void OnApplicationPause(bool paused){if(paused)Save();}
        void OnApplicationQuit(){Save();}
        public string[] Blueprints = { "密封环", "连接法兰", "缓冲垫片" };
        CabinController player; CabinSystems systems;
        Transform head; Vector3 headRest, origin; Quaternion facing;
        float blend, angle, progress, errorTime, printClock;
        Vector2 offset; Vector2[] contour;
        int selected; bool drawing, failed; string message = "选择图纸，开始轮廓校准";
        GameObject part; Material material, beamMaterial; LineRenderer beam; CabinFabricatorScreen screen;
        const int Samples = 240;
        public static CabinFabricator Attach(CabinSystems systems)
        {
            var value = systems.GetComponent<CabinFabricator>();
            if (!value && systems.printerHead) value = systems.gameObject.AddComponent<CabinFabricator>();
            return value;
        }
        void Start()
        {
            player = GetComponent<CabinController>(); systems = GetComponent<CabinSystems>(); head = systems.printerHead;
            if (!player || !head) { enabled = false; return; }
            foreach(var item in FindObjectsOfType<CabinInteractable>())
                if(item.name=="ACT_PrintButton" && item.gameObject.scene==gameObject.scene) { Button=item; break; }
            headRest = head.localPosition;
            origin = head.position;
            facing = Quaternion.Euler(0,270,0);
            // Printer-local space: +X right, +Y up, +Z away from the observer.
            origin += facing * new Vector3(0,-.16f,0);
            MakeContour(); Load();
            screen=new GameObject("Fabricator | physical descending terminal").AddComponent<CabinFabricatorScreen>();
            screen.Initialize(this,player.viewCamera,head.parent);
        }
        public CabinInteractable Button { get; private set; }
        public bool Hit(Ray ray)
        {
            if(!head || !Button) return false;
            float distance;
            if(!new Plane(facing*Vector3.forward,origin).Raycast(ray,out distance) || distance>4.5f)return false;
            Vector3 local=Quaternion.Inverse(facing)*(ray.GetPoint(distance)-origin);
            if(Mathf.Abs(local.x)>.48f || local.y<-.22f || local.y>.42f)return false;
            RaycastHit hit;
            // Respect furniture and walls in front of the machine.
            return !Physics.Raycast(ray,out hit,distance-.18f) || (Quaternion.Inverse(facing)*(hit.point-origin)).z>-.18f;
        }
        public void Open()
        {
            if (!enabled || !player || player.IsPaused || player.ComputerOpen || player.IsTurning || player.cinematicWindowLock) return;
            Focused = true; player.SetHoverForTest(null); message = Printing ? "正在自动打印，可返回舱室等待" : "选择图纸，开始轮廓校准";
        }
        public void Close() { Focused = false; drawing = false; }
        void MakeContour()
        {
            contour = new Vector2[Samples+1];
            for (int i=0;i<=Samples;i++)
            {
                float t = i * Mathf.PI*2/Samples;
                float r = 115 + (selected==1 ? 12*Mathf.Sin(4*t) : selected==2 ? 8*Mathf.Sin(3*t) : 0);
                contour[i] = new Vector2(r*Mathf.Sin(t), r*(1-Mathf.Cos(t)));
            }
        }
        public void BeginDrawing()
        {
            if (Printing || !systems.MainPowerOn) { message="主电源关闭，无法启动"; return; }
            MakeContour(); angle=0; offset=Vector2.zero; progress=0; errorTime=0; drawing=true; failed=false;
            message="按住 Q / E 旋转图纸，让轮廓持续穿过固定绘制点";
        }
        void Update()
        {
            if (!player) return;
            if (Focused && (player.ComputerOpen || player.cinematicWindowLock || Galaxies.Zyl.ZylScreenCurtain.BlocksInput)) Close();
            if (Focused && Input.GetKeyDown(KeyCode.Escape)) Close();
            if (player.IsPaused) return;
            if(Printing){saveClock+=Time.deltaTime;if(saveClock>2){saveClock=0;Save();}}
            blend = Mathf.MoveTowards(blend, Focused ? 1 : 0, Time.deltaTime/ .85f);
            if (drawing && Focused)
            {
                float dt=Time.deltaTime;
                float rotation=((Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0))*75*dt;
                angle+=rotation; offset=Rotate(offset,rotation) + Vector2.left*(26*dt);
                int start=Mathf.FloorToInt(progress); float best=float.MaxValue, next=progress;
                for(int i=start;i<Mathf.Min(Samples,start+10);i++)
                {
                    Vector2 a=Rotate(contour[i],angle)+offset, b=Rotate(contour[i+1],angle)+offset;
                    float t=Mathf.Clamp01(Vector2.Dot(-a,b-a)/(b-a).sqrMagnitude);
                    float p=i+t; if(p<progress) continue;
                    float d=(a+(b-a)*t).magnitude;
                    if(d<best) {best=d;next=p;}
                }
                progress=next;
                errorTime=best>15 ? errorTime+dt : Mathf.Max(0,errorTime-dt*2);
                if(errorTime>.7f) {drawing=false;failed=true;message="轮廓偏离绘制点 / 可重新开始";}
                else if(progress>=Samples-.15f) StartPrinting();
            }
            if (Printing && systems.MainPowerOn)
            {
                printClock+=Time.deltaTime; PrintProgress=Mathf.Clamp01(printClock/18);
                if(material) material.SetFloat("_Progress", PrintProgress);
                if(PrintProgress>=1)
                {
                    Printing=false; systems.SetPrinter(false); head.localPosition=headRest; if(beam)beam.enabled=false;
                    Completed[selected]++; Save(); message="打印完成 / 可领取部件";
                    player.ShowNotice("打印完成："+Blueprints[selected]+" / 点击打印机领取");
                    if(CabinAudio.Instance) CabinAudio.Instance.Play(CabinSound.Switch,origin);
                }
            }
        }
        void LateUpdate()
        {
            if(!player) return;
            if(Printing && systems.MainPowerOn && !player.IsPaused)
            {
                float t=printClock;
                head.position=origin+facing*new Vector3(Mathf.Sin(t*5)*.12f,.16f,Mathf.Cos(t*5)*.12f);
                if(beam){beam.SetPosition(0,head.position+Vector3.down*.11f);beam.SetPosition(1,new Vector3(head.position.x,origin.y-.27f+PrintProgress*.12f,head.position.z));}
            }
            const float deployedAt = .52f;
            float deployment = Mathf.InverseLerp(0,deployedAt,blend);
            float cameraFocus = Mathf.InverseLerp(deployedAt,1,blend);
            if(screen)screen.Refresh(origin,facing,deployment,!Galaxies.Zyl.ZylScreenCurtain.IsOpaque,Focused&&cameraFocus>.98f&&!player.IsPaused);
            if(beam)beam.enabled=Printing&&systems.MainPowerOn;
            if(cameraFocus>0 && player.viewCamera)
            {
                var cam=player.viewCamera; float t=Mathf.SmoothStep(0,1,cameraFocus);
                Vector3 target=origin+facing*new Vector3(.08f,.125f,CabinFabricatorScreen.MountDepth-.875f);
                cam.transform.position=Vector3.Lerp(cam.transform.position,target,t);
                cam.transform.rotation=Quaternion.Slerp(cam.transform.rotation,facing,t);
                cam.fieldOfView=Mathf.Lerp(cam.fieldOfView,53,t);
            }
        }
        void StartPrinting()
        {
            drawing=false; Printing=true; printClock=0; PrintProgress=0; systems.SetPrinter(true); Close();
            if(part) { Destroy(part.GetComponent<MeshFilter>().sharedMesh); Destroy(part); } if(material) Destroy(material); if(beamMaterial)Destroy(beamMaterial);
            part=new GameObject("Fabricator | printed "+Blueprints[selected]);
            part.transform.SetPositionAndRotation(origin+facing*new Vector3(0,-.2625f,.028f),facing);
            part.transform.SetParent(head.parent,true);
            var mesh=new Mesh(); var vertices=new Vector3[(Samples+1)*4]; var triangles=new int[Samples*24]; int index=0;
            for(int i=0;i<=Samples;i++)
            {
                float t=i*Mathf.PI*2/Samples, r=.12f+(selected==1?.012f*Mathf.Sin(t*4):selected==2?.008f*Mathf.Sin(t*3):0);
                for(int j=0;j<4;j++) vertices[i*4+j]=new Vector3(Mathf.Sin(t)*(r-(j%2)*.035f),(j/2)*.12f,Mathf.Cos(t)*(r-(j%2)*.035f));
            }
            for(int i=0;i<Samples;i++) for(int j=0;j<4;j++)
            {
                int[] cycle={0,1,3,2}; int a=i*4+cycle[j],b=i*4+cycle[(j+1)%4],c=a+4,d=b+4;
                triangles[index++]=a;triangles[index++]=c;triangles[index++]=b;triangles[index++]=b;triangles[index++]=c;triangles[index++]=d;
            }
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(Resources.Load<Shader>("FabricatorLayer")); part.AddComponent<MeshRenderer>().sharedMaterial=material;
            material.SetFloat("_Progress",0);
            var beamObject=new GameObject("Active deposition beam");beamObject.transform.SetParent(part.transform,false);
            beam=beamObject.AddComponent<LineRenderer>();beam.positionCount=2;beam.startWidth=.004f;beam.endWidth=.002f;beam.useWorldSpace=true;
            beamMaterial=new Material(Shader.Find("Standard"));beamMaterial.color=new Color(.7f,.85f,.65f);beamMaterial.EnableKeyword("_EMISSION");beamMaterial.SetColor("_EmissionColor",new Color(.3f,1,.65f)*3);beam.sharedMaterial=beamMaterial;
            player.ShowNotice("轮廓闭合 / 自动打印已开始"); Save();
        }
        static Vector2 Rotate(Vector2 p,float degrees) {float a=degrees*Mathf.Deg2Rad;return new Vector2(p.x*Mathf.Cos(a)-p.y*Mathf.Sin(a),p.x*Mathf.Sin(a)+p.y*Mathf.Cos(a));}
        internal bool Drawing { get { return drawing; } }
        internal bool Failed { get { return failed; } }
        internal int Selected { get { return selected; } }
        internal string Message { get { return message; } }
        internal float TraceProgress { get { return progress; } }
        internal float TraceAngle { get { return angle; } }
        internal Vector2 TraceOffset { get { return offset; } }
        internal Vector2[] Points { get { return contour; } }
        public void SelectBlueprint(int index)
        {
            if(!Focused||Printing||drawing||index<0||index>=Blueprints.Length)return;
            selected=index;MakeContour();failed=false;
        }
        public void ClaimParts()
        {
            if(!Focused)return;
            if(HasPendingClaim && (Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))){DiscardPendingClaim();return;}
            var nav=GetComponent<NavigationMode>();
            bool mapped=nav && nav.Campaign!=null && nav.Campaign.items!=null && nav.Campaign.items.Length>0;
            if(!string.IsNullOrEmpty(receiptId)||mapped)
            {
                if(!nav){message="导航尚未就绪";return;}
                if(!string.IsNullOrEmpty(receiptId) && nav.Session!=null && !nav.Session.Blocked && !nav.IsGalaxyOverview && nav.Session.Save.voyageId!=receiptVoyage)
                {
                    message="有绑定旧航程的待领取部件；请先返回原航程。若已重开，按住 Shift 点击领取可丢弃旧批次，之后重新打印。";return;
                }
                if(string.IsNullOrEmpty(receiptId))
                {
                    if(!nav.PreparePrintedItems(Completed,out receiptVoyage,out receiptPayload,out receiptCounts)){message=nav.Message;return;}
                    receiptId=Guid.NewGuid().ToString("N");
                }
                if(!Save()){message="领取凭据保存失败，请重试";return;}
                if(!nav.ReceivePrintedItems(receiptVoyage,receiptId,receiptPayload)){message=nav.Message;return;}
                var oldId=receiptId;var oldVoyage=receiptVoyage;var oldPayload=receiptPayload;var oldCounts=receiptCounts;
                for(int i=0;i<3;i++){Inventory[i]+=receiptCounts[i];Completed[i]-=receiptCounts[i];}
                receiptId=receiptVoyage=receiptPayload=null;receiptCounts=null;
                if(!Save())
                {
                    for(int i=0;i<3;i++){Inventory[i]-=oldCounts[i];Completed[i]+=oldCounts[i];}
                    receiptId=oldId;receiptVoyage=oldVoyage;receiptPayload=oldPayload;receiptCounts=oldCounts;
                    message="航程已收货，领取记录保存失败；请重试完成同步";return;
                }
                message="已存入本航程物品库存";
                if(Array.Exists(Completed,n=>n>0))message+="；未绑定部件仍保留在打印机";
            }
            else
            {
                var amounts=(int[])Completed.Clone();
                for(int i=0;i<3;i++){Inventory[i]+=amounts[i];Completed[i]=0;}
                if(!Save()){for(int i=0;i<3;i++){Inventory[i]-=amounts[i];Completed[i]+=amounts[i];}message="库存保存失败，请重试";return;}
                message="已存入部件库存";
            }
            if(!Printing&&part&&Completed[selected]==0){Destroy(part.GetComponent<MeshFilter>().sharedMesh);Destroy(part);}
        }
        public void DiscardPendingClaim()
        {
            if(!Focused||string.IsNullOrEmpty(receiptId))return;
            var oldId=receiptId;var oldVoyage=receiptVoyage;var oldPayload=receiptPayload;var oldCounts=receiptCounts;
            for(int i=0;i<3;i++)Completed[i]-=oldCounts[i];
            receiptId=receiptVoyage=receiptPayload=null;receiptCounts=null;
            if(!Save())
            {
                for(int i=0;i<3;i++)Completed[i]+=oldCounts[i];
                receiptId=oldId;receiptVoyage=oldVoyage;receiptPayload=oldPayload;receiptCounts=oldCounts;
                message="重置未保存，请重试";return;
            }
            message="旧领取批次已丢弃；请重新打印。旧航程已到账的物品不变。";
        }
        internal bool HasPendingClaim {get{return !string.IsNullOrEmpty(receiptId);}}
        void OnDestroy(){if(screen)Destroy(screen.gameObject);if(part){var f=part.GetComponent<MeshFilter>();if(f)Destroy(f.sharedMesh);Destroy(part);}if(material)Destroy(material);if(beamMaterial)Destroy(beamMaterial);}
    }
}
