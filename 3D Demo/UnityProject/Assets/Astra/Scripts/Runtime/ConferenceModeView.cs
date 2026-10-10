using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AstraCabin
{
    public sealed partial class ConferenceMode
    {
        // Speaker-view composition: the call owns the canvas; drawers are opt-in.
        public bool CaptionsVisible { get; private set; } = true;
        public string SidePanel { get; private set; } = "";
        public int TranscriptCount => transcript.Count;
        public string LastTranscriptText => transcript.Count==0?"":transcript[transcript.Count-1].message;
        public void ToggleCaptions() { CaptionsVisible=!CaptionsVisible; }
        public void TogglePanel(string panel) { if(panel=="participants"||panel=="chat")SidePanel=SidePanel==panel?"":panel; }

        sealed class TranscriptEntry { public string sender,message;public bool local; }
        readonly List<TranscriptEntry> transcript=new List<TranscriptEntry>();
        string transcriptNode,lastCaption;
        bool showSelf,viewMenu;
        GUIStyle text,hit,reply;
        Vector2 dialogueScroll,chatScroll,participantsScroll;
        static readonly Color Ink=new Color(.075f,.075f,.085f),Panel=new Color(.13f,.13f,.15f),Paper=new Color(.94f,.94f,.95f),Muted=new Color(.66f,.67f,.70f),Blue=new Color(.26f,.57f,1f),Green=new Color(.37f,.79f,.27f);

        void ResetMeetingUI()
        {
            transcript.Clear();transcriptNode=null;lastCaption=null;SidePanel="";viewMenu=false;showSelf=false;CaptionsVisible=true;
            dialogueScroll=chatScroll=participantsScroll=Vector2.zero;
        }
        void TrackTranscript()
        {
            if(!Player)return;
            string caption=Player.CurrentSubtitle;
            if(string.IsNullOrEmpty(caption)){lastCaption=null;return;}
            string sender=string.IsNullOrEmpty(Player.CurrentSpeaker)?"中继站":Player.CurrentSpeaker;
            if(transcript.Count>0&&transcriptNode==Player.CurrentNodeId&&lastCaption!=null&&caption.StartsWith(lastCaption,StringComparison.Ordinal)&&!transcript[transcript.Count-1].local)
                transcript[transcript.Count-1].message=caption;
            else
            {
                transcript.Add(new TranscriptEntry{sender=sender,message=caption});chatScroll.y=float.MaxValue;
                if(transcript.Count>256)transcript.RemoveAt(0);
            }
            transcriptNode=Player.CurrentNodeId;lastCaption=caption;
        }
        void RecordReply(string slot)
        {
            var choice=Array.Find(Player.CurrentChoices,c=>c.id==slot);
            if(choice==null||!Player.CanChoose(choice))return;
            transcript.Add(new TranscriptEntry{sender="探测舱 04（我）",message=choice.text,local=true});
            lastCaption=null;chatScroll.y=float.MaxValue;
        }

        static void Box(Rect r,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        static void Outline(Rect r,Color color,float thickness=1)
        {Box(new Rect(r.x,r.y,r.width,thickness),color);Box(new Rect(r.x,r.yMax-thickness,r.width,thickness),color);Box(new Rect(r.x,r.y,thickness,r.height),color);Box(new Rect(r.xMax-thickness,r.y,thickness,r.height),color);}
        static void Stroke(Vector2 a,Vector2 b,Color color,float width=1.6f)
        {
            int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)*1.3f));
            for(int i=0;i<=count;i++){var p=Vector2.Lerp(a,b,i/(float)count);Box(new Rect(p.x-width/2,p.y-width/2,width,width),color);}
        }
        static void Arc(Vector2 center,float rx,float ry,float start,float end,Color color)
        {
            var previous=center+new Vector2(Mathf.Cos(start*Mathf.Deg2Rad)*rx,Mathf.Sin(start*Mathf.Deg2Rad)*ry);
            for(int i=1;i<=24;i++){float a=Mathf.Lerp(start,end,i/24f)*Mathf.Deg2Rad;var next=center+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry);Stroke(previous,next,color);previous=next;}
        }
        void Label(Rect r,string value,int size=14,Color? color=null,TextAnchor align=TextAnchor.UpperLeft)
        {text.fontSize=size;text.normal.textColor=color??Paper;text.alignment=align;GUI.Label(r,value,text);}
        void Icon(string type,Vector2 p,Color color)
        {
            switch(type)
            {
                case "mic":
                    Arc(p+new Vector2(0,-4),4,8,0,360,color);Arc(p+new Vector2(0,-2),8,9,0,180,color);
                    Stroke(p+new Vector2(0,7),p+new Vector2(0,12),color);Stroke(p+new Vector2(-5,12),p+new Vector2(5,12),color);break;
                case "camera":
                    Outline(new Rect(p.x-12,p.y-7,16,14),color,1.7f);
                    Stroke(p+new Vector2(4,-3),p+new Vector2(11,-7),color);Stroke(p+new Vector2(11,-7),p+new Vector2(11,7),color);Stroke(p+new Vector2(11,7),p+new Vector2(4,3),color);break;
                case "participants":
                    Arc(p+new Vector2(-4,-7),4,4,0,360,color);Arc(p+new Vector2(7,-3),3,3,0,360,color);
                    Arc(p+new Vector2(-4,9),8,9,180,360,color);Stroke(p+new Vector2(-12,9),p+new Vector2(4,9),color);
                    Arc(p+new Vector2(8,10),6,7,240,360,color);break;
                case "chat":
                    Outline(new Rect(p.x-11,p.y-9,22,16),color,1.7f);Stroke(p+new Vector2(-6,7),p+new Vector2(-6,12),color);Stroke(p+new Vector2(-6,12),p+new Vector2(0,7),color);break;
                case "captions":
                    Outline(new Rect(p.x-13,p.y-9,26,19),color,1.7f);Label(new Rect(p.x-12,p.y-10,24,21),"CC",12,color,TextAnchor.MiddleCenter);break;
                case "pause": Box(new Rect(p.x-7,p.y-10,5,21),color);Box(new Rect(p.x+3,p.y-10,5,21),color);break;
                case "play": Stroke(p+new Vector2(-6,-10),p+new Vector2(10,0),color);Stroke(p+new Vector2(10,0),p+new Vector2(-6,10),color);Stroke(p+new Vector2(-6,10),p+new Vector2(-6,-10),color);break;
                case "leave":
                    Outline(new Rect(p.x-11,p.y-10,9,21),color,1.7f);Stroke(p+new Vector2(-2,0),p+new Vector2(11,0),color);Stroke(p+new Vector2(6,-5),p+new Vector2(11,0),color);Stroke(p+new Vector2(6,5),p+new Vector2(11,0),color);break;
            }
        }
        bool ToolButton(Rect r,string icon,string label,bool selected=false,bool enabled=true,Color? tint=null,string tooltip="")
        {
            bool hover=r.Contains(Event.current.mousePosition)&&enabled;
            if(selected||hover)Box(new Rect(r.x+3,r.y+2,r.width-6,r.height-4),selected?new Color(.21f,.23f,.28f):new Color(.20f,.20f,.22f));
            Color color=enabled?(tint??Paper):new Color(.47f,.48f,.50f);
            Icon(icon,new Vector2(r.center.x,r.y+22),color);
            if(!enabled&&(icon=="mic"||icon=="camera"))Stroke(new Vector2(r.center.x-13,r.y+10),new Vector2(r.center.x+13,r.y+35),new Color(.89f,.27f,.31f),2);
            Label(new Rect(r.x,r.y+41,r.width,22),label,12,color,TextAnchor.UpperCenter);
            bool old=GUI.enabled;GUI.enabled=old&&enabled;bool clicked=GUI.Button(r,new GUIContent("",tooltip),hit);GUI.enabled=old;return clicked;
        }
        bool FlatButton(Rect r,string label,bool enabled=true,bool primary=false)
        {
            bool hover=r.Contains(Event.current.mousePosition)&&enabled;
            Box(r,primary?(hover?new Color(.18f,.42f,.83f):new Color(.14f,.34f,.72f)):(hover?new Color(.23f,.25f,.30f):new Color(.16f,.18f,.22f)));
            Outline(r,primary?new Color(.34f,.62f,1f):new Color(.35f,.38f,.44f));
            reply.normal.textColor=enabled?Paper:Muted;
            bool old=GUI.enabled;GUI.enabled=old&&enabled;bool clicked=GUI.Button(r,label,reply);GUI.enabled=old;return clicked;
        }

        public void Draw(Rect bounds,Font font)
        {
            if(text==null)
            {
                text=new GUIStyle(GUI.skin.label){font=font,wordWrap=true,richText=false,padding=new RectOffset(0,0,0,0)};
                hit=new GUIStyle(GUIStyle.none);
                reply=new GUIStyle(GUIStyle.none){font=font,fontSize=15,wordWrap=true,richText=false,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(12,12,6,6)};
            }
            GUI.BeginGroup(bounds);float w=bounds.width,h=bounds.height;
            Box(new Rect(0,0,w,h),Ink);
            if(!Player){Label(new Rect(30,30,w-60,60),"当前没有会议。请从导航节点接入。",20);GUI.EndGroup();return;}
            var names=Player.Participants;
            string speaker=string.IsNullOrEmpty(Player.CurrentSpeaker)?(names.Length>0?names[0]:"中继站"):Player.CurrentSpeaker;
            float drawer=string.IsNullOrEmpty(SidePanel)?0:Mathf.Min(278,w*.30f);
            float toolbarY=h-68;
            // Compact meeting header, with layout controls in the same position as Zoom.
            Label(new Rect(14,7,170,24),"●  "+TimeSpan.FromSeconds(elapsed).ToString(@"mm\:ss"),12,new Color(.57f,.76f,.64f));
            Label(new Rect(180,7,w-360,24),title,13,Muted,TextAnchor.UpperCenter);
            if(FlatButton(new Rect(w-139,4,127,27),"发言人视图  ▾"))viewMenu=!viewMenu;
            var canvas=new Rect(0,36,w-drawer,toolbarY-36);
            float vw=Mathf.Min(canvas.width-24,(canvas.height-12)*16/9f),vh=vw*9/16f;
            var screen=new Rect(canvas.center.x-vw/2,canvas.center.y-vh/2,vw,vh);
            Box(screen,Color.black);if(video)GUI.DrawTexture(screen,video,ScaleMode.ScaleToFit,false);
            Outline(screen,Green,1.5f);
            float nameWidth=Mathf.Min(screen.width-20,Mathf.Max(148,speaker.Length*14+44));
            Box(new Rect(screen.x+2,screen.yMax-28,nameWidth,26),new Color(0,0,0,.75f));
            Icon("mic",new Vector2(screen.x+17,screen.yMax-16),Paper);
            Label(new Rect(screen.x+33,screen.yMax-25,nameWidth-40,23),speaker,13);
            if(showSelf)
            {
                var tile=new Rect(screen.xMax-158,screen.y+12,146,82);Box(tile,new Color(.18f,.18f,.20f));Outline(tile,new Color(.38f,.39f,.41f));
                Label(new Rect(tile.x,tile.y+14,tile.width,37),"04",28,Paper,TextAnchor.MiddleCenter);
                Label(new Rect(tile.x+8,tile.yMax-23,tile.width-16,20),"探测舱 04（我）",11,Muted);
            }
            DrawCaptions(screen,speaker);
            if(drawer>0)DrawDrawer(new Rect(w-drawer,36,drawer,toolbarY-36),names);
            Box(new Rect(0,toolbarY,w,68),new Color(.105f,.105f,.12f));
            Box(new Rect(0,toolbarY,w,1),new Color(.22f,.22f,.24f));
            ToolButton(new Rect(8,toolbarY+1,78,64),"mic","文字接入",false,false,tooltip:"本终端通过演出选项回复，不采集麦克风");
            ToolButton(new Rect(88,toolbarY+1,78,64),"camera","视频关闭",false,false,tooltip:"本地摄像头未接入");
            float start=(w-352)/2;
            if(ToolButton(new Rect(start,toolbarY+1,88,64),"participants","参会者 ("+(names.Length+1)+")",SidePanel=="participants"))TogglePanel("participants");
            if(ToolButton(new Rect(start+88,toolbarY+1,88,64),"chat","聊天",SidePanel=="chat"))TogglePanel("chat");
            if(ToolButton(new Rect(start+176,toolbarY+1,88,64),"captions","字幕",CaptionsVisible))ToggleCaptions();
            if(ToolButton(new Rect(start+264,toolbarY+1,88,64),UserPaused?"play":"pause",UserPaused?"继续":"暂停",UserPaused,true,Green))SetPaused(!UserPaused);
            if(ToolButton(new Rect(w-99,toolbarY+1,90,64),"leave","离开",false,true,new Color(.98f,.32f,.38f),"暂离会议，返回导航后可以继续"))computer.OpenApp("navigation");
            if(viewMenu)
            {
                var menu=new Rect(w-210,36,198,82);Box(menu,Panel);Outline(menu,new Color(.35f,.35f,.39f));
                Label(new Rect(menu.x+13,menu.y+10,175,23),"✓  发言人视图",13);
                if(FlatButton(new Rect(menu.x+8,menu.y+39,182,32),showSelf?"隐藏本地画面":"显示本地画面")){showSelf=!showSelf;viewMenu=false;}
            }
            if(UserPaused||Error!=null)DrawPause(screen);
            GUI.EndGroup();
        }

        void DrawCaptions(Rect screen,string speaker)
        {
            if(!Player.IsChoosing)
            {
                if(!CaptionsVisible||string.IsNullOrEmpty(Player.CurrentSubtitle))return;
                float width=Mathf.Min(820,screen.width-56);text.fontSize=18;
                string value=speaker+"："+Player.CurrentSubtitle;
                float height=Mathf.Min(screen.height*.40f,Mathf.Max(40,text.CalcHeight(new GUIContent(value),width-30)+18));
                var caption=new Rect(screen.center.x-width/2,screen.yMax-height-39,width,height);
                Box(caption,new Color(.025f,.025f,.03f,.88f));
                dialogueScroll=GUI.BeginScrollView(new Rect(caption.x+14,caption.y+8,width-24,height-14),dialogueScroll,new Rect(0,0,width-46,Mathf.Max(height-16,text.CalcHeight(new GUIContent(value),width-46))));
                Label(new Rect(0,0,width-46,text.CalcHeight(new GUIContent(value),width-46)),value,18);
                GUI.EndScrollView();return;
            }
            // Choices remain visible even when captions are disabled.
            var choices=Player.CurrentChoices;int cols=screen.width>=610?2:1;
            float panelWidth=Mathf.Min(860,screen.width-40),inner=panelWidth-44,cw=(inner-10*(cols-1))/cols;
            text.fontSize=17;float subtitleH=Mathf.Max(28,text.CalcHeight(new GUIContent(Player.CurrentSubtitle),inner));
            var rows=new float[Mathf.CeilToInt(choices.Length/(float)cols)];
            for(int i=0;i<choices.Length;i++)rows[i/cols]=Mathf.Max(rows[i/cols],Mathf.Max(42,reply.CalcHeight(new GUIContent(choices[i].text),cw)));
            float contentH=32+subtitleH+10;foreach(float row in rows)contentH+=row+8;
            float panelHeight=Mathf.Min(screen.height*.52f,contentH+20);
            var panel=new Rect(screen.center.x-panelWidth/2,screen.yMax-panelHeight-39,panelWidth,panelHeight);
            Box(panel,new Color(.045f,.055f,.075f,.97f));Outline(panel,new Color(.32f,.37f,.46f));
            dialogueScroll=GUI.BeginScrollView(new Rect(panel.x+16,panel.y+10,panelWidth-26,panelHeight-18),dialogueScroll,new Rect(0,0,inner,contentH));
            Label(new Rect(0,0,inner-180,23),"回复主讲人",12,Blue);
            Label(new Rect(inner-178,0,178,23),"等待回复  "+Mathf.CeilToInt(Player.ChoiceSecondsRemaining)+" 秒",12,Muted,TextAnchor.UpperRight);
            Label(new Rect(0,30,inner,subtitleH),Player.CurrentSubtitle,17);
            float y=40+subtitleH;
            for(int i=0;i<choices.Length;i++)
            {
                var c=choices[i];if(FlatButton(new Rect((i%cols)*(cw+10),y,cw,rows[i/cols]),c.text,IsAdvancing&&Player.CanChoose(c),true))QueueChoice(c.id);
                if(i%cols==cols-1)y+=rows[i/cols]+8;
            }
            GUI.EndScrollView();
        }

        void DrawDrawer(Rect r,string[] names)
        {
            Box(r,Panel);Box(new Rect(r.x,r.y,1,r.height),new Color(.28f,.28f,.31f));
            Label(new Rect(r.x+17,r.y+13,r.width-63,28),SidePanel=="chat"?"会议聊天":"参会者 ("+(names.Length+1)+")",17);
            var close=new Rect(r.xMax-40,r.y+9,28,28);
            if(close.Contains(Event.current.mousePosition))Box(close,new Color(.23f,.25f,.30f));
            Label(close,"×",20,Muted,TextAnchor.MiddleCenter);
            if(GUI.Button(close,new GUIContent("","关闭侧栏"),hit)){SidePanel="";return;}
            Box(new Rect(r.x+12,r.y+50,r.width-24,1),new Color(.28f,.28f,.31f));
            if(SidePanel=="participants")
            {
                var all=names.Concat(new[]{"探测舱 04（我）"}).ToArray();
                participantsScroll=GUI.BeginScrollView(new Rect(r.x+12,r.y+61,r.width-24,r.height-75),participantsScroll,new Rect(0,0,r.width-43,all.Length*78));
                for(int i=0;i<all.Length;i++)
                {
                    float y=i*78;Box(new Rect(2,y+2,34,34),new Color(.25f,.28f,.34f));
                    Label(new Rect(2,y+4,34,30),i==all.Length-1?"04":all[i].Substring(0,1),16,Paper,TextAnchor.MiddleCenter);
                    Label(new Rect(46,y,r.width-104,39),all[i],14);
                    Label(new Rect(46,y+41,r.width-104,20),i==all.Length-1?"文字接入":"主讲人",12,Muted);
                }
                GUI.EndScrollView();
            }
            else
            {
                float inner=r.width-51,total=8;text.fontSize=14;
                foreach(var entry in transcript)total+=48+text.CalcHeight(new GUIContent(entry.message),inner);
                chatScroll=GUI.BeginScrollView(new Rect(r.x+15,r.y+61,r.width-27,r.height-121),chatScroll,new Rect(0,0,inner,total));
                float y=0;
                foreach(var entry in transcript)
                {
                    Label(new Rect(0,y,inner,23),entry.sender,12,entry.local?Blue:Muted);y+=25;
                    text.fontSize=14;float height=text.CalcHeight(new GUIContent(entry.message),inner);
                    Label(new Rect(0,y,inner,height),entry.message,14);y+=height+23;
                }
                GUI.EndScrollView();
                Label(new Rect(r.x+16,r.yMax-48,r.width-32,39),"从画面中的选项发送回复",12,Muted);
            }
        }
        void DrawPause(Rect screen)
        {
            Box(screen,new Color(0,0,0,.76f));
            float width=Mathf.Min(540,screen.width-50),x=screen.center.x-width/2,y=screen.center.y-60;
            Label(new Rect(x,y,width,98),Error??"会议已暂停\n继续后恢复画面和回复倒计时。",20,Paper,TextAnchor.MiddleCenter);
            if(Error==null){if(FlatButton(new Rect(screen.center.x-78,y+110,156,40),"继续会议",true,true))SetPaused(false);}
            else if(FlatButton(new Rect(screen.center.x-116,y+110,232,40),ending!=null?"重试保存会议结果":"重新接入会议",true,true))
            {if(ending!=null){Error=null;TryComplete();}else{var ev=computer.Navigation.Session.Engine.Pending;try{Begin(ev);}catch(Exception ex){Error=ex.Message;}}}
        }
    }
}
