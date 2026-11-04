using System;
using System.Collections.Generic;
using UnityEngine;
namespace AstraCabin {
 public sealed class ConferenceTranscriptLine {public string Sender,Message;public bool Local;}
 public sealed class ConferenceCallSurface {
  public string Title="",Speaker="",Caption="",ReplyText="",SidePanel="";public string[] Participants=new string[0];public Texture Video;
  public double Elapsed;public bool Paused,HasError,CaptionsVisible=true,ShowSelf,ViewMenu,Choosing;
  public Action<bool> SetPaused;public Action Leave,Reply;public Action<Rect> DrawCaptions,DrawDrawer,DrawPause;
  public readonly List<ConferenceTranscriptLine> Transcript=new List<ConferenceTranscriptLine>();public Vector2 Scroll;
 }
 public sealed partial class ConferenceMode {
        void EnsureConferenceStyles(Font font){
            if(text==null)
            {
                text=new GUIStyle(GUI.skin.label){font=font,wordWrap=true,richText=false,padding=new RectOffset(0,0,0,0)};
                hit=new GUIStyle(GUIStyle.none);
                reply=new GUIStyle(GUIStyle.none){font=font,fontSize=15,wordWrap=true,richText=false,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(12,12,6,6)};
            }
        }
        // Both relay performances and galaxy-specific calls use exactly the same frame and controls.
        public void DrawCallSurface(Rect bounds,Font font,ConferenceCallSurface view){EnsureConferenceStyles(font);
            GUI.BeginGroup(bounds);float w=bounds.width,h=bounds.height;
            Box(new Rect(0,0,w,h),Ink);
            var names=view.Participants;string speaker=view.Speaker;
            float drawer=string.IsNullOrEmpty(view.SidePanel)?0:Mathf.Min(278,w*.30f);
            float toolbarY=h-68;
            // Compact meeting header, with layout controls in the same position as Zoom.
            Label(new Rect(14,7,170,24),"●  "+TimeSpan.FromSeconds(view.Elapsed).ToString(@"mm\:ss"),12,new Color(.57f,.76f,.64f));
            Label(new Rect(180,7,w-360,24),view.Title,13,Muted,TextAnchor.UpperCenter);
            if(FlatButton(new Rect(w-139,4,127,27),"发言人视图  ▾"))view.ViewMenu=!view.ViewMenu;
            var canvas=new Rect(0,36,w-drawer,toolbarY-36);
            float vw=Mathf.Min(canvas.width-24,(canvas.height-12)*16/9f),vh=vw*9/16f;
            var screen=new Rect(canvas.center.x-vw/2,canvas.center.y-vh/2,vw,vh);
            Box(screen,Color.black);if(view.Video)GUI.DrawTexture(screen,view.Video,ScaleMode.ScaleToFit,false);
            Outline(screen,Green,1.5f);
            float nameWidth=Mathf.Min(screen.width-20,Mathf.Max(148,speaker.Length*14+44));
            Box(new Rect(screen.x+2,screen.yMax-28,nameWidth,26),new Color(0,0,0,.75f));
            Icon("mic",new Vector2(screen.x+17,screen.yMax-16),Paper);
            Label(new Rect(screen.x+33,screen.yMax-25,nameWidth-40,23),speaker,13);
            if(view.ShowSelf)
            {
                var tile=new Rect(screen.xMax-158,screen.y+12,146,82);Box(tile,new Color(.18f,.18f,.20f));Outline(tile,new Color(.38f,.39f,.41f));
                Label(new Rect(tile.x,tile.y+14,tile.width,37),"04",28,Paper,TextAnchor.MiddleCenter);
                Label(new Rect(tile.x+8,tile.yMax-23,tile.width-16,20),"探测舱 04（我）",11,Muted);
            }
            if(view.DrawCaptions!=null)view.DrawCaptions(screen);else DrawExternalCaptions(screen,view);
            if(drawer>0){var drawerRect=new Rect(w-drawer,36,drawer,toolbarY-36);if(view.DrawDrawer!=null)view.DrawDrawer(drawerRect);else DrawExternalDrawer(drawerRect,view);}
            Box(new Rect(0,toolbarY,w,68),new Color(.105f,.105f,.12f));
            Box(new Rect(0,toolbarY,w,1),new Color(.22f,.22f,.24f));
            ToolButton(new Rect(8,toolbarY+1,78,64),"mic","文字接入",false,false,tooltip:"本终端通过演出选项回复，不采集麦克风");
            ToolButton(new Rect(88,toolbarY+1,78,64),"camera","视频关闭",false,false,tooltip:"本地摄像头未接入");
            float start=(w-352)/2;
            if(ToolButton(new Rect(start,toolbarY+1,88,64),"participants","参会者 ("+(names.Length+1)+")",view.SidePanel=="participants"))view.SidePanel=view.SidePanel=="participants"?"":"participants";
            if(ToolButton(new Rect(start+88,toolbarY+1,88,64),"chat","聊天",view.SidePanel=="chat"))view.SidePanel=view.SidePanel=="chat"?"":"chat";
            if(ToolButton(new Rect(start+176,toolbarY+1,88,64),"captions","字幕",view.CaptionsVisible))view.CaptionsVisible=!view.CaptionsVisible;
            if(ToolButton(new Rect(start+264,toolbarY+1,88,64),view.Paused?"play":"pause",view.Paused?"继续":"暂停",view.Paused,true,Green)){view.Paused=!view.Paused;view.SetPaused?.Invoke(view.Paused);};
            if(ToolButton(new Rect(w-99,toolbarY+1,90,64),"leave","离开",false,true,new Color(.98f,.32f,.38f),"暂离会议，返回导航后可以继续"))view.Leave?.Invoke();
            if(view.ViewMenu)
            {
                var menu=new Rect(w-210,36,198,82);Box(menu,Panel);Outline(menu,new Color(.35f,.35f,.39f));
                Label(new Rect(menu.x+13,menu.y+10,175,23),"✓  发言人视图",13);
                if(FlatButton(new Rect(menu.x+8,menu.y+39,182,32),view.ShowSelf?"隐藏本地画面":"显示本地画面")){view.ShowSelf=!view.ShowSelf;view.ViewMenu=false;}
            }
            if(view.Paused||view.HasError){if(view.DrawPause!=null)view.DrawPause(screen);else DrawExternalPause(screen,view);}
            GUI.EndGroup();
        }


