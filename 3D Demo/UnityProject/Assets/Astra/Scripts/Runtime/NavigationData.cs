using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AstraCabin.Navigation
{
    [Serializable] public sealed class Campaign
    {
        public int schemaVersion;
        public string id, name, startSectorId;
        public ResourceDefinition[] resources;
        public Amount[] travelCosts;
        public Sector[] sectors;
        public EventDefinition[] events;
    }
    [Serializable] public sealed class ResourceDefinition { public string id, name; public int initial, capacity; }
    [Serializable] public sealed class Amount { public string resourceId; public int amount; }
    [Serializable] public sealed class Sector
    {
        public string id, name, description, startNodeId, nextSectorId;
        public MapNode[] nodes;
    }
    [Serializable] public sealed class MapNode
    {
        public string id, name, eventId;
        public float x, y;
        public bool exit;
        public string[] next;
    }
    [Serializable] public sealed class EventDefinition
    {
        public string id, type, title, description;
        public int setpiece;
        public float duration;
        public Choice[] choices;
    }
    [Serializable] public sealed class Choice
    {
        public string id, text, result;
        public Amount[] costs, rewards;
    }
    [Serializable] public sealed class Command { public string kind, id; }
    [Serializable] public sealed class VoyageSave
    {
        public int schemaVersion = 1;
        public string fingerprint;
        public List<Command> commands = new List<Command>();
    }

    // Handlers own presentation and completion gates; the engine owns all economy/state changes.
    // Register another handler before validating/loading a campaign to add a new event type.
    public interface IEventHandler
    {
        string Type { get; }
        bool NeedsObservation { get; }
        string Validate(EventDefinition definition);
        void Begin(NavigationMode host, EventDefinition definition);
        bool IsComplete(NavigationMode host, EventDefinition definition);
    }
    public sealed class EventRegistry
    {
        readonly Dictionary<string, IEventHandler> handlers = new Dictionary<string, IEventHandler>(StringComparer.Ordinal);
        public void Register(IEventHandler handler)
        {
            if (handler == null || string.IsNullOrWhiteSpace(handler.Type) || handlers.ContainsKey(handler.Type))
                throw new ArgumentException("事件处理器为空或类型重复");
            handlers.Add(handler.Type, handler);
        }
        public IEventHandler Find(string type) { IEventHandler value; return type != null && handlers.TryGetValue(type, out value) ? value : null; }
        public static EventRegistry CreateDefault()
        {
            var registry = new EventRegistry(); registry.Register(new ChoiceEventHandler()); registry.Register(new SetpieceEventHandler()); return registry;
        }
    }
    public sealed class ChoiceEventHandler : IEventHandler
    {
        public string Type { get { return "choice"; } }
        public bool NeedsObservation { get { return false; } }
        public string Validate(EventDefinition e) { return null; }
        public void Begin(NavigationMode host, EventDefinition e) { }
        public bool IsComplete(NavigationMode host, EventDefinition e) { return true; }
    }
    public sealed class SetpieceEventHandler : IEventHandler
    {
        public string Type { get { return "setpiece"; } }
        public bool NeedsObservation { get { return true; } }
        public string Validate(EventDefinition e)
        {
            return e.setpiece < 1 || e.setpiece > 9 || float.IsNaN(e.duration) || float.IsInfinity(e.duration) || e.duration < 1 || e.duration > 300
                ? "演出编号必须为 1–9，时长必须为 1–300 秒" : null;
        }
        public void Begin(NavigationMode host, EventDefinition e) { host.Director.TriggerMode(e.setpiece); }
        public bool IsComplete(NavigationMode host, EventDefinition e)
        {
            return host.Director.ActiveMode == e.setpiece && host.Director.ModeTime >= e.duration &&
                (e.setpiece != 3 || (!host.Director.CrisisActive && host.Director.RepairedCount == 3));
        }
    }

    public static class CampaignValidator
    {
        public const int MaxBytes = 2 * 1024 * 1024;
        static readonly Regex Identifier = new Regex("^[A-Za-z0-9_-]{1,64}$");
        static bool Id(string value) { return value != null && Identifier.IsMatch(value); }
        static bool Text(string value, int max) { return !string.IsNullOrWhiteSpace(value) && value.Length <= max; }
        public static string Fingerprint(Campaign campaign)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(campaign)))).Replace("-", "");
        }
        public static List<string> Validate(Campaign c, EventRegistry registry)
        {
            var errors = new List<string>();
            if (c == null) { errors.Add("配置为空"); return errors; }
            if (c.schemaVersion != 1) errors.Add("不支持的 schemaVersion（需要 1）");
            if (!Id(c.id) || !Text(c.name, 80)) errors.Add("航程 ID 或名称无效");
            if (c.resources == null || c.resources.Length < 1 || c.resources.Length > 6 || c.resources.Any(r => r == null) ||
                c.sectors == null || c.sectors.Length < 1 || c.sectors.Length > 16 || c.sectors.Any(s => s == null) ||
                c.events == null || c.events.Length > 256 || c.events.Any(e => e == null))
            { errors.Add("资源需 1–6 项、星域需 1–16 项、事件最多 256 项，数组不可为空或含 null"); return errors; }
            var resources = new HashSet<string>();
            foreach (var r in c.resources)
            {
                if (!Id(r.id) || !resources.Add(r.id) || !Text(r.name, 12) || r.capacity < 1 || r.capacity > 1000000 || r.initial < 0 || r.initial > r.capacity)
                    errors.Add("资源定义无效或 ID 重复：" + r.id);
            }
            ValidateAmounts(c.travelCosts, resources, "航行消耗", errors);
            var eventIds = new HashSet<string>();
            foreach (var e in c.events)
            {
                if (!Id(e.id) || !eventIds.Add(e.id) || !Text(e.title, 80) || !Text(e.description, 4000)) errors.Add("事件定义无效或 ID 重复：" + e.id);
                var handler = registry.Find(e.type);
                if (handler == null) errors.Add(e.id + "：未知事件类型 " + e.type);
                else { string detail = handler.Validate(e); if (detail != null) errors.Add(e.id + "：" + detail); }
                if (e.choices == null || e.choices.Length < 1 || e.choices.Length > 8 || e.choices.Any(o => o == null))
                { errors.Add(e.id + "：选项需 1–8 项且不可含 null"); continue; }
                var ids = new HashSet<string>();
                foreach (var o in e.choices)
                {
                    if (!Id(o.id) || !ids.Add(o.id) || !Text(o.text, 160) || !Text(o.result, 2000)) errors.Add(e.id + "：选项内容无效或 ID 重复");
                    ValidateAmounts(o.costs, resources, e.id + "/" + o.id + " 消耗", errors);
                    ValidateAmounts(o.rewards, resources, e.id + "/" + o.id + " 收益", errors);
                }
                if (!e.choices.Any(o => o.costs != null && o.costs.Length == 0)) errors.Add(e.id + "：必须有一个无消耗选项以避免事件卡死");
            }
            var sectors = new Dictionary<string, Sector>();
            foreach (var s in c.sectors)
                if (!Id(s.id) || sectors.ContainsKey(s.id)) errors.Add("星域 ID 无效或重复：" + s.id); else sectors.Add(s.id, s);
            foreach (var s in c.sectors)
            {
                if (!Text(s.name, 80) || (s.description != null && s.description.Length > 2000)) errors.Add(s.id + "：星域文字无效");
                if (!string.IsNullOrEmpty(s.nextSectorId) && !sectors.ContainsKey(s.nextSectorId)) errors.Add(s.id + "：下一星域不存在");
                if (s.nodes == null || s.nodes.Length < 2 || s.nodes.Length > 128 || s.nodes.Any(n => n == null))
                { errors.Add(s.id + "：节点需 2–128 项且不可含 null"); continue; }
                var nodes = new Dictionary<string, MapNode>();
                foreach (var n in s.nodes)
                {
                    if (!Id(n.id) || nodes.ContainsKey(n.id)) errors.Add(s.id + "：节点 ID 无效或重复 " + n.id); else nodes.Add(n.id, n);
                    if (!Text(n.name, 32) || float.IsNaN(n.x) || float.IsNaN(n.y) || n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1) errors.Add(s.id + "/" + n.id + "：名称或坐标无效");
                    if (!string.IsNullOrEmpty(n.eventId) && !eventIds.Contains(n.eventId)) errors.Add(s.id + "/" + n.id + "：事件不存在");
                    if (n.next == null || n.next.Length > 128 || n.next.Distinct().Count() != n.next.Length) errors.Add(s.id + "/" + n.id + "：连线数组无效或重复");
                    if (n.exit && (n.next == null || n.next.Length != 0)) errors.Add(s.id + "/" + n.id + "：出口不能有星域内连线");
                    if (!n.exit && n.id != s.startNodeId && string.IsNullOrEmpty(n.eventId)) errors.Add(s.id + "/" + n.id + "：普通节点必须关联事件");
                    if (!n.exit && n.next != null && n.next.Length == 0) errors.Add(s.id + "/" + n.id + "：非出口节点不能是死路");
                }
                foreach (var n in s.nodes)
                    foreach (var target in n.next ?? new string[0])
                        if (target == null || !nodes.ContainsKey(target) || target == n.id) errors.Add(s.id + "/" + n.id + "：连线目标无效 " + target);
                if (s.startNodeId == null || !nodes.ContainsKey(s.startNodeId)) errors.Add(s.id + "：起点不存在");
                else
                {
                    var start = nodes[s.startNodeId];
                    if (start.exit || !string.IsNullOrEmpty(start.eventId)) errors.Add(s.id + "：起点不能是出口或关联事件");
                    var reached = new HashSet<string>(); var visiting = new HashSet<string>(); bool cycle = false;
                    Action<string> walk = null;
                    walk = id => { if (id == null || !nodes.ContainsKey(id)) return; if (visiting.Contains(id)) { cycle = true; return; } if (!reached.Add(id)) return;
                        visiting.Add(id); foreach (var next in nodes[id].next ?? new string[0]) walk(next); visiting.Remove(id); };
                    walk(s.startNodeId);
                    if (cycle) errors.Add(s.id + "：航线有环，必须单向前进");
                    if (reached.Count != nodes.Count) errors.Add(s.id + "：存在起点不可达节点");
                    if (!s.nodes.Any(n => n.exit)) errors.Add(s.id + "：缺少出口");
                }
            }
            if (c.startSectorId == null || !sectors.ContainsKey(c.startSectorId)) errors.Add("起始星域不存在");
            else
            {
                var seen = new HashSet<string>(); string id = c.startSectorId;
                while (!string.IsNullOrEmpty(id) && sectors.ContainsKey(id)) { if (!seen.Add(id)) { errors.Add("星域顺序有环"); break; } id = sectors[id].nextSectorId; }
                if (seen.Count != sectors.Count) errors.Add("存在航程不可达星域");
            }
            return errors;
        }
        static void ValidateAmounts(Amount[] values, HashSet<string> resources, string where, List<string> errors)
        {
            if (values == null || values.Length > 6) { errors.Add(where + "：资源列表无效"); return; }
            var seen = new HashSet<string>();
            foreach (var v in values)
                if (v == null || v.resourceId == null || !resources.Contains(v.resourceId) || !seen.Add(v.resourceId) || v.amount <= 0 || v.amount > 1000000)
                    errors.Add(where + "：未知资源、重复项或数量无效（需要正整数）");
        }
    }
}
