using System;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    public sealed class StomachTalkSetpiece : MonoBehaviour
    {
        public enum Step {WaitingForEnvironment,WaterView,Turning,Dialogue,Finished,Error}
        public Step Phase {get;private set;}
        public bool Active {get;private set;}
        public bool Complete {get;private set;}
        public string Error {get;private set;}
        SetpieceDirector director;
        ZylPresentationParameters parameters;
        MihuoerDialoguePlayer dialogue;
        Camera chestCamera;
        Transform chest;
        Vector3 chestTarget;
        float elapsed,targetYaw;
        Vector2 scroll;
        Font font;
        GUIStyle text,button;
        ZylStomachEnvironment Exterior {get{return director&&director.computer&&director.computer.Navigation?director.computer.Navigation.Exterior:null;}}
        public void Initialize(SetpieceDirector value){director=value;}
        public void Show()
        {
            Clear();parameters=ZylPresentationParameters.Load();if(director.computer.Navigation&&director.computer.Navigation.Countdown)director.computer.Navigation.Countdown.ReloadParameters();Active=true;Phase=Step.WaitingForEnvironment;
            if(!director.computer.Navigation||director.computer.Navigation.ActiveGalaxyId!="zyl"){Fail("肚中会谈需要zyl胃内航程，请使用星域调试工具从炸弹信标开始。");return;}
            director.computer.Close();director.controller.SnapToWall(1);director.controller.EnterWindowView();director.controller.cinematicWindowLock=true;
        }
        public void Resume(){if(!Active||Complete)return;if(Phase==Step.Error){Show();return;}director.computer.Close();director.controller.SnapToWall(1);director.controller.EnterWindowView();director.controller.cinematicWindowLock=true;}
        void Fail(string message){Error=message;Phase=Step.Error;director.controller.cinematicWindowLock=false;director.controller.ShowNotice(message);}
        void Update()
        {
            if(!Active||Complete||director.controller.IsPaused||Phase==Step.Error)return;
            var exterior=Exterior;if(!exterior){Fail("胃内环境管理器不可用。");return;}
            if(exterior.Error!=null){Fail(exterior.Error);return;}
            if(Phase==Step.WaitingForEnvironment)
            {
                if(!exterior.Ready||exterior.Busy||ZylScreenCurtain.BlocksInput||!director.controller.IsAtWindow)return;
                if(!exterior.MihuoerAnchor||!exterior.MihuoerActor){Fail("胃内场景缺少米霍尔位置锚点或角色。");return;}
                exterior.MihuoerActor.SetSeatedOnCraft(false);exterior.SetCabinYaw(0); // Always restart from the user's authored water-facing landing rotation.
                targetYaw=exterior.CabinYawTowardMihuoer();elapsed=0;Phase=Step.WaterView;
            }
            if(director.computer.IsOpen)return;
            elapsed+=Time.deltaTime;
            if(Phase==Step.WaterView&&elapsed>=parameters.stomachWaterHold){elapsed=0;Phase=Step.Turning;}
            if(Phase==Step.Turning)
            {
                float t=Mathf.Clamp01(elapsed/parameters.stomachTurnDuration);exterior.SetCabinYaw(Mathf.LerpAngle(0,targetYaw,Mathf.SmoothStep(0,1,t)));
                if(t>=1){var nav=director.computer.Navigation;nav.Countdown.BeginStomachTalk(nav.Session?.Engine.Pending?.id??"stomach13");dialogue=new MihuoerDialoguePlayer(parameters.stomachDialogue,(i,a)=>ApplyTimer(i,"line",a),(i,j,a)=>ApplyTimer(i,"reply",a));Phase=Step.Dialogue;elapsed=0;scroll=Vector2.zero;}
            }
            if(Phase==Step.Dialogue)
            {
                dialogue.Tick(Time.deltaTime,false);
                exterior.MihuoerActor.Pose=IsChestLine(dialogue.Current)?MihuoerPose.Idle:
                    MihuoerActing.Standing(dialogue,MihuoerPose.Explain,MihuoerPose.Slumped);
                if(dialogue.Ended)Finish();
            }
        }

        static bool IsChestLine(MihuoerDialogueLine line) => line != null &&
            (line.timerKey == "timer_mw61emp8_uh2xm70wzu" || (line.text.Contains("胸前") && line.text.Contains("倒计时")));

        void LateUpdate()
        {
            bool closeup = Active && Phase == Step.Dialogue && IsChestLine(dialogue?.Current);
            if (!closeup || director.computer.IsOpen || ZylScreenCurtain.IsOpaque)
            {
                if (chestCamera) chestCamera.enabled = false;
                return;
            }

            var actor = Exterior.MihuoerActor.Actor;
            if (!chest)
            {
                foreach (var bone in actor.GetComponentsInChildren<Transform>())
                    if (bone.name == "chest") { chest = bone; break; }
                if (!chest) return;
                chestTarget = chest.InverseTransformPoint(MihuoerCountdownSkin.FindCountdownSurface(actor, chest, parameters));
            }
            if (!chestCamera)
            {
                var source = director.controller.viewCamera;
                chestCamera = new GameObject("Zyl13 | chest countdown closeup").AddComponent<Camera>();
                chestCamera.CopyFrom(source);
                chestCamera.targetTexture = null;
                chestCamera.rect = new Rect(0, 0, 1, 1);
                chestCamera.depth = source.depth + 10;
                chestCamera.cullingMask = 1 << ZylStomachEnvironment.EnvironmentLayer;
                chestCamera.clearFlags = CameraClearFlags.SolidColor;
                chestCamera.fieldOfView = 35;
                chestCamera.nearClipPlane = .035f;
            }
            chestCamera.enabled = true;
            Vector3 target = chest.TransformPoint(chestTarget);
            Vector3 eye = target + actor.forward * .69f + actor.right * .055f + actor.up * .035f;
            chestCamera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, actor.up));
        }

        void ReleaseCloseup()
        {
            if (chestCamera) { chestCamera.enabled = false; Destroy(chestCamera.gameObject); }
            chestCamera = null;
            chest = null;
        }

        void ApplyTimer(int line,string phase,CountdownAction action){var nav=director.computer.Navigation;if(nav&&nav.Countdown)nav.Countdown.Apply(action,(nav.Session?.Engine.Pending?.id??"stomach13")+"/stomach/"+(string.IsNullOrEmpty(parameters.stomachDialogue[line].timerKey)?line.ToString():parameters.stomachDialogue[line].timerKey)+"/"+phase);}
        public bool ChooseReply(int index)
        {
            if(!Active||Phase!=Step.Dialogue||director.controller.IsPaused||director.computer.IsOpen||dialogue==null||!dialogue.Choose(index))return false;
            scroll=Vector2.zero;if(dialogue.Ended)Finish();return true;
        }
        void Finish()
        {
            ReleaseCloseup();
            Complete=true;Phase=Step.Finished;if(Exterior&&Exterior.MihuoerActor&&!Exterior.MihuoerActor.SeatedOnCraft)Exterior.MihuoerActor.Pose=MihuoerPose.Slumped;
            director.controller.cinematicWindowLock=false;director.controller.ExitWindowView();director.controller.SnapToWall(0);
            director.computer.Open();director.computer.OpenApp("navigation");
        }
        void Styles()
        {
            if(text!=null)return;font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},22);
            text=new GUIStyle(GUI.skin.label){font=font,fontSize=22,wordWrap=true,richText=false};text.normal.textColor=Color.white;
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=20,wordWrap=true,richText=false};
        }
        void OnGUI()
        {
            if(!Active||Complete||director.controller.IsPaused||director.computer.IsOpen||ZylScreenCurtain.IsOpaque)return;
            Styles();var matrix=GUI.matrix;var color=GUI.color;bool enabled=GUI.enabled;GUI.depth=-60;
            try
            {
                GUI.matrix=Matrix4x4.identity;GUI.enabled=enabled&&!director.controller.IsPaused;
                string line=Phase==Step.Error?Error:Phase==Step.Dialogue&&dialogue.Current!=null?"米霍尔："+dialogue.Current.text:null;
                if(string.IsNullOrEmpty(line))return;
                string[] options=Phase==Step.Dialogue&&dialogue.WaitingForReply?dialogue.Current.replies:new string[0];
                float width=Mathf.Min(900,Screen.width-48),inner=width-48;float h=text.CalcHeight(new GUIContent(line),inner)+24;
                foreach(var option in options)h+=Mathf.Max(44,button.CalcHeight(new GUIContent(option),inner))+8;
                float height=Mathf.Min(Screen.height*.5f,h+20);var area=new Rect((Screen.width-width)/2,Screen.height-height-75,width,height);
                GUI.color=new Color(.015f,.025f,.02f,.93f);GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=Color.white;
                scroll=GUI.BeginScrollView(new Rect(area.x+12,area.y+10,width-24,height-20),scroll,new Rect(0,0,inner,h));
                float y=text.CalcHeight(new GUIContent(line),inner)+24;GUI.Label(new Rect(0,0,inner,y-12),line,text);int selected=-1;
                for(int i=0;i<options.Length;i++){float oh=Mathf.Max(44,button.CalcHeight(new GUIContent(options[i]),inner));if(GUI.Button(new Rect(0,y,inner,oh),options[i],button))selected=i;y+=oh+8;}
                GUI.EndScrollView();if(selected>=0)ChooseReply(selected);
            }
            finally{GUI.matrix=matrix;GUI.color=color;GUI.enabled=enabled;}
        }
        public void Clear(){ReleaseCloseup();if(director)director.controller.cinematicWindowLock=false;if(Exterior&&Exterior.MihuoerActor&&!Exterior.MihuoerActor.SeatedOnCraft)Exterior.MihuoerActor.Pose=MihuoerPose.Slumped;Active=Complete=false;dialogue=null;Error=null;elapsed=0;scroll=Vector2.zero;}
        void OnDestroy(){ReleaseCloseup();if(font)Destroy(font);}
    }
}
