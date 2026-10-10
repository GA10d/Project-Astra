using System;
using System.Collections.Generic;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    public enum ToolSpecies { Human, StandingAlien, HalfOrc, CrawlerAlien }
    public enum ToolDialogueKind { Text, Choice }
    public enum ToolNodeKind { Unit, Condition, GlobalChoice, End }
    public enum ToolCompare { Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual }
    public enum ToolShot { Establishing, Medium, Close, TwoShot, OverShoulder, Profile, LowAngle, Insert, Tracking, Custom }
    public enum ToolFx { None, SignalGlitch, Disturbance, EmergencyLight }

    [Serializable] public sealed class ToolStat
    {
        public string key = "credits";
        public int value;
    }
    [Serializable] public sealed class ToolCondition
    {
        public string key = "credits";
        public ToolCompare compare = ToolCompare.GreaterOrEqual;
        public int value;
    }
    [Serializable] public sealed class ToolEffect
    {
        public string key = "credits";
        public int delta;
    }
    [Serializable] public sealed class ToolChoice
    {
        public string id = "option";
        public string text = "新选项";
        public ToolCondition condition = new ToolCondition();
        public bool hasCondition;
        public ToolEffect[] effects = Array.Empty<ToolEffect>();
    }
    [Serializable] public sealed class ToolCharacterLook
    {
        // Creator variants preserve the Quaternius skeleton and use bounded mesh shape keys.
        public bool creatorEnabled;
        // Zero preserves existing V2 JSON and saved male/alien characters.
        public int humanModel; // 0 male, 1 female (Human species only)
        public int faceShape;
        public int eyeShape;
        public int browShape;
        public int noseShape;
        public int mouthShape;
        public int chinStyle; // 0 preserves existing saved looks, 1 round, 2 broad square.
        public int hairStyle;
        public int hairUnderHat=1;
        public int bodyType;
        public int outfitStyle;
        public int legStyle;
        public int footStyle;
        [Range(-1,1)] public float jawWidth;
        public int faceAccessory;
        public int gearAccessory;
        public int facialMark;
        [Range(.92f,1.08f)] public float faceWidth = 1;
        [Range(.9f,1.1f)] public float bodyScale = 1;
        public int hairOrHeadgear;
        public int accessory;
        public Color skin = new Color(.65f,.53f,.40f);
        public Color suit = new Color(.36f,.40f,.34f);
        public Color hair = new Color(.18f,.15f,.12f);
        public Color eyes = new Color(.16f,.27f,.20f);
        public Color accent = new Color(.64f,.52f,.31f);
    }

    [Serializable] public sealed class ToolAction
    {
        public string id;
        public string displayName;
        public float length = 2;
        public bool loop;
        public bool crawlerAllowed;
    }
    [Serializable] public sealed class ToolCameraPreset
    {
        public ToolShot shot;
        public string displayName;
        public bool moving;
        public float fov = 36;
    }
    [Serializable] public sealed class ToolScenePreset
    {
        public string id;
        public string displayName;
        public Color wall = new Color(.22f,.26f,.21f);
        public Color floor = new Color(.12f,.15f,.12f);
        public Color light = new Color(.85f,.73f,.53f);
    }

    [Serializable] public sealed class ToolActorSpan
    {
        public string id = "actor";
        public ToolCharacter character;
        [Min(0)] public float start;
        [Min(.1f)] public float duration = 8;
    }
    [Serializable] public sealed class ToolActionSpan
    {
        public string actorId = "actor";
        public string actionId = "Idle_Loop";
        [Min(0)] public float start;
        [Min(.1f)] public float duration = 2;
        [Range(.25f,3)] public float speed = 1;
    }
    [Serializable] public sealed class ToolDialogueSpan
    {
        public string actorId = "actor";
        public ToolDialogueKind kind;
        [TextArea(2,5)] public string text = "新的台词";
        [Min(0)] public float start;
        [Range(4,60)] public float charactersPerSecond = 18;
        [Min(0)] public float hold = .7f;
        [Min(1)] public float patienceSeconds = 6;
        public ToolChoice[] choices = Array.Empty<ToolChoice>();
        public float Duration => kind == ToolDialogueKind.Choice ? patienceSeconds : (text == null ? 0 : text.Length) / Mathf.Max(1, charactersPerSecond) + hold;
    }
    [Serializable] public sealed class ToolSceneSpan
    {
        public string sceneId = "archive";
        [Min(0)] public float start;
        [Min(.1f)] public float duration = 8;
    }
    [Serializable] public sealed class ToolCameraSpan
    {
        public ToolShot shot = ToolShot.Medium;
        public ToolFx fx;
        public string actorId = "actor";
        public string partnerId;
        [Min(0)] public float start;
        [Min(.1f)] public float duration = 3;
        public Vector3 customPosition = new Vector3(.2f,1.6f,-2.6f);
        public Vector3 customEuler = new Vector3(3,0,0);
        [Range(20,75)] public float customFov = 36;
    }

    [Serializable] public sealed class ToolGraphEdge
    {
        public string slot = "next";
        public string target;
    }
    [Serializable] public sealed class ToolGraphNode
    {
        public string id = "node";
        public ToolNodeKind kind;
        public Vector2 position;
        public ToolUnit unit;
        public ToolCondition condition = new ToolCondition();
        public string prompt = "请选择";
        public float patienceSeconds = 6;
        public ToolChoice[] choices = Array.Empty<ToolChoice>();
        public List<ToolGraphEdge> edges = new List<ToolGraphEdge>();
    }



    public static class ToolValidation
    {
        public static List<string> Unit(ToolUnit unit, ToolLibrary library)
        {
            var errors = new List<string>();
            if (!unit || !library) { errors.Add("缺少演出单元或材料库"); return errors; }
            if (unit.duration <= 0) errors.Add("演出单元时长必须大于零");
            foreach (var a in unit.actors)
            {
                if (a.character == null) errors.Add("人物轨缺少角色");
                if (a.start < 0 || a.duration <= 0 || a.start + a.duration > unit.duration + .001f) errors.Add("人物片段超出时长: " + a.id);
            }
            foreach (var a in unit.actions)
            {
                var preset = library.Action(a.actionId);
                if (preset == null) errors.Add("未知动作: " + a.actionId);
                if (a.duration <= 0 || a.speed <= 0) errors.Add("动作时长或速度无效: " + a.actionId);
                bool covered = false;
                foreach (var actor in unit.actors)
                    if (actor.id == a.actorId && actor.start <= a.start + .001f && actor.start + actor.duration >= a.start + a.duration - .001f) covered = true;
                if (!covered) errors.Add("动作未被人物轨完整覆盖: " + a.actionId);
                if (a.start + a.duration > unit.duration + .001f) errors.Add("动作超出单元时长: " + a.actionId);
            }
            var text = new List<ToolDialogueSpan>(unit.dialogue); text.Sort((x,y) => x.start.CompareTo(y.start));
            for (int i=0;i<text.Count;i++)
            {
                if (text[i].start < 0 || text[i].start + text[i].Duration > unit.duration + .001f) errors.Add("台词超出时长");
                if (i > 0 && text[i-1].start + text[i-1].Duration > text[i].start + .001f) errors.Add("台词片段重叠");
                if (text[i].kind == ToolDialogueKind.Choice)
                {
                    if (i != text.Count-1) errors.Add("选择片段必须是台词轨最后一段");
                    if (text[i].choices == null || text[i].choices.Length == 0) errors.Add("选择片段没有明文选项");
                    CheckChoices(text[i].choices, null, "演出单元 " + unit.id, errors);
                }
            }
            foreach (var scene in unit.scenes) if (scene.duration <= 0 || scene.start < 0 || scene.start + scene.duration > unit.duration + .001f || library.Scene(scene.sceneId) == null) errors.Add("场景片段无效: " + scene.sceneId);
            foreach (var camera in unit.cameras) if (camera.duration <= 0 || camera.start < 0 || camera.start + camera.duration > unit.duration + .001f) errors.Add("镜头片段无效");
            return errors;
        }
        public static List<string> Package(ToolPackage package)
        {
            var errors = new List<string>();
            if (!package || !package.library || !package.graph) { errors.Add("缺少材料库或演出树"); return errors; }
            var statNames=new HashSet<string>();
            if(package.initialStats!=null)
                foreach(var stat in package.initialStats)
                    if(stat==null || string.IsNullOrWhiteSpace(stat.key) || !statNames.Add(stat.key))errors.Add("全局资源名称为空或重复");
            if(package.initialPatience<0)errors.Add("初始耐心不能为负数");
            var graph = package.graph;
            if (graph.Node(graph.entryNode) == null) errors.Add("演出树入口不存在");
            var ids = new HashSet<string>();
            foreach (var node in graph.nodes)
            {
                if (string.IsNullOrEmpty(node.id) || !ids.Add(node.id)) errors.Add("演出树节点 ID 重复或为空");
                if (node.kind == ToolNodeKind.Unit)
                {
                    if (!node.unit) errors.Add("演出节点缺少演出单元: " + node.id);
                    else errors.AddRange(Unit(node.unit, package.library));
                }
                var slots = new HashSet<string>();
                foreach (var edge in node.edges)
                {
                    if (!slots.Add(edge.slot)) errors.Add("同一出口重复连接: " + node.id + "/" + edge.slot);
                    if (graph.Node(edge.target) == null) errors.Add("出口指向不存在节点: " + node.id + "/" + edge.slot);
                }
                if (node.kind == ToolNodeKind.Condition && (!slots.Contains("yes") || !slots.Contains("no"))) errors.Add("判断节点须连接 yes/no: " + node.id);
                if (node.kind == ToolNodeKind.GlobalChoice && (node.choices == null || node.choices.Length == 0 || !slots.Contains("silence"))) errors.Add("全局选择缺少选项或沉默出口: " + node.id);
                if (node.kind == ToolNodeKind.GlobalChoice) CheckChoices(node.choices, slots, "全局选择 " + node.id, errors);
                if (node.kind == ToolNodeKind.Unit && node.unit)
                {
                    ToolDialogueSpan last = null;
                    foreach (var d in node.unit.dialogue) if (last == null || d.start > last.start) last = d;
                    if (last != null && last.kind == ToolDialogueKind.Choice)
                    {
                        if (!slots.Contains("silence")) errors.Add("选择演出单元缺少沉默出口: " + node.id);
                        CheckChoices(last.choices, slots, "演出节点 " + node.id, errors);
                    }
                    else if (slots.Count != 1 || !slots.Contains("next")) errors.Add("纯文本演出单元须连接 next 出口: " + node.id);
                }
            }
            return errors;
        }
        static void CheckChoices(ToolChoice[] choices, HashSet<string> slots, string owner, List<string> errors)
        {
            if (choices == null) return;
            var ids = new HashSet<string>();
            foreach (var choice in choices)
            {
                if (choice == null || string.IsNullOrWhiteSpace(choice.id) || choice.id == "silence" || !ids.Add(choice.id))
                { errors.Add("选项出口为空、重复或保留名: " + owner); continue; }
                if (string.IsNullOrWhiteSpace(choice.text)) errors.Add("选项文案为空: " + owner + "/" + choice.id);
                if (slots != null && !slots.Contains(choice.id)) errors.Add("选项未连接出口: " + owner + "/" + choice.id);
            }
            if (slots != null)
                foreach (var slot in slots)
                    if (slot != "silence" && !ids.Contains(slot)) errors.Add("出口没有对应选项: " + owner + "/" + slot);
        }
    }
}
