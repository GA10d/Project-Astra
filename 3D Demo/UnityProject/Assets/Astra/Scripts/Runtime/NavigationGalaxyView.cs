using UnityEngine;

namespace AstraCabin
{
    public sealed partial class NavigationMode
    {
        static readonly Color[] GalaxyColors = {
            new Color(.96f, .68f, .34f), new Color(.38f, .81f, .78f), new Color(.69f, .62f, .94f)
        };
        Texture2D[] galaxyImages;
        readonly float[] galaxyHover = new float[3];
        bool galaxyTransition, galaxyTransitionCommitted;
        string galaxyTransitionTarget;
        float galaxyTransitionStart, galaxyTransitionDuration;

        public bool IsGalaxyTransitioning { get { return galaxyTransition; } }
        bool GalaxyReducedMotion { get { return computer && computer.Combat && computer.Combat.reducedMotion; } }
        float GalaxyTransitionProgress { get { return Mathf.Clamp01((Time.realtimeSinceStartup - galaxyTransitionStart) / galaxyTransitionDuration); } }
        float GalaxyContentAlpha
        {
            get
            {
                if (!galaxyTransition) return 1;
                float t = GalaxyTransitionProgress;
                return t < .5f ? 1 - Mathf.SmoothStep(0, 1, t * 2) : Mathf.SmoothStep(0, 1, (t - .5f) * 2);
            }
        }

        // All pointer and automation requests share the same state gate and transition.
        public bool RequestEnterGalaxy(string id)
        {
            Initialize();
            if (galaxyTransition || confirmation != null || System.Array.IndexOf(GalaxyIds, id) < 0) return false;
            if (!CanSwitchGalaxy && id != ActiveGalaxyId) return false;
            if (!IsGalaxyOverview && id == ActiveGalaxyId) return true;
            StartGalaxyTransition(id);
            return true;
        }
        public bool RequestGalaxyOverview()
        {
            Initialize();
            if (galaxyTransition || confirmation != null) return false;
            if (IsGalaxyOverview) return true;
            StartGalaxyTransition(null);
            return true;
        }
        void StartGalaxyTransition(string id)
        {
            galaxyTransition = true; galaxyTransitionCommitted = false;
            galaxyTransitionTarget = id; galaxyTransitionStart = Time.realtimeSinceStartup;
            galaxyTransitionDuration = GalaxyReducedMotion ? .24f : .68f;
        }
        void TickGalaxyTransition()
        {
            if (!galaxyTransition) return;
            float t = GalaxyTransitionProgress;
            if (t >= .5f && !galaxyTransitionCommitted)
            {
                galaxyTransitionCommitted = true;
                bool changed = galaxyTransitionTarget == null ? ReturnToGalaxies() : EnterGalaxy(galaxyTransitionTarget);
                if (changed) { showJournal = false; detailScroll = Vector2.zero; journalScroll = Vector2.zero; }
            }
            if (t >= 1) galaxyTransition = false;
        }

        void DrawGalaxyOverview(float w, float h)
        {
            EnsureGalaxyImages();
            Block(new Rect(18, 52, w - 36, 1), new Color(.22f, .32f, .26f));
            Label(new Rect(24, 67, w - 48, 28), "选择星系 · 接入航路", 20, paper);
            Label(new Rect(24, 98, w - 48, 21), "星图已展开。点击星球，查看该星系中的航行关卡。", 14, muted);
            Rect field = new Rect(18, 132, w - 36, h - 193);
            DrawGalaxyField(field);
            float cardWidth = Mathf.Min(260, (field.width - 64) / 3);
            for (int i = 0; i < GalaxyIds.Length; i++)
            {
                float cx = field.x + field.width * (.20f + i * .30f);
                DrawGalaxyCard(new Rect(cx - cardWidth / 2, field.y + 16, cardWidth, field.height - 32), i);
            }
            string status = CanSwitchGalaxy ? "DEEP SPACE / 03 SECTORS     ·     航行进度会保留" : "当前事件待处置 · 返回 " + ActiveGalaxyId + " 完成事件后，可切换星系";
            Label(new Rect(24, h - 43, w - 48, 25), status, 13, CanSwitchGalaxy ? muted : amber);
        }

