using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AstraCabin.Navigation;
using AstraCabin.Galaxies.Zyl;
namespace AstraCabin.Editor
{
    public sealed partial class ZylNavigationDebugWindow
    {
        [SerializeField] List<ItemAmount> inventoryDraft=new List<ItemAmount>();
        [SerializeField] bool autoSupplyItems=true,showInventory=true;
        [SerializeField] string inventoryTargetSector,inventoryTargetNode;
        NavigationSession inventorySession;
        void NormalizeInventory(Campaign c,bool readCurrent=false,NavigationMode nav=null)
        {
            if(inventoryDraft==null)inventoryDraft=new List<ItemAmount>();
            inventoryDraft=(c.items??new ItemDefinition[0]).Select(d=>{
                var old=inventoryDraft.FirstOrDefault(x=>x.itemId==d.id);int quantity=old==null?d.initial:old.amount;
                if(readCurrent&&nav&&nav.Session!=null&&nav.Session.Engine.Items.ContainsKey(d.id))quantity=nav.Session.Engine.Items[d.id];
                return new ItemAmount{itemId=d.id,amount=Mathf.Clamp(quantity,0,d.capacity)};
            }).ToList();
        }
        ItemAmount[] InventorySnapshot(){return inventoryDraft.Select(x=>new ItemAmount{itemId=x.itemId,amount=x.amount}).ToArray();}
        ZylDebugRequest BuildInventoryRequest()
        {
            NormalizeInventory(campaign);return ZylDebugRoutePlanner.Build(campaign,routes[routeIndex],InventorySnapshot(),autoSupplyItems);
        }
        ZylDebugRequest ReplayInventoryRequest(NavigationMode nav)
        {
            var latest=ReadLatest();NormalizeInventory(latest);return ZylDebugRoutePlanner.Replay(latest,nav.Session,InventorySnapshot(),autoSupplyItems);
        }
        void ApplyInventory(NavigationMode nav,ItemAmount[] values)
        {
            if(!nav.EditorSetDebugInventory(values))throw new InvalidOperationException(nav.EditorDebugError);
            message="已更新本次隔离调试库存；不修改物品定义或正式存档。";
        }
        void FillInventoryTarget(NavigationMode nav,Campaign definitions,bool live)
        {
            var target=targets[targetIndex];var node=definitions.sectors.First(s=>s.id==target.sectorId).nodes.First(n=>n.id==target.nodeId);
            bool already=live&&nav.Session.Engine.Sector.id==target.sectorId&&nav.Session.Engine.Node.id==target.nodeId;
            var need=ZylDebugInventory.TargetMinimum(definitions,node,already);
            var error=ZylDebugInventory.Validate(definitions,need);if(error!=null)throw new InvalidOperationException("无法一次携带到达并满足所有选项所需的数量，可能超过容量。请启用沿途补齐，并在抵达后补齐当前事件。");
            foreach(var item in need){var draft=inventoryDraft.First(x=>x.itemId==item.itemId);draft.amount=Math.Max(live?nav.Session.Engine.Items[item.itemId]:draft.amount,item.amount);}
            if(live)ApplyInventory(nav,need.Select(x=>new ItemAmount{itemId=x.itemId,amount=inventoryDraft.First(d=>d.itemId==x.itemId).amount}).ToArray());
            else message="已补齐选中信标的入场与选项需求；沿途消耗由自动补齐开关处理。";
        }
        void DrawInventory(NavigationMode nav,bool waiting)
        {
            EditorGUILayout.Space();showInventory=EditorGUILayout.Foldout(showInventory,"调试库存",true);if(!showInventory)return;
            bool live=nav&&nav.EditorCanEditInventory;var definitions=live?nav.Campaign:campaign;if(definitions==null)return;
            if(live&&inventorySession!=nav.Session){NormalizeInventory(definitions,true,nav);inventorySession=nav.Session;}
            else NormalizeInventory(definitions);
            using(new EditorGUI.DisabledScope(waiting||Application.isPlaying&&!live))
            {
                autoSupplyItems=EditorGUILayout.ToggleLeft("启动/重播时，自动补齐沿途与目标的物品门槛",autoSupplyItems);
                EditorGUILayout.HelpBox(live?"当前持有为实时值；输入数量后点设置，或点击＋1/－1立即修改。仅改变当前隔离航程。":"填写本次调试的初始持有数量，点击上方从选中信标开始后生效。不会修改星图物品定义。",MessageType.None);
                if(inventoryDraft.Count==0)EditorGUILayout.HelpBox("当前星图未定义物品。请先在星域编辑器添加物品定义，再重新读取星图。",MessageType.Info);
                foreach(var definition in definitions.items??new ItemDefinition[0])
                {
                    var row=inventoryDraft.First(x=>x.itemId==definition.id);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(definition.name+"  ["+definition.id+"]",EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    int held=live?nav.Session.Engine.Items[definition.id]:row.amount;
                    GUILayout.Label(live?"持有 "+held+" / "+definition.capacity:"容量 "+definition.capacity,GUILayout.Width(130));
                    row.amount=Mathf.Clamp(EditorGUILayout.IntField(row.amount,GUILayout.MinWidth(65)),0,definition.capacity);
                    if(GUILayout.Button("设置",GUILayout.Width(44)))try{if(live)ApplyInventory(nav,new[]{new ItemAmount{itemId=row.itemId,amount=row.amount}});else message="已配置启动时持有数量。";}catch(Exception e){message=e.Message;}
                    using(new EditorGUI.DisabledScope(held<=0))if(GUILayout.Button("－1",GUILayout.Width(38)))try{row.amount=held-1;if(live)ApplyInventory(nav,new[]{new ItemAmount{itemId=row.itemId,amount=row.amount}});}catch(Exception e){message=e.Message;}
                    using(new EditorGUI.DisabledScope(held>=definition.capacity))if(GUILayout.Button("＋1",GUILayout.Width(38)))try{row.amount=held+1;if(live)ApplyInventory(nav,new[]{new ItemAmount{itemId=row.itemId,amount=row.amount}});}catch(Exception e){message=e.Message;}
                    EditorGUILayout.EndHorizontal();EditorGUILayout.EndVertical();
                }
                EditorGUILayout.BeginHorizontal();
                using(new EditorGUI.DisabledScope(campaign==null||targets.Count==0||live&&CampaignValidator.Fingerprint(campaign)!=CampaignValidator.Fingerprint(definitions)))
                    if(GUILayout.Button("补齐选中信标所需物品"))try{FillInventoryTarget(nav,definitions,live);}catch(Exception e){message=e.Message;}
                if(live&&GUILayout.Button("补齐当前事件选项"))try{ApplyInventory(nav,ZylDebugInventory.TopUp(nav.Session.Engine,ZylDebugInventory.ChoiceMinimum(nav.Session.Engine.Pending)));NormalizeInventory(definitions,true,nav);}catch(Exception e){message=e.Message;}
                EditorGUILayout.EndHorizontal();EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button(live?"应用全部数量":"恢复物品初始数量"))try{if(live)ApplyInventory(nav,InventorySnapshot());else inventoryDraft=(definitions.items??new ItemDefinition[0]).Select(x=>new ItemAmount{itemId=x.id,amount=x.initial}).ToList();}catch(Exception e){message=e.Message;}
                if(GUILayout.Button("清空库存"))try{foreach(var row in inventoryDraft)row.amount=0;if(live)ApplyInventory(nav,InventorySnapshot());else message="已将启动携带数量全部设为0。";}catch(Exception e){message=e.Message;}
                if(live&&GUILayout.Button("读取当前数量"))NormalizeInventory(definitions,true,nav);
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
