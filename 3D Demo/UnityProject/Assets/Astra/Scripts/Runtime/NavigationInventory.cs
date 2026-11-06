using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AstraCabin.Navigation
{
    [Serializable] public sealed class ItemDefinition
    {
        public string id, name, fabricatorBlueprint;
        public int initial, capacity;
    }
    [Serializable] public sealed class ItemAmount { public string itemId; public int amount; }
    [Serializable] public sealed class ItemDelivery { public ItemAmount[] items; }

    public static class InventoryRules
    {
        public static string AmountError(ItemAmount[] values, ItemDefinition[] definitions)
        {
            if(values==null)return null;
            if(values.Length>64)return "物品列表最多64项";
            var seen=new HashSet<string>();
            foreach(var a in values)
                if(a==null || a.itemId==null || !definitions.Any(d=>d!=null&&d.id==a.itemId) || !seen.Add(a.itemId) || a.amount<1 || a.amount>1000000)
                    return "未知物品、重复项或数量无效";
            return null;
        }
        public static void Validate(Campaign c,List<string> errors)
        {
            var items=c.items??new ItemDefinition[0];
            if(items.Length>64){errors.Add("物品最多64种");return;}
            var ids=new HashSet<string>();var blueprints=new HashSet<string>();
            foreach(var i in items)
            {
                if(i==null){errors.Add("物品不可为null");continue;}
                if(i.id==null || !System.Text.RegularExpressions.Regex.IsMatch(i.id,@"^[A-Za-z0-9_-]{1,64}$") || !ids.Add(i.id) || string.IsNullOrWhiteSpace(i.name) || i.name.Length>32 || i.capacity<1 || i.capacity>1000000 || i.initial<0 || i.initial>i.capacity)errors.Add("物品定义无效："+i.id);
                if(!string.IsNullOrEmpty(i.fabricatorBlueprint) && (!new[]{"seal_ring","connection_flange","buffer_gasket"}.Contains(i.fabricatorBlueprint)||!blueprints.Add(i.fabricatorBlueprint)))errors.Add("打印图纸无效或重复绑定："+i.id);
            }
            Action<ItemAmount[],string> check=(a,where)=>{var e=AmountError(a,items);if(e!=null)errors.Add(where+"："+e);};
            foreach(var s in c.sectors)foreach(var n in s.nodes??new MapNode[0])
            {
                if(n==null)continue;
                check(n.requiredItems,n.id);check(n.itemCosts,n.id);
                if(n.id==s.startNodeId && ((n.requiredItems?.Length??0)+(n.itemCosts?.Length??0)>0))errors.Add(n.id+"：起点不能有物品门槛");
            }
            foreach(var e in c.events)foreach(var o in e.choices??new Choice[0])
            {
                if(o==null)continue;
                check(o.requiredItems,e.id);check(o.itemCosts,e.id);check(o.itemRewards,e.id);
                if(e.type=="performance" && ((o.requiredItems?.Length??0)+(o.itemCosts?.Length??0)>0))errors.Add(e.id+"：自动会议结算不能有物品门槛");
            }
        }
    }
    public sealed partial class NavigationEngine
    {
        public readonly Dictionary<string,int> Items=new Dictionary<string,int>();
        public readonly Dictionary<string,string> ItemReceipts=new Dictionary<string,string>();
        public string ItemText(ItemAmount[] values)
        {
            return values==null||values.Length==0?"无":string.Join("、",values.Select(a=>Campaign.items.First(i=>i.id==a.itemId).name+" ×"+a.amount));
        }
        public string ItemError(ItemAmount[] values)
        {
            foreach(var a in values??new ItemAmount[0])if(Items[a.itemId]<a.amount)return "缺少物品："+Campaign.items.First(i=>i.id==a.itemId).name+"（需要 "+a.amount+"，持有 "+Items[a.itemId]+"）";
            return null;
        }
        public string ItemRewardError(Choice choice)
        {
            foreach(var a in choice.itemRewards??new ItemAmount[0])
            {
                int spent=(choice.itemCosts??new ItemAmount[0]).Where(v=>v.itemId==a.itemId).Sum(v=>v.amount);
                if(Items[a.itemId]-spent+a.amount>Campaign.items.First(i=>i.id==a.itemId).capacity)return "物品库存容量不足";
            }
            return null;
        }
        void SpendItems(ItemAmount[] values){foreach(var a in values??new ItemAmount[0])Items[a.itemId]-=a.amount;}
        bool CollectItems(Command command,out string error)
        {
            error=null;
            Guid receipt;
            if(!Guid.TryParseExact(command.id,"N",out receipt)){error="领取凭据无效";return false;}
            if(ItemReceipts.ContainsKey(command.id)){error="领取凭据重复";return false;}
            ItemDelivery delivery;
            try{delivery=JsonUtility.FromJson<ItemDelivery>(command.outcome);}catch{error="领取内容无效";return false;}
            if(delivery==null||delivery.items==null||delivery.items.Length==0){error="领取内容为空";return false;}
            error=InventoryRules.AmountError(delivery.items,Campaign.items??new ItemDefinition[0]);if(error!=null)return false;
            foreach(var a in delivery.items)
            {
                var definition=Campaign.items.First(i=>i.id==a.itemId);
                if(string.IsNullOrEmpty(definition.fabricatorBlueprint)){error="物品未绑定打印图纸";return false;}
                if(Items[a.itemId]+a.amount>definition.capacity){error="物品库存容量不足："+definition.name;return false;}
            }
            foreach(var a in delivery.items)Items[a.itemId]+=a.amount;
            ItemReceipts.Add(command.id,command.outcome);Journal.Add("领取打印部件 · "+ItemText(delivery.items));return true;
        }
    }
}