        void DrawGalaxyField(Rect area)
        {
            Block(area, new Color(.022f, .047f, .041f));
            Color grid = new Color(.065f, .115f, .097f);
            for (float x = area.x + 28; x < area.xMax; x += 48) Block(new Rect(x, area.y, 1, area.height), grid);
            for (float y = area.y + 24; y < area.yMax; y += 48) Block(new Rect(area.x, y, area.width, 1), grid);
            for (int i = 0; i < 96; i++)
            {
                float x = area.x + 11 + ((i * 197 + 39) % 997) / 997f * (area.width - 22);
                float y = area.y + 10 + ((i * 173 + 71) % 881) / 881f * (area.height - 20);
                float light = i % 7 == 0 ? .53f : .27f;
                Color c = new Color(.60f, .75f, .65f, light);
                Block(new Rect(x, y, i % 7 == 0 ? 2 : 1, 1), c);
                if (i % 13 == 0) Block(new Rect(x, y - 1, 1, 3), c);
            }
            // Thin scan lines and corner marks belong to the image, not a transformed GUI matrix.
            for (float y = area.y; y < area.yMax; y += 5) Block(new Rect(area.x, y, area.width, 1), new Color(0, 0, 0, .11f));
            DrawGalaxyCorners(area, new Color(.24f, .36f, .28f), 18);
        }

        void DrawGalaxyCard(Rect card, int index)
        {
            string id = GalaxyIds[index]; Color accent = GalaxyColors[index];
            bool allowed = CanSwitchGalaxy || ActiveGalaxyId == id;
            bool hover = card.Contains(Event.current.mousePosition) && GUI.enabled && allowed;
            if (Event.current.type == EventType.Repaint)
                galaxyHover[index] = GalaxyReducedMotion ? (hover ? 1 : 0) : Mathf.MoveTowards(galaxyHover[index], hover ? 1 : 0, Time.unscaledDeltaTime * 5);
            float hot = galaxyHover[index];
            float planetSize = Mathf.Min(180, card.height * .54f);
            float bob = GalaxyReducedMotion ? 0 : Mathf.Sin(Time.realtimeSinceStartup * .65f + index * 1.8f) * 2;
            Vector2 center = new Vector2(card.center.x, card.y + card.height * .38f + bob);
            Color orbit = new Color(accent.r, accent.g, accent.b, .12f + hot * .17f);
            for (int n = 0; n < 84; n++)
            {
                float angle = n / 84f * Mathf.PI * 2;
                float x = center.x + Mathf.Cos(angle) * planetSize * .63f;
                float y = center.y + Mathf.Sin(angle) * planetSize * .22f + 11;
                Block(new Rect(x, y, 2, 1), orbit);
            }
            if (!GalaxyReducedMotion)
            {
                float phase = Time.realtimeSinceStartup * .13f + index * 2.1f;
                Block(new Rect(center.x + Mathf.Cos(phase) * planetSize * .63f - 2, center.y + Mathf.Sin(phase) * planetSize * .22f + 9, 4, 4), new Color(accent.r, accent.g, accent.b, .6f));
            }
            var imageColor = GUI.color;
            GUI.color = new Color(imageColor.r, imageColor.g, imageColor.b, imageColor.a * (allowed ? 1 : .48f));
            GUI.DrawTexture(new Rect(center.x - planetSize / 2, center.y - planetSize / 2, planetSize, planetSize), galaxyImages[index], ScaleMode.StretchToFill, true);
            GUI.color = imageColor;
            if (hot > .01f)
            {
                Block(new Rect(card.x, card.y, card.width, card.height), new Color(accent.r, accent.g, accent.b, hot * .018f));
                DrawGalaxyCorners(card, new Color(accent.r, accent.g, accent.b, hot * .70f), 12);
            }
            float textY = card.y + card.height * .72f;
            Label(new Rect(card.x, textY - 9, card.width, 37), id, 26, accent, TextAnchor.MiddleCenter);
            Block(new Rect(card.center.x - 28, textY + 35, 56, 1), new Color(accent.r, accent.g, accent.b, .46f + hot * .30f));
            bool content = GalaxyHasContent(id);
            string state = content ? (id == ActiveGalaxyId && Session != null ? "航路已接入 · 继续航程" : "航路已接入") : "待开放";
            if (!allowed) state = "完成当前事件后接入";
            Label(new Rect(card.x + 2, textY + 46, card.width - 4, 27), state, 13, allowed ? muted : new Color(.41f, .44f, .40f), TextAnchor.MiddleCenter);
            bool originalEnabled = GUI.enabled; GUI.enabled = originalEnabled && allowed;
            if (GUI.Button(card, new GUIContent("", "进入 " + id + " 星系"), GUIStyle.none)) RequestEnterGalaxy(id);
            GUI.enabled = originalEnabled;
        }

