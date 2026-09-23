using System;
using System.Linq;
using UnityEngine;
using AstraCabin.Navigation;

namespace AstraCabin
{
    public sealed partial class NavigationMode
    {
        GUIStyle navText, navButton;
        Font navFont;
        Vector2 detailScroll, journalScroll;
        bool showJournal;
        float detailHeight = 500;
        readonly Color paper = new Color(.77f, .86f, .71f), muted = new Color(.42f, .55f, .45f), signal = new Color(.63f, .87f, .56f), amber = new Color(.95f, .70f, .34f);
        void Styles(Font font)
        {
            if (navText != null) return;
            navFont = font;
            navText = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, wordWrap = true, richText = false, padding = new RectOffset(0, 0, 0, 0) };
            navButton = new GUIStyle(GUI.skin.button) { font = font, fontSize = 15, wordWrap = true, richText = false };
        }
        static void Block(Rect r, Color color) { var old = GUI.color; GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old; }
        void Label(Rect r, string value, int size = 15, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        { navText.fontSize = size; navText.normal.textColor = color ?? paper; navText.alignment = align; GUI.Label(r, value, navText); }
        bool Button(Rect r, string title, bool enabled = true)
        { bool old = GUI.enabled; GUI.enabled = old && enabled; bool clicked = GUI.Button(r, title, navButton); GUI.enabled = old; return clicked; }
        static void Line(Vector2 a, Vector2 b, Color color, float width = 2)
        {
            // Axis-aligned strips respect nested IMGUI window clipping, unlike rotating GUI.matrix.
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 3));
            for (int i = 0; i <= count; i++) { Vector2 p = Vector2.Lerp(a, b, i / (float)count); Block(new Rect(p.x - width / 2, p.y - width / 2, width, width), color); }
        }
        public void Draw(Rect bounds, Font font)
        {
            Initialize(); Styles(font);
            GUI.BeginGroup(bounds);
            float w = bounds.width, h = bounds.height;
            Block(new Rect(0, 0, w, h), new Color(.035f, .065f, .053f));
            Label(new Rect(18, 12, w - 550, 30), "NAV / 深空航路", 24);
            if (Button(new Rect(w - 432, 12, 126, 30), showJournal ? "返回星图" : "航行日志")) showJournal = !showJournal;
            if (Button(new Rect(w - 298, 12, 136, 30), "导入星域配置")) confirmation = "import";
            if (Button(new Rect(w - 154, 12, 136, 30), "新航程", Session != null)) confirmation = "restart";
            if (Session == null)
            {
                Label(new Rect(24, 90, w - 48, 150), Message, 18, amber);
                Label(new Rect(24, 258, w - 48, 100), "将有效 campaign.json 放入以下路径，再点击「导入星域配置」：\n" + ImportPath);
            }
            else
            {
                var engine = Session.Engine;
                float rw = (w - 36) / Campaign.resources.Length;
                for (int i = 0; i < Campaign.resources.Length; i++)
                {
                    var r = Campaign.resources[i]; float x = 18 + i * rw;
                    Block(new Rect(x, 53, rw - 8, 47), new Color(.10f, .16f, .12f));
                    Label(new Rect(x + 10, 60, rw - 20, 31), r.name + "   " + engine.Balances[r.id] + " / " + r.capacity, 17);
                }
                Label(new Rect(18, 111, w - 36, 28), Campaign.name + "  /  " + engine.Sector.name + "     ·     每跳消耗 " + engine.Amounts(Campaign.travelCosts, "-"), 15, amber);
                bool oldEnabled = GUI.enabled; GUI.enabled = oldEnabled && !Session.Blocked && confirmation == null;
                if (showJournal) DrawJournal(new Rect(18, 148, w - 36, h - 211));
                else
                {
                    float mapWidth = (w - 54) * .66f;
                    DrawMap(new Rect(18, 148, mapWidth, h - 211));
                    DrawDetail(new Rect(36 + mapWidth, 148, w - mapWidth - 54, h - 211));
                }
                GUI.enabled = oldEnabled;
                Label(new Rect(18, h - 54, w - 36, 47), Session.Blocked ? Session.Error : Message, 14, Session.Blocked ? amber : muted);
            }
            if (confirmation != null) DrawConfirmation(w, h);
            GUI.EndGroup();
        }
        Vector2 NodePoint(Rect area, MapNode n) { return new Vector2(area.x + 54 + n.x * (area.width - 108), area.y + 42 + n.y * (area.height - 105)); }
        void DrawMap(Rect area)
        {
            var e = Session.Engine;
            Block(area, new Color(.045f, .09f, .077f));
            for (int i = 0; i < 58; i++)
            {
                float x = area.x + 8 + ((i * 197 + 17) % 991) / 991f * (area.width - 16);
                float y = area.y + 8 + ((i * 131 + 53) % 887) / 887f * (area.height - 36);
                Block(new Rect(x, y, i % 4 == 0 ? 2 : 1, 1), muted * .6f);
            }
            for (int i = 1; i < 7; i++) Block(new Rect(area.x + i * area.width / 7, area.y, 1, area.height), new Color(.09f, .15f, .12f));
            foreach (var n in e.Sector.nodes)
                foreach (var id in n.next)
                {
                    var target = e.Sector.nodes.First(v => v.id == id);
                    bool active = n.id == e.Node.id && e.Pending == null && !e.Complete;
                    Vector2 a = NodePoint(area, n), b = NodePoint(area, target);
                    Line(a, b, active ? signal * .8f : muted * .45f);
                    Vector2 tip = Vector2.Lerp(a, b, .65f), direction = (b - a).normalized, side = new Vector2(-direction.y, direction.x);
                    Line(tip, tip - direction * 7 + side * 4, active ? signal : muted * .6f);
                    Line(tip, tip - direction * 7 - side * 4, active ? signal : muted * .6f);
                }
            foreach (var n in e.Sector.nodes)
            {
                Vector2 p = NodePoint(area, n); bool current = n.id == e.Node.id, visited = e.Visited.Contains(e.Key(n));
                bool reachable = e.Node.next.Contains(n.id) && e.Pending == null && !e.Complete;
                Color color = current ? amber : reachable ? signal : visited ? muted : new Color(.36f, .47f, .39f);
                if (selectedId == n.id) { Block(new Rect(p.x - 19, p.y - 19, 38, 38), color); Block(new Rect(p.x - 17, p.y - 17, 34, 34), new Color(.04f, .08f, .06f)); }
                Block(new Rect(p.x - 11, p.y - 11, 22, 22), color);
                string symbol = current ? "◆" : n.exit ? ">" : visited ? "·" : !string.IsNullOrEmpty(n.eventId) && Campaign.events.First(v => v.id == n.eventId).type == "setpiece" ? "!" : "+";
                Label(new Rect(p.x - 12, p.y - 14, 24, 28), symbol, 19, new Color(.04f, .08f, .05f), TextAnchor.MiddleCenter);
                Label(new Rect(p.x - 57, p.y + 23, 114, 39), n.name, 13, color, TextAnchor.UpperCenter);
                if (GUI.Button(new Rect(p.x - 24, p.y - 24, 48, 62), new GUIContent("", n.name), GUIStyle.none)) { selectedId = n.id; detailScroll = Vector2.zero; }
            }
            Label(new Rect(area.x + 12, area.yMax - 25, area.width - 24, 22), "橙色 当前信标    绿色 可达    暗色 未开放    → 单向航线", 12, muted);
        }
        void DrawDetail(Rect area)
        {
            var e = Session.Engine;
            Block(area, new Color(.08f, .12f, .095f));
            float width = area.width - 30, contentWidth = width - 16;
            // A generous scroll surface supports long custom descriptions and up to eight options.
            detailScroll = GUI.BeginScrollView(new Rect(area.x + 12, area.y + 12, width + 5, area.height - 24), detailScroll, new Rect(0, 0, contentWidth, Mathf.Max(area.height - 26, detailHeight)));
            float y = 0;
            if (e.Complete)
            {
                Label(new Rect(0, y, contentWidth, 40), "航程完成", 24, signal); y += 52;
                Label(new Rect(0, y, contentWidth, 140), "全部星域已通过。航行日志保存了本次选择与资源收支。\n\n可使用「新航程」重新选择其他路线。", 17);
            }
            else if (e.Pending != null)
            {
                var ev = e.Pending; Label(new Rect(0, y, contentWidth, 26), "事件待处置 / " + (ev.type == "setpiece" ? "演出" : "抉择"), 13, amber); y += 31;
                Label(new Rect(0, y, contentWidth, 45), ev.title, 23); y += 47;
                navText.fontSize = 15; float dh = navText.CalcHeight(new GUIContent(ev.description), contentWidth);
                Label(new Rect(0, y, contentWidth, dh + 8), ev.description); y += dh + 23;
                if (Registry.Find(ev.type).NeedsObservation)
                {
                    string state = e.Observed ? "演出已完成 / 可结算" : OwnsPresentation ? (ev.setpiece == 3 ? "等待修复全部三处裂痕" : "演出进行中 / " + Mathf.FloorToInt(Director.ModeTime) + " 秒") : "待观看 / 可继续未完成演出";
                    Label(new Rect(0, y, contentWidth, 32), state, 15, amber); y += 36;
                    if (Button(new Rect(0, y, contentWidth, 34), OwnsPresentation ? "返回舱室观察" : "播放舱内演出")) BeginPresentation(); y += 48;
                }
                foreach (var choice in ev.choices)
                {
                    string error = e.ChoiceError(choice.id);
                    navButton.fontSize = 15;
                    float bh = Mathf.Max(44, navButton.CalcHeight(new GUIContent(choice.text), contentWidth));
                    if (Button(new Rect(0, y, contentWidth, bh), choice.text, error == null)) { Choose(choice.id); break; }
                    y += bh + 5;
                    string cost = "消耗 " + e.Amounts(choice.costs, "-") + "\n获得 " + e.Amounts(choice.rewards, "+");
                    if (error == "资源不足") cost += "（资源不足）";
                    navText.fontSize = 13; float ch = navText.CalcHeight(new GUIContent(cost), contentWidth);
                    Label(new Rect(0, y, contentWidth, ch + 3), cost, 13, muted); y += ch + 18;
                }
                Label(new Rect(0, y + 8, contentWidth, 48), "超过容量的收益会舍弃；日志记录实际到账数量。", 12, muted);
            }
            else
            {
                var node = e.Sector.nodes.FirstOrDefault(n => n.id == selectedId) ?? e.Node;
                Label(new Rect(0, 0, contentWidth, 28), node.exit ? "星域出口" : "信标档案", 13, amber);
                Label(new Rect(0, 37, contentWidth, 70), node.name, 24);
                string info = node.id == e.Node.id ? "当前位置。沿亮起的航线选择下一处信标。" : e.Visited.Contains(e.Key(node)) ? "此信标已访问，无法返回。" : e.Node.next.Contains(node.id) ? "航路已确认，可以跃迁。" : "尚未接入当前航线。请先抵达与它相连的信标。";
                Label(new Rect(0, 114, contentWidth, 95), info);
                if (!string.IsNullOrEmpty(node.eventId)) Label(new Rect(0, 212, contentWidth, 65), Campaign.events.First(ev => ev.id == node.eventId).title, 17, amber);
                if (e.Node.exit)
                { if (Button(new Rect(0, 284, contentWidth, 44), string.IsNullOrEmpty(e.Sector.nextSectorId) ? "完成本次航程" : "进入下一星域")) Advance(); }
                else
                {
                    string why = e.TravelError(node.id);
                    if (Button(new Rect(0, 284, contentWidth, 44), "跃迁至此信标", why == null)) Travel(node.id);
                    if (why != null && e.Node.next.Contains(node.id)) Label(new Rect(0, 341, contentWidth, 65), why, 14, amber);
                }
            }
            detailHeight = e.Pending != null ? y + 80 : 410;
            GUI.EndScrollView();
        }
        void DrawJournal(Rect area)
        {
            var lines = Session.Engine.Journal;
            float height = 0; navText.fontSize = 16;
            foreach (var line in lines) height += navText.CalcHeight(new GUIContent(line), area.width - 55) + 28;
            journalScroll = GUI.BeginScrollView(area, journalScroll, new Rect(0, 0, area.width - 20, height + 16));
            float y = 8;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                navText.fontSize = 16; float lh = navText.CalcHeight(new GUIContent(lines[i]), area.width - 55);
                Label(new Rect(10, y, area.width - 55, lh), lines[i], 16); y += lh + 28;
            }
            GUI.EndScrollView();
        }
        void DrawConfirmation(float w, float h)
        {
            Block(new Rect(0, 0, w, h), new Color(0, 0, 0, .88f));
            float x = (w - 690) / 2, y = Mathf.Max(15, (h - 330) / 2);
            bool import = confirmation == "import";
            Label(new Rect(x, y, 690, 40), import ? "导入配置并开始新航程" : "重新开始航程", 25, amber);
            Label(new Rect(x, y + 62, 690, 180), import ? "从策划工具导出 campaign.json，替换以下文件：\n" + ImportPath + "\n\n导入成功后使用新地图并重置航程。旧配置和航程会保留备份；校验失败时保留当前航程。" : "当前航程将重置到第一个星域。旧航行存档会保留备份，电脑里的个人文档不受影响。", 17);
            if (Button(new Rect(x + 355, y + 266, 155, 40), "取消")) confirmation = null;
            if (Button(new Rect(x + 528, y + 266, 162, 40), import ? "校验并导入" : "开始新航程"))
            {
                if (import) { try { ImportCampaign(NavigationStorage.ReadLimited(ImportPath)); } catch (Exception e) { Message = "无法读取导入文件：" + e.Message; } }
                else Restart();
                confirmation = null;
            }
        }
        void OnGUI()
        {
            if (!loaded || !OwnsPresentation || computer.IsOpen || controller.IsPaused || Session == null) return;
            if (navText == null) return;
            var old = GUI.matrix; float scale = Mathf.Min(Screen.width / 1360f, Screen.height / 780f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1360 * scale) / 2, (Screen.height - 780 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            Block(new Rect(325, 695, 710, 60), new Color(.035f, .065f, .053f, .94f));
            Label(new Rect(343, 705, 470, 44), Session.Engine.Observed ? "演出完成 · 返回导航模式选择处置方案" : "导航事件 · Q / E 观察四周，完成后回到终端", 16);
            if (Button(new Rect(829, 708, 187, 34), "返回导航模式", computer.systems.ScreenHasPower && !controller.IsTurning && !controller.IsObservingWindow))
            { computer.Open(); computer.OpenApp("navigation"); }
            GUI.matrix = old;
        }
    }
}
