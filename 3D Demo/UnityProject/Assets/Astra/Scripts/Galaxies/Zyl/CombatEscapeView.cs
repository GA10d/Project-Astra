using UnityEngine;
using AstraCabin.Galaxies.Zyl;
namespace AstraCabin
{
    public sealed partial class CombatMode
    {
        public EscapeFlight EscapeStory {get;private set;}
        public string EscapeText {get;set;}
        public void BeginEscapeStory(EscapeFlight flight){Suspend();EscapeStory=flight;escapeImpacts=0;ClearFeedback();}
        public void EndEscapeStory(){EscapeStory=null;EscapeText=null;ClearFeedback();}
        void EscapeFeedback(float dt)
        {
            if(!IsVisible||EscapeStory.Paused||controller.IsPaused){ClearFeedback();return;}
            float target=reducedMotion?0:-EscapeStory.Bank*MaxCabinRoll;
            float eased=Mathf.Lerp(roll,target,1-Mathf.Exp(-1.8f*dt));roll=Mathf.MoveTowards(roll,eased,CabinRollSpeed*dt);
            controller.combatEuler=new Vector3(0,0,roll);controller.combatOffset=Vector3.zero;controller.combatFov=reducedMotion?0:2;
            systems.SetCombatFeedback(reducedMotion?0:EscapeStory.ImpactAge<.12f?1:0,reducedMotion?0:Mathf.Clamp01(1-EscapeStory.ImpactAge));
            if(EscapeStory.Impacts>escapeImpacts){escapeImpacts=EscapeStory.Impacts;sound.Play("damage",.6f,muted);}
            sound.SetActive(true,.72f,muted);
        }
        int escapeImpacts;
        void DrawEscape(float w,float h)
        {
            EnsureEscapeArt();
            var s=EscapeStory;
            Label(new Rect(14,6,205,24),"逃出生天 / TOW LINK",17,green);
            Label(new Rect(14,33,210,22),"领航：米霍尔 · 牵引连接稳定",13,ink);
            Meter(new Rect(235,13,155,10),1,green,"玩家船体 100");
            Meter(new Rect(415,13,155,10),0,red,"引擎失效 · 无法操纵");
            Meter(new Rect(595,13,135,10),s.Progress,amber,"出口航程 "+Mathf.FloorToInt(s.Progress*100)+"%");
            if(Button(new Rect(w-177,10,165,42),s.Paused?"继续 / P":"暂停 / P",dim))s.Paused=!s.Paused;
            Rect field=new Rect(12,69,w-202,h-121),rail=new Rect(w-179,69,167,h-121);
            GUI.BeginGroup(field);float fw=field.width,fh=field.height;
            Fill(new Rect(0,0,fw,fh),new Color(.025f,.018f,.022f));
            for(int i=0;i<stars.Length;i++)
            {
                float x=Mathf.Repeat(stars[i].x*fw-s.MotionTime*(23+i%3*18),fw),y=stars[i].y*fh;
                Line(new Vector2(x,y),new Vector2(x+3+i%3*3,y),new Color(.42f,.30f,.22f,.25f),1);
            }
            // Filled tissue outside the moving silhouette, with layered ribs at different depths.
            Vector2 lastTop=Vector2.zero,lastBottom=Vector2.zero;
            for(float x=0;x<=fw+5;x+=5)
            {
                float nx=x/fw,top=s.Ceiling(nx)*fh,bottom=s.Floor(nx)*fh;
                var tissue=new Color(.19f+.035f*Mathf.Sin((nx+s.MotionTime*.19f)*24),.046f,.05f);
                Fill(new Rect(x,0,6,Mathf.Max(0,top)),tissue);Fill(new Rect(x,bottom,6,Mathf.Max(0,fh-bottom)),tissue);
                if(x>0){Line(lastTop,new Vector2(x,top),new Color(.67f,.23f,.20f),2);Line(lastBottom,new Vector2(x,bottom),new Color(.67f,.23f,.20f),2);}
                if(((int)x/5)%5==0){Line(new Vector2(x,top-5),new Vector2(x-22,Mathf.Max(0,top-36)),new Color(.29f,.08f,.075f),2);Line(new Vector2(x,bottom+5),new Vector2(x-18,bottom+35),new Color(.29f,.08f,.075f),2);}
                lastTop=new Vector2(x,top);lastBottom=new Vector2(x,bottom);
            }
            foreach(var o in s.Obstacles)
            {
                float age=s.MotionAge(o.encounter);
                if(o.hit)
                {
                    if(age>2)continue;
                    for(int n=0;n<9;n++)
                    {
                        var p=new Vector2((.59f-age*.19f)*fw,o.y*fh)+new Vector2(Mathf.Cos(n*2.4f+o.seed)*age*75,Mathf.Sin(n*2.4f+o.seed)*age*90);
                        float nx=Mathf.Clamp01(p.x/fw);p.y=Mathf.Clamp(p.y,s.Ceiling(nx)*fh+12,s.Floor(nx)*fh-12);Line(p,p+new Vector2(Mathf.Cos(n)*10,Mathf.Sin(n)*10),new Color(.78f,.52f,.27f,1-age/2),o.ram?7:3);
                    }
                    continue;
                }
                if(o.x<-.1f||o.x>1.1f)continue;
                var pos=new Vector2(o.x*fw,o.y*fh);float size=o.size*fh;
                if(o.ram)
                {
                    // A compacted mass of stone and dead wood leaves only narrow wall gaps.
                    float width=fw*.065f;
                    for(float dx=-width*.5f;dx<width*.5f;dx+=3)
                    {
                        float nx=(pos.x+dx)/fw,ceiling=s.Ceiling(nx)*fh,floor=s.Floor(nx)*fh,gap=floor-ceiling;
                        float edge=.045f+.018f*Mathf.Sin(dx*.31f+o.seed),top=ceiling+gap*edge,bottom=floor-gap*edge;
                        float shade=.38f+.045f*Mathf.Sin(dx*.28f+o.seed);Fill(new Rect(pos.x+dx,top,4,bottom-top),new Color(shade,shade*.80f,shade*.60f));
                        for(int n=1;n<9;n++){float yy=Mathf.Lerp(top,bottom,n/9f);Fill(new Rect(pos.x+dx,yy+Mathf.Sin(dx*.19f+n)*5,4,2),new Color(.23f,.18f,.13f));}
                    }
                    float topY=s.Ceiling(o.x)*fh,bottomY=s.Floor(o.x)*fh;
                    for(int n=0;n<4;n++){float yy=Mathf.Lerp(topY,bottomY,.18f+n*.20f);Line(new Vector2(pos.x-width*.36f,yy-8),new Vector2(pos.x+width*.32f,yy+11),new Color(.28f,.16f,.075f),5);}
                    if(age>-2&&age<0)Label(new Rect(pos.x-105,topY+24,210,24),"通道堵塞 · 必须撞开",13,amber,TextAnchor.MiddleCenter);
                }
                else if(o.tree)
                {
                    var v=new Vector2(Mathf.Cos(o.seed+age*.2f),Mathf.Sin(o.seed+age*.2f));var tip=pos+v*size*1.8f;
                    Line(pos-v*size*1.8f,tip,new Color(.45f,.30f,.17f),7);
                    for(int n=0;n<4;n++){var b=Vector2.Lerp(pos-v*size,tip,n/4f);Line(b,b+new Vector2(-v.y,v.x)*size*(n%2==0?1:-1),new Color(.58f,.39f,.21f),3);}
                }
                else EscapeSprite(escapeRock,pos,age*10+o.seed*30,size*2.5f,Color.white);

            }
            Vector2 leader=new Vector2(s.Leader.x*fw,s.Leader.y*fh),player=new Vector2(s.Player.x*fw,s.Player.y*fh);
            var behind=leader+TowOffset(new Vector2(-22,0),s.LeaderAngle);var front=player+TowOffset(new Vector2(18,0),s.PlayerAngle);
            Line(behind,front,new Color(.2f,.7f,.65f,.2f),6);Line(behind,front,new Color(.58f,1,.83f),1.5f);
            for(int n=0;n<7;n++){float f=Mathf.Repeat(n/7f+s.MotionTime*.28f,1);var p=Vector2.Lerp(behind,front,f);Fill(new Rect(p.x-2,p.y-2,4,4),green);}
            Sprite(ship,player,s.PlayerAngle,43,green);EscapeSprite(escapeShip,leader,s.LeaderAngle,65,s.ImpactAge<.25f?Color.white:amber);
            Line(behind,behind-new Vector2(18+4*Mathf.Sin(s.MotionTime*31),0),amber,4);
            Label(new Rect(player.x-45,player.y+26,135,22),"PROBE 04 / 被牵引",12,green);
            Label(new Rect(leader.x-35,leader.y+30,140,22),"米霍尔 / 领航",12,amber);
            if(s.ImpactAge<.65f){float a=1-s.ImpactAge/.65f;Fill(new Rect(0,0,fw,3),new Color(1,.35f,.16f,a));Fill(new Rect(0,fh-3,fw,3),new Color(1,.35f,.16f,a));}
            if(s.Progress>.90f){float alpha=Mathf.InverseLerp(.90f,1,s.Progress);Fill(new Rect(fw*(1-alpha*.18f),0,fw*.2f,fh),new Color(.7f,.9f,.83f,alpha*.2f));Label(new Rect(fw-190,fh/2-15,180,30),"出口信号 · 已锁定",17,green);}
            if(s.Paused){Fill(new Rect(0,0,fw,fh),new Color(0,0,0,.75f));Label(new Rect(0,fh/2-20,fw,40),"牵引航线已暂停 / P 或 ESC 继续",22,ink,TextAnchor.MiddleCenter);}
            GUI.EndGroup();Border(field,dim);Fill(rail,new Color(.035f,.08f,.067f));Border(rail,dim);
            Meter(new Rect(rail.x+12,rail.y+38,143,10),s.Hull/100,s.Hull<70?amber:green,"米霍尔船体"+Mathf.CeilToInt(s.Hull)+"/100");
            Label(new Rect(rail.x+12,rail.y+112,145,23),"距出口",13,dim);
            Label(new Rect(rail.x+12,rail.y+139,145,34),Mathf.CeilToInt(s.Distance).ToString("N0")+" m",24,green);
            Label(new Rect(rail.x+12,rail.y+197,145,23),"米霍尔 · 通讯中",13,amber);
            bool wrap=textStyle.wordWrap;textStyle.wordWrap=true;
            var textRect=new Rect(rail.x+12,rail.y+230,143,Mathf.Max(55,rail.height-245));
            string text=EscapeText??"";if(text!=escapePreviousText){escapeScroll=Vector2.zero;escapePreviousText=text;}textStyle.fontSize=16;
            float th=textStyle.CalcHeight(new GUIContent(text),textRect.width-14);
            escapeScroll=GUI.BeginScrollView(textRect,escapeScroll,new Rect(0,0,textRect.width-14,Mathf.Max(th,textRect.height)));Label(new Rect(0,0,textRect.width-14,Mathf.Max(th,textRect.height)),text,16,ink,TextAnchor.UpperLeft);GUI.EndScrollView();textStyle.wordWrap=wrap;
            Label(new Rect(15,h-44,w-25,20),"引擎动力失效 · 由米霍尔牵引 · 舵面姿态随牵引航向调整",14,amber);
            Label(new Rect(15,h-23,w-25,18),"航线自动执行   /   P 暂停   ESC 暂停/继续   M 静音",12,dim);
        }
        Vector2 escapeScroll;
        string escapePreviousText;
        Texture2D escapeShip,escapeRock;
        Texture2D[] escapeShipFrames,escapeRockFrames;
        void EnsureEscapeArt()
        {
            if(escapeShip)return;
            escapeShip=new Texture2D(96,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,name="Mihuoer tow craft"};
            escapeRock=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,name="Escape rock"};
            for(int y=0;y<64;y++)for(int x=0;x<96;x++)
            {
                float u=(x-48)/44f,v=(y-32)/28f;Color c=Color.clear;
                // Transparent canopy over the oval salvage hull and two towing nacelles.
                if(u*u+v*v*5<.85f)c=new Color(.5f,.58f,.6f);
                if(u*u/.56f+(v+.16f)*(v+.16f)/.20f<1)c=new Color(.22f,.6f,.66f,.84f);
                if(Mathf.Abs(u)<.65f&&v<-.12f&&v>-.24f)c=new Color(.85f,.91f,.84f);
                if(u<-.35f&&u>-.85f&&Mathf.Abs(v)>.27f&&Mathf.Abs(v)<.45f)c=new Color(.8f,.67f,.43f);
                if(u<-.72f&&u>-.94f&&Mathf.Abs(v)>.27f&&Mathf.Abs(v)<.43f)c=Color.white;
                if(v>-.02f&&v<.08f&&u>-.2f&&u<.68f)c=new Color(.72f,.72f,.63f);
                escapeShip.SetPixel(x,y,c);
            }
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float u=(x-31.5f)/30,v=(y-31.5f)/30,a=Mathf.Atan2(v,u);float edge=.8f+.1f*Mathf.Sin(a*5)+.05f*Mathf.Sin(a*9);
                float n=Mathf.PerlinNoise(x*.18f,y*.18f);Color c=new Color(.30f+n*.16f-u*.06f,.27f+n*.13f-u*.05f,.23f+n*.10f-u*.04f);
                if(u*u+v*v>edge*edge)c=Color.clear;else if(Mathf.Abs(v-u*.4f+.12f)<.05f)c*=.65f;
                escapeRock.SetPixel(x,y,c);
            }
            escapeShip.Apply();escapeRock.Apply();escapeShipFrames=BakeEscapeFrames(escapeShip);escapeRockFrames=BakeEscapeFrames(escapeRock);
        }
        void EscapeSprite(Texture2D texture,Vector2 center,float angle,float size,Color tint)
        {
            var color=GUI.color;
            try{GUI.color=tint;float height=size*texture.height/texture.width;var frames=texture==escapeShip?escapeShipFrames:escapeRockFrames;int frame=Mathf.RoundToInt(Mathf.Repeat(angle,360)/5)%72;GUI.DrawTexture(new Rect(center.x-size*.5f,center.y-height*.5f,size,height),frames[frame]);}
            finally{GUI.color=color;}
        }
        static Vector2 TowOffset(Vector2 offset,float angle)
        {float a=Mathf.Round(angle/5)*5*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(a)*offset.x-Mathf.Sin(a)*offset.y,Mathf.Sin(a)*offset.x+Mathf.Cos(a)*offset.y);}
        static Texture2D[] BakeEscapeFrames(Texture2D source)
        {
            int w=source.width,h=source.height;float cx=(w-1)*.5f,cy=(h-1)*.5f;var pixels=source.GetPixels32();var frames=new Texture2D[72];
            for(int n=0;n<72;n++)
            {
                float a=n*5*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);var dst=new Color32[w*h];
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){float dx=x-cx,dy=y-cy;int sx=Mathf.RoundToInt(c*dx-s*dy+cx),sy=Mathf.RoundToInt(s*dx+c*dy+cy);if(sx>=0&&sx<w&&sy>=0&&sy<h)dst[y*w+x]=pixels[sy*w+sx];}
                frames[n]=new Texture2D(w,h,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};frames[n].SetPixels32(dst);frames[n].Apply();
            }
            return frames;
        }
        void DisposeEscapeArt(){if(escapeShipFrames!=null)foreach(var t in escapeShipFrames)if(t)Destroy(t);if(escapeRockFrames!=null)foreach(var t in escapeRockFrames)if(t)Destroy(t);if(escapeShip)Destroy(escapeShip);if(escapeRock)Destroy(escapeRock);}
    }
}


