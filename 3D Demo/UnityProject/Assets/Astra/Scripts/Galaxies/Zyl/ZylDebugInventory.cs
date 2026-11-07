#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AstraCabin.Navigation;
using AstraCabin.Galaxies.Zyl;
namespace AstraCabin.Galaxies.Zyl
{
    public static class ZylDebugInventory
    {
        public const string CommandKind="zylDebugInventory";
        public static bool IsIsolatedPath(string path)
        {
            if(!ZylEditorDebugSession.Enabled||string.IsNullOrEmpty(path))return false;
            try{return Path.GetFullPath(path).StartsWith(Path.GetFullPath(Path.Combine(ZylEditorDebugSession.Root,"Navigation"))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
            catch{return false;}
        }
        public static string Validate(Campaign c,ItemAmount[] items)
        {
            if(items==null||items.Length>64)return "调试库存列表无效";
            var seen=new HashSet<string>();
            foreach(var item in items)
            {
                var definition=item==null?null:(c.items??new ItemDefinition[0]).FirstOrDefault(x=>x.id==item.itemId);
                if(definition==null||!seen.Add(item.itemId)||item.amount<0||item.amount>definition.capacity)return "调试物品不存在、重复或数量超出容量";
            }
            return null;
        }
        public static Command Create(Campaign c,ItemAmount[] items)
        {
            string error=Validate(c,items);if(error!=null)throw new InvalidDataException(error);
            return new Command{kind=CommandKind,id=Guid.NewGuid().ToString("N"),outcome=JsonUtility.ToJson(new ItemDelivery{items=items})};
        }
        public static bool Apply(NavigationEngine engine,Command command,out string error)
        {
            error=null;Guid id;
            if(!Guid.TryParseExact(command.id,"N",out id)||string.IsNullOrEmpty(command.outcome)){error="调试库存指令无效";return false;}
            ItemDelivery value;try{value=JsonUtility.FromJson<ItemDelivery>(command.outcome);}catch{error="调试库存数据无效";return false;}
            error=Validate(engine.Campaign,value?.items);if(error!=null)return false;
            // All rows validate before any mutation; zero and unbound items are intentional here.
            foreach(var item in value.items)engine.Items[item.itemId]=item.amount;
            engine.Journal.Add("[隔离调试] 设置库存："+string.Join("、",value.items.Select(x=>engine.Campaign.items.First(d=>d.id==x.itemId).name+" ×"+x.amount)));return true;
        }
        public static void Record(NavigationEngine engine,VoyageSave save,ItemAmount[] values)
        {
            if(values==null||values.Length==0)return;
            string invalid=Validate(engine.Campaign,values);if(invalid!=null)throw new InvalidDataException(invalid);
            var changes=values.Where(x=>!engine.Items.ContainsKey(x.itemId)||engine.Items[x.itemId]!=x.amount).ToArray();if(changes.Length==0)return;
            var command=Create(engine.Campaign,changes);string error;if(!engine.Apply(command,out error))throw new InvalidDataException(error);save.commands.Add(command);
        }
        public static ItemAmount[] Minimum(params ItemAmount[][] sets)
        {
            var result=new Dictionary<string,int>();
            foreach(var set in sets)foreach(var value in set??new ItemAmount[0]){int before;result.TryGetValue(value.itemId,out before);result[value.itemId]=Math.Max(before,value.amount);}
            return result.Select(x=>new ItemAmount{itemId=x.Key,amount=x.Value}).ToArray();
        }
        public static ItemAmount[] ChoiceMinimum(EventDefinition e)
        {
            return e==null?new ItemAmount[0]:Minimum(e.choices.SelectMany(o=>new[]{o.requiredItems,o.itemCosts}).ToArray());
        }
        public static ItemAmount[] TargetMinimum(Campaign c,MapNode node,bool alreadyHere)
        {
            var e=c.events.FirstOrDefault(x=>x.id==node.eventId);var after=ChoiceMinimum(e);
            if(alreadyHere)return after;
            // Arrival costs are spent before choices become available. Requirements only inspect holdings.
            var need=Minimum(node.requiredItems,node.itemCosts).ToDictionary(x=>x.itemId,x=>x.amount);
            foreach(var value in after)
            {
                int spent=(node.itemCosts??new ItemAmount[0]).Where(x=>x.itemId==value.itemId).Sum(x=>x.amount),before;need.TryGetValue(value.itemId,out before);need[value.itemId]=Math.Max(before,spent+value.amount);
            }
            return need.Select(x=>new ItemAmount{itemId=x.Key,amount=x.Value}).ToArray();
        }
        public static ItemAmount[] TopUp(NavigationEngine engine,ItemAmount[] minimum)
        {
            return minimum.Where(x=>engine.Items[x.itemId]<x.amount).Select(x=>new ItemAmount{itemId=x.itemId,amount=x.amount}).ToArray();
        }
        public static void Supply(NavigationEngine engine,VoyageSave save,params ItemAmount[][] sets)
        {Record(engine,save,TopUp(engine,Minimum(sets)));}
    }
}
namespace AstraCabin
{
    public sealed partial class NavigationMode
    {
        public bool EditorCanEditInventory
        {
            get{return Application.isPlaying&&!EditorDebugBusy&&Session!=null&&!Session.Blocked&&ActiveGalaxyId=="zyl"&&ZylDebugInventory.IsIsolatedPath(SavePath)&&string.Equals(Path.GetFullPath(Session.Path),Path.GetFullPath(SavePath),StringComparison.OrdinalIgnoreCase);}
        }
        public bool EditorSetDebugInventory(ItemAmount[] items)
        {
            if(!EditorCanEditInventory){EditorDebugError="仅允许修改本次隔离调试航程的库存；普通Play或同步中不可操作。";return false;}
            try
            {
                var command=ZylDebugInventory.Create(Campaign,items);
                if(items.All(x=>Session.Engine.Items[x.itemId]==x.amount)){EditorDebugError=null;Message="隔离调试库存已是指定数量。";return true;}
                if(!Session.Commit(command.kind,command.id,command.outcome))throw new InvalidOperationException(Session.Error);
                EditorDebugError=null;Message="隔离调试库存已更新。";return true;
            }
            catch(Exception e){EditorDebugError=e.Message;return false;}
        }
    }
}
#endif
