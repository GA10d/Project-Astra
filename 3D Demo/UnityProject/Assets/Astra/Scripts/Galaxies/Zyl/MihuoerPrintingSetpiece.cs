using System;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    public sealed class MihuoerPrintingSetpiece : MonoBehaviour
    {
        public enum Step {WaitingForEnvironment,Dialogue,Finished,Error}
        public Step Phase {get;private set;}
        public bool Active {get;private set;}
        public bool Complete {get;private set;}
        public string Error {get;private set;}
        SetpieceDirector director;
        ZylPresentationParameters parameters;
        MihuoerDialoguePlayer dialogue;
        Vector2 scroll;
        Font font;
        GUIStyle text,button;
        ZylStomachEnvironment Exterior {get{return director&&director.computer&&director.computer.Navigation?director.computer.Navigation.Exterior:null;}}
        public void Initialize(SetpieceDirector value){director=value;}
        public void Show()
        {
            Clear();parameters=ZylPresentationParameters.Load();if(director.computer.Navigation&&director.computer.Navigation.Countdown)director.computer.Navigation.Countdown.ReloadParameters();Active=true;Phase=Step.WaitingForEnvironment;
            if(!director.computer.Navigation||director.computer.Navigation.ActiveGalaxyId!="zyl"){Fail("米霍尔需要打印构件：请先进入zyl胃内航程。");return;}
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
                if(!exterior.MihuoerShipAnchor||!exterior.MihuoerActor){Fail("胃内缺少米霍尔飞船或角色。");return;}
                try{exterior.SetCabinYaw(exterior.CabinYawTowardCraft());exterior.MihuoerActor.SetSeatedOnCraft(true);}
                catch(Exception e){Fail(e.Message);return;}
                dialogue=new MihuoerDialoguePlayer(parameters.printingDialogue,(i,a)=>ApplyTimer(i,"line",a),(i,j,a)=>ApplyTimer(i,"reply",a));Phase=Step.Dialogue;scroll=Vector2.zero;
            }
            if(director.computer.IsOpen)return;
            if(Phase==Step.Dialogue){dialogue.Tick(Time.deltaTime,false);exterior.MihuoerActor.Speaking=!dialogue.Ended&&!dialogue.WaitingForReply;if(dialogue.Ended)Finish();}
        }
        void ApplyTimer(int line,string phase,CountdownAction action){var nav=director.computer.Navigation;if(nav&&nav.Countdown)nav.Countdown.Apply(action,(nav.Session?.Engine.Pending?.id??"printing14")+"/printing/"+(string.IsNullOrEmpty(parameters.printingDialogue[line].timerKey)?line.ToString():parameters.printingDialogue[line].timerKey)+"/"+phase);}
        public bool ChooseReply(int index)
        {
            if(!Active||Phase!=Step.Dialogue||director.controller.IsPaused||director.computer.IsOpen||dialogue==null||!dialogue.Choose(index))return false;
            scroll=Vector2.zero;if(dialogue.Ended)Finish();return true;
        }
        void Finish()
        {
            Complete=true;Phase=Step.Finished;if(Exterior&&Exterior.MihuoerActor)Exterior.MihuoerActor.Speaking=false;
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
        public void Clear(){if(director)director.controller.cinematicWindowLock=false;if(Exterior&&Exterior.MihuoerActor)Exterior.MihuoerActor.Speaking=false;Active=Complete=false;dialogue=null;Error=null;scroll=Vector2.zero;}
        void OnDestroy(){if(font)Destroy(font);}
    }
}
