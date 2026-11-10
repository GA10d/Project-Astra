using System;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    public sealed class FoolsSetpiece : MonoBehaviour
    {
        public enum Step {WaitingForEnvironment,Playing,Returning,GroundDialogue,Walking,CrestDialogue,Orbit,Pullback,FinalHold,SceneFadeOut,SceneBlackHold,SceneFadeIn,ClosingDialogue,Finished,Error}
        public bool Active {get;private set;}
        public bool Complete {get;private set;}
        public string Error {get;private set;}
        public Step Phase {get;private set;}
        public FoolsStage Stage {get;private set;}
        public FoolsSlope Slope {get;private set;}
        public FoolsWalker Walker {get;private set;}
        public float Elapsed {get;private set;}
        public float WalkDistance {get;private set;}
        public float TransitionAlpha {get;private set;}
        public int ClosingCutFrame {get;private set;}=-1;
        public float SettlementFadeDuration {get{return parameters!=null?parameters.foolsSettlementFadeDuration:ZylPresentationParameters.Load().foolsSettlementFadeDuration;}}
        public static readonly Vector3 ClosingActorAuth=new Vector3(5.4003f,0,6.3208f);
        public static readonly Vector3 ClosingCameraAuth=new Vector3(-.4397f,1.55f,3.8074f);
        Vector3 closingGround,closingCamera,closingFacing;
        ZylScreenCurtain Curtain {get{return director?director.GetComponent<ZylScreenCurtain>():null;}}
        public bool WalkInterrupted {get;private set;}
        public float OrbitFraction {get;private set;}
        public float PullbackFraction {get;private set;}
        public string CurrentText {get{return dialogue?.Current?.text;}}
        SetpieceDirector director;
        ZylPresentationParameters parameters;
        MihuoerDialoguePlayer dialogue;
        float phaseTime, dialogueTime;
        FoolsActionTimeline actions;
        readonly ZylCameraEffectTrack cameraEffects = new ZylCameraEffectTrack();
        bool capturedUI,oldUI;
        Camera cabinCamera;
        bool cabinCameraWasEnabled;
        Font font;GUIStyle subtitle;Vector2 subtitleScroll;
        ZylStomachEnvironment Exterior {get{return director&&director.computer&&director.computer.Navigation?director.computer.Navigation.Exterior:null;}}
        public void Initialize(SetpieceDirector owner){director=owner;}
        public void Show()
        {
            Clear();parameters=ZylPresentationParameters.Load();actions=new FoolsActionTimeline(parameters);Active=true;Phase=Step.WaitingForEnvironment;
            if(!director.computer.Navigation||director.computer.Navigation.ActiveGalaxyId!="zyl"){Fail("都是傻瓜：请先进入zyl胃内航程。");return;}
            director.computer.Close();director.controller.SnapToWall(1);director.controller.cinematicWindowLock=true;
        }
        public void Resume(){if(!Active||Complete)return;if(Phase==Step.Error){Show();return;}director.computer.Close();director.controller.cinematicWindowLock=true;}
        void Fail(string message){Error=message;TransitionAlpha=0;Phase=Step.Error;RestoreView();director.controller.ShowNotice(message);}
        void Update(){Tick(Time.deltaTime);}
        void Enter(Step phase){Phase=phase;phaseTime=0;subtitleScroll=Vector2.zero;}
        void Dialogue(MihuoerDialogueLine[] lines,string key)
        {
            dialogueTime=0;
            cameraEffects.Bind(parameters.foolsCameraEffects, lines, key);
            dialogue=new MihuoerDialoguePlayer(lines,(i,action)=>{
                var nav=director.computer.Navigation;var line=lines[i];
                if(nav&&nav.Countdown)nav.Countdown.Apply(action,(nav.Session?.Engine.Pending?.id??"fools15")+"/fools/"+key+"/"+(string.IsNullOrEmpty(line.timerKey)?i.ToString():line.timerKey));
            });
        }
        MihuoerPose DialoguePose => MihuoerActing.Standing(dialogue,MihuoerPose.Explain,MihuoerPose.Idle);
        void SampleAction(bool atCrest, float dt, bool advance)
        {
            if (advance)
            {
                dialogueTime += dt;
                dialogue.Tick(dt, false);
            }
            var cue = actions.Sample(atCrest, dialogueTime, out float localTime);
            Walker.Sample(atCrest ? Slope.Length : 0f, false, cue?.Pose ?? DialoguePose, dt, cue, localTime);
        }
        public void Tick(float dt)
        {
            if(!Active||Complete||Phase==Step.Error)return;
            if(capturedUI)director.controller.showUI=oldUI&&director.controller.IsPaused;
            if(director.controller.IsPaused||director.computer.IsOpen)return;
            var exterior=Exterior;
            if(!exterior){Fail("都是傻瓜：胃内环境不可用。");return;}
            if(exterior.Error!=null){Fail(exterior.Error);return;}
            if(float.IsNaN(dt)||float.IsInfinity(dt))return;dt=Mathf.Max(0,dt);
            try
            {
                if(Phase==Step.WaitingForEnvironment)
                {
                    if(!exterior.Ready||exterior.Busy||ZylScreenCurtain.BlocksInput)return;
                    if(!exterior.MihuoerActor||!exterior.MihuoerShipAnchor){Fail("都是傻瓜：胃内飞船缺失。");return;}
                    exterior.MihuoerActor.SetSeatedOnCraft(true);
                    Stage=new FoolsStage(exterior.MihuoerActor.Craft,exterior.MihuoerShipAnchor.transform.parent,exterior.AuthoredToRuntime,director.controller.viewCamera,parameters.foolsMicrobeSpeed,parameters.cinematicCameraFilter);
                    oldUI=director.controller.showUI;capturedUI=true;director.controller.showUI=false;
                    cabinCamera=director.controller.viewCamera;
                    cabinCameraWasEnabled=cabinCamera.enabled;
                    cabinCamera.enabled=false;
                    Stage.Camera.enabled=true;
                    Enter(Step.Playing);
                }
                Elapsed+=dt;phaseTime+=dt;
                switch(Phase)
                {
                    case Step.Playing:
                        Stage.Sample(phaseTime,parameters.foolsStopHold);
                        if(phaseTime>=Stage.ReturnStartTime(parameters.foolsStopHold,parameters.foolsVirusHold))Enter(Step.Returning);
                        break;
                    case Step.Returning:
                        Stage.SampleReturn(phaseTime,parameters.foolsReturnDuration,parameters.foolsStopHold,parameters.foolsVirusHold);
                        if(phaseTime>=parameters.foolsReturnDuration)
                        {
                            Slope=new FoolsSlope(exterior.MihuoerShipAnchor.transform.parent,exterior.AuthoredToRuntime,exterior.MihuoerAnchor.transform.position);
                            Walker=new FoolsWalker(exterior.MihuoerActor,Slope);Dialogue(parameters.foolsGroundDialogue,"ground");Enter(Step.GroundDialogue);SampleAction(false,0f,false);GroundShot();
                        }
                        break;
                    case Step.GroundDialogue:
                        SampleAction(false,dt,true);GroundShot();
                        if(dialogue.Ended){Dialogue(parameters.foolsWalkDialogue,"walk");Enter(Step.Walking);}
                        break;
                    case Step.Walking:
                        WalkDistance=Mathf.Min(Slope.Length,phaseTime*parameters.foolsWalkSpeed);
                        // Position outranks every outstanding line, including an already-ended/empty sequence.
                        if(WalkDistance>=Slope.Length)
                        {
                            dialogue.Tick(0,true);WalkInterrupted=dialogue.Interrupted;
                            Dialogue(parameters.foolsCrestDialogue,"crest");SampleAction(true,dt,false);Enter(Step.CrestDialogue);HeadShot(0);
                        }
                        else {dialogue.Tick(dt,false);Walker.Sample(WalkDistance,true,MihuoerPose.Idle,dt);BackShot();}
                        break;
                    case Step.CrestDialogue:
                        SampleAction(true,dt,true);HeadShot(0);
                        if(phaseTime>=parameters.foolsFrontHold)Enter(Step.Orbit);
                        break;
                    case Step.Orbit:
                        SampleAction(true,dt,true);
                        OrbitPullShot(Mathf.Clamp01(phaseTime/parameters.foolsOrbitDuration),PullProgress(phaseTime-parameters.foolsOrbitDuration*.5f));
                        if(phaseTime>=parameters.foolsOrbitDuration)
                        {
                            if(phaseTime>=parameters.foolsOrbitDuration*.5f+parameters.foolsPullbackDuration)Enter(Step.FinalHold);
                            else Enter(Step.Pullback);
                        }
                        break;
                    case Step.Pullback:
                        SampleAction(true,dt,true);
                        OrbitPullShot(1,PullProgress(parameters.foolsOrbitDuration*.5f+phaseTime));
                        if(parameters.foolsOrbitDuration*.5f+phaseTime>=parameters.foolsPullbackDuration)Enter(Step.FinalHold);
                        break;
                    case Step.FinalHold:
                        SampleAction(true,dt,true);PullShot(1);
                        if(phaseTime>=parameters.foolsFinalHold&&dialogue.Ended){dialogue=null;TransitionAlpha=.001f;Curtain.ResetPaintConfirmation();Enter(Step.SceneFadeOut);}
                        break;
                    case Step.SceneFadeOut:
                        TransitionAlpha=Mathf.Max(.001f,Mathf.SmoothStep(0,1,Mathf.Clamp01(phaseTime/parameters.foolsSceneFadeDuration)));
                        if(phaseTime>=parameters.foolsSceneFadeDuration){TransitionAlpha=1;Enter(Step.SceneBlackHold);}
                        break;
                    case Step.SceneBlackHold:
                        if(phaseTime<.15f||!Curtain||Curtain.OpaquePaintedFrame<0||Time.frameCount<=Curtain.OpaquePaintedFrame)break;
                        closingGround=Slope.Ground(exterior.AuthoredToRuntime.MultiplyPoint3x4(ClosingActorAuth));
                        closingCamera=exterior.AuthoredToRuntime.MultiplyPoint3x4(ClosingCameraAuth);
                        closingFacing=closingCamera-closingGround;
                        Walker.SampleStandingAt(closingGround,closingFacing,MihuoerPose.Idle,dt);ClosingShot();ClosingCutFrame=Time.frameCount;
                        Dialogue(parameters.foolsClosingDialogue,"closing");Enter(Step.SceneFadeIn);
                        break;
                    case Step.SceneFadeIn:
                        Walker.SampleStandingAt(closingGround,closingFacing,MihuoerPose.Idle,dt);ClosingShot();
                        TransitionAlpha=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(phaseTime/parameters.foolsSceneFadeDuration));
                        if(phaseTime>=parameters.foolsSceneFadeDuration){TransitionAlpha=0;Enter(Step.ClosingDialogue);}
                        break;
                    case Step.ClosingDialogue:
                        dialogue.Tick(dt,false);
                        var closingCue=actions.SampleClosing(dialogue,out float closingCueTime);
                        Walker.SampleStandingAt(closingGround,closingFacing,DialoguePose,dt,cue:closingCue,cueTime:closingCueTime);
                        ClosingShot();if(dialogue.Ended)Finish();
                        break;
                }
                if (!Complete)
                {
                    Stage.UpdateBreathing(Elapsed);
                    switch (Phase)
                    {
                        case Step.GroundDialogue:
                        case Step.CrestDialogue:
                        case Step.Orbit:
                        case Step.Pullback:
                        case Step.FinalHold:
                            cameraEffects.Sample(Stage.Camera, dialogueTime);
                            break;
                        case Step.Walking:
                            cameraEffects.Sample(Stage.Camera, phaseTime);
                            break;
                        case Step.ClosingDialogue:
                            cameraEffects.SampleDialogue(Stage.Camera, dialogue);
                            break;
                        default:
                            cameraEffects.Clear();
                            break;
                    }
                }
            }
            catch(Exception e){Fail(e.Message);}
        }
        void CameraPose(Vector3 position,Vector3 target,float fov,bool resolve=true)
        {
            var camera=Stage.Camera;camera.transform.position=resolve?Slope.CameraPosition(position,target):position;camera.transform.rotation=Quaternion.LookRotation(target-camera.transform.position,Slope.Up);camera.fieldOfView=fov;
        }
        void GroundShot(){var root=Walker.Root;CameraPose(root.position+root.forward*2.5f+root.right*.7f+Slope.Up*1.2f,root.position+Slope.Up*.85f,47);}
        void BackShot(){CameraPose(Slope.FixedCameraPosition,Walker.Root.position+Slope.Up*.95f,45,false);}
        Vector3 HeadCenter {get{return Walker.Head.position+Slope.Up*.035f;}}
        void HeadShot(float degrees)
        {
            var center=HeadCenter;var offset=Quaternion.AngleAxis(degrees,Slope.Up)*(Walker.Root.forward*.86f);
            CameraPose(center+offset+Slope.Up*.035f,center,40,false);
        }
        float PullProgress(float elapsed){return Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/parameters.foolsPullbackDuration));}
        void PullShot(float progress){OrbitPullShot(1,progress);}
        void OrbitPullShot(float orbit,float pull)
        {
            OrbitFraction=orbit;PullbackFraction=pull;
            var root=Walker.Root;var center=HeadCenter;
            float rotationProgress=Mathf.SmoothStep(0,1,orbit);
            var direction=Quaternion.AngleAxis(180*rotationProgress,Slope.Up)*root.forward;
            var near=center+direction*.86f+Slope.Up*.035f;
            var far=root.position+direction*parameters.foolsOrbitEndDistance+Slope.Up*parameters.foolsOrbitEndHeight;
            var position=pull<=0?near:Slope.CameraPosition(Vector3.Lerp(near,far,pull),center);
            CameraPose(position,Vector3.Lerp(center,root.position+root.forward*1.8f+Slope.Up*1.05f,pull),Mathf.Lerp(40,parameters.foolsOrbitTargetFov,rotationProgress),false);
        }
        void ClosingShot(){CameraPose(closingCamera,Walker.Root.position+Slope.Up*.85f,44,false);}
        void Finish()
        {
            Complete=true;Phase=Step.Finished;RestoreView();director.controller.ExitWindowView();director.controller.SnapToWall(0);
            director.computer.Open();director.computer.OpenApp("navigation");
        }
        void OnGUI()
        {
            if(!Active||Complete||dialogue?.Current==null||Phase==Step.SceneFadeOut||Phase==Step.SceneBlackHold||Phase==Step.SceneFadeIn||director.controller.IsPaused||director.computer.IsOpen||ZylScreenCurtain.IsOpaque)return;
            if(subtitle==null){font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},23);subtitle=new GUIStyle(GUI.skin.label){font=font,fontSize=23,wordWrap=true,richText=false};subtitle.normal.textColor=Color.white;}
            var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;
            try
            {
                GUI.depth=-65;GUI.matrix=Matrix4x4.identity;float width=Mathf.Min(1000,Screen.width-64),inner=width-40;
                string text="米霍尔："+CurrentText;float textHeight=subtitle.CalcHeight(new GUIContent(text),inner);float height=Mathf.Min(Screen.height*.28f,textHeight+32);
                var area=new Rect((Screen.width-width)*.5f,Screen.height-height-40,width,height);
                GUI.color=new Color(.012f,.018f,.015f,.88f);GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=Color.white;
                subtitleScroll=GUI.BeginScrollView(new Rect(area.x+16,area.y+12,width-32,height-24),subtitleScroll,new Rect(0,0,inner,textHeight));GUI.Label(new Rect(0,0,inner,textHeight),text,subtitle);GUI.EndScrollView();
            }
            finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;}
        }
        void RestoreView()
        {
            cameraEffects.Clear();
            if(Stage!=null&&Stage.Camera)Stage.Camera.enabled=false;
            if(cabinCamera)cabinCamera.enabled=cabinCameraWasEnabled;
            cabinCamera=null;
            if(Stage!=null)Stage.RestoreBreathing();
            if(director){director.controller.cinematicWindowLock=false;if(capturedUI)director.controller.showUI=oldUI;}
            capturedUI=false;
        }
        public void Clear()
        {
            RestoreView();if(Walker!=null)Walker.Dispose();Walker=null;if(Slope!=null)Slope.Dispose();Slope=null;
            if(Stage!=null)Stage.Dispose();Stage=null;dialogue=null;TransitionAlpha=0;ClosingCutFrame=-1;Active=Complete=WalkInterrupted=false;Error=null;Elapsed=WalkDistance=phaseTime=OrbitFraction=PullbackFraction=0;Phase=Step.WaitingForEnvironment;
        }
        void OnDisable(){Clear();}
        void OnDestroy(){Clear();if(font)Destroy(font);}
    }
}
