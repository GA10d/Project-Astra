using System;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    public sealed class FarewellSetpiece:MonoBehaviour
    {
        public enum Step {Talking,ChestBurst,SignalLost,ApproachingWindow,PlanetEnergy,Fragments,Finished=7,Departing,DepartureReturn,Error,ExplosionIllustration}
        public bool Active {get;private set;}public bool Complete {get;private set;}public Step Phase {get;private set;}
        public string Error {get;private set;}public FarewellStage Stage {get;private set;}public bool ZeroTriggered {get;private set;}
        public bool DialogueInterrupted {get{return dialogue!=null&&dialogue.Interrupted;}}public ConferenceCallSurface Surface {get;private set;}
        public float Elapsed {get;private set;}public float PhaseTime {get;private set;}
        MihuoerActionTrack actionTrack;
        readonly ZylCameraEffectTrack cameraEffects = new ZylCameraEffectTrack();
        SetpieceDirector director;ZylPresentationParameters p;MihuoerDialoguePlayer dialogue;MihuoerCountdown timer;bool zeroPending;Vector2 scroll;GUIStyle label,button;string captionKey;
        float chestPreRoll, planetStartedAt, planetDuration, illustrationTime;
        public float PlanetElapsed => Mathf.Clamp(Elapsed - planetStartedAt, 0, planetDuration);
        public float PlanetDuration => planetDuration;

        public float CinematicShake
        {
            get
            {
                if (!Active || p == null) return 0;
                float sustain = p.farewellShakeSustain;
                switch (Phase)
                {
                    case Step.ChestBurst:
                        return p.farewellShake * sustain;
                    case Step.SignalLost:
                    case Step.ApproachingWindow:
                    case Step.PlanetEnergy:
                        float progress = PlanetElapsed / planetDuration;
                        return p.farewellShake * Mathf.SmoothStep(sustain, .90f, progress);
                    case Step.ExplosionIllustration:
                        return ImpactShake(PhaseTime);
                    case Step.Fragments:
                        return ImpactShake(illustrationTime + PhaseTime);
                    default:
                        return 0;
                }
            }
        }

        internal void ShowRecoveryAlert()
        {
            director.computer.SetEventAlert(p.promptRecoveryTitle, p.promptRecoveryBody, farewellRecovery: true);
        }
        const string DepartureTitle = "泰德野里的别离";
        float departureDuration;
        const float TitleFade = .9f;
        const float DepartureFade = .85f;
        const float CabinFade = .7f;
        public bool DepartureActive => Phase == Step.Departing || Phase == Step.DepartureReturn;
        public float DepartureAlpha => Phase == Step.Departing
            ? Mathf.SmoothStep(0, 1, (PhaseTime - departureDuration + DepartureFade) / DepartureFade)
            : Phase == Step.DepartureReturn ? 1 - Mathf.SmoothStep(0, 1, PhaseTime / CabinFade) : 0;
        public float DepartureTitleAlpha => Phase == Step.Departing
            ? Mathf.SmoothStep(0, 1, (PhaseTime - p.farewellDepartureTitleDelay) / TitleFade) : 0;
        public int DepartureTitleCount
        {
            get
            {
                if (Phase != Step.Departing || PhaseTime < p.farewellDepartureTitleDelay) return 0;
                return DepartureTitle.Length;
            }
        }
        GUIStyle departureTitle;
        bool previousCabinHud;

        public void PrepareDeparture()
        {
            var controller = director.controller;
            if (Stage == null)
            {
                // An observed event can be settled after reopening the game, without replaying the call.
                p = ZylPresentationParameters.Load();
                Stage = new FarewellStage(controller.viewCamera, p);
                Stage.ShowExterior(controller);
                Stage.SamplePlanetEnergy(p.farewellPlanetDuration, p.farewellPlanetDuration);
                Stage.FinishPlanetEnergy();
                Stage.SampleFragments(p.farewellDebrisHold);
            }
            float flightEnd = p.farewellDepartureFlightTime;
            float cameraEnd = p.farewellDepartureHold + p.farewellDepartureCameraTime;
            float titleEnd = p.farewellDepartureTitleDelay + TitleFade;
            // Keep the complete title on screen and let the camera settle before the closing fade.
            departureDuration = Mathf.Max(flightEnd, Mathf.Max(cameraEnd + .6f, titleEnd + 1.2f) + DepartureFade);
            Stage.PrepareDeparture(controller, director.stars, departureDuration);
        }

        public void BeginDeparture()
        {
            director.computer.ClearEventAlert();
            director.computer.Close();
            var controller = director.controller;
            previousCabinHud = controller.showUI;
            controller.showUI = false;
            controller.cinematicWindowLock = true;
            controller.ShowNotice("");
            director.GetComponent<ZylScreenCurtain>().ResetPaintConfirmation();
            Stage.BeginDeparture();
            director.PlayCue("engine", .65f);
            Enter(Step.Departing);
        }

        void TickDeparture(float dt)
        {
            PhaseTime += dt;
            if (Phase == Step.Departing)
            {
                Stage.SampleDeparture(PhaseTime);
                if (PhaseTime < departureDuration) return;
                int painted = director.GetComponent<ZylScreenCurtain>().OpaquePaintedFrame;
                if (painted < 0 || Time.frameCount <= painted) return;
                Stage.EndDeparture();
                director.controller.SnapToWall(1);
                Enter(Step.DepartureReturn);
                return;
            }
            if (PhaseTime >= CabinFade) director.computer.Navigation.FinishFarewellDeparture();
        }

        internal void DrawDepartureTitle(Font font)
        {
            if (Phase != Step.Departing || Event.current.type != EventType.Repaint) return;
            if (departureTitle == null)
                departureTitle = new GUIStyle(GUI.skin.label) { font = font, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter };
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            int visible = DepartureTitleCount;
            var color = GUI.color;
            if (visible > 0)
            {
                float widthFraction = p.farewellDepartureTitleWidth * .01f;
                departureTitle.fontSize = Mathf.Max(18, Mathf.RoundToInt(
                    Mathf.Min(Screen.width * widthFraction / DepartureTitle.Length, Screen.height * .27f)));
                departureTitle.alignment = TextAnchor.MiddleLeft;
                float width = departureTitle.CalcSize(new GUIContent(DepartureTitle)).x;
                var bounds = new Rect((Screen.width - width) * .5f, Screen.height * .06f,
                    width, Screen.height * .35f);
                string text = DepartureTitle;
                var shadow = bounds;
                shadow.y += 3 * scale;
                GUI.color = new Color(0, 0, 0, .75f * DepartureTitleAlpha);
                GUI.Label(shadow, text, departureTitle);
                GUI.color = new Color(.97f, .94f, .86f, DepartureTitleAlpha);
                GUI.Label(bounds, text, departureTitle);
            }
            if (director.controller.IsPaused)
            {
                departureTitle.alignment = TextAnchor.MiddleCenter;
                departureTitle.fontSize = Mathf.Max(14, Mathf.RoundToInt(20 * scale));
                GUI.color = Color.white;
                GUI.Label(new Rect(0, Screen.height * .85f, Screen.width, 40 * scale), "暂停  /  ESC 继续", departureTitle);
            }
            GUI.color = color;
        }

        float ImpactShake(float elapsed)
        {
            float tail = 1 - Mathf.Clamp01(elapsed / p.farewellImpactDecay);
            return p.farewellShake * tail * tail;
        }

        public void Initialize(SetpieceDirector owner){director=owner;}
        public void Show()
        {
            Clear();Active=true;Phase=Step.Talking;p=ZylPresentationParameters.Load();
            actionTrack=new MihuoerActionTrack(p.farewellActionClips,p.farewellDialogue);cameraEffects.Bind(p.farewellCameraEffects,p.farewellDialogue,"dialogue");
            try
            {
                if(!director.computer.Navigation||director.computer.Navigation.ActiveGalaxyId!="zyl")throw new InvalidOperationException("别离需要zyl航程");
                Stage=new FarewellStage(director.controller.viewCamera, p);timer=director.computer.Navigation.Countdown;timer.ReachedZero.AddListener(ReachedZero);Stage.AttachCountdown(timer,p);
                if (p.farewellIllustrationEnabled) Stage.LoadExplosionIllustration();
                Surface=new ConferenceCallSurface{Title="米霍尔 · 视频通话",Speaker="米霍尔",Participants=new[]{"米霍尔"},Video=Stage.Video};Surface.Leave=()=>director.computer.OpenApp("navigation");Surface.SetPaused=value=>Surface.Paused=value;Surface.DrawCaptions=DrawCaptions;
                timer.Apply(new CountdownAction{kind="show"},EventKey+"/farewell/show");timer.Apply(new CountdownAction{kind="start"},EventKey+"/farewell/start");
                dialogue=new MihuoerDialoguePlayer(p.farewellDialogue,(i,a)=>ApplyTimer(i,-1,a),(i,j,a)=>ApplyTimer(i,j,a));
                director.controller.SnapToWall(0);director.computer.Open();director.computer.OpenApp("encounter");SyncCaption();Stage.Render();
            }
            catch(Exception e){Fail(e.Message);}
        }
        string EventKey {get{return director.computer.Navigation.Session?.Engine.Pending?.id??"farewell17";}}
        void ApplyTimer(int i,int reply,CountdownAction action){var line=p.farewellDialogue[i];timer.Apply(action,EventKey+"/farewell/"+(string.IsNullOrEmpty(line.timerKey)?i.ToString():line.timerKey)+"/"+(reply<0?"line":"reply"+reply));}
        void ReachedZero(){if(Active&&Phase==Step.Talking)zeroPending=true;}
        public void Resume(){if(!Active||Complete)return;if(Phase==Step.Error){Show();return;}if(Phase<=Step.SignalLost){director.computer.Open();director.computer.OpenApp("encounter");}else director.computer.Close();}
        void Enter(Step step){Phase=step;PhaseTime=0;}
        void Update(){Tick(Time.deltaTime);}
        public void Tick(float dt)
        {
            if ((!Active && !DepartureActive) || director.controller.IsPaused ||
                float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0) return;
            if (DepartureActive)
            {
                TickDeparture(dt);
                return;
            }
            if (!Active || Complete || Phase == Step.Error) return;
            if(Phase<=Step.SignalLost&&(!director.computer.IsOpen||director.computer.CurrentApp!="encounter"||Surface.Paused))return;
            if(Phase>Step.SignalLost&&director.computer.IsOpen)return;
            try
            {
                Elapsed+=dt;PhaseTime+=dt;
                switch(Phase)
                {
                    case Step.Talking:
                        if(zeroPending||timer.Clock.State.remaining<=0)
                        {
                            ZeroTriggered = true;
                            dialogue.Tick(0, true);
                            cameraEffects.Clear();
                            Surface.Choosing = false;
                            Surface.Caption = "";
                            Enter(Step.ChestBurst);
                            if (chestPreRoll <= 0) Stage.AnchorChestBurst();
                            Stage.SampleChest(0, CinematicShake, chestPreRoll, dt);
                            break;
                        }
                        dialogue.Tick(dt, false);
                        var cue=actionTrack.SampleDialogue(dialogue,out float cueTime);
                        Stage.SampleActor(dt,MihuoerActing.Standing(dialogue,MihuoerPose.Explain,MihuoerPose.Idle),cue,cueTime,dialogue.WaitingForReply);
                        SyncCaption();
                        cameraEffects.SampleDialogue(Stage.Camera, dialogue);
                        UpdateChestPreview();
                        break;
                    case Step.ChestBurst:
                        Stage.SampleChest(PhaseTime, CinematicShake, chestPreRoll, dt);
                        if (PhaseTime >= p.farewellChestDuration)
                        {
                            Surface.Video = null;
                            Surface.Caption = "信号丢失……";
                            Surface.Choosing = false;
                            var controller = director.controller;
                            planetStartedAt = Elapsed;
                            // Keep at least a short visible tail if a custom signal hold is longer than the blast.
                            planetDuration = Mathf.Max(p.farewellPlanetDuration,
                                p.farewellSignalHold + controller.turnDuration + controller.observationDuration + 1.2f);
                            Stage.ShowExterior(controller);
                            Stage.SamplePlanetEnergy(0, planetDuration);
                            Enter(Step.SignalLost);
                        }
                        break;
                    case Step.SignalLost:
                        Stage.SamplePlanetEnergy(PlanetElapsed, planetDuration);
                        if (PhaseTime >= p.farewellSignalHold)
                        {
                            director.computer.CloseEncounterApp(false);
                            director.computer.Close();
                            director.controller.SnapToWall(0);
                            director.controller.Turn(1);
                            director.controller.cinematicWindowLock = true;
                            Enter(Step.ApproachingWindow);
                        }
                        break;
                    case Step.ApproachingWindow:
                        Stage.SamplePlanetEnergy(PlanetElapsed, planetDuration);
                        if (director.controller.IsTurning) break;
                        if (director.controller.IsAtWindow) Enter(Step.PlanetEnergy);
                        else if (!director.controller.IsObservingWindow && !director.controller.EnterWindowView())
                            throw new InvalidOperationException("无法切换到舷窗观察");
                        break;
                    case Step.PlanetEnergy:
                        Stage.SamplePlanetEnergy(PlanetElapsed, planetDuration);
                        float illustrationOffset = Stage.HasExplosionIllustration
                            ? Mathf.Max(p.farewellIllustrationStartOffset,
                                -Mathf.Min(p.farewellFinalFlashDuration, planetDuration * (1 - p.farewellFractureStart)))
                            : 0;
                        if (Elapsed - planetStartedAt >= planetDuration + illustrationOffset)
                        {
                            if (Stage.BeginExplosionIllustration(director.controller, PlanetElapsed, planetDuration))
                                Enter(Step.ExplosionIllustration);
                            else
                            {
                                Stage.FinishPlanetEnergy();
                                Enter(Step.Fragments);
                            }
                        }
                        break;
                    case Step.ExplosionIllustration:
                        Stage.SampleExplosionIllustration(PhaseTime, PlanetElapsed, planetDuration);
                        if (PhaseTime >= Stage.ExplosionIllustrationDuration)
                        {
                            illustrationTime = Stage.ExplosionIllustrationDuration;
                            Stage.ReleaseExplosionIllustration();
                            Enter(Step.Fragments);
                        }
                        break;
                    case Step.Fragments:
                        Stage.SampleFragments(Mathf.Min(PhaseTime, p.farewellDebrisHold));
                        if (PhaseTime >= p.farewellDebrisHold)
                        {
                            Complete = true;
                            Enter(Step.Finished);
                            director.controller.cinematicWindowLock = false;
                            director.controller.ExitWindowView();
                            director.controller.SnapToWall(0);
                            director.computer.Open();
                            director.computer.OpenApp("navigation");
                        }
                        break;
                }
            }
            catch(Exception e){Fail(e.Message);}
        }
        void UpdateChestPreview()
        {
            var clock = timer.Clock.State;
            float preview = clock.running || chestPreRoll > 0
                ? Mathf.Max(0, p.farewellChestLeadTime - (float)clock.remaining)
                : 0;
            if (preview > 0 && chestPreRoll <= 0) Stage.AnchorChestBurst();
            chestPreRoll = preview;
            Stage.SampleChestPreview(preview);
        }

        void SyncCaption()
        {
            Surface.Caption=dialogue.Current?.text??"";Surface.Choosing=dialogue.WaitingForReply;Surface.Elapsed=Elapsed;
            string key=dialogue.Index.ToString();if(key!=captionKey){captionKey=key;scroll=Vector2.zero;if(!string.IsNullOrEmpty(Surface.Caption))Surface.Transcript.Add(new ConferenceTranscriptLine{Sender="米霍尔",Message=Surface.Caption});}
        }
        public bool ChooseReply(int index)
        {
            if(Phase!=Step.Talking||Surface.Paused||director.controller.IsPaused||timer.Clock.State.remaining<=0||!dialogue.WaitingForReply||index<0||index>=dialogue.Current.replies.Length)return false;
            string text=dialogue.Current.replies[index];if(!dialogue.Choose(index))return false;Surface.Transcript.Add(new ConferenceTranscriptLine{Sender="探测舱 04（我）",Message=text,Local=true});SyncCaption();return true;
        }
        void DrawCaptions(Rect screen)
        {
            if(Phase!=Step.Talking){if(Phase==Step.SignalLost)GUI.Label(new Rect(screen.x+20,screen.center.y-30,screen.width-40,60),"ASTRA LINK\n连接中断 · 无法恢复远端信号",label);return;}
            if(!Surface.CaptionsVisible&&!Surface.Choosing)return;string text=Surface.CaptionsVisible||Surface.Choosing?"米霍尔："+Surface.Caption:"";float width=screen.width-40,inner=width-32;float th=label.CalcHeight(new GUIContent(text),inner);float height=th+22;var options=dialogue.WaitingForReply?dialogue.Current.replies:new string[0];foreach(var option in options)height+=Mathf.Max(42,button.CalcHeight(new GUIContent(option),inner))+8;
            float view=Mathf.Min(screen.height*.65f,height+20);var area=new Rect(screen.x+20,screen.yMax-view-36,width,view);var color=GUI.color;GUI.color=new Color(0,0,0,.86f);GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=color;
            scroll=GUI.BeginScrollView(new Rect(area.x+8,area.y+8,width-16,view-16),scroll,new Rect(0,0,inner,height));GUI.Label(new Rect(0,0,inner,th+8),text,label);float y=th+14;int selected=-1;
            for(int i=0;i<options.Length;i++){float h=Mathf.Max(42,button.CalcHeight(new GUIContent(options[i]),inner));if(GUI.Button(new Rect(0,y,inner,h),options[i],button))selected=i;y+=h+8;}GUI.EndScrollView();if(selected>=0)ChooseReply(selected);
        }
        public void DrawCall(Rect bounds,Font font)
        {
            if(Surface==null)return;if(label==null){label=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true,richText=false};label.normal.textColor=Color.white;button=new GUIStyle(GUI.skin.button){font=font,fontSize=16,wordWrap=true,richText=false};}
            director.computer.Meeting.DrawCallSurface(bounds,font,Surface);
        }
        void Fail(string message){cameraEffects.Clear();Error="别离："+message;Phase=Step.Error;Stage?.ReleaseExplosionIllustration();director.controller.cinematicWindowLock=false;director.controller.ShowNotice(Error);}
        public void Clear()
        {
            cameraEffects.Clear();
            if (DepartureActive && director && director.controller)
                director.controller.showUI = previousCabinHud;
            if(timer)timer.ReachedZero.RemoveListener(ReachedZero);timer=null;if(Active&&director&&director.computer)director.computer.CloseEncounterApp(false);if(Stage!=null)Stage.Dispose();Stage=null;Surface=null;dialogue=null;actionTrack=null;
            if(director){director.controller.cinematicWindowLock=false;}Active=Complete=ZeroTriggered=zeroPending=false;Elapsed=PhaseTime=chestPreRoll=planetStartedAt=planetDuration=illustrationTime=0;Error=null;captionKey=null;Phase=Step.Talking;
        }
        void OnDisable(){Clear();}void OnDestroy(){Clear();}
    }
}

