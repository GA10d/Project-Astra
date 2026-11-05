using System;
using System.Collections.Generic;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl {
 public sealed class MihuoerEncounter : MonoBehaviour {
  public enum Step { WaitingForView,Arrival,Invitation,Greeting,Story,Finished }
  public Step Phase {get;private set;}
  public bool Active {get;private set;}
  public bool Complete {get;private set;}
  public float StoryTime {get;private set;}
  public bool WaitingForView {get{return Active&&Phase==Step.WaitingForView;}}
  public RenderTexture Video {get;private set;}
  public string ActorAnimation {get;private set;}
  readonly string[] actorClips={"SeatedWave","SeatedTalk","Explain","Think","Concern"};readonly float[] blendFrom=new float[5];string talkTarget;
  public string CreatureAnimation {get;private set;}
  public float JawOpenness {get;private set;}
  public float CreatureDistance {get;private set;}
  public float CreatureScale {get{return beast?beast.localScale.x:0;}}
  public const float MouthTriggerDistance=16500f;
  float mouthStartedAt=-1,engulfStartedAt=-1;
  ZylPresentationParameters p=new ZylPresentationParameters();
  public bool LoadingEnvironment {get;private set;}
  public float TransitionAlpha {get{return LoadingEnvironment?1:Phase==Step.Finished?1-Mathf.SmoothStep(0,1,finishTime/p.fadeInDuration):0;}}
  float blackElapsed; ZylScreenCurtain screenCurtain;
  public float MouthClearance {get;private set;}
  public bool Swallowing {get{return engulfStartedAt>=0;}}
  bool Concerned {get{return mouthStartedAt>=0&&(CreatureDistance<=p.concernDistance||engulfStartedAt>=0);}}
  LoopingStarfield callStars;Light creatureKey,mouthSearch,mouthBounce;
  public bool CallStarsReady {get{return callStars&&callStars.Initialized&&callStars.gameObject.activeInHierarchy;}}
  public bool MouthLightReady {get{return mouthSearch&&mouthSearch.enabled&&mouthSearch.intensity>0;}}
  ConferenceCallSurface callSurface;float callElapsed;
  MihuoerDialoguePlayer dialogue; Vector2 dialogueReplyScroll; string captionKey;
  public bool DialogueInterrupted {get{return dialogue!=null&&dialogue.Interrupted;}}
  public ConferenceCallSurface CallSurface {get{return callSurface;}}
  public Vector3 MouthViewport {get{return videoCamera&&mouthAnchor?videoCamera.WorldToViewportPoint(mouthAnchor.position):Vector3.zero;}}
  public bool MouthAnchorReady {get{return mouthAnchor;}}
  SetpieceDirector d;Transform root,ship,pilot,beast,mouthAnchor;Camera videoCamera;Animation actorAnimation,beastAnimation;
  readonly List<Material> materials=new List<Material>();float arrival,clock,finishTime,talkBlend;bool callRequested,invitationPressed;int callRequestFrame=-1;
  static readonly Rect InviteButton=new Rect(1330,370,210,50);Font uiFont;GUIStyle text,title,button;
  public void Initialize(SetpieceDirector owner){d=owner;screenCurtain=GetComponent<ZylScreenCurtain>()??gameObject.AddComponent<ZylScreenCurtain>();screenCurtain.Initialize(owner);}
  public void Show(){
   Clear();p=ZylPresentationParameters.Load(d.computer.Navigation && d.computer.Navigation.ActiveGalaxyId=="zyl");Active=true;Phase=Step.WaitingForView;
   root=new GameObject("12 | 泰德野里_初遇米霍尔").transform;
   d.visitors.ShowVisitor();d.visitors.enabled=false;ship=d.visitors.VisitorPilot.parent;d.visitors.VisitorPilot.gameObject.SetActive(false);
   foreach(var shipLight in d.visitors.GeneratedRoot.GetComponentsInChildren<Light>())shipLight.intensity*=.25f;
   foreach(var renderer in ship.GetComponentsInChildren<Renderer>())if(renderer.sharedMaterial&&renderer.sharedMaterial.name.Contains("Pale cyan canopy")){var glass=new Material(renderer.sharedMaterial);glass.color=new Color(.08f,.22f,.25f,.045f);glass.SetFloat("_Glossiness",.15f);glass.SetFloat("_SpecularHighlights",0);glass.SetFloat("_GlossyReflections",0);renderer.sharedMaterial=glass;materials.Add(glass);}
   ship.localScale=Vector3.one*1.08f;ship.position=new Vector3(9,1.05f,12);
   var actor=Resources.Load<GameObject>("Galaxies/Zyl/Encounter/Mihuoer");
   var creature=Resources.Load<GameObject>("Galaxies/Zyl/Encounter/Taideler");
   if(!actor||!creature)throw new InvalidOperationException("初遇米霍尔模型资源未导入");
   pilot=Instantiate(actor,ship).transform;pilot.name="米霍尔 | Blender seated animation";
   pilot.localPosition=new Vector3(0,.06f,-.12f);pilot.localRotation=Quaternion.Euler(0,180,0);pilot.localScale=Vector3.one*1.12f;
   Layer(pilot.gameObject);actorAnimation=pilot.GetComponentInChildren<Animation>();PrepareMaterials(pilot);if(d.computer.Navigation&&d.computer.Navigation.Countdown){d.computer.Navigation.Countdown.ReloadParameters();d.computer.Navigation.Countdown.BeginEncounter(d.computer.Navigation.Session?.Engine.Pending?.id??"encounter12");MihuoerCountdownSkin.Attach(pilot.gameObject,d.computer.Navigation.Countdown,p);}
   beast=Instantiate(creature,root).transform;beast.name="泰德勒尔 | recessed mouth animation collection";beast.localRotation=Quaternion.Euler(0,-90,0);Layer(beast.gameObject,27);beastAnimation=beast.GetComponentInChildren<Animation>();PrepareMaterials(beast,true);beast.gameObject.SetActive(false);
   foreach(var child in beast.GetComponentsInChildren<Transform>(true))if(child.name=="Mouth_Center")mouthAnchor=child;
   if(!mouthAnchor)throw new InvalidOperationException("巨兽缺少口部定位锚点");
   callSurface=new ConferenceCallSurface{Title="米霍尔 · 视频通话",Speaker="米霍尔",Participants=new[]{"米霍尔"},ReplyText=p.playerReplyText};callSurface.DrawCaptions=DrawDialogueCaptions;callSurface.Reply=Reply;callSurface.SetPaused=value=>callSurface.Paused=value;callSurface.Leave=()=>d.computer.OpenApp("navigation");
   var light=new GameObject("Encounter exterior key");light.transform.SetParent(root,false);light.transform.position=new Vector3(2,5,-4);var lamp=light.AddComponent<Light>();lamp.type=LightType.Point;lamp.range=130;lamp.intensity=.8f;lamp.cullingMask=1<<26;lamp.shadows=LightShadows.None;
   var vc=new GameObject("ASTRA LINK live exterior camera");vc.transform.SetParent(root,false);videoCamera=vc.AddComponent<Camera>();videoCamera.enabled=false;videoCamera.cullingMask=(1<<26)|(1<<27);videoCamera.nearClipPlane=.05f;videoCamera.farClipPlane=p.videoFarClip;videoCamera.fieldOfView=p.videoFov;videoCamera.clearFlags=CameraClearFlags.SolidColor;videoCamera.backgroundColor=new Color(.005f,.01f,.025f);
   Video=new RenderTexture(1280,720,24){name="Mihuoer live call"};Video.Create();videoCamera.targetTexture=Video;videoCamera.aspect=16f/9f;CreateCallEnvironment();
   d.computer.SetEventAlert("舷窗检测到未知身份生物","舷窗检测到未知身份生物");d.computer.Open();d.PlayCue("alert",.6f);
  }
  void Layer(GameObject value,int layer=26){foreach(var t in value.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
  void PrepareMaterials(Transform value,bool preserveSurface=false){foreach(var renderer in value.GetComponentsInChildren<Renderer>(true)){
   var shared=renderer.sharedMaterials;for(int i=0;i<shared.Length;i++){if(!shared[i])continue;var m=new Material(shared[i]);m.shader=Shader.Find("Standard");if(!preserveSurface)m.SetFloat("_Glossiness",.23f);m.EnableKeyword("_EMISSION");float glow=preserveSurface?(m.name.Contains("Mucosa")?.012f:.035f):.16f;m.SetColor("_EmissionColor",new Color(glow,glow,glow));if(m.mainTexture)m.SetTexture("_EmissionMap",m.mainTexture);materials.Add(m);shared[i]=m;}renderer.sharedMaterials=shared;
  }}
  void Sample(Animation animation,string clip,float time){if(!animation||!animation.GetClip(clip))return;animation.enabled=false;animation.Play(clip);var state=animation[clip];state.speed=0;state.time=Mathf.Repeat(time,Mathf.Max(.1f,state.length));animation.Sample();}
  public void View(){if(!WaitingForView)return;d.computer.ClearEventAlert();d.computer.Close();d.controller.SnapToWall(1);d.controller.EnterWindowView();d.controller.cinematicWindowLock=true;Phase=Step.Arrival;arrival=0;}
  void RequestCall(){callRequested=true;callRequestFrame=Time.frameCount;}
  public void AcceptCall(){if(!Active||LoadingEnvironment||Phase<Step.Invitation||Complete)return;if(Phase==Step.Invitation){ Phase=Step.Greeting;talkBlend=0; }d.controller.SnapToWall(0);d.computer.Open();d.computer.OpenApp("encounter");RenderVideo();}
  public void Reply(){if(Phase!=Step.Greeting)return;Phase=Step.Story;StoryTime=0;dialogue=new MihuoerDialoguePlayer(p.freeDialogue,(i,a)=>ApplyTimer(i,"line",a),(i,j,a)=>ApplyTimer(i,"reply",a));dialogueReplyScroll=Vector2.zero;callSurface.Transcript.Add(new ConferenceTranscriptLine{Sender="探测舱 04（我）",Message=callSurface.ReplyText,Local=true});beast.gameObject.SetActive(true);d.PlayCue("echo",.35f);}
  void ApplyTimer(int line,string phase,CountdownAction action){var nav=d.computer.Navigation;if(nav&&nav.Countdown)nav.Countdown.Apply(action,(nav.Session?.Engine.Pending?.id??"encounter12")+"/free/"+(string.IsNullOrEmpty(p.freeDialogue[line].timerKey)?line.ToString():p.freeDialogue[line].timerKey)+"/"+phase);}
  void Update(){
   if(!Active||d.controller.IsPaused)return;
   float dt=Time.deltaTime;
   if(LoadingEnvironment){
    blackElapsed+=dt;
    // Do not start scene IO until an opaque full-screen Repaint has actually occurred.
    if(screenCurtain && screenCurtain.OpaquePaintedFrame>=0 && Time.frameCount>screenCurtain.OpaquePaintedFrame){
     var exterior=d.computer.Navigation?d.computer.Navigation.Exterior:null;
     bool ready=!exterior||exterior.AwaitSwallowEnvironment();
     if(ready && blackElapsed>=p.blackHoldDuration)FinishInStomach();
    }
    return;
   }
   if(!d.computer.IsOpen && Phase>=Step.Invitation && !Complete){
    float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
    var pointer=new Vector2(Input.mousePosition.x/scale,(Screen.height-Input.mousePosition.y)/scale);
    if(Input.GetMouseButtonDown(0))invitationPressed=InviteButton.Contains(pointer);
    if(Input.GetMouseButtonUp(0)){if(invitationPressed&&InviteButton.Contains(pointer))RequestCall();invitationPressed=false;}
   }else invitationPressed=false;
   if(callRequested&&Time.frameCount>callRequestFrame){callRequested=false;AcceptCall();}
   if(Phase==Step.Arrival||Phase==Step.Invitation){if(d.computer.IsOpen)return;arrival+=dt;clock+=dt;float blend=Mathf.SmoothStep(0,1,arrival/p.visitorArrivalDuration);ship.position=Vector3.Lerp(new Vector3(9,1.05f,12),new Vector3(7.6f,1.05f,0),blend);ship.position+=Vector3.up*Mathf.Sin(clock*.7f)*.025f;Sample(actorAnimation,"SeatedWave",clock);ActorAnimation="SeatedWave";if(arrival>=p.visitorInvitationTime&&Phase==Step.Arrival){Phase=Step.Invitation;d.PlayCue("alert",.6f);}}
   if(Phase==Step.Greeting||Phase==Step.Story){if(!d.computer.IsOpen||d.computer.CurrentApp!="encounter"||callSurface.Paused)return;callElapsed+=dt;clock+=dt;if(Phase==Step.Story){StoryTime+=dt;AnimateBeast();dialogue.Tick(dt,Concerned);if(MouthClearance<=p.loadDistance){LoadingEnvironment=true;blackElapsed=0;screenCurtain.ResetPaintConfirmation();}}SampleTalk();SyncDialogueCaption();RenderVideo();}
   if(Phase==Step.Finished)finishTime+=dt;
  }
  void FinishInStomach(){
   Complete=true;LoadingEnvironment=false;Phase=Step.Finished;finishTime=0;
   callStars.gameObject.SetActive(false);creatureKey.enabled=mouthSearch.enabled=mouthBounce.enabled=false;
   d.computer.CloseEncounterApp(true);d.computer.Close();d.controller.cinematicWindowLock=false;
   d.controller.SnapToWall(1); // Normal central cabin view facing the window wall, not a close-up.
   d.visitors.GeneratedRoot.gameObject.SetActive(false);beast.gameObject.SetActive(false);
  }
  void AnimateBeast(){
   float t=StoryTime,journey=t<2f?t*t/4f:t-1f,progress=Mathf.Clamp01(journey/p.approachDuration);
   float swallow=engulfStartedAt<0?0:Mathf.SmoothStep(0,1,(t-engulfStartedAt)/p.swallowDuration);
   // Logarithmic clearance gives steady apparent growth instead of a tiny distant hold followed by a rush.
   float size=p.beastScale,clearance=Mathf.Exp(Mathf.Lerp(Mathf.Log(p.initialClearance),Mathf.Log(p.finalClearance),progress));
   float offAxis=Mathf.SmoothStep(0,1,Mathf.InverseLerp(250,1600,clearance));
   beast.position=new Vector3(ship.position.x+4f*size+clearance-p.swallowAdvance*swallow,2.15f-1.7f*size+clearance*.32f*offAxis,-.65f-clearance*.1f*offAxis);beast.localScale=Vector3.one*size;
   CreatureDistance=Vector3.Distance(beast.position,ship.position);
   MouthClearance=beast.position.x-ship.position.x-4f*size;
   if(mouthStartedAt<0&&CreatureDistance<=p.mouthTriggerDistance)mouthStartedAt=t;
   // Hold the open-flight animation and continue approaching until the mouth actually reaches us.
   if(engulfStartedAt<0&&mouthStartedAt>=0&&t-mouthStartedAt>=beastAnimation["OpenTransition"].length/p.mouthPlaybackSpeed&&mouthAnchor.position.x-ship.position.x<=p.swallowDistance)engulfStartedAt=t;
   float mouthTime=mouthStartedAt<0?-1:t-mouthStartedAt;
   var flight=beastAnimation["Flight"];var opening=beastAnimation["OpenTransition"];var openFlight=beastAnimation["OpenFlight"];
   if(flight==null||opening==null||openFlight==null)throw new InvalidOperationException("泰德勒尔三段动画不完整");
   beastAnimation.enabled=false;flight.enabled=opening.enabled=openFlight.enabled=false;flight.speed=opening.speed=openFlight.speed=0;
   if(mouthTime<0){CreatureAnimation="Flight";flight.enabled=true;flight.weight=1;flight.time=Mathf.Repeat(t,flight.length);}
   else if(mouthTime<opening.length/p.mouthPlaybackSpeed){CreatureAnimation="OpenTransition";float blend=Mathf.SmoothStep(0,1,mouthTime/.5f);flight.enabled=blend<1;flight.weight=1-blend;flight.time=Mathf.Repeat(t,flight.length);opening.enabled=true;opening.weight=blend;opening.wrapMode=WrapMode.ClampForever;opening.time=Mathf.Clamp(mouthTime*p.mouthPlaybackSpeed,0,opening.length);}
   else{CreatureAnimation="OpenFlight";openFlight.enabled=true;openFlight.weight=1;openFlight.time=Mathf.Repeat(mouthTime-opening.length/p.mouthPlaybackSpeed,openFlight.length);}
   beastAnimation.Sample();JawOpenness=0;
   foreach(var renderer in beast.GetComponentsInChildren<SkinnedMeshRenderer>())for(int i=0;i<renderer.sharedMesh.blendShapeCount;i++)if(renderer.sharedMesh.GetBlendShapeName(i).EndsWith("Jaw_Open"))JawOpenness=Mathf.Max(JawOpenness,renderer.GetBlendShapeWeight(i)/100f);
   CenterMouth();CreatureDistance=Vector3.Distance(beast.position,ship.position);MouthClearance=mouthAnchor.position.x-ship.position.x;
   UpdateEncounterLighting();
  }
  void CenterMouth(){
   ConfigureVideoCamera();Vector3 center=mouthAnchor.position;
   float depthAlongX=center.x-videoCamera.transform.position.x;
   Vector3 target=videoCamera.transform.position+videoCamera.transform.forward*(depthAlongX/Mathf.Max(.01f,videoCamera.transform.forward.x));
   Vector3 correction=target-center;correction.x=0;
   beast.position+=correction*Mathf.SmoothStep(0,1,JawOpenness);
  }
  void CreateCallEnvironment(){
   var sky=new GameObject("ASTRA LINK | distant starfield");sky.transform.SetParent(root,false);sky.layer=26;
   callStars=sky.AddComponent<LoopingStarfield>();callStars.volumeCenter=new Vector3(28000,0,0);callStars.size=new Vector3(2500,44000,56000);callStars.starCount=(int)p.starCount;callStars.pointSize=p.starSize;callStars.seed=73219;callStars.animate=false;callStars.velocity=Vector3.zero;callStars.starShader=d.stars?d.stars.starShader:Shader.Find("Particles/Standard Unlit");callStars.Initialize();
   creatureKey=EncounterLight("Distant cold starlight",LightType.Directional,new Color(.62f,.72f,1f),p.coldLight,0);
   creatureKey.transform.rotation=Quaternion.LookRotation(new Vector3(1,-.35f,.22f));
   mouthSearch=EncounterLight("Ship mouth searchlight",LightType.Spot,new Color(.72f,.85f,1f),0,6500);mouthSearch.spotAngle=65;
   mouthBounce=EncounterLight("Warm reflected cavity light",LightType.Point,new Color(1f,.42f,.3f),0,4000);
  }
  Light EncounterLight(string name,LightType type,Color color,float intensity,float range){var go=new GameObject(name);go.transform.SetParent(root,false);var lamp=go.AddComponent<Light>();lamp.type=type;lamp.color=color;lamp.intensity=intensity;lamp.range=range;lamp.cullingMask=1<<27;lamp.renderMode=LightRenderMode.ForcePixel;lamp.shadows=LightShadows.None;return lamp;}
  void UpdateEncounterLighting(){
   float near=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(120,2500,MouthClearance));
   creatureKey.intensity=p.coldLight*(1-near);
   mouthSearch.transform.position=ship.position+new Vector3(-2,1.6f,0);mouthSearch.transform.rotation=Quaternion.LookRotation(Vector3.right);
   mouthSearch.intensity=Mathf.Lerp(.3f,p.mouthSearchLight,near)*Mathf.SmoothStep(0,1,JawOpenness);
   mouthBounce.transform.position=ship.position+new Vector3(450,160,-280);mouthBounce.intensity=p.mouthBounceLight*near*JawOpenness;
  }
  void SampleTalk(){
   if(!actorAnimation)return;string target=Phase==Step.Greeting?"SeatedTalk":DialogueInterrupted?"Concern":dialogue==null||dialogue.Ended||dialogue.WaitingForReply?"Think":dialogue.Index%2==0?"Explain":"SeatedTalk";
   actorAnimation.enabled=false;
   if(talkTarget!=target){for(int i=0;i<actorClips.Length;i++){var state=actorAnimation[actorClips[i]];blendFrom[i]=state!=null&&state.enabled?state.weight:0;}talkTarget=target;talkBlend=0;}
   talkBlend=Mathf.Min(1,talkBlend+Time.deltaTime/p.actorBlendDuration);float alpha=Mathf.SmoothStep(0,1,talkBlend);
   for(int i=0;i<actorClips.Length;i++){var state=actorAnimation[actorClips[i]];if(state==null)throw new InvalidOperationException("米霍尔动作缺失："+actorClips[i]);state.weight=Mathf.Lerp(blendFrom[i],actorClips[i]==target?1:0,alpha);state.enabled=state.weight>.0001f;state.speed=0;state.time=Mathf.Repeat(clock,state.length);}
   ActorAnimation=target;actorAnimation.Sample();
  }
  void ConfigureVideoCamera(){videoCamera.transform.position=ship.position+new Vector3(-3.1f,1.15f,0);videoCamera.transform.LookAt(ship.position+Vector3.up*.85f);}
  public void RenderVideo(){if(!videoCamera||!ship)return;ConfigureVideoCamera();bool fog=RenderSettings.fog;try{RenderSettings.fog=false;videoCamera.Render();}finally{RenderSettings.fog=fog;}}
  void Styles(Font supplied=null){if(text!=null)return;uiFont=supplied?supplied:Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},22);text=new GUIStyle(GUI.skin.label){font=uiFont,fontSize=20,wordWrap=true};text.normal.textColor=new Color(.83f,.9f,.83f);title=new GUIStyle(text){fontSize=24};button=new GUIStyle(GUI.skin.button){font=uiFont,fontSize=20,wordWrap=true};}
  void Fill(Rect r,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
  void SyncDialogueCaption(){
   if(callSurface==null)return;
   string key=Phase==Step.Greeting?"greeting":DialogueInterrupted?"interrupted":dialogue==null||dialogue.Ended?"ended":"line"+dialogue.Index;
   string line=Phase==Step.Greeting?p.greetingText:DialogueInterrupted?p.concernText:dialogue?.Current?.text??"";
   callSurface.Caption=line;callSurface.Choosing=Phase==Step.Greeting||(!LoadingEnvironment&&dialogue!=null&&dialogue.WaitingForReply);
   if(key!=captionKey){captionKey=key;dialogueReplyScroll=Vector2.zero;if(!string.IsNullOrEmpty(line))callSurface.Transcript.Add(new ConferenceTranscriptLine{Sender="米霍尔",Message=line});}
  }
  public bool ChooseDialogueReply(int option){
   if(Phase!=Step.Story||LoadingEnvironment||d.controller.IsPaused||callSurface.Paused||Concerned||dialogue==null||!dialogue.WaitingForReply||option<0||option>=dialogue.Current.replies.Length)return false;
   string reply=dialogue.Current.replies[option];if(!dialogue.Choose(option))return false;
   callSurface.Transcript.Add(new ConferenceTranscriptLine{Sender="探测舱 04（我）",Message=reply,Local=true});SyncDialogueCaption();return true;
  }
  void DrawDialogueCaptions(Rect screen){
   bool greeting=Phase==Step.Greeting;
   string[] options=greeting?new[]{p.playerReplyText}:!LoadingEnvironment&&dialogue!=null&&dialogue.WaitingForReply?dialogue.Current.replies:new string[0];
   if(options.Length==0&&(!callSurface.CaptionsVisible||string.IsNullOrEmpty(callSurface.Caption)))return;
   float width=Mathf.Min(820,screen.width-32),inner=width-36;
   string caption=callSurface.CaptionsVisible||options.Length>0?"米霍尔："+callSurface.Caption:"";
   float captionHeight=Mathf.Max(32,text.CalcHeight(new GUIContent(caption),inner));
   float content=captionHeight+18;foreach(var option in options)content+=Mathf.Max(42,button.CalcHeight(new GUIContent(option),inner))+8;
   float height=Mathf.Min(content+18,screen.height*.65f);
   var panel=new Rect(screen.center.x-width/2,screen.yMax-height-39,width,height);Fill(panel,new Color(.025f,.025f,.03f,.9f));
   var area=new Rect(panel.x+8,panel.y+8,width-16,height-16);
   dialogueReplyScroll=GUI.BeginScrollView(area,dialogueReplyScroll,new Rect(0,0,inner,content));
   GUI.Label(new Rect(0,0,inner,captionHeight),caption,text);float y=captionHeight+18;
   int selected=-1;bool enabled=GUI.enabled;GUI.enabled=enabled&&!callSurface.Paused&&!LoadingEnvironment;
   for(int i=0;i<options.Length;i++){float h=Mathf.Max(42,button.CalcHeight(new GUIContent(options[i]),inner));if(GUI.Button(new Rect(0,y,inner,h),options[i],button))selected=i;y+=h+8;}
   GUI.enabled=enabled;GUI.EndScrollView();
   // Mutate after closing the IMGUI scroll group, so the selected line cannot change layout midway.
   if(selected>=0){if(greeting)Reply();else ChooseDialogueReply(selected);}
  }
  public void DrawCall(Rect bounds,Font supplied){
   Styles(supplied);callSurface.Video=Video;callSurface.Elapsed=callElapsed;SyncDialogueCaption();
   d.computer.Meeting.DrawCallSurface(bounds,supplied,callSurface);
  }
  void OnGUI(){if(!Active)return;Styles();Matrix4x4 matrix=GUI.matrix;int depth=GUI.depth;bool oldEnabled=GUI.enabled;GUI.depth=-80;GUI.enabled=oldEnabled&&!d.controller.IsPaused;float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
   if(!LoadingEnvironment&&!d.computer.IsOpen&&Phase>=Step.Invitation&&!Complete){GUI.depth=-80;Fill(new Rect(1150,235,420,210),new Color(.025f,.06f,.055f,.97f));GUI.Label(new Rect(1170,255,380,92),"有不明身份生物向你发出通话邀请",text);if(GUI.Button(InviteButton,Phase==Step.Invitation?"◉  查看":"继续通话",button))RequestCall();if(callRequested)GUIUtility.hotControl=0;}
   GUI.matrix=matrix;GUI.depth=depth;GUI.enabled=oldEnabled;
  }
  public void Clear(){if(d&&d.computer)d.computer.CloseEncounterApp();if(d&&d.visitors)d.visitors.enabled=true;if(d&&d.controller)d.controller.cinematicWindowLock=false;if(root){root.gameObject.SetActive(false);Destroy(root.gameObject);}if(Video){Video.Release();Destroy(Video);}foreach(var m in materials)if(m)Destroy(m);materials.Clear();callStars=null;creatureKey=mouthSearch=mouthBounce=null;callSurface=null;callElapsed=0;dialogue=null;captionKey=null;dialogueReplyScroll=Vector2.zero;root=ship=pilot=beast=mouthAnchor=null;videoCamera=null;Video=null;LoadingEnvironment=false;blackElapsed=0;if(screenCurtain)screenCurtain.ResetPaintConfirmation();Active=Complete=false;arrival=clock=StoryTime=finishTime=talkBlend=0;callRequested=invitationPressed=false;callRequestFrame=-1;CreatureAnimation=ActorAnimation=talkTarget=null;Array.Clear(blendFrom,0,blendFrom.Length);JawOpenness=CreatureDistance=0;mouthStartedAt=engulfStartedAt=-1;MouthClearance=0;Phase=Step.WaitingForView;}
  void OnDestroy(){Clear();}
 }
}