        static void DrawGalaxyCorners(Rect r, Color c, float length)
        {
            Block(new Rect(r.x, r.y, length, 1), c); Block(new Rect(r.x, r.y, 1, length), c);
            Block(new Rect(r.xMax - length, r.y, length, 1), c); Block(new Rect(r.xMax - 1, r.y, 1, length), c);
            Block(new Rect(r.x, r.yMax - 1, length, 1), c); Block(new Rect(r.x, r.yMax - length, 1, length), c);
            Block(new Rect(r.xMax - length, r.yMax - 1, length, 1), c); Block(new Rect(r.xMax - 1, r.yMax - length, 1, length), c);
        }

        void DrawGalaxyWaiting(float w, float h)
        {
            EnsureGalaxyImages();
            int index = Mathf.Max(0, System.Array.IndexOf(GalaxyIds, ActiveGalaxyId));
            Rect field = new Rect(18, 58, w - 36, h - 119);
            DrawGalaxyField(field);
            float size = Mathf.Min(184, field.height * .5f);
            float cy = field.y + field.height * .33f;
            GUI.DrawTexture(new Rect(field.center.x - size / 2, cy - size / 2, size, size), galaxyImages[index], ScaleMode.StretchToFill, true);
            Label(new Rect(field.x, cy + size / 2 + 16, field.width, 36), ActiveGalaxyId + " 星系", 26, GalaxyColors[index], TextAnchor.MiddleCenter);
            Label(new Rect(field.x + 32, cy + size / 2 + 61, field.width - 64, 38), "航路正在筹备 · 敬请期待", 17, muted, TextAnchor.MiddleCenter);
            bool importError = !string.IsNullOrEmpty(Message) && (Message.StartsWith("导入失败") || Message.StartsWith("无法读取导入文件"));
            Label(new Rect(24, h - 49, w - 48, 44), importError ? Message : "可返回星系总览，继续探索其他航路。", 13, importError ? amber : muted);
        }

        void DrawGalaxyConfigError(float w, float h)
        {
            Block(new Rect(18, 58, w - 36, h - 119), new Color(.045f, .09f, .077f));
            Label(new Rect(36, 85, w - 72, 36), "航路配置无法接通", 24, amber);
            Label(new Rect(36, 143, w - 72, Mathf.Max(110, h - 300)), Message, 18, paper);
            float y = Mathf.Min(h - 144, 360);
            if (Button(new Rect(36, y, 230, 42), "重新导入星域配置", confirmation == null)) confirmation = "import";
            Label(new Rect(24, h - 44, w - 48, 25), "修正配置后重新导入，或返回星系总览选择其他航路。", 13, muted);
        }

        void DrawGalaxyTransition(float w, float h)
        {
            float t = GalaxyTransitionProgress;
            float envelope = Mathf.Sin(t * Mathf.PI);
            if (!GalaxyReducedMotion)
            {
                float sweep = Mathf.Lerp(53, h - 25, t);
                Block(new Rect(18, sweep, w - 36, 2), new Color(.60f, .80f, .62f, envelope * .22f));
                for (int i = 0; i < 6; i++)
                    Block(new Rect(18, sweep - i * 3, w - 36, 1), new Color(.39f, .65f, .47f, envelope * .035f));
            }
            float labelAlpha = 1 - Mathf.Abs(t - .5f) * 3.5f;
            if (labelAlpha > 0)
            {
                string text = galaxyTransitionTarget == null ? "展开星系总览" : "接入 " + galaxyTransitionTarget + " 星系";
                Label(new Rect(18, h * .48f, w - 36, 32), text, 18, new Color(.72f, .84f, .72f, labelAlpha), TextAnchor.MiddleCenter);
                Block(new Rect(w / 2 - 78, h * .48f + 41, 156, 2), new Color(.15f, .28f, .20f, labelAlpha));
                Block(new Rect(w / 2 - 78, h * .48f + 41, 156 * t, 2), new Color(.66f, .85f, .60f, labelAlpha));
            }
        }