namespace AstraCabin
{
    public sealed partial class NavigationMode
    {
        public bool PreparePrintedItems(int[] counts,out string voyage,out string payload,out int[] acceptedCounts)
        {
            voyage=null;payload=null;acceptedCounts=new int[3];Initialize();
            if(counts==null||counts.Length!=3||counts.Any(n=>n<0)){Message="打印部件数量无效。";return false;}
            if(Session==null||Session.Blocked||IsGalaxyOverview||Session.Engine.Complete){Message="请先在导航中进入有效的未完成航程，再领取剧情部件。";return false;}
            var amounts=new System.Collections.Generic.List<Navigation.ItemAmount>();
            string[] keys={"seal_ring","connection_flange","buffer_gasket"};
            for(int n=0;n<3;n++)if(counts[n]>0)
            {
                var item=(Campaign.items??new Navigation.ItemDefinition[0]).FirstOrDefault(i=>i.fabricatorBlueprint==keys[n]);
                if(item==null)continue;
                acceptedCounts[n]=counts[n];
                amounts.Add(new Navigation.ItemAmount{itemId=item.id,amount=counts[n]});
            }
            if(amounts.Count==0)
            {
                if(counts.All(n=>n==0)){Message="没有已完成的部件。";return false;}
                string[] names={"密封环","连接法兰","缓冲垫片"};
                string completed=string.Join("、",Enumerable.Range(0,3).Where(n=>counts[n]>0).Select(n=>names[n]));
                var bound=(Campaign.items??new Navigation.ItemDefinition[0]).Where(i=>!string.IsNullOrEmpty(i.fabricatorBlueprint)).ToArray();
                string expected=string.Join("、",bound.Select(i=>i.name+"（"+names[Array.IndexOf(keys,i.fabricatorBlueprint)]+"）"));
                Message="已完成的"+completed+"未绑定本航程物品，已保留。"+(bound.Length>0?"请打印并领取："+expected+"。":"请在星图物品定义中绑定打印机图纸。");return false;
            }
            if(!Session.EnsureVoyageIdentity()){Message=Session.Error;return false;}
            voyage=Session.Save.voyageId;
            payload=JsonUtility.ToJson(new Navigation.ItemDelivery{items=amounts.ToArray()});return true;
        }
        public bool ReceivePrintedItems(string voyage,string receipt,string payload)
        {
            if(Session==null||Session.Blocked||Session.Save.voyageId!=voyage){Message="领取绑定的航程已改变，请返回原航程领取。";return false;}
            string previous;
            if(Session.Engine.ItemReceipts.TryGetValue(receipt,out previous))return previous==payload;
            if(!Session.Commit("collectItems",receipt,payload)){Message=Session.Error;return false;}
            Message="打印部件已存入本航程库存。";return true;
        }
    }
}
