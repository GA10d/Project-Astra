using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AstraCabin.Navigation;
using AstraCabin.Galaxies.Zyl;
namespace AstraCabin.Editor
{
    public sealed partial class ZylNavigationDebugWindow : EditorWindow
    {
        Campaign campaign;
        readonly List<ZylDebugRouteStep> targets=new List<ZylDebugRouteStep>();
        List<ZylDebugRoute> routes=new List<ZylDebugRoute>();
        string[] names=new string[0];
        int targetIndex,routeIndex;
        string message="";
        Vector2 scroll;
        [MenuItem("Astra/Zyl 星域导航调试")]
        public static void Open(){var window=GetWindow<ZylNavigationDebugWindow>("ZYL 导航调试");window.minSize=new Vector2(480,440);}
        void OnEnable(){Reload();EditorApplication.update+=Repaint;}
        void OnDisable(){EditorApplication.update-=Repaint;}
        Campaign ReadLatest()
        {
            var c=JsonUtility.FromJson<Campaign>(NavigationStorage.ReadLimited(Path.Combine(Application.streamingAssetsPath,"Navigation/zyl/campaign.json")));
            var errors=CampaignValidator.Validate(c,EventRegistry.CreateDefault());if(errors.Count>0)throw new InvalidDataException(string.Join("；",errors.Take(5)));return c;
        }
        void Reload()
        {
            try
            {
                string old=!string.IsNullOrEmpty(inventoryTargetNode)?inventoryTargetSector+"/"+inventoryTargetNode:targets.Count>targetIndex?targets[targetIndex].sectorId+"/"+targets[targetIndex].nodeId:null;
                campaign=ReadLatest();targets.Clear();var labels=new List<string>();
                foreach(var sector in campaign.sectors)foreach(var node in sector.nodes){targets.Add(new ZylDebugRouteStep{sectorId=sector.id,nodeId=node.id});labels.Add(sector.name+" / "+node.name+" ["+node.id+"]");}
                names=labels.ToArray();targetIndex=Math.Max(0,targets.FindIndex(t=>t.sectorId+"/"+t.nodeId==old));NormalizeInventory(campaign);UpdateRoutes();message="已读取源码中的最新zyl星图。";
            }
            catch(Exception e){message=e.Message;campaign=null;}
        }
        void UpdateRoutes(){routeIndex=0;routes=campaign==null||targets.Count==0?new List<ZylDebugRoute>():ZylDebugRoutePlanner.FindRoutes(campaign,targets[targetIndex].sectorId,targets[targetIndex].nodeId);}
        void Queue(ZylDebugRequest request)
        {
            if(EditorApplication.isPlaying)
            {
                if(!ZylEditorDebugSession.Enabled)throw new InvalidOperationException("当前是普通Play。请先停止，再从本窗口启动隔离调试。");
            }
            else
            {
                if(!FindObjectOfType<SetpieceDirector>())throw new InvalidOperationException("当前场景没有飞船控制器。请先打开 Cabin04 或已有玩家船的场景。");
                ZylEditorDebugSession.EnableNewRun();
            }
            ZylEditorDebugSession.Queue(request);
            if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;
            message="已请求隔离调试；等待环境同步后进入目标事件。";
        }
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("ZYL / 星域导航调试",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("仅在Unity编辑器中使用。每次从本窗口进入Play都会创建独立调试存档；普通Play与正式存档不受影响。",MessageType.Info);
            if(GUILayout.Button("重新读取星图"))Reload();
            var nav=Application.isPlaying?FindObjectOfType<NavigationMode>():null;
            bool waiting=EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode!=EditorApplication.isPlaying||nav&&nav.EditorDebugBusy;
            using(new EditorGUI.DisabledScope(campaign==null||waiting))
            {
                if(names.Length>0){int selected=EditorGUILayout.Popup("起始信标",targetIndex,names);if(selected!=targetIndex){targetIndex=selected;inventoryTargetSector=targets[selected].sectorId;inventoryTargetNode=targets[selected].nodeId;UpdateRoutes();}}
                if(routes.Count>0)
                {
                    routeIndex=EditorGUILayout.Popup("前置路线",routeIndex,routes.Select((r,i)=>"路线 "+(i+1)+" / "+r.steps.Count+" 个信标").ToArray());
                    EditorGUILayout.HelpBox(routes[routeIndex].label,MessageType.None);
                }
                EditorGUILayout.HelpBox("前置演出直接记为完成，前置事件优先使用可结算的无消耗选项。库存面板可指定携带数量及自动补给。目标事件从入口正常播放；目标信标尚未结算。",MessageType.None);
                using(new EditorGUI.DisabledScope(routes.Count==0||Application.isPlaying&&!ZylEditorDebugSession.Enabled))
                    if(GUILayout.Button("从选中信标开始",GUILayout.Height(36)))try{Queue(BuildInventoryRequest());}catch(Exception e){message=e.Message;}
                using(new EditorGUI.DisabledScope(!nav||!ZylEditorDebugSession.Enabled||nav.Session==null||nav.ActiveGalaxyId!="zyl"||string.IsNullOrEmpty(nav.Session.Engine.Node.eventId)))
                    if(GUILayout.Button("重播当前事件",GUILayout.Height(32)))try{Queue(ReplayInventoryRequest(nav));}catch(Exception e){message=e.Message;}
            }
            DrawInventory(nav,waiting);
            if(Application.isPlaying&&!ZylEditorDebugSession.Enabled)EditorGUILayout.HelpBox("当前为普通Play，请停止后从本窗口启动隔离调试。",MessageType.Warning);
            if(nav&&nav.Session!=null)
            {
                EditorGUILayout.Space();EditorGUILayout.LabelField("当前信标",nav.Session.Engine.Node.name);
                EditorGUILayout.LabelField("运行状态",nav.EditorDebugBusy?"同步环境中":nav.Session.Engine.Pending==null?"无待处理事件":"事件："+nav.Session.Engine.Pending.title);
                if(!string.IsNullOrEmpty(nav.EditorDebugError))EditorGUILayout.HelpBox(nav.EditorDebugError,MessageType.Error);
            }
            if(ZylEditorDebugSession.Enabled){EditorGUILayout.LabelField("隔离存档目录");EditorGUILayout.SelectableLabel(ZylEditorDebugSession.Root,EditorStyles.wordWrappedMiniLabel,GUILayout.Height(42));}
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }
    [InitializeOnLoad]
    static class ZylNavigationDebugBoot
    {
        static ZylNavigationDebugBoot(){EditorApplication.update+=Update;EditorApplication.playModeStateChanged+=OnState;}
        static void OnState(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredEditMode)ZylEditorDebugSession.Disable();}
        static void Update()
        {
            if(!Application.isPlaying||!ZylEditorDebugSession.Enabled||EditorApplication.isCompiling)return;
            var nav=UnityEngine.Object.FindObjectOfType<NavigationMode>();if(!nav||!nav.Director||nav.EditorDebugBusy)return;
            var request=ZylEditorDebugSession.TakeRequest();if(request!=null)nav.StartCoroutine(nav.EditorApplyZylDebug(request));
        }
    }
}