        void DrawExternalCaptions(Rect screen,ConferenceCallSurface view){
            if(!view.Choosing&&!view.CaptionsVisible)return;
            float width=Mathf.Min(820,screen.width-56);text.fontSize=18;
            string value=view.Speaker+"："+view.Caption;
            float height=Mathf.Min(screen.height*.4f,Mathf.Max(40,text.CalcHeight(new GUIContent(value),width-30)+18));
            if(view.Choosing)height+=60;
            var panel=new Rect(screen.center.x-width/2,screen.yMax-height-39,width,height);
            Box(panel,new Color(.025f,.025f,.03f,.88f));
            Label(new Rect(panel.x+15,panel.y+9,width-30,height-18-(view.Choosing?58:0)),value,18);
            if(view.Choosing&&FlatButton(new Rect(panel.x+18,panel.yMax-53,width-36,42),view.ReplyText,!view.Paused,true))view.Reply?.Invoke();
        }
        void DrawExternalDrawer(Rect r,ConferenceCallSurface view){
            Box(r,Panel);Box(new Rect(r.x,r.y,1,r.height),new Color(.28f,.28f,.31f));Label(new Rect(r.x+17,r.y+13,r.width-63,28),view.SidePanel=="chat"?"会议聊天":"参会者 ("+(view.Participants.Length+1)+")",17);
            if(FlatButton(new Rect(r.xMax-40,r.y+9,28,28),"×")){view.SidePanel="";return;}
            var area=new Rect(r.x+12,r.y+52,r.width-24,r.height-64);float y=0;
            float content= view.SidePanel=="participants"?(view.Participants.Length+1)*58:64;
            if(view.SidePanel=="chat"){text.fontSize=14;foreach(var entry in view.Transcript)content+=48+text.CalcHeight(new GUIContent(entry.Message),area.width-28);}
            view.Scroll=GUI.BeginScrollView(area,view.Scroll,new Rect(0,0,area.width-18,content));
            if(view.SidePanel=="participants"){foreach(var name in view.Participants){Label(new Rect(8,y+10,area.width-32,38),name,15);y+=58;}Label(new Rect(8,y+10,area.width-32,38),"探测舱 04（我）",15,Muted);}
            else foreach(var entry in view.Transcript){float height=text.CalcHeight(new GUIContent(entry.Message),area.width-28);Label(new Rect(5,y,area.width-28,22),entry.Sender,12,entry.Local?Blue:Muted);Label(new Rect(5,y+24,area.width-28,height+8),entry.Message,14);y+=height+48;}
            GUI.EndScrollView();
        }
        void DrawExternalPause(Rect screen,ConferenceCallSurface view){Box(screen,new Color(0,0,0,.72f));Label(new Rect(screen.center.x-250,screen.center.y-55,500,70),"会议已暂停\n继续后恢复画面。",20,Paper,TextAnchor.MiddleCenter);if(FlatButton(new Rect(screen.center.x-78,screen.center.y+45,156,40),"继续会议",true,true)){view.Paused=false;view.SetPaused?.Invoke(false);}}
 }
}

