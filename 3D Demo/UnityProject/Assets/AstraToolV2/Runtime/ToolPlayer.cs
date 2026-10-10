using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    public sealed class ToolPlayer : MonoBehaviour
    {
        public ToolPackage package;
        public bool autoPlay = true;
        // Hosts render their own UI and advance only while their application is visible.
        public bool showGUI = true, externalClock;
        public int presentationLayer = -1;
        public bool IsFinished => finished;
        public bool IsPaused => paused;
        public string Failure { get; private set; }
        public string CurrentSpeaker => speaker;
        public float ChoiceSecondsRemaining => waiting;
        public string[] Participants => live.Values.Where(v=>v).Select(v=>v.name).ToArray();
        public ToolChoice[] CurrentChoices => finished || node == null ? Array.Empty<ToolChoice>() :
            (node.kind == ToolNodeKind.GlobalChoice ? node.choices : activeDialogue != null && activeDialogue.kind == ToolDialogueKind.Choice ? activeDialogue.choices : null) ?? Array.Empty<ToolChoice>();
        public bool IsChoosing => !finished && node != null && (node.kind == ToolNodeKind.GlobalChoice || activeDialogue != null && activeDialogue.kind == ToolDialogueKind.Choice);
        public event Action<string> Completed;
        public bool CanChoose(ToolChoice choice) => !choice.hasCondition || ToolLogic.Test(choice.condition,Stats);
        public void SetPaused(bool value) { paused=value; }
        void Fail(string reason) { Failure=reason; paused=true; }
        void SetPresentationLayer(GameObject root)
        {
            if(presentationLayer<0)return;
            foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=presentationLayer;
            foreach(var light in root.GetComponentsInChildren<Light>(true))light.cullingMask=1<<presentationLayer;
        }
        public Camera lens;
        public string CurrentNodeId => node==null?"":node.id;
        public float CurrentTime => clock;
        public string CurrentSubtitle => subtitle;
        public Dictionary<string,int> Stats = new Dictionary<string,int>();
        readonly Dictionary<string,GameObject> live = new Dictionary<string,GameObject>();
        ToolGraphNode node;
        ToolStage stage;
        float clock,waiting,patienceStart;
        string subtitle="",speaker="";
        ToolDialogueSpan activeDialogue;
        bool paused,finished;
        int transitions;
        void Start(){if(autoPlay)Begin();}
        public void Stop()
        {
            paused=true;finished=false;Failure=null;subtitle="";activeDialogue=null;node=null;foreach(var actor in live.Values)if(actor)Destroy(actor);live.Clear();
            if(stage)Destroy(stage.gameObject);stage=null;
        }
        public void Begin()
        {
            Stop();
            if(!package||!package.graph||!package.library){Fail("演出包缺少演出树或资源库");return;}
            Stats.Clear();foreach(var stat in package.initialStats)Stats[stat.key]=stat.value;
            Stats["patience"]=package.initialPatience;
            if(!lens)lens=Camera.main;if(!lens)lens=new GameObject("Tool camera").AddComponent<Camera>();
            lens.clearFlags=CameraClearFlags.SolidColor;lens.backgroundColor=new Color(.035f,.055f,.045f);
            if(!lens.GetComponent<ToolScreenFx>())lens.gameObject.AddComponent<ToolScreenFx>();
            lens.GetComponent<ToolScreenFx>().enabled=true;
            if(!stage){var root=new GameObject("Tool stage");stage=root.AddComponent<ToolStage>();}
            if(!stage.IsBuilt)stage.Build();
            stage.transform.SetParent(transform,false);SetPresentationLayer(stage.gameObject);
            transitions=0;finished=false;paused=false;Enter(package.graph.entryNode);
        }
        void Enter(string id)
        {
            if(++transitions>128){Fail("演出树超过 128 次跳转，请检查循环");return;}
            node=package.graph.Node(id);clock=0;waiting=0;activeDialogue=null;subtitle="";speaker="";
            if(node==null){Fail("演出树出口未连接有效节点");return;}
            if(node.kind==ToolNodeKind.Condition)
            {
                Enter(ToolLogic.Exit(node,ToolLogic.Test(node.condition,Stats)?"yes":"no"));return;
            }
            if(node.kind==ToolNodeKind.End){finished=true;Completed?.Invoke(node.id);return;}
            if(node.kind==ToolNodeKind.GlobalChoice){waiting=Mathf.Min(node.patienceSeconds,Mathf.Max(0,Stats["patience"]));patienceStart=waiting;subtitle=node.prompt;return;}
            foreach(var actor in live.Values)if(actor){actor.SetActive(false);Destroy(actor);}
            live.Clear();
            if(!node.unit){Fail("演出节点缺少单元");return;}
            RenderUnit(0);
        }
        void Update()
        {
            if(externalClock)return;
            if(Input.GetKeyDown(KeyCode.Space))paused=!paused;
            Tick(Time.deltaTime);
        }
        public void Tick(float dt)
        {
            if(node==null||finished||paused||Failure!=null)return;
            dt=Mathf.Max(0,dt);
            if(node.kind==ToolNodeKind.GlobalChoice)
            {
                waiting-=dt;if(waiting<=0)Choose("silence");return;
            }
            if(node.unit==null){Fail("演出节点缺少单元");return;}
            clock+=dt;RenderUnit(Mathf.Min(clock,Mathf.Max(0,node.unit.duration-.001f)));
            if(activeDialogue!=null&&activeDialogue.kind==ToolDialogueKind.Choice)
            {
                float start=activeDialogue.start+activeDialogue.Duration;
                waiting=Mathf.Max(0,start-clock);
                if(waiting<=0){Choose("silence");return;}
            }
            else if(clock>=node.unit.duration)Enter(ToolLogic.Exit(node,"next"));
        }
        public void Seek(float time) {if(node!=null&&node.kind==ToolNodeKind.Unit&&node.unit){clock=Mathf.Clamp(time,0,node.unit.duration);RenderUnit(clock);}}
        public void Choose(string slot)
        {
            if(node==null||finished||paused||Failure!=null)return;
            ToolChoice choice=null;
            if(node.kind==ToolNodeKind.GlobalChoice)choice=ToolLogic.Choice(node.choices,slot);
            else if(activeDialogue!=null&&activeDialogue.kind==ToolDialogueKind.Choice)choice=ToolLogic.Choice(activeDialogue.choices,slot);
            else return;
            if(choice!=null)
            {
                if(choice.hasCondition&&!ToolLogic.Test(choice.condition,Stats))return;
                ToolLogic.Apply(choice.effects,Stats);
            }
            if(slot!="silence"&&choice==null)return;
            int cost=Mathf.CeilToInt(node.kind==ToolNodeKind.GlobalChoice?patienceStart-waiting:clock-activeDialogue.start);
            Stats["patience"]=Mathf.Max(0,Stats["patience"]-Mathf.Max(0,cost));
            Enter(ToolLogic.Exit(node,slot));
        }
        void RenderUnit(float time)
        {
            var unit=node.unit;
            var activeActors=unit.actors.Where(a=>a.start<=time&&a.start+a.duration>time).ToArray();
            var ids=new HashSet<string>(activeActors.Select(a=>a.id));
            foreach(var old in live.Keys.ToArray())if(!ids.Contains(old)){Destroy(live[old]);live.Remove(old);}
            for(int i=0;i<activeActors.Length;i++)
            {
                var a=activeActors[i];if(!a.character)continue;
                if(!live.TryGetValue(a.id,out var go)||!go){go=ToolCharacterView.Create(a.character,stage.transform);live[a.id]=go;SetPresentationLayer(go);}
                go.transform.localPosition=new Vector3((i-(activeActors.Length-1)*.5f)*1.7f,0,0);
                var action=unit.actions.LastOrDefault(x=>x.actorId==a.id&&x.start<=time&&x.start+x.duration>time);
                go.GetComponent<ToolMotion>().SampleTimeline(unit,a.id,time);
            }
            var scene=unit.scenes.LastOrDefault(s=>s.start<=time&&s.start+s.duration>time);
            var camera=unit.cameras.LastOrDefault(c=>c.start<=time&&c.start+c.duration>time);
            var currentActor=camera!=null?camera.actorId:activeActors.Length>0?activeActors[0].id:null;
            live.TryGetValue(currentActor??"",out var subject);
            live.TryGetValue(camera==null?"":camera.partnerId??"",out var partner);
            bool crawler=activeActors.Any(a=>a.id==currentActor&&a.character&&a.character.species==ToolSpecies.CrawlerAlien);
            bool tall=activeActors.Any(a=>a.id==currentActor&&a.character&&a.character.species==ToolSpecies.StandingAlien);
            foreach(var liveActor in live.Values)liveActor.transform.rotation=Quaternion.identity;
            ToolBlocking.Apply(camera,subject?subject.transform:null,partner?partner.transform:null);
            ToolCameraMath.Apply(lens,camera,subject?subject.transform:null,partner?partner.transform:null,crawler,tall,time);
            var fx=camera==null?ToolFx.None:camera.fx;lens.GetComponent<ToolScreenFx>().effect=fx;
            stage.Set(package.library.Scene(scene==null?"archive":scene.sceneId),fx,time);
            activeDialogue=unit.dialogue.LastOrDefault(d=>d.start<=time&&(d.kind==ToolDialogueKind.Choice||d.start+d.Duration>time));
            if(activeDialogue==null){subtitle="";speaker="";return;}
            var actor=unit.actors.FirstOrDefault(a=>a.id==activeDialogue.actorId);
            speaker=actor!=null&&actor.character?actor.character.displayName:"系统";
            if(activeDialogue.kind==ToolDialogueKind.Text)
            {
                int count=Mathf.Clamp(Mathf.FloorToInt((time-activeDialogue.start)*activeDialogue.charactersPerSecond),0,activeDialogue.text.Length);
                subtitle=activeDialogue.text.Substring(0,count);
            }
            else subtitle=activeDialogue.text;
        }
        void OnGUI()
        {
            if(!showGUI||!package||node==null)return;
            bool choosing=node.kind==ToolNodeKind.GlobalChoice||activeDialogue!=null&&activeDialogue.kind==ToolDialogueKind.Choice;
            var choices=choosing?(node.kind==ToolNodeKind.GlobalChoice?node.choices:activeDialogue.choices)??Array.Empty<ToolChoice>():Array.Empty<ToolChoice>();
            float width=Mathf.Max(200,Screen.width-40);
            int columns=Mathf.Clamp(Mathf.FloorToInt((width-40)/230),1,4);
            int rows=choosing?Mathf.CeilToInt((float)choices.Length/columns):0;
            var label=new GUIStyle(GUI.skin.label){fontSize=18,alignment=TextAnchor.UpperLeft,wordWrap=true};
            label.normal.textColor=new Color(.94f,.94f,.83f);
            float subtitleHeight=Mathf.Max(56,label.CalcHeight(new GUIContent(subtitle),width-34));
            float buttonY=42+subtitleHeight+6;
            float height=choosing?buttonY+rows*50+12:buttonY+28;
            var panel=new Rect(20,Screen.height-height-20,width,height);
            Color previous=GUI.color;
            GUI.color=new Color(.035f,.055f,.045f,.96f);
            GUI.DrawTexture(panel,Texture2D.whiteTexture);
            GUI.color=previous;
            var small=new GUIStyle(label){fontSize=15,alignment=TextAnchor.MiddleRight};
            var button=new GUIStyle(GUI.skin.button){fontSize=16,wordWrap=true};
            GUI.Label(new Rect(panel.x+17,panel.y+9,panel.width-34,26),"ASTRA / "+node.id+"     "+speaker,label);
            GUI.Label(new Rect(panel.x+17,panel.y+42,panel.width-34,subtitleHeight),subtitle,label);
            if(choosing&&!finished)
            {
                float gap=8,buttonWidth=(panel.width-34-gap*(columns-1))/columns;
                for(int i=0;i<choices.Length;i++)
                {
                    var c=choices[i];bool enabled=!c.hasCondition||ToolLogic.Test(c.condition,Stats);
                    GUI.enabled=enabled;
                    var rect=new Rect(panel.x+17+(i%columns)*(buttonWidth+gap),panel.y+buttonY+(i/columns)*50,buttonWidth,42);
                    if(GUI.Button(rect,c.text,button))Choose(c.id);
                }
                GUI.enabled=true;
                GUI.Label(new Rect(panel.xMax-250,panel.y+8,232,24),"沉默倒计时 "+Mathf.CeilToInt(waiting)+"s",small);
            }
            if(finished&&GUI.Button(new Rect(Screen.width-150,20,130,35),"重新播放",button))Begin();
        }
    }
}
