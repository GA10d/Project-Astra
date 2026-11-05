#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using AstraCabin.Navigation;
using AstraCabin.Galaxies.Zyl;
namespace AstraCabin
{
    public sealed partial class NavigationMode
    {
        public bool EditorDebugBusy {get;private set;}
        public string EditorDebugError {get;private set;}
        public IEnumerator EditorApplyZylDebug(ZylDebugRequest request)
        {
            if(!ZylEditorDebugSession.Enabled||EditorDebugBusy){EditorDebugError="请从调试窗口启动隔离Play模式";yield break;}
            EditorDebugBusy=true;EditorDebugError=null;
            try
            {
                Initialize();
                string expected=Path.Combine(ZylEditorDebugSession.Root,"Navigation");
                if(Path.GetFullPath(storageRoot)!=Path.GetFullPath(expected))throw new InvalidOperationException("当前运行未使用隔离存档，请停止Play后从调试窗口启动");
                if(request==null||request.campaign==null||request.prefix==null)throw new InvalidDataException("调试请求不完整");
                Parse(JsonUtility.ToJson(request.campaign));
                if(request.prefix.fingerprint!=CampaignValidator.Fingerprint(request.campaign))throw new InvalidDataException("调试快照与配置不匹配");
                var candidate=new NavigationEngine(request.campaign,Registry);
                foreach(var command in request.prefix.commands){string error;if(!candidate.Apply(command,out error))throw new InvalidDataException(error);}
                if(candidate.Sector.id!=request.sectorId||!candidate.Sector.nodes.Any(n=>n.id==request.nodeId))throw new InvalidDataException("目标信标与前置路线不匹配");
                bool atTarget=candidate.Node.id==request.nodeId;
                if(!atTarget&&candidate.TravelError(request.nodeId)!=null)throw new InvalidDataException(candidate.TravelError(request.nodeId));
                // All validation precedes presentation cleanup and any writes, always inside the per-run sandbox.
                OwnsPresentation=false;Session=null;
                if(computer.Meeting&&computer.Meeting.HasSession)computer.Meeting.Stop();
                Director.ResetToDefault();
                ActiveGalaxyId="zyl";string folder=GalaxyStorageFolder("zyl");
                ConfigPath=Path.Combine(folder,"campaign.json");SavePath=Path.Combine(folder,"voyage.json");ImportPath=GalaxyImportPath("zyl");
                NavigationStorage.WriteAtomic(ConfigPath,JsonUtility.ToJson(request.campaign,true));
                NavigationStorage.WriteAtomic(SavePath,JsonUtility.ToJson(request.prefix,true));
                Campaign=request.campaign;Session=new NavigationSession(Campaign,Registry,SavePath);if(Session.Blocked)throw new InvalidDataException(Session.Error);
                IsGalaxyReserved=false;IsGalaxyOverview=false;confirmation=null;showJournal=false;galaxyTransition=false;
                Exterior.EditorRefreshForDebug();
            }
            catch(Exception e){EditorDebugError=e.Message;EditorDebugBusy=false;}
            if(EditorDebugError!=null)yield break;
            // Let additive load/unload finish before starting another event; never leave an old stomach exterior behind.
            yield return null;
            while(Exterior.Busy)yield return null;
            if(Exterior.Error!=null){EditorDebugError=Exterior.Error;EditorDebugBusy=false;yield break;}
            bool swallowed=ZylStomachEnvironment.HasSwallowedVoyage("zyl",Session);
            if(Exterior.Ready!=swallowed){EditorDebugError="环境未与调试航程同步";EditorDebugBusy=false;yield break;}
            try
            {
                bool portalPassed=Session.Save.commands.Any(c=>c.kind=="observed"&&Campaign.events.Any(e=>e.id==c.id&&e.type=="setpiece"&&e.setpiece==SetpieceDirector.PortalMode));
                if(!swallowed&&portalPassed){Director.TriggerMode(SetpieceDirector.PortalMode);Director.portal.EditorRestoreCompleted();}
                computer.Open();computer.OpenApp("navigation");IsGalaxyOverview=false;
                selectedId=request.nodeId;detailScroll=journalScroll=Vector2.zero;
                if(Session.Engine.Node.id!=request.nodeId&&!Travel(request.nodeId))throw new InvalidOperationException(Message);
                Message="隔离调试 · "+Session.Engine.Node.name+"（前置演出已跳过）";
            }
            catch(Exception e){EditorDebugError=e.Message;}
            finally{EditorDebugBusy=false;}
        }
    }
}
#endif
