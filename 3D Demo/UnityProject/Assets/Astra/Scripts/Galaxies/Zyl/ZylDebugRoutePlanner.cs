#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstraCabin.Navigation;
namespace AstraCabin.Galaxies.Zyl
{
    [Serializable] public sealed class ZylDebugRouteStep {public string sectorId,nodeId;}
    [Serializable] public sealed class ZylDebugRequest {public Campaign campaign;public VoyageSave prefix;public string sectorId,nodeId;}
    public sealed class ZylDebugRoute {public readonly List<ZylDebugRouteStep> steps=new List<ZylDebugRouteStep>();public string label;}
    public static class ZylDebugRoutePlanner
    {
        public static List<ZylDebugRoute> FindRoutes(Campaign campaign,string sectorId,string nodeId)
        {
            var routes=new List<ZylDebugRoute>();var start=campaign.sectors.First(s=>s.id==campaign.startSectorId);
            Visit(campaign,start,start.startNodeId,sectorId,nodeId,new List<ZylDebugRouteStep>(),routes);
            return routes;
        }
        static void Visit(Campaign c,Sector sector,string nodeId,string targetSector,string targetNode,List<ZylDebugRouteStep> prefix,List<ZylDebugRoute> result)
        {
            if(prefix.Any(s=>s.sectorId==sector.id&&s.nodeId==nodeId))throw new InvalidDataException("航路包含环");
            var node=sector.nodes.First(n=>n.id==nodeId);prefix.Add(new ZylDebugRouteStep{sectorId=sector.id,nodeId=nodeId});
            if(sector.id==targetSector&&nodeId==targetNode)
            {
                if(result.Count>=256)throw new InvalidDataException("前置路线超过256条，请缩小配置范围");
                var route=new ZylDebugRoute();route.steps.AddRange(prefix);route.label=string.Join(" → ",prefix.Select(s=>c.sectors.First(x=>x.id==s.sectorId).nodes.First(n=>n.id==s.nodeId).name));result.Add(route);
            }
            else if(node.exit&&!string.IsNullOrEmpty(sector.nextSectorId)){var next=c.sectors.First(s=>s.id==sector.nextSectorId);Visit(c,next,next.startNodeId,targetSector,targetNode,prefix,result);}
            else foreach(var next in node.next)Visit(c,sector,next,targetSector,targetNode,prefix,result);
            prefix.RemoveAt(prefix.Count-1);
        }
        static void Apply(NavigationEngine engine,VoyageSave save,string kind,string id)
        {
            var command=new Command{kind=kind,id=id};string error;if(!engine.Apply(command,out error))throw new InvalidDataException(error);save.commands.Add(command);
        }
        static void Settle(NavigationEngine engine,VoyageSave save)
        {
            var e=engine.Pending;if(e==null)return;
            if(e.type=="performance")throw new InvalidDataException("前置路线含performance会议，需要明确结局；本工具不伪造会议结局");
            if(engine.Registry.Find(e.type).NeedsObservation)Apply(engine,save,"observed",e.id);
            var choice=e.choices.FirstOrDefault(x=>x.costs.Length==0&&engine.CanPay(x.costs))??e.choices.FirstOrDefault(x=>engine.CanPay(x.costs));
            if(choice==null)throw new InvalidDataException("前置事件资源不足："+e.title);
            Apply(engine,save,"choose",choice.id);
        }
        public static ZylDebugRequest Build(Campaign campaign,ZylDebugRoute route)
        {
            if(route==null||route.steps.Count==0)throw new InvalidDataException("没有前置路线");
            var engine=new NavigationEngine(campaign,EventRegistry.CreateDefault());var save=new VoyageSave{fingerprint=CampaignValidator.Fingerprint(campaign)};
            for(int i=0;i<route.steps.Count;i++)
            {
                var step=route.steps[i];
                if(engine.Sector.id!=step.sectorId){Settle(engine,save);Apply(engine,save,"advance",engine.Sector.id);}
                bool same=engine.Sector.id==step.sectorId&&engine.Node.id==step.nodeId;
                if(i==route.steps.Count-1)return new ZylDebugRequest{campaign=campaign,prefix=save,sectorId=step.sectorId,nodeId=step.nodeId};
                if(!same)Apply(engine,save,"travel",step.nodeId);Settle(engine,save);
            }
            throw new InvalidDataException("无法构造调试路线");
        }
        public static ZylDebugRequest Replay(Campaign latest,NavigationSession current)
        {
            if(current==null||current.Blocked||string.IsNullOrEmpty(current.Engine.Node.eventId))throw new InvalidDataException("当前信标没有可重播事件");
            string sector=current.Engine.Sector.id,node=current.Engine.Node.id;
            var engine=new NavigationEngine(latest,EventRegistry.CreateDefault());var save=new VoyageSave{fingerprint=CampaignValidator.Fingerprint(latest)};
            foreach(var command in current.Save.commands)
            {
                if(command.kind=="travel"&&engine.Sector.id==sector&&command.id==node)return new ZylDebugRequest{campaign=latest,prefix=save,sectorId=sector,nodeId=node};
                string error;if(!engine.Apply(command,out error))throw new InvalidDataException("当前路线无法用于最新配置："+error);
                save.commands.Add(new Command{kind=command.kind,id=command.id,outcome=command.outcome});
            }
            throw new InvalidDataException("当前事件入口记录未找到");
        }
    }
}
#endif
