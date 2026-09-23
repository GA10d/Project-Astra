using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AstraCabin.Navigation
{
    // Deterministic command replay keeps position, resource transactions and one-shot rewards
    // consistent. No MonoBehaviour, scene side effects, or player-document writes occur here.
    public sealed class NavigationEngine
    {
        public readonly Campaign Campaign;
        public readonly EventRegistry Registry;
        public readonly Dictionary<string, int> Balances = new Dictionary<string, int>();
        public readonly HashSet<string> Visited = new HashSet<string>();
        public readonly List<string> Journal = new List<string>();
        public Sector Sector { get; private set; }
        public MapNode Node { get; private set; }
        public EventDefinition Pending { get; private set; }
        public bool Observed { get; private set; }
        public bool Complete { get; private set; }
        public NavigationEngine(Campaign campaign, EventRegistry registry)
        {
            Campaign = campaign; Registry = registry;
            foreach (var r in campaign.resources) Balances.Add(r.id, r.initial);
            Enter(campaign.sectors.First(s => s.id == campaign.startSectorId));
        }
        void Enter(Sector sector)
        {
            Sector = sector; Node = sector.nodes.First(n => n.id == sector.startNodeId); Pending = null; Observed = false;
            Visited.Add(Key(Node)); Journal.Add("进入星域 · " + sector.name);
        }
        public string Key(MapNode node) { return Sector.id + "/" + node.id; }
        public string ResourceName(string id) { return Campaign.resources.First(r => r.id == id).name; }
        public string Amounts(Amount[] amounts, string prefix)
        { return amounts.Length == 0 ? "无" : string.Join("  ", amounts.Select(a => ResourceName(a.resourceId) + " " + prefix + a.amount)); }
        public bool CanPay(Amount[] costs) { return costs.All(c => Balances[c.resourceId] >= c.amount); }
        public string TravelError(string id)
        {
            if (Complete) return "航程已完成";
            if (Pending != null) return "请先完成当前事件";
            if (Node.exit) return "请前往下一星域";
            if (!Node.next.Contains(id)) return "该节点不在当前可达航线上";
            if (Visited.Contains(Sector.id + "/" + id)) return "不能重复访问已完成节点";
            if (!CanPay(Campaign.travelCosts)) return "航行资源不足；可在菜单中重新开始航程";
            return null;
        }
        public string ChoiceError(string id)
        {
            if (Pending == null) return "没有待处理事件";
            if (Registry.Find(Pending.type).NeedsObservation && !Observed) return "请先完成舱内演出";
            var choice = Pending.choices.FirstOrDefault(o => o.id == id);
            if (choice == null) return "选项不存在";
            return CanPay(choice.costs) ? null : "资源不足";
        }
        void Pay(Amount[] costs) { foreach (var c in costs) Balances[c.resourceId] -= c.amount; }
        public bool Apply(Command command, out string error)
        {
            error = null;
            if (command == null) { error = "指令为空"; return false; }
            switch (command.kind)
            {
                case "travel":
                    error = TravelError(command.id); if (error != null) return false;
                    Pay(Campaign.travelCosts); Node = Sector.nodes.First(n => n.id == command.id); Visited.Add(Key(Node));
                    Pending = string.IsNullOrEmpty(Node.eventId) ? null : Campaign.events.First(e => e.id == Node.eventId); Observed = false;
                    Journal.Add("抵达 · " + Node.name + " / 消耗 " + Amounts(Campaign.travelCosts, "-")); return true;
                case "observed":
                    if (Pending == null || Pending.id != command.id || Observed || !Registry.Find(Pending.type).NeedsObservation)
                    { error = "演出结算状态不匹配"; return false; }
                    Observed = true; Journal.Add("演出完成 · " + Pending.title); return true;
                case "choose":
                    error = ChoiceError(command.id); if (error != null) return false;
                    var choice = Pending.choices.First(o => o.id == command.id); Pay(choice.costs);
                    var actual = new List<string>();
                    foreach (var reward in choice.rewards)
                    {
                        int before = Balances[reward.resourceId], cap = Campaign.resources.First(r => r.id == reward.resourceId).capacity;
                        Balances[reward.resourceId] = Math.Min(cap, before + reward.amount);
                        actual.Add(ResourceName(reward.resourceId) + " +" + (Balances[reward.resourceId] - before));
                    }
                    Journal.Add(Pending.title + " · " + choice.text + "\n" + choice.result + "\n消耗 " + Amounts(choice.costs, "-") + " / 实收 " + (actual.Count == 0 ? "无" : string.Join("  ", actual)));
                    Pending = null; Observed = false; return true;
                case "advance":
                    if (Complete || Pending != null || !Node.exit || command.id != Sector.id) { error = "尚未抵达可用出口"; return false; }
                    if (string.IsNullOrEmpty(Sector.nextSectorId)) { Complete = true; Journal.Add("航程完成 · " + Campaign.name); }
                    else Enter(Campaign.sectors.First(s => s.id == Sector.nextSectorId));
                    return true;
                default: error = "未知航行指令"; return false;
            }
        }
    }

    public static class NavigationStorage
    {
        public static string ReadLimited(string path)
        {
            if (new FileInfo(path).Length > CampaignValidator.MaxBytes) throw new IOException("文件超过 2 MB 上限");
            return File.ReadAllText(path, Encoding.UTF8);
        }
        public static void WriteAtomic(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
        public static void Archive(string path)
        {
            if (File.Exists(path)) File.Copy(path, path + ".preserved-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
        }
    }

    public sealed class NavigationSession
    {
        public NavigationEngine Engine { get; private set; }
        public VoyageSave Save { get; private set; }
        public bool Blocked { get; private set; }
        public string Error { get; private set; }
        public readonly string Path;
        readonly Campaign campaign;
        readonly EventRegistry registry;
        public NavigationSession(Campaign campaign, EventRegistry registry, string path)
        {
            this.campaign = campaign; this.registry = registry; Path = path;
            Engine = new NavigationEngine(campaign, registry);
            Save = new VoyageSave { fingerprint = CampaignValidator.Fingerprint(campaign) };
            try
            {
                if (!File.Exists(path)) return;
                var loaded = JsonUtility.FromJson<VoyageSave>(NavigationStorage.ReadLimited(path));
                Engine = Replay(loaded); Save = loaded;
            }
            catch (Exception e) { Blocked = true; Error = "存档未载入，原文件已保留：" + e.Message; }
        }
        NavigationEngine Replay(VoyageSave save)
        {
            if (save == null || save.schemaVersion != 1 || save.fingerprint != CampaignValidator.Fingerprint(campaign) || save.commands == null || save.commands.Count > 8192)
                throw new InvalidDataException("配置版本不匹配或存档格式无效");
            var candidate = new NavigationEngine(campaign, registry);
            foreach (var command in save.commands) { string why; if (!candidate.Apply(command, out why)) throw new InvalidDataException(why); }
            return candidate;
        }
        public bool Commit(string kind, string id)
        {
            if (Blocked) return false;
            try
            {
                var candidate = new VoyageSave { fingerprint = Save.fingerprint, commands = new List<Command>(Save.commands) };
                candidate.commands.Add(new Command { kind = kind, id = id });
                var next = Replay(candidate);
                // Commit durable data before exposing a world action or a reward.
                NavigationStorage.WriteAtomic(Path, JsonUtility.ToJson(candidate, true));
                Engine = next; Save = candidate; Error = null; return true;
            }
            catch (Exception e) { Error = "操作未提交：" + e.Message; return false; }
        }
        public bool Restart()
        {
            try
            {
                NavigationStorage.Archive(Path);
                var save = new VoyageSave { fingerprint = CampaignValidator.Fingerprint(campaign) };
                NavigationStorage.WriteAtomic(Path, JsonUtility.ToJson(save, true));
                Save = save; Engine = new NavigationEngine(campaign, registry); Blocked = false; Error = null; return true;
            }
            catch (Exception e) { Error = "新航程未创建：" + e.Message; return false; }
        }
    }
}
