using System.Collections.Generic;
using UnityEngine;
namespace AstraCabin {
 public sealed class PortalSetpiece : MonoBehaviour {
  public bool Started {get;private set;} public bool Complete {get;private set;}
  public float Elapsed {get;private set;}
  SetpieceDirector d; Transform root,gate,destination,tunnel; Material glow,blue,dark;
  readonly List<Transform> planets=new List<Transform>();
  readonly float[] spinRates={1.2f,.45f,.18f};
  readonly List<Material> mats=new List<Material>(); readonly List<LineRenderer> rings=new List<LineRenderer>();
  public void Initialize(SetpieceDirector value){d=value;}
  Material Mat(string shader,Color color){var m=new Material(Shader.Find(shader));m.color=color;mats.Add(m);return m;}
  Transform Node(string name,Transform parent){var g=new GameObject(name);g.layer=26;g.transform.SetParent(parent,false);return g.transform;}
  LineRenderer Ring(Transform parent,float radius,Material mat,float width){var t=Node("Energy ring",parent);var l=t.gameObject.AddComponent<LineRenderer>();l.useWorldSpace=false;l.loop=true;l.positionCount=128;l.sharedMaterial=mat;l.widthMultiplier=width;for(int j=0;j<128;j++){float a=j*Mathf.PI*2/128;l.SetPosition(j,new Vector3(0,Mathf.Cos(a)*radius,Mathf.Sin(a)*radius));}return l;}
  public void Show(){Clear();root=Node("11 | 泰德野里_误入星际传送门",null);glow=Mat("Astra/SetpieceGlow",new Color(.16f,.65f,1,1));blue=Mat("Astra/SetpieceGlow",new Color(.65f,.25f,1,1));dark=new Material(Resources.Load<Shader>("PortalHorizon"));mats.Add(dark);
   gate=Node("Approaching portal",root);for(int i=0;i<12;i++){var l=Ring(gate,3+i*.055f,i%3==0?blue:glow,.025f+i%3*.018f);l.transform.localRotation=Quaternion.Euler(i*13,0,0);rings.Add(l);}
   var disc=GameObject.CreatePrimitive(PrimitiveType.Sphere);Destroy(disc.GetComponent<Collider>());disc.name="Opening event horizon";disc.layer=26;disc.transform.SetParent(gate,false);disc.transform.localScale=new Vector3(.02f,5.95f,5.95f);disc.GetComponent<Renderer>().sharedMaterial=dark;
   tunnel=Node("Interstellar transit",root);for(int i=0;i<42;i++){var l=Ring(tunnel,2.8f+(i%4)*.18f,i%2==0?glow:blue,.035f);l.transform.localPosition=new Vector3(3+i*2,1.65f,0);}
   destination=Node("Distant planetary system",root);
   Color[] colors={new Color(1f,.53f,.29f),new Color(.95f,.94f,.91f),new Color(.65f,.58f,.49f)};
   Vector3[] positions={new Vector3(38,-24,-41),new Vector3(88,12,13),new Vector3(125,19,-30)};
   float[] diameters={46,7,4.5f};
   for(int i=0;i<3;i++){
    var p=GameObject.CreatePrimitive(PrimitiveType.Sphere);Destroy(p.GetComponent<Collider>());
    p.name=i==0?"Near red planet":"Distant planet "+i;p.layer=26;p.transform.SetParent(destination,false);
    p.transform.localPosition=positions[i];p.transform.localScale=Vector3.one*diameters[i];
    var surface=new Material(Resources.Load<Shader>("PortalPlanet"));surface.color=colors[i];
    surface.mainTexture=Resources.Load<Texture2D>("PortalCraterAlbedo");surface.SetTextureScale("_MainTex",Vector2.one);
    surface.SetTextureOffset("_MainTex",new Vector2(i*.231f,0));
    p.transform.localRotation=Quaternion.Euler(0,i*71,13+i*9);mats.Add(surface);
    p.GetComponent<Renderer>().sharedMaterial=surface;planets.Add(p.transform);
   }
   gate.gameObject.SetActive(false);tunnel.gameObject.SetActive(false);destination.gameObject.SetActive(false);
   d.computer.SetEventAlert("舷窗检测到不明物体","舷窗外检测到不明物体，引擎动力损坏无法躲避");d.computer.Open();d.PlayCue("alert",.65f);
  }
  public void View(){if(Started)return;Started=true;Elapsed=0;d.computer.ClearEventAlert();d.computer.Close();d.controller.SnapToWall(1);d.controller.EnterWindowView();d.controller.cinematicWindowLock=true;gate.gameObject.SetActive(true);Animate();}
  void Update(){
   if(!Started||d.controller.IsPaused||d.controller.ComputerOpen)return;
   if(!Complete){Elapsed+=Time.deltaTime;Animate();}
   if(Elapsed>=32)for(int i=0;i<planets.Count;i++)planets[i].Rotate(Vector3.up,spinRates[i]*Time.deltaTime,Space.Self);
  }
  void Animate(){float t=Elapsed;dark.SetFloat("_Clock",t);gate.position=new Vector3(Mathf.Lerp(38,2.8f,Mathf.SmoothStep(0,1,t/20)),1.65f,0);float opening=Mathf.SmoothStep(.20f,1.65f,Mathf.InverseLerp(5,15,t));gate.localScale=new Vector3(1,opening,opening);if(t>20){float pull=Mathf.SmoothStep(0,1,(t-20)/3);gate.position=new Vector3(Mathf.Lerp(2.8f,2.2f,pull),1.65f,0);gate.localScale*=1+pull*4;}
   bool warp=t>=21.5f&&t<32;dark.SetFloat("_TransitBlend",Mathf.SmoothStep(0,1,Mathf.InverseLerp(21.5f,23,t)));gate.gameObject.SetActive(t<23);tunnel.gameObject.SetActive(warp);if(d.stars)d.stars.gameObject.SetActive(!warp);if(warp){for(int i=0;i<tunnel.childCount;i++)tunnel.GetChild(i).localPosition=new Vector3(2+Mathf.Repeat(i*2-(t-23)*24,84),1.65f,0);d.Shake(.18f,.15f);}
   destination.gameObject.SetActive(t>=32);if(t>=35){Complete=true;d.controller.cinematicWindowLock=false;d.controller.ShowNotice("已穿越传送门 / 抵达未知星域");}
  }
  public void Clear(){planets.Clear();if(d)d.controller.cinematicWindowLock=false;Started=false;Complete=false;Elapsed=0;if(root){root.gameObject.SetActive(false);Destroy(root.gameObject);}foreach(var m in mats)if(m)Destroy(m);mats.Clear();rings.Clear();}
  void OnDestroy(){Clear();}
 }
}