        void EnsureGalaxyImages()
        {
            if (galaxyImages != null) return;
            galaxyImages = new Texture2D[3];
            for (int i = 0; i < galaxyImages.Length; i++) galaxyImages[i] = CreateGalaxyImage(i);
        }
        // Small native textures give each destination its own illustrated surface. The fixed
        // palette, spherical light, craters/oceans/bands and point filtering retain the CRT look.
        static Texture2D CreateGalaxyImage(int kind)
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            Color accent = GalaxyColors[kind];
            Vector3 sun = new Vector3(-.5f, .65f, .57f).normalized;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float px = (x - 63.5f) / 46f, py = (y - 63.5f) / 46f;
                float radius = Mathf.Sqrt(px * px + py * py);
                Color color = new Color(accent.r, accent.g, accent.b, Mathf.Max(0, 1 - Mathf.Abs(radius - .99f) / .26f) * .13f);
                float ringX = (x - 63.5f) * .94f + (y - 63.5f) * .34f;
                float ringY = -(x - 63.5f) * .34f + (y - 63.5f) * .94f;
                float ringDistance = Mathf.Sqrt(ringX * ringX / (61 * 61) + ringY * ringY / (18 * 18));
                bool ring = kind == 2 && ringDistance > .77f && ringDistance < 1.0f;
                if (ring) color = Color.Lerp(new Color(.34f, .29f, .51f, .9f), new Color(.67f, .61f, .79f, .95f), Mathf.Floor((1 - ringDistance) * 35) % 3 / 2);
                if (radius <= 1)
                {
                    float nz = Mathf.Sqrt(Mathf.Max(0, 1 - radius * radius));
                    Vector3 normal = new Vector3(px, py, nz);
                    float light = Mathf.Clamp01(Vector3.Dot(normal, sun) * .78f + .21f);
                    float noise = Mathf.PerlinNoise(x * .081f + kind * 27, y * .081f + kind * 13);
                    float grain = ((x * 37 + y * 19) % 17) / 17f;
                    Color surface;
                    if (kind == 0)
                    {
                        surface = Color.Lerp(new Color(.33f, .17f, .09f), new Color(.88f, .62f, .31f), Mathf.Floor(noise * 6) / 5);
                        for (int n = 0; n < 13; n++)
                        {
                            float cx = (((n * 31 + 13) % 79) - 39) / 46f;
                            float cy = (((n * 47 + 21) % 79) - 39) / 46f;
                            float cr = .065f + (n % 4) * .026f;
                            float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy)) / cr;
                            if (d < 1.2f && d > .85f) surface *= 1.25f;
                            if (d < .85f) surface *= .67f + d * .15f;
                        }
                    }
                    else if (kind == 1)
                    {
                        float land = Mathf.PerlinNoise(x * .04f + 87, y * .045f + 35);
                        surface = land > .57f ? Color.Lerp(new Color(.15f, .37f, .28f), new Color(.36f, .61f, .38f), noise) : Color.Lerp(new Color(.06f, .21f, .28f), new Color(.24f, .68f, .69f), noise);
                        float cloud = Mathf.PerlinNoise(x * .071f + y * .02f + 17, y * .091f + 81);
                        if (cloud > .65f || py > .81f) surface = Color.Lerp(surface, new Color(.68f, .83f, .74f), .64f);
                    }
                    else
                    {
                        float band = Mathf.Sin(py * 31 + Mathf.PerlinNoise(x * .045f, y * .07f) * 3) * .5f + .5f;
                        surface = Color.Lerp(new Color(.29f, .19f, .46f), new Color(.68f, .55f, .83f), Mathf.Floor(band * 5) / 4);
                    }
                    float dither = ((x + y) % 2 == 0 ? .025f : -.025f) + (grain - .5f) * .055f;
                    float shade = Mathf.Clamp(Mathf.Floor((light + dither) * 9) / 8, .17f, 1.04f);
                    color = new Color(surface.r * shade, surface.g * shade, surface.b * shade, 1);
                    if (ring && ringY < 0)
                    {
                        Color frontRing = Color.Lerp(new Color(.44f, .37f, .59f), new Color(.77f, .70f, .88f), Mathf.Floor((1 - ringDistance) * 35) % 3 / 2);
                        color = Color.Lerp(color, frontRing, .86f);
                    }
                }
                pixels[y * size + x] = color;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Navigation planet " + GalaxyIds[kind], filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }
        void OnDestroy()
        {
            if (galaxyImages == null) return;
            foreach (Texture2D texture in galaxyImages) if (texture) Destroy(texture);
            galaxyImages = null;
        }
    }
}
