using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstraCabin
{
    [DisallowMultipleComponent]
    public sealed partial class CombatMode : MonoBehaviour
    {
        public readonly CombatSimulation sim = new CombatSimulation();
        public bool automationMode, reducedMotion, muted;
        public bool IsVisible { get { return computer && computer.IsOpen && computer.CurrentApp == "combat"; } }
        public bool IsLive { get { return IsVisible && sim.phase == CombatSimulation.Phase.Running; } }
        public bool PhysicalFeedbackEnabled { get { return IsLive && !sim.training && !reducedMotion; } }
        public float FieldZoom { get { return zoom; } }
        CabinComputer computer;
        CabinController controller;
        CabinSystems systems;
        CombatSound sound;
        GUIStyle textStyle, buttonStyle;
        Texture2D ship, heavy, boss, nebula, ringTexture;
        readonly Dictionary<Texture2D, Texture2D[]> rotations = new Dictionary<Texture2D, Texture2D[]>();
        Vector2 cameraPosition;
        float accumulator, zoom = .8f, roll, kick, lightTimer, feedbackClock;
        bool focused = true, wasVisible, armed, pendingBoost, pendingMissile, pendingFire, mouseOverField;
        readonly Color ink = new Color(.74f, .88f, .78f), dim = new Color(.36f, .54f, .47f), green = new Color(.4f, 1, .76f), amber = new Color(1, .69f, .3f), red = new Color(1, .31f, .24f);
        readonly Vector2[] stars = new Vector2[140];
        public float CabinRoll { get { return roll; } }
        public const float MaxCabinRoll = 5, CabinRollSpeed = 3;
        public float LightPulse { get { return lightTimer; } }

        void Awake()
        {
            computer = GetComponent<CabinComputer>(); controller = GetComponent<CabinController>(); systems = GetComponent<CabinSystems>();
            sound = gameObject.AddComponent<CombatSound>();
            var rng = new System.Random(61);
            for (int i = 0; i < stars.Length; i++) stars[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
        }
        void OnApplicationFocus(bool value) { focused = value; if (!value && !automationMode) Suspend(); }
        void OnApplicationPause(bool value) { if (value) Suspend(); }
        public void StartSortie(bool practice)
        { sim.Start(practice); accumulator = 0; armed = false; cameraPosition = sim.position; zoom = .83f; ClearFeedback(); sound.BeginSortie(); }
        public void Suspend() { sim.Pause(); accumulator = 0; armed = false; ClearFeedback(); }
        public void HandleEscape() { if (sim.phase == CombatSimulation.Phase.Running) Suspend(); else if (sim.phase == CombatSimulation.Phase.Paused) sim.Resume(); else computer.Close(); }
        public void ReturnToMenu() { sim.ToMenu(); ClearFeedback(); }
        void ClearFeedback()
        {
            roll = kick = lightTimer = 0;
            pendingBoost = pendingMissile = pendingFire = mouseOverField = false;
            if (controller) { controller.combatEuler = Vector3.zero; controller.combatOffset = Vector3.zero; controller.combatFov = 0; }
            if (systems) systems.SetCombatFeedback(0, 0);
            if (sound) sound.SetActive(false, 0, muted);
        }
        void Update()
        {
            bool visible = IsVisible;
            if ((!visible && wasVisible) || (!focused && !automationMode) || (visible && computer.HasPendingEventAlert)) Suspend();
            wasVisible = visible;
            if (!visible || (!focused && !automationMode)) return;
            if (!automationMode && Input.GetKeyDown(KeyCode.P)) { if (sim.phase == CombatSimulation.Phase.Paused) sim.Resume(); else Suspend(); }
            if (!automationMode && Input.GetKeyDown(KeyCode.M)) muted = !muted;
            if (!IsLive) { ClearFeedback(); return; }
            if (!Input.GetMouseButton(0)) armed = true;
            pendingBoost |= Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
            pendingMissile |= mouseOverField && Input.GetMouseButtonDown(1);
            CombatSimulation.Controls controls = new CombatSimulation.Controls {
                turn = (Input.GetKey(KeyCode.A) ? 1 : 0) - (Input.GetKey(KeyCode.D) ? 1 : 0),
                thrust = Input.GetKey(KeyCode.W), brake = Input.GetKey(KeyCode.S),
                fire = pendingFire || (armed && mouseOverField && Input.GetMouseButton(0)),
                boost = pendingBoost, missile = pendingMissile
            };
            if (!automationMode)
            {
                // Bound catch-up after stalls; do not advance while the process is suspended.
                accumulator = Mathf.Min(accumulator + Time.unscaledDeltaTime, .1f);
                while (accumulator >= 1f / 120f)
                { sim.Step(1f / 120f, controls); accumulator -= 1f / 120f; controls.boost = controls.missile = pendingBoost = pendingMissile = pendingFire = false; }
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f); feedbackClock += dt;
            cameraPosition = Vector2.Lerp(cameraPosition, sim.position + sim.velocity * .36f, 1 - Mathf.Exp(-5 * dt));
            // Acceleration changes the real camera FOV; do not stack a second zoom.
            zoom = .83f;
            foreach (var cue in sim.cues)
            {
                sound.Play(cue.name, cue.strength, muted);
                if (PhysicalFeedbackEnabled && cue.name == "shot") kick = Mathf.Min(1, kick + .18f);
                if (PhysicalFeedbackEnabled && cue.name == "damage") lightTimer = .85f;
            }
            sim.cues.Clear();
            if (!IsLive) { ClearFeedback(); return; }
            if (!PhysicalFeedbackEnabled)
            {
                roll = kick = lightTimer = 0;
                if (controller) { controller.combatEuler = controller.combatOffset = Vector3.zero; controller.combatFov = 0; }
                if (systems) systems.SetCombatFeedback(0, 0);
                sound.SetActive(true, sim.velocity.magnitude / CombatSimulation.BoostSpeed, muted);
                return;
            }
            kick = Mathf.Max(0, kick - dt * 5); lightTimer = Mathf.Max(0, lightTimer - dt);
            float availableTurn = CombatSimulation.FlightTurnRate(sim.velocity.magnitude) * (1 + Mathf.Min(sim.engineLevel, 3) * .04f);
            float rollTarget = -Mathf.Clamp(sim.angularVelocity / availableTurn, -1, 1) * MaxCabinRoll;
            float easedRoll = Mathf.Lerp(roll, rollTarget, 1 - Mathf.Exp(-1.8f * dt));
            roll = Mathf.MoveTowards(roll, easedRoll, CabinRollSpeed * dt);
            float shake = reducedMotion ? 0 : sim.trauma * sim.trauma;
            if (controller)
            {
                controller.combatEuler = new Vector3((Mathf.PerlinNoise(feedbackClock * 21, 2) - .5f) * shake * 2.5f - (reducedMotion ? 0 : kick * .35f), 0, roll + (Mathf.PerlinNoise(3, feedbackClock * 18) - .5f) * shake * 1.8f);
                controller.combatOffset = Vector3.zero;
                float speed = sim.velocity.magnitude;
                float fovTarget = 4 * Mathf.InverseLerp(CombatSimulation.CruiseSpeed, CombatSimulation.ThrustSpeed, speed)
                    + 3 * Mathf.InverseLerp(CombatSimulation.ThrustSpeed, CombatSimulation.BoostSpeed, speed);
                controller.combatFov = Mathf.Lerp(controller.combatFov, fovTarget, 1 - Mathf.Exp(-3 * dt));
            }
            if (systems) systems.SetCombatFeedback(reducedMotion ? 0 : lightTimer > .72f ? 1 : 0, reducedMotion ? 0 : lightTimer > 0 ? (.5f + .5f * Mathf.Sin(feedbackClock * 25)) : 0);
            sound.SetActive(true, sim.velocity.magnitude / CombatSimulation.BoostSpeed, muted);
        }
        void EnsureArt(Font font)
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, richText = false, alignment = TextAnchor.MiddleLeft, wordWrap = false, padding = new RectOffset(0, 0, 0, 0) };
            buttonStyle = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter };
            ship = ShipTexture(0); heavy = ShipTexture(1); boss = ShipTexture(2);
            foreach (Texture2D source in new[] { ship, heavy, boss })
            {
                Color32[] pixels = source.GetPixels32(); var atlas = new Texture2D[72];
                for (int frame = 0; frame < 72; frame++)
                {
                    float a = frame * 5 * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                    var rotated = new Color32[64 * 64];
                    for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    {
                        float dx = x - 31.5f, dy = y - 31.5f;
                        int sx = Mathf.RoundToInt(cos * dx - sin * dy + 31.5f), sy = Mathf.RoundToInt(sin * dx + cos * dy + 31.5f);
                        if (sx >= 0 && sx < 64 && sy >= 0 && sy < 64) rotated[y * 64 + x] = pixels[sy * 64 + sx];
                    }
                    atlas[frame] = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    atlas[frame].SetPixels32(rotated); atlas[frame].Apply();
                }
                rotations.Add(source, atlas);
            }
            ringTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            { float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)); ringTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1.6f - Mathf.Abs(d - 62)))); }
            ringTexture.Apply();
            nebula = new Texture2D(192, 96, TextureFormat.RGBA32, false); nebula.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 96; y++) for (int x = 0; x < 192; x++)
            {
                float n = Mathf.PerlinNoise(x * .025f, y * .035f) * Mathf.PerlinNoise(x * .08f + 22, y * .08f + 13);
                float lane = Mathf.Exp(-Mathf.Pow((y - 50 + Mathf.Sin(x * .022f) * 25) / 26, 2));
                nebula.SetPixel(x, y, new Color(.025f + n * .055f, .05f + n * lane * .17f, .06f + n * lane * .15f));
            }
            nebula.Apply();
        }
        static Texture2D ShipTexture(int type)
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false); t.filterMode = FilterMode.Point;
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float u = (x - 32) / 30f, v = (y - 32) / 30f;
                bool body = u > -.72f && u < .98f && Mathf.Abs(v) < (.99f - u) * .2f;
                bool wings = u > -.62f && u < .17f && Mathf.Abs(v) < (type == 2 ? .85f : .75f) && u < .18f - Mathf.Abs(v) * (type == 1 ? .35f : .65f);
                bool tail = u < -.48f && u > -.88f && Mathf.Abs(v) < .46f;
                Color c = Color.clear;
                if (body || wings || tail) c = Mathf.Abs(v) < .065f ? Color.white : new Color(.63f, .75f, .71f);
                if ((body || wings) && ((x + y) % 11 == 0)) c *= .58f;
                if (u > .16f && u < .5f && Mathf.Abs(v) < .1f) c = new Color(.08f, .2f, .19f);
                if (type == 2 && u > -.15f && u < .28f && Mathf.Abs(v) > .48f && Mathf.Abs(v) < .85f) c = Color.white;
                t.SetPixel(x, y, c);
            }
            t.Apply(); return t;
        }
        public void Draw(Rect content, Font font)
        {
            EnsureArt(font); GUI.BeginGroup(content);
            float w = content.width, h = content.height;
            Fill(new Rect(0, 0, w, h), new Color(.024f, .047f, .043f));
            if (sim.phase == CombatSimulation.Phase.Menu) DrawMenu(w, h);
            else
            {
                DrawHUD(w);
                Rect field = new Rect(12, 69, w - 202, h - 121);
                // Mouse coordinates from IMGUI already include desktop scaling and nested
                // window offsets. Buffer presses so a click between physics ticks is kept.
                mouseOverField = field.Contains(Event.current.mousePosition);
                if (!automationMode && sim.phase == CombatSimulation.Phase.Running && mouseOverField && Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 0) pendingFire = true;
                    if (Event.current.button == 1) pendingMissile = true;
                    Event.current.Use();
                }
                DrawField(field); DrawRail(new Rect(w - 179, 69, 167, h - 121));
                Label(new Rect(15, h - 44, w - 25, 20), "W 推进   S 刹车   A / D 调整方向   SHIFT 躲避   左键 子弹   右键 锁定导弹", 14, ink);
            Label(new Rect(15, h - 23, w - 25, 17), "机头决定弹道 · 前方 ±40° 锁定 · " + (sim.training ? "体感联动：关闭（练习）" : reducedMotion ? "体感联动：降低" : "体感联动：开启") + " · P 暂停 · M 静音", 12, dim);
                if (sim.phase != CombatSimulation.Phase.Running) DrawOverlay(w, h);
            }
            GUI.EndGroup();
        }
        void DrawMenu(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), nebula, ScaleMode.StretchToFill);
            for (int i = 0; i < stars.Length; i++) Fill(new Rect(stars[i].x * w, stars[i].y * h, 1.5f, 1.5f), new Color(.5f, .72f, .63f, .35f));
            Label(new Rect(38, 25, 580, 22), "PIGEON PROGRAM  /  TACTICAL FLIGHT CONSOLE", 13, green);
            Label(new Rect(36, 59, 590, 68), "战斗模式", 45, ink);
            Label(new Rect(39, 130, 620, 28), "保持航向。抓住窗口。活着回来。", 21, ink);
            Label(new Rect(40, 178, 590, 25), "演练任务 04 / 外围封锁线", 17, amber);
            Label(new Rect(40, 212, 590, 23), "三段遭遇 · 战间改装 · 重型指挥机", 15, dim);
            Label(new Rect(40, 244, 590, 24), "在二维战术重建中驾驶探针，清除封锁并返回。", 16, ink);
            Label(new Rect(40, 277, 590, 22), "演练独立结算，舱内姿态与受击反馈同步。", 14, dim);
            float by = Mathf.Min(h - 137, 334);
            if (Button(new Rect(40, by, 224, 48), "开始出击  /  SORTIE", green)) StartSortie(false);
            if (Button(new Rect(279, by, 170, 48), "自由练习", ink)) StartSortie(true);
            if (Button(new Rect(40, by + 64, 202, 30), muted ? "声音：关闭" : "声音：开启", dim)) muted = !muted;
            if (Button(new Rect(257, by + 64, 192, 30), reducedMotion ? "动态效果：降低" : "动态效果：标准", dim)) reducedMotion = !reducedMotion;
            Vector2 c = new Vector2(w - 242, h * .46f);
            Ring(c, 137, new Color(.24f, .44f, .37f), 64); Ring(c, 105, new Color(.16f, .31f, .27f), 64);
            Line(c + Vector2.left * 160, c + Vector2.right * 160, dim, 1); Line(c + Vector2.up * 160, c + Vector2.down * 160, dim, 1);
            Sprite(ship, c, -27, 148, green);
            Label(new Rect(c.x - 128, c.y + 164, 270, 24), "PROBE 04   /   INTERCEPTOR", 14, ink);
            Label(new Rect(c.x - 128, c.y + 190, 280, 22), "HULL 100   SHIELD 60   BUS 100", 12, dim);
            Label(new Rect(40, h - 29, w - 70, 20), "W 推进 · S 刹车 · A / D 调整方向 · Shift 躲避 · 左键子弹 · 右键导弹", 13, dim);
        }
        void DrawHUD(float w)
        {
            Label(new Rect(14, 6, 195, 23), sim.training ? "练习场 / TRAINING" : "封锁线 / SECTOR 0" + sim.sector, 16, green);
            Label(new Rect(14, 32, 200, 22), "敌机 " + sim.enemies.Count + "   /   击毁 " + sim.kills, 14, ink);
            Meter(new Rect(229, 13, 161, 11), sim.hull / 100, sim.hull < 30 ? red : green, "船体 " + Mathf.CeilToInt(sim.hull));
            Meter(new Rect(412, 13, 161, 11), sim.shield / sim.maxShield, new Color(.4f, .72f, 1), "护盾 " + Mathf.CeilToInt(sim.shield));
            Meter(new Rect(595, 13, 135, 11), sim.energy / 100, green, "推进电容 " + Mathf.FloorToInt(sim.energy));
            Meter(new Rect(752, 13, 129, 11), sim.heat / 100, sim.overheated ? red : amber, sim.overheated ? "过热 / 冷却中" : "武器热量 " + Mathf.FloorToInt(sim.heat));
            if (Button(new Rect(w - 177, 10, 165, 42), sim.phase == CombatSimulation.Phase.Paused ? "继续  /  P" : "暂停  /  P", dim)) { if (sim.phase == CombatSimulation.Phase.Paused) sim.Resume(); else Suspend(); }
        }
        void DrawOverlay(float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(.008f, .025f, .02f, .91f));
            if (sim.phase == CombatSimulation.Phase.Upgrade)
            {
                Label(new Rect(40, 38, w - 80, 45), "航段清除 / 选择一项改装", 29, green);
                Label(new Rect(40, 96, w - 80, 30), "整备：船体 +25 · 护盾充满 · 导弹 +3。下一段：" + (sim.sector == 1 ? "重型护航编队" : "指挥机"), 16, ink);
                string[] names = { "01 / 武器电路", "02 / 矢量推进", "03 / 护盾电容" };
                string[] details = { "提高机炮伤害与射速\n更快击穿重型目标", "更快转向与推进\n电容恢复更快，缩短冷却", "护盾上限 +30\n提高脱战恢复速率" };
                float cw = (w - 104) / 3;
                for (int i = 0; i < 3; i++)
                {
                    Rect card = new Rect(40 + i * (cw + 12), 164, cw, h - 224); Fill(card, new Color(.06f, .13f, .1f)); Border(card, dim);
                    Label(new Rect(card.x + 20, card.y + 18, cw - 40, 30), names[i], 22, ink);
                    Label(new Rect(card.x + 20, card.y + 67, cw - 40, 65), details[i], 17, dim);
                    if (Button(new Rect(card.x + 20, card.yMax - 66, cw - 40, 42), "安装并继续", green)) { sim.Upgrade(i); armed = false; }
                }
                return;
            }
            bool paused = sim.phase == CombatSimulation.Phase.Paused;
            string heading = paused ? "战斗已暂停" : sim.phase == CombatSimulation.Phase.Won ? "封锁解除 / 任务完成" : "信号丢失 / 船体失效";
            Label(new Rect(0, 52, w, 55), heading, 36, paused || sim.phase == CombatSimulation.Phase.Won ? green : amber, TextAnchor.MiddleCenter);
            Label(new Rect(0, 118, w, 32), paused ? "飞行、武器与敌机已冻结" : "击毁 " + sim.kills + "     得分 " + sim.score + "     命中率 " + (sim.shots == 0 ? 0 : Mathf.RoundToInt(sim.hits * 100f / sim.shots)) + "%", 18, ink, TextAnchor.MiddleCenter);
            float x = w / 2 - 225;
            if (Button(new Rect(x, 181, 450, 48), paused ? "继续飞行 / RESUME" : "再次出击 / RETRY", green)) { if (paused) { sim.Resume(); armed = false; } else StartSortie(sim.training); }
            if (Button(new Rect(x, 243, 216, 38), muted ? "声音：关闭" : "声音：开启", dim)) muted = !muted;
            if (Button(new Rect(x + 232, 243, 218, 38), reducedMotion ? "动态效果：降低" : "动态效果：标准", dim)) reducedMotion = !reducedMotion;
            if (Button(new Rect(x, 298, 216, 42), "返回任务界面", ink)) ReturnToMenu();
            if (Button(new Rect(x + 232, 298, 218, 42), "退出终端", ink)) computer.Close();
            Label(new Rect(0, h - 54, w, 25), "最小化、切换应用或离开终端会自动暂停。", 14, dim, TextAnchor.MiddleCenter);
        }
        void Label(Rect r, string s, int size, Color c, TextAnchor align = TextAnchor.MiddleLeft)
        { textStyle.fontSize = size; textStyle.normal.textColor = c; textStyle.alignment = align; GUI.Label(r, s, textStyle); }
        bool Button(Rect r, string s, Color c)
        { bool hover = r.Contains(Event.current.mousePosition); Fill(r, hover ? new Color(.15f, .25f, .19f) : new Color(.055f, .12f, .09f)); Border(r, c); buttonStyle.fontSize = 16; buttonStyle.normal.textColor = c; return GUI.Button(r, s, buttonStyle); }
        void Meter(Rect r, float amount, Color c, string s)
        { Fill(r, new Color(.12f, .21f, .17f)); Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(amount), r.height), c); Label(new Rect(r.x, r.y + 16, r.width + 5, 23), s, 13, c); }
        static void Fill(Rect r, Color c) { Color old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old; }
        static void Border(Rect r, Color c) { Fill(new Rect(r.x, r.y, r.width, 1), c); Fill(new Rect(r.x, r.yMax - 1, r.width, 1), c); Fill(new Rect(r.x, r.y, 1, r.height), c); Fill(new Rect(r.xMax - 1, r.y, 1, r.height), c); }
        static void Line(Vector2 a, Vector2 b, Color c, float width)
        {
            if (Event.current.type != EventType.Repaint) return;
            // Axis-aligned pixel spans respect nested IMGUI clips. Rotating GUI.matrix
            // would rotate the desktop clip too and leak art outside the app window.
            bool steep = Mathf.Abs(b.y - a.y) > Mathf.Abs(b.x - a.x);
            if (steep) { a = new Vector2(a.y, a.x); b = new Vector2(b.y, b.x); }
            if (a.x > b.x) { Vector2 t = a; a = b; b = t; }
            int count = Mathf.Min(4096, Mathf.Max(1, Mathf.CeilToInt(b.x - a.x)));
            float run = a.x, last = Mathf.Round(a.y);
            for (int i = 1; i <= count; i++)
            {
                float x = Mathf.Lerp(a.x, b.x, i / (float)count), y = Mathf.Round(Mathf.Lerp(a.y, b.y, i / (float)count));
                if (y == last && i != count) continue;
                if (steep) Fill(new Rect(last - width / 2, run, width, Mathf.Max(1, x - run)), c);
                else Fill(new Rect(run, last - width / 2, Mathf.Max(1, x - run), width), c);
                run = x; last = y;
            }
        }
        void Ring(Vector2 c, float radius, Color color, int segments)
        { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(new Rect(c.x - radius, c.y - radius, radius * 2, radius * 2), ringTexture); GUI.color = old; }
        static void Brackets(Vector2 c, float r, Color color)
        { for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) { Vector2 p = c + new Vector2(x, y) * r; Line(p, p - Vector2.right * x * 9, color, 2); Line(p, p - Vector2.up * y * 9, color, 2); } }
        void Sprite(Texture2D texture, Vector2 c, float angle, float size, Color tint)
        { Color color = GUI.color; GUI.color = tint; int frame = Mathf.RoundToInt(Mathf.Repeat(angle, 360) / 5) % 72; GUI.DrawTexture(new Rect(c.x - size / 2, c.y - size / 2, size, size), rotations[texture][frame]); GUI.color = color; }
        void OnDisable() { Suspend(); }
        void OnDestroy() { foreach (Texture2D t in new[] { ship, heavy, boss, nebula, ringTexture }) if (t) Destroy(t); foreach (var atlas in rotations.Values) foreach (var t in atlas) if (t) Destroy(t); }
    }
}
