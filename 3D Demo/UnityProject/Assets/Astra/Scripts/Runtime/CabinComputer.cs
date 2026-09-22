using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AstraCabin
{
    /// <summary>A self-contained, offline shipboard desktop. All virtual files live in one controlled save file.</summary>
    [DisallowMultipleComponent]
    public sealed class CabinComputer : MonoBehaviour
    {
        [Serializable] public sealed class DocumentRecord
        {
            public string id;
            public string name;
            public string content;
            public string modifiedAt;
            public bool trashed;
        }
        [Serializable] public sealed class ArchiveLink { public string label; public string url; }
        [Serializable] public sealed class ArchivePage
        {
            public string id, url, title, source, date, category, summary, body, imageResource;
            public ArchiveLink[] links;
        }
        [Serializable] public sealed class ArchiveCollection
        {
            public int schemaVersion = 1;
            public string snapshotDate, homeTitle, homeSubtitle;
            public ArchivePage[] pages;
        }
        [Serializable] private sealed class DocumentStore
        {
            public int schemaVersion = 1;
            public List<DocumentRecord> documents = new List<DocumentRecord>();
        }

        public CabinController controller;
        public CabinSystems systems;
        public CabinPresentation presentation;
        public bool automationMode;
        [HideInInspector] public string eventAlertTitle = "", eventAlertBody = "";
        string acknowledgedAlert = "";
        public bool HasPendingEventAlert { get { return !string.IsNullOrEmpty(eventAlertTitle) && acknowledgedAlert != eventAlertTitle + eventAlertBody; } }
        public bool IsOpen { get; private set; }
        public CombatMode Combat { get; private set; }
        public string CurrentApp { get { return activeApp; } }
        public string CurrentDocumentId { get { return editorDocumentId; } }
        public string CurrentDocumentText { get { return editorBody; } }
        public string CurrentUrl { get { return browserUrl; } }
        public string LastStatus { get { return status; } }
        public string PersistencePath { get { return Path.Combine(Application.persistentDataPath, qaStorage ? "astra-computer-v1.qa.json" : "astra-computer-v1.json"); } }
        public bool IsTestStorage { get { return qaStorage; } }
        public bool HasUnsavedChanges { get { return editorDirty; } }

        const int MaxDocuments = 128;
        const int MaxContentLength = 200000;
        const float DesktopWidth = 1360f;
        const float DesktopHeight = 780f;
        const string HomeUrl = "archive://home";
        readonly Color desktopColor = new Color(0.055f, 0.20f, 0.18f);
        readonly Color chromeColor = new Color(0.69f, 0.70f, 0.65f);
        readonly Color paperColor = new Color(0.83f, 0.82f, 0.72f);
        readonly Color inkColor = new Color(0.11f, 0.16f, 0.14f);
        readonly Color accentColor = new Color(0.43f, 0.13f, 0.10f);
        readonly Color titleColor = new Color(0.075f, 0.20f, 0.23f);
        readonly List<string> runningApps = new List<string>();
        readonly List<string> history = new List<string>();
        readonly Dictionary<string, Texture2D> archiveImages = new Dictionary<string, Texture2D>();
        DocumentStore store;
        ArchiveCollection archive;
        string activeApp = "browser";
        string selectedDocumentId = "";
        string selectedArchiveId = "";
        string editorDocumentId = "";
        string editorName = "";
        string editorBody = "";
        string renameText = "";
        string browserUrl = HomeUrl;
        string address = HomeUrl;
        string status = "本地档案已装载。外部网络不可用。";
        string focusNext = "";
        string confirmationId = "";
        bool editorDirty, statusError, initialized, ownsFont, archiveFolder, qaStorage;
        float dirtyAt;
        int historyIndex = -1;
        Rect windowRect = new Rect(174f, 74f, 1130f, 602f);
        Rect restoreRect;
        bool maximized, toggleMaximize;
        Vector2 fileScroll, browserScroll, editorScroll, trashScroll;
        IMECompositionMode previousIme;
        Font font;
        GUIStyle label, small, title, heading, body, mono, button, field, area, center, lightLabel, link, row, right, masthead;

        void Awake()
        {
            if (!controller) controller = FindObjectOfType<CabinController>();
            if (!systems) systems = FindObjectOfType<CabinSystems>();
            if (!presentation && Camera.main) presentation = Camera.main.GetComponent<CabinPresentation>();
            Initialize();
            Combat = GetComponent<CombatMode>() ?? gameObject.AddComponent<CombatMode>();
        }

        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            qaStorage = Array.IndexOf(Environment.GetCommandLineArgs(), "--astra-qa") >= 0 ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "--astra-setpiece-qa") >= 0 ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "--astra-combat-qa") >= 0;
            LoadArchive();
            ReloadStore();
            runningApps.Add("browser");
            Navigate(HomeUrl);
        }

        void LoadArchive()
        {
            try
            {
                TextAsset asset = Resources.Load<TextAsset>("Computer/Archive");
                if (asset) archive = JsonUtility.FromJson<ArchiveCollection>(asset.text);
            }
            catch (Exception exception) { Debug.LogWarning("Cabin archive: " + exception.Message); }
            if (archive == null) archive = new ArchiveCollection();
            if (archive.pages == null) archive.pages = new ArchivePage[0];
            if (string.IsNullOrEmpty(archive.snapshotDate)) archive.snapshotDate = "20XX-08-12";
            if (string.IsNullOrEmpty(archive.homeTitle)) archive.homeTitle = "地球公共信息档案";
            if (string.IsNullOrEmpty(archive.homeSubtitle)) archive.homeSubtitle = "PUBLIC INFORMATION MIRROR / 船载离线镜像";
        }

        public bool Open()
        {
            Initialize();
            if (IsOpen) return true;
            if (controller && (controller.IsObservingWindow || controller.IsTurning)) return false;
            if ((systems && !systems.ScreenHasPower) || (controller && controller.IsPaused))
            {
                if (controller) controller.ShowNotice("终端无电源 / 请接通主电源");
                return false;
            }
            IsOpen = true;
            previousIme = Input.imeCompositionMode;
            Input.imeCompositionMode = activeApp == "combat" ? IMECompositionMode.Off : IMECompositionMode.On;
            if (controller) controller.SetComputerMode(true);
            if (presentation) presentation.computerBlur = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            PlayClick();
            return true;
        }

        public void SetOpen(bool value) { if (value) Open(); else Close(); }

        public void SetEventAlert(string title, string detail)
        {
            eventAlertTitle = title ?? "";
            eventAlertBody = detail ?? "";
            // A new event can repeat the previous wording before OnGUI sees an empty frame.
            acknowledgedAlert = "";
        }

        public void ClearEventAlert() { SetEventAlert("", ""); }

        public void Close()
        {
            if (!IsOpen) return;
            if (Combat) Combat.Suspend();
            SaveCurrentDocument();
            Persist();
            IsOpen = false;
            Input.imeCompositionMode = previousIme;
            if (controller) controller.SetComputerMode(false);
            if (presentation) presentation.computerBlur = false;
            GUIUtility.keyboardControl = 0;
            PlayClick();
            if (statusError && controller) controller.ShowNotice("终端存储失败：输入仍保留在本次运行内。");
        }

        void Update()
        {
            if (IsOpen && systems && !systems.ScreenHasPower) Close();
            // Do not normalize a filename or serialize a partial IME edit while a candidate is active.
            if (editorDirty && string.IsNullOrEmpty(Input.compositionString) && Time.unscaledTime > dirtyAt + 1.25f) SaveCurrentDocument();
        }

        void OnApplicationPause(bool pause) { if (pause && initialized) { SaveCurrentDocument(); Persist(); } }
        void OnApplicationQuit() { if (initialized) { SaveCurrentDocument(); Persist(); } }
        void OnDisable() { if (IsOpen) Close(); }
        void OnDestroy() { if (ownsFont && font) Destroy(font); }

        public DocumentRecord[] GetDocuments() { Initialize(); return store.documents.ToArray(); }
        public ArchivePage[] GetArchivePages() { Initialize(); return archive.pages; }
        public DocumentRecord GetDocument(string id)
        {
            Initialize();
            return store.documents.Find(document => document.id == id);
        }

        public void ReloadStore()
        {
            DocumentStore loaded = null;
            try
            {
                if (File.Exists(PersistencePath))
                {
                    FileInfo info = new FileInfo(PersistencePath);
                    if (info.Length > 32L * 1024L * 1024L) throw new IOException("存档超过32 MB上限");
                    loaded = JsonUtility.FromJson<DocumentStore>(File.ReadAllText(PersistencePath, System.Text.Encoding.UTF8));
                    if (loaded == null || loaded.documents == null || loaded.schemaVersion != 1)
                        throw new IOException("未知的档案格式");
                }
            }
            catch (Exception exception)
            {
                SetStatus("无法读取个人档案：" + exception.Message + "。原文件未改动。", true);
                // Do not replace a user's unreadable file with empty state.
                store = store ?? new DocumentStore();
                persistenceBlocked = true;
                return;
            }
            persistenceBlocked = false;
            store = loaded ?? new DocumentStore();
            store.documents.RemoveAll(document => document == null || string.IsNullOrEmpty(document.id));
            if (loaded == null)
            {
                store.documents.Add(new DocumentRecord
                {
                    id = Guid.NewGuid().ToString("N"), name = "欢迎登舱.txt", modifiedAt = Timestamp(),
                    content = "致乘员 04：\n\n这是你的船载终端。\n\nDocuments 保存你自己的文本。Archive 保存只读的地球历史快照。浏览器只访问本地镜像，不会连接真实网络。\n\n在文本编辑器中输入中文或英文；Ctrl+S 保存；停止输入后系统也会自动保存。Esc 保存并离开终端。\n\n删掉的便签会进入垃圾堆，可以恢复。\n\n如果有人能读到这段话：请记住地球。\n\n—— 舱室使用手册 / 04"
                });
                Persist();
            }
            DocumentRecord current = store.documents.Find(document => document.id == editorDocumentId && !document.trashed);
            if (current != null) LoadEditor(current);
        }

        bool persistenceBlocked;
        bool Persist()
        {
            if (store == null) return false;
            if (persistenceBlocked) { SetStatus("原存档无法读取，自动写入已禁用以保护文件。输入保留在内存。", true); return false; }
            string tempPath = PersistencePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(tempPath, JsonUtility.ToJson(store, true), new System.Text.UTF8Encoding(false));
                if (File.Exists(PersistencePath)) File.Replace(tempPath, PersistencePath, null);
                else File.Move(tempPath, PersistencePath);
                if (File.Exists(tempPath)) File.Delete(tempPath);
                return true;
            }
            catch (Exception exception) { SetStatus("磁盘写入失败：" + exception.Message, true); return false; }
        }

        static string Timestamp() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm"); }
        static string CleanName(string value)
        {
            value = (value ?? "").Trim();
            foreach (char character in new[] { '\r', '\n', '\t', '\0', '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) value = value.Replace(character, '_');
            if (value.Length > 80) value = value.Substring(0, 80);
            if (string.IsNullOrWhiteSpace(value) || value == "." || value == "..") value = "未命名.txt";
            return value;
        }

        string UniqueName(string desired, string exceptId = "")
        {
            desired = CleanName(desired);
            string result = desired;
            int suffix = 2;
            while (store.documents.Exists(document => !document.trashed && document.id != exceptId && string.Equals(document.name, result, StringComparison.OrdinalIgnoreCase)))
            { result = desired + " (" + suffix++ + ")"; }
            return result;
        }

        public string CreateDocument(string name, string content)
        {
            Initialize();
            SaveCurrentDocument();
            if (store.documents.Count >= MaxDocuments) { SetStatus("文件容量已满（128 项）。请整理垃圾堆后重试。", true); return ""; }
            DocumentRecord document = new DocumentRecord { id = Guid.NewGuid().ToString("N"), name = UniqueName(name), content = content ?? "", modifiedAt = Timestamp() };
            if (document.content.Length > MaxContentLength) document.content = document.content.Substring(0, MaxContentLength);
            store.documents.Add(document);
            selectedDocumentId = document.id;
            LoadEditor(document);
            OpenApp("editor");
            if (Persist()) SetStatus("已创建 Documents/" + document.name);
            focusNext = "EditorBody";
            return document.id;
        }

        public bool SaveCurrentDocument()
        {
            if (store == null || string.IsNullOrEmpty(editorDocumentId)) return true;
            DocumentRecord document = store.documents.Find(item => item.id == editorDocumentId && !item.trashed);
            if (document == null) return false;
            if (!editorDirty && document.name == editorName && document.content == editorBody) return true;
            document.name = UniqueName(editorName, document.id);
            document.content = editorBody ?? "";
            document.modifiedAt = Timestamp();
            editorName = document.name;
            bool success = Persist();
            if (success) { editorDirty = false; SetStatus("已保存 Documents/" + document.name + "  /  " + document.modifiedAt); }
            else dirtyAt = Time.unscaledTime;
            return success;
        }

        public bool OpenDocument(string id)
        {
            DocumentRecord document = GetDocument(id);
            if (document == null || document.trashed) return false;
            SaveCurrentDocument();
            LoadEditor(document);
            OpenApp("editor");
            focusNext = "EditorBody";
            return true;
        }

        public void UpdateEditorBuffer(string name, string content)
        {
            if (string.IsNullOrEmpty(editorDocumentId)) return;
            string newContent = content ?? "";
            if (newContent.Length > MaxContentLength) newContent = newContent.Substring(0, MaxContentLength);
            if (name == editorName && newContent == editorBody) return;
            editorName = name ?? "";
            editorBody = newContent;
            editorDirty = true;
            dirtyAt = Time.unscaledTime;
            SetStatus("编辑中  /  " + editorBody.Length + " 字符  /  停止输入后自动保存；Esc 保存并离开");
        }

        public bool RenameDocument(string id, string newName)
        {
            DocumentRecord document = GetDocument(id);
            if (document == null || document.trashed) return false;
            if (id == editorDocumentId) SaveCurrentDocument();
            document.name = UniqueName(newName, id);
            document.modifiedAt = Timestamp();
            if (id == editorDocumentId) editorName = document.name;
            renameText = document.name;
            bool success = Persist();
            if (success) SetStatus("文件已重命名为 " + document.name);
            return success;
        }

        public bool MoveToTrash(string id)
        {
            DocumentRecord document = GetDocument(id);
            if (document == null || document.trashed) return false;
            if (id == editorDocumentId) { SaveCurrentDocument(); editorDocumentId = ""; editorName = editorBody = ""; editorDirty = false; }
            document.trashed = true;
            selectedDocumentId = "";
            bool success = Persist();
            if (success) SetStatus("已移入垃圾堆：" + document.name + "。可随时恢复。");
            return success;
        }

        public bool Restore(string id)
        {
            DocumentRecord document = GetDocument(id);
            if (document == null || !document.trashed) return false;
            document.name = UniqueName(document.name, id);
            document.trashed = false;
            selectedDocumentId = document.id;
            bool success = Persist();
            if (success) SetStatus("已恢复到 Documents：" + document.name);
            return success;
        }

        public bool DeletePermanently(string id)
        {
            DocumentRecord document = GetDocument(id);
            if (document == null || !document.trashed) return false;
            store.documents.Remove(document);
            if (selectedDocumentId == id) selectedDocumentId = "";
            bool success = Persist();
            if (success) SetStatus("已永久删除：" + document.name);
            return success;
        }

        void LoadEditor(DocumentRecord document)
        {
            editorDocumentId = document.id;
            editorName = document.name;
            editorBody = document.content ?? "";
            editorDirty = false;
            editorScroll = Vector2.zero;
        }

        public void OpenApp(string app)
        {
            Initialize();
            app = (app ?? "").ToLowerInvariant();
            if (app == "filemanager" || app == "documents") app = "files";
            if (app == "text" || app == "notepad") app = "editor";
            if (app == "recyclebin") app = "trash";
            if (app != "files" && app != "editor" && app != "browser" && app != "trash" && app != "combat") return;
            if (activeApp == "combat" && app != "combat" && Combat) Combat.Suspend();
            SaveCurrentDocument();
            if (app == "editor" && string.IsNullOrEmpty(editorDocumentId))
            {
                DocumentRecord available = store.documents.Find(document => !document.trashed);
                if (available != null) LoadEditor(available);
            }
            if (!runningApps.Contains(app)) runningApps.Add(app);
            activeApp = app;
            if (IsOpen) Input.imeCompositionMode = app == "combat" ? IMECompositionMode.Off : IMECompositionMode.On;
            if (app == "combat" && !maximized) { restoreRect = windowRect; windowRect = new Rect(154, 42, 1198, 684); maximized = true; }
            confirmationId = "";
            GUIUtility.keyboardControl = 0;
            SetStatus(AppName(app) + " / 就绪");
        }

        public void Navigate(string url)
        {
            if (!initialized) Initialize();
            url = (url ?? "").Trim();
            if (string.IsNullOrEmpty(url) || url == "home" || url == "about:home" || url.TrimEnd('/') == "astra://archive") url = HomeUrl;
            if (url.Length > 512) url = url.Substring(0, 512);
            browserUrl = address = url;
            browserScroll = Vector2.zero;
            if (historyIndex >= 0 && history[historyIndex] == url) return;
            if (historyIndex < history.Count - 1) history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
            history.Add(url);
            if (history.Count > 100) history.RemoveAt(0);
            historyIndex = history.Count - 1;
        }

        public bool GoBack() { return StepHistory(-1); }
        public bool GoForward() { return StepHistory(1); }
        bool StepHistory(int direction)
        {
            int next = historyIndex + direction;
            if (next < 0 || next >= history.Count) return false;
            historyIndex = next;
            browserUrl = address = history[historyIndex];
            browserScroll = Vector2.zero;
            return true;
        }

        ArchivePage FindPage(string url)
        {
            string normalized = (url ?? "").TrimEnd('/');
            return Array.Find(archive.pages, page => page != null && string.Equals((page.url ?? "").TrimEnd('/'), normalized, StringComparison.OrdinalIgnoreCase));
        }

        Texture2D ArchiveImage(ArchivePage page)
        {
            if (page == null || string.IsNullOrEmpty(page.imageResource)) return null;
            Texture2D texture;
            if (!archiveImages.TryGetValue(page.imageResource, out texture))
            {
                texture = Resources.Load<Texture2D>(page.imageResource);
                archiveImages[page.imageResource] = texture;
            }
            return texture;
        }

        void SetStatus(string message, bool error = false) { status = message; statusError = error; }
        void PlayClick() { if (CabinAudio.Instance) CabinAudio.Instance.Play(CabinSound.Click, controller ? controller.centerPosition : transform.position); }
        static string AppName(string app)
        {
            switch (app) { case "combat": return "战斗模式"; case "files": return "文件管理"; case "editor": return "文本编辑器"; case "trash": return "垃圾堆"; default: return "离线浏览器"; }
        }

        void EnsureStyles()
        {
            if (label != null) return;
            try { font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, 18); }
            catch (Exception) { font = null; }
            ownsFont = font != null;
            if (!font) font = GUI.skin.font;
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Clip, richText = false };
            label.normal.textColor = inkColor;
            small = new GUIStyle(label) { fontSize = 14 };
            title = new GUIStyle(label) { fontSize = 18, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(0.91f, 0.93f, 0.82f);
            heading = new GUIStyle(label) { fontSize = 25, fontStyle = FontStyle.Bold, wordWrap = true };
            body = new GUIStyle(label) { fontSize = 19, wordWrap = true, alignment = TextAnchor.UpperLeft };
            mono = new GUIStyle(small) { fontStyle = FontStyle.Bold };
            button = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(5, 5, 0, 0) };
            field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 18, padding = new RectOffset(7, 7, 4, 4), alignment = TextAnchor.MiddleLeft, richText = false };
            field.normal.textColor = field.focused.textColor = field.hover.textColor = field.active.textColor = inkColor;
            field.normal.background = field.focused.background = field.hover.background = field.active.background = Texture2D.whiteTexture;
            area = new GUIStyle(field) { fontSize = 19, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(14, 14, 12, 12) };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            lightLabel = new GUIStyle(label); lightLabel.normal.textColor = new Color(0.85f, 0.90f, 0.78f);
            link = new GUIStyle(label) { fontSize = 19, wordWrap = true, fontStyle = FontStyle.Bold }; link.normal.textColor = new Color(0.10f, 0.28f, 0.31f);
            row = new GUIStyle(label) { padding = new RectOffset(9, 6, 0, 0) };
            right = new GUIStyle(small) { alignment = TextAnchor.MiddleRight };
            masthead = new GUIStyle(heading) { fontSize = 37 };
        }

        static void Fill(Rect rectangle, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rectangle, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void Bevel(Rect rectangle, bool sunken = false, Color? fill = null)
        {
            Fill(rectangle, fill ?? chromeColor);
            Color light = new Color(0.91f, 0.92f, 0.84f);
            Color dark = new Color(0.23f, 0.26f, 0.24f);
            Fill(new Rect(rectangle.x, rectangle.y, rectangle.width, 2), sunken ? dark : light);
            Fill(new Rect(rectangle.x, rectangle.y, 2, rectangle.height), sunken ? dark : light);
            Fill(new Rect(rectangle.x, rectangle.yMax - 2, rectangle.width, 2), sunken ? light : dark);
            Fill(new Rect(rectangle.xMax - 2, rectangle.y, 2, rectangle.height), sunken ? light : dark);
        }

        bool RetroButton(Rect rectangle, string text, bool enabled = true, bool selected = false)
        {
            bool oldEnabled = GUI.enabled;
            GUI.enabled = enabled && oldEnabled;
            bool hover = rectangle.Contains(Event.current.mousePosition) && GUI.enabled;
            Bevel(rectangle, selected || (hover && Input.GetMouseButton(0)), hover ? new Color(0.78f, 0.80f, 0.72f) : chromeColor);
            Color old = GUI.color;
            if (!GUI.enabled) GUI.color = new Color(0.6f, 0.63f, 0.58f);
            bool result = GUI.Button(rectangle, text, button);
            GUI.color = old;
            GUI.enabled = oldEnabled;
            if (result) PlayClick();
            return result;
        }

        string RetroField(Rect rectangle, string value, string controlName, int limit = 200)
        {
            Bevel(rectangle, true, paperColor);
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = paperColor;
            GUI.SetNextControlName(controlName);
            string result = GUI.TextField(new Rect(rectangle.x + 2, rectangle.y + 2, rectangle.width - 4, rectangle.height - 4), value ?? "", limit, field);
            GUI.backgroundColor = old;
            return result;
        }

        void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown) return;
            if (current.keyCode == KeyCode.Escape) { if (activeApp == "combat" && Combat) Combat.HandleEscape(); else Close(); current.Use(); return; }
            if ((current.control || current.command) && current.keyCode == KeyCode.S)
            { SaveCurrentDocument(); PlayClick(); current.Use(); }
            else if ((current.control || current.command) && current.keyCode == KeyCode.N && (activeApp == "editor" || activeApp == "files"))
            { CreateDocument("未命名.txt", ""); current.Use(); }
            else if ((current.control || current.command) && current.keyCode == KeyCode.L && activeApp == "browser")
            { focusNext = "BrowserAddress"; current.Use(); }
            else if ((current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) && string.IsNullOrEmpty(Input.compositionString) && activeApp == "browser" && GUI.GetNameOfFocusedControl() == "BrowserAddress")
            { Navigate(address); GUI.FocusControl(null); current.Use(); }
        }

        // Apply the camera's combat pose once, outside every desktop/window clip.
        // IMGUI then inverse-transforms mouse events through this same matrix.
        // Positive camera roll appears clockwise on a screen with Y pointing down.
        public Matrix4x4 DesktopMotionMatrix
        {
            get
            {
                if (!IsOpen || !Combat || !Combat.PhysicalFeedbackEnabled || !controller)
                    return Matrix4x4.identity;
                Vector3 euler = controller.combatEuler;
                float fov = controller.viewCamera ? controller.viewCamera.fieldOfView : 74f;
                float focal = Screen.height * .5f / Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
                Vector3 center = new Vector3(Screen.width * .5f, Screen.height * .5f, 0);
                Vector3 offset = new Vector3(-Mathf.Tan(euler.y * Mathf.Deg2Rad) * focal,
                    -Mathf.Tan(euler.x * Mathf.Deg2Rad) * focal, 0);
                float fovScale = Mathf.Tan((fov - controller.combatFov) * .5f * Mathf.Deg2Rad) / Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
                return Matrix4x4.Translate(center + offset) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, euler.z))
                    * Matrix4x4.Scale(new Vector3(fovScale, fovScale, 1)) * Matrix4x4.Translate(-center);
            }
        }

        void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();
            int oldDepth = GUI.depth;
            GUI.depth = -100;
            HandleKeyboard();
            if (!IsOpen) { GUI.depth = oldDepth; return; }
            Matrix4x4 oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            float x = (Screen.width / scale - DesktopWidth) * 0.5f;
            float y = (Screen.height / scale - DesktopHeight) * 0.5f;
            GUI.matrix = DesktopMotionMatrix * Matrix4x4.TRS(new Vector3(x * scale, y * scale, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            Fill(new Rect(-9, -9, DesktopWidth + 25, DesktopHeight + 26), new Color(0, 0, 0, 0.58f));
            Bevel(new Rect(-5, -5, DesktopWidth + 10, DesktopHeight + 10), false, new Color(0.18f, 0.21f, 0.18f));
            Fill(new Rect(0, 0, DesktopWidth, DesktopHeight), desktopColor);
            Fill(new Rect(0, 0, DesktopWidth, 34), new Color(0.025f, 0.08f, 0.075f));
            GUI.Label(new Rect(18, 0, 600, 34), "ASTRA / OS 3.1      •      CIVIL TERMINAL 04", lightLabel);
            GUI.Label(new Rect(916, 0, 424, 34), activeApp == "combat" ? "LOCAL MODE   |   [ ESC ] 战斗菜单" : "LOCAL MODE   |   [ ESC ] 保存并离开", lightLabel);
            for (int lineIndex = 37; lineIndex < 736; lineIndex += 4) Fill(new Rect(0, lineIndex, DesktopWidth, 1), new Color(0, 0, 0, 0.045f));
            DrawDesktop();
            if (!string.IsNullOrEmpty(activeApp))
            {
                Fill(new Rect(windowRect.x + 7, windowRect.y + 7, windowRect.width, windowRect.height), new Color(0, 0, 0, 0.4f));
                windowRect = GUI.Window(7404, windowRect, DrawAppWindow, "", GUIStyle.none);
                if (toggleMaximize)
                {
                    toggleMaximize = false;
                    if (maximized) { windowRect = restoreRect; maximized = false; }
                    else { restoreRect = windowRect; windowRect = new Rect(154, 42, 1198, 684); maximized = true; }
                }
                windowRect.x = Mathf.Clamp(windowRect.x, 6, DesktopWidth - windowRect.width - 6);
                windowRect.y = Mathf.Clamp(windowRect.y, 40, 734 - windowRect.height);
            }
            DrawTaskbar();
            if (HasPendingEventAlert)
            {
                GUI.ModalWindow(7405, new Rect(370, 242, 680, 222), DrawEventAlert, "", GUIStyle.none);
                GUI.BringWindowToFront(7405);
            }
            if (string.IsNullOrEmpty(eventAlertTitle)) acknowledgedAlert = "";
            if (!string.IsNullOrEmpty(focusNext)) { GUI.FocusControl(focusNext); if (Event.current.type == EventType.Repaint) focusNext = ""; }
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
        }

        void DrawEventAlert(int id)
        {
            Bevel(new Rect(0, 0, 680, 222), false, chromeColor);
            Fill(new Rect(5, 5, 670, 38), accentColor);
            GUI.Label(new Rect(18, 7, 630, 32), "舰载监控 / " + eventAlertTitle, lightLabel);
            GUI.Label(new Rect(24, 64, 630, 92), eventAlertBody, body);
            if (GUI.Button(new Rect(490, 169, 160, 34), "确认 / ACK", button))
                acknowledgedAlert = eventAlertTitle + eventAlertBody;
        }

        void DrawDesktop()
        {
            DesktopIcon(28, 64, "files", "文件管理", "FILE MANAGER");
            DesktopIcon(28, 194, "editor", "文本编辑器", "TEXT EDITOR");
            DesktopIcon(28, 324, "browser", "离线浏览器", "EARTH MIRROR");
            DesktopIcon(28, 454, "trash", "垃圾堆", "RECYCLE BIN");
            DesktopIcon(28, 584, "combat", "战斗模式", "COMBAT MODE");
            if (string.IsNullOrEmpty(activeApp))
            {
                GUIStyle watermark = new GUIStyle(masthead); watermark.normal.textColor = new Color(0.26f, 0.40f, 0.32f); watermark.fontSize = 78;
                GUI.Label(new Rect(380, 246, 820, 115), "ASTRA / OS", watermark);
                GUI.Label(new Rect(388, 363, 660, 35), "SHIPBOARD OPERATING SYSTEM    •    VERSION 3.1", lightLabel);
                GUI.Label(new Rect(388, 416, 660, 35), "乘员 04，你的个人档案已就绪。", lightLabel);
                GUI.Label(new Rect(388, 455, 660, 35), "地球镜像同步已中断  /  LAST SYNC " + archive.snapshotDate, lightLabel);
            }
            GUIStyle note = new GUIStyle(small); note.normal.textColor = new Color(0.55f, 0.68f, 0.56f);
            GUI.Label(new Rect(194, 698, 900, 25), "PIGEON PROGRAM   /   NO EXTERNAL CONNECTION   /   所有网页均为世界内的离线快照", note);
        }

        void DesktopIcon(float x, float y, string app, string caption, string subtitle)
        {
            Rect hit = new Rect(x - 5, y - 4, 126, 114);
            if (hit.Contains(Event.current.mousePosition)) Fill(hit, new Color(0.7f, 0.78f, 0.61f, 0.12f));
            float ix = x + 34, iy = y + 3;
            if (app == "files")
            {
                Fill(new Rect(ix + 3, iy + 2, 24, 10), new Color(0.79f, 0.64f, 0.31f));
                Bevel(new Rect(ix, iy + 10, 53, 38), false, new Color(0.79f, 0.67f, 0.39f));
                Fill(new Rect(ix + 5, iy + 18, 42, 2), new Color(0.93f, 0.83f, 0.57f));
            }
            else if (app == "editor")
            {
                Bevel(new Rect(ix + 6, iy, 40, 52), false, paperColor);
                for (int index = 0; index < 5; index++) Fill(new Rect(ix + 13, iy + 13 + index * 6, index == 4 ? 17 : 25, 2), new Color(0.30f, 0.36f, 0.31f));
                Fill(new Rect(ix + 37, iy + 22, 5, 26), new Color(0.67f, 0.35f, 0.16f));
            }
            else if (app == "browser")
            {
                Bevel(new Rect(ix, iy, 55, 42));
                Fill(new Rect(ix + 5, iy + 5, 45, 28), new Color(0.045f, 0.16f, 0.16f));
                for (int index = 0; index < 3; index++) Fill(new Rect(ix + 14 + index * 11, iy + 9, 2, 19), new Color(0.41f, 0.64f, 0.46f));
                for (int index = 0; index < 2; index++) Fill(new Rect(ix + 11, iy + 13 + index * 10, 34, 1), new Color(0.41f, 0.64f, 0.46f));
                Fill(new Rect(ix + 24, iy + 41, 7, 6), chromeColor); Fill(new Rect(ix + 15, iy + 47, 26, 4), chromeColor);
            }
            else if (app == "combat")
            {
                Bevel(new Rect(ix, iy, 55, 51), false, new Color(.08f, .17f, .13f));
                Fill(new Rect(ix + 25, iy + 8, 6, 35), new Color(.54f, .9f, .68f));
                Fill(new Rect(ix + 15, iy + 24, 26, 12), new Color(.54f, .9f, .68f));
                Fill(new Rect(ix + 6, iy + 4, 7, 2), paperColor); Fill(new Rect(ix + 44, iy + 44, 7, 2), paperColor);
            }
            else
            {
                Bevel(new Rect(ix + 9, iy + 9, 35, 42), false, new Color(0.58f, 0.65f, 0.58f));
                for (int index = 0; index < 3; index++) Fill(new Rect(ix + 15 + index * 10, iy + 17, 2, 25), new Color(0.28f, 0.36f, 0.30f));
                Bevel(new Rect(ix + 5, iy + 5, 43, 6)); Fill(new Rect(ix + 21, iy, 14, 5), chromeColor);
            }
            GUIStyle captionStyle = new GUIStyle(lightLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
            GUI.Label(new Rect(x - 5, y + 58, 126, 29), caption, captionStyle);
            captionStyle.fontSize = 10;
            GUI.Label(new Rect(x - 5, y + 86, 126, 17), subtitle, captionStyle);
            if (GUI.Button(hit, GUIContent.none, GUIStyle.none)) { OpenApp(app); PlayClick(); }
        }

        void DrawTaskbar()
        {
            Bevel(new Rect(0, 738, DesktopWidth, 42));
            if (RetroButton(new Rect(7, 744, 130, 29), "■  ASTRA", true, string.IsNullOrEmpty(activeApp))) { SaveCurrentDocument(); activeApp = ""; }
            float x = 148;
            foreach (string app in runningApps.ToArray())
            {
                if (RetroButton(new Rect(x, 744, 151, 29), AppName(app), true, activeApp == app))
                { if (activeApp == app) { SaveCurrentDocument(); activeApp = ""; } else OpenApp(app); }
                x += 157;
            }
            Bevel(new Rect(DesktopWidth - 248, 744, 238, 29), true);
            GUI.Label(new Rect(DesktopWidth - 238, 744, 221, 29), "LOCAL   |   " + archive.snapshotDate, small);
        }

        void DrawAppWindow(int id)
        {
            float width = windowRect.width, height = windowRect.height;
            Bevel(new Rect(0, 0, width, height));
            Fill(new Rect(4, 4, width - 8, 31), titleColor);
            string windowName = AppName(activeApp);
            if (activeApp == "editor" && !string.IsNullOrEmpty(editorDocumentId)) windowName += " — " + editorName + (editorDirty ? " *" : "");
            if (activeApp == "browser") windowName += " — EARTH MIRROR 1.4";
            GUI.Label(new Rect(13, 4, width - 133, 31), "▣  " + windowName, title);
            if (RetroButton(new Rect(width - 96, 8, 26, 23), "_")) { SaveCurrentDocument(); activeApp = ""; }
            if (RetroButton(new Rect(width - 65, 8, 26, 23), "□")) toggleMaximize = true;
            if (RetroButton(new Rect(width - 34, 8, 26, 23), "×")) { SaveCurrentDocument(); runningApps.Remove(activeApp); activeApp = ""; }
            if (string.IsNullOrEmpty(activeApp)) return;
            GUI.Label(new Rect(15, 37, 620, 24), activeApp == "combat" ? "舰载战术重建  /  PROBE 04  /  独立演练任务" : "文件(F)      编辑(E)      查看(V)      帮助(H)", small);
            GUI.Label(new Rect(width - 390, 37, 374, 24), activeApp == "combat" ? "[ P ] 暂停     [ ESC ] 战斗菜单" : "[ CTRL+S ] 保存     [ ESC ] 返回舱室", right);
            Fill(new Rect(8, 64, width - 16, 1), new Color(0.35f, 0.39f, 0.34f));
            bool wasEnabled = GUI.enabled;
            if (!string.IsNullOrEmpty(confirmationId)) GUI.enabled = false;
            if (activeApp == "combat" && Combat) Combat.Draw(new Rect(12, 70, width - 24, height - 110), font);
            else if (activeApp == "browser") DrawBrowser(width, height);
            else if (activeApp == "editor") DrawEditor(width, height);
            else DrawFiles(width, height, activeApp == "trash");
            GUI.enabled = wasEnabled;
            Bevel(new Rect(8, height - 33, width - 16, 25), true);
            GUIStyle statusStyle = new GUIStyle(small);
            if (statusError) statusStyle.normal.textColor = new Color(0.53f, 0.09f, 0.055f);
            GUI.Label(new Rect(15, height - 32, width - 33, 24), status, statusStyle);
            if (!string.IsNullOrEmpty(confirmationId)) DrawDeleteConfirmation(width, height);
            if (!maximized) GUI.DragWindow(new Rect(4, 4, width - 106, 31));
        }

        void DrawEditor(float width, float height)
        {
            if (RetroButton(new Rect(12, 74, 100, 30), "新建")) CreateDocument("未命名.txt", "");
            if (RetroButton(new Rect(120, 74, 115, 30), "保存 Ctrl+S", !string.IsNullOrEmpty(editorDocumentId))) SaveCurrentDocument();
            if (RetroButton(new Rect(243, 74, 111, 30), "文件管理")) OpenApp("files");
            GUI.Label(new Rect(width - 325, 73, 309, 32), "UTF-8   /   本地自动保存", right);
            if (string.IsNullOrEmpty(editorDocumentId))
            {
                Bevel(new Rect(12, 119, width - 24, height - 165), true, paperColor);
                GUI.Label(new Rect(35, 150, width - 70, 40), "没有打开的文本文件", heading);
                GUI.Label(new Rect(35, 206, width - 70, 70), "点击“新建”创建一份便签，或前往文件管理打开 Documents 中的文本。", body);
                return;
            }
            GUI.Label(new Rect(15, 114, 88, 31), "文件名", label);
            string newName = RetroField(new Rect(93, 112, width - 108, 34), editorName, "EditorName", 80);
            Rect viewport = new Rect(12, 156, width - 24, height - 204);
            Bevel(viewport, true, paperColor);
            float contentWidth = viewport.width - 24;
            float contentHeight = Mathf.Max(viewport.height - 8, area.CalcHeight(new GUIContent(editorBody + "\n\n"), contentWidth) + 24);
            // Only keyboard-driven caret changes request scrolling. Repaints, the wheel, and
            // scrollbar dragging must leave the reader's chosen scroll position untouched.
            bool followCaret = GUI.GetNameOfFocusedControl() == "EditorBody" &&
                (Event.current.type == EventType.KeyDown ||
                 (Event.current.type == EventType.ExecuteCommand &&
                  (Event.current.commandName == "Paste" || Event.current.commandName == "Cut" || Event.current.commandName == "SelectAll")));
            editorScroll = GUI.BeginScrollView(new Rect(viewport.x + 3, viewport.y + 3, viewport.width - 6, viewport.height - 6), editorScroll, new Rect(0, 0, contentWidth, contentHeight));
            Color old = GUI.backgroundColor; GUI.backgroundColor = paperColor;
            GUI.SetNextControlName("EditorBody");
            string newBody = GUI.TextArea(new Rect(0, 0, contentWidth, contentHeight), editorBody, MaxContentLength, area);
            GUI.backgroundColor = old;
            GUI.EndScrollView();
            UpdateEditorBuffer(newName, newBody);
            if (followCaret && GUI.GetNameOfFocusedControl() == "EditorBody" && GUIUtility.keyboardControl != 0)
            {
                TextEditor editorState = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
                if (editorState != null)
                {
                    int cursor = Mathf.Clamp(editorState.cursorIndex, 0, newBody.Length);
                    Vector2 caret = area.GetCursorPixelPosition(new Rect(0, 0, contentWidth, contentHeight), new GUIContent(newBody), cursor);
                    float lineHeight = Mathf.Max(22f, area.lineHeight);
                    float visibleHeight = viewport.height - 6;
                    float nextHeight = Mathf.Max(contentHeight, area.CalcHeight(new GUIContent(newBody + "\n\n"), contentWidth) + 24);
                    if (caret.y < editorScroll.y + 12f) editorScroll.y = caret.y - 12f;
                    else if (caret.y + lineHeight > editorScroll.y + visibleHeight - 12f)
                        editorScroll.y = caret.y + lineHeight - visibleHeight + 12f;
                    editorScroll.y = Mathf.Clamp(editorScroll.y, 0f, Mathf.Max(0f, nextHeight - visibleHeight));
                }
            }
        }

        void DrawFiles(float width, float height, bool trash)
        {
            GUI.Label(new Rect(16, 73, width - 30, 30), trash ? "C:\\  垃圾堆   /   RECYCLE BIN" : "C:\\  " + (archiveFolder ? "Archive  /  只读新闻档案" : "Documents  /  个人文本"), mono);
            if (RetroButton(new Rect(12, 111, 140, 34), "Documents", !trash, !archiveFolder && !trash)) { archiveFolder = false; selectedDocumentId = ""; }
            if (RetroButton(new Rect(12, 153, 140, 34), "Archive [R]", !trash, archiveFolder && !trash)) { archiveFolder = true; selectedArchiveId = ""; }
            if (RetroButton(new Rect(12, 211, 140, 34), trash ? "文件管理" : "垃圾堆")) OpenApp(trash ? "files" : "trash");
            GUI.Label(new Rect(17, height - 149, 129, 77), trash ? "删除的文本仍可恢复。\n\n永久删除不可撤销。" : "Archive 是只读镜像。\n\n个人文本保存在 Documents。", new GUIStyle(small) { wordWrap = true, alignment = TextAnchor.UpperLeft });
            Rect list = new Rect(166, 110, width - 180, height - 224);
            Bevel(list, true, paperColor);
            Fill(new Rect(list.x + 3, list.y + 3, list.width - 6, 30), new Color(0.61f, 0.66f, 0.59f));
            GUI.Label(new Rect(list.x + 12, list.y + 3, list.width - 240, 30), "名称 / NAME", mono);
            GUI.Label(new Rect(list.xMax - 217, list.y + 3, 205, 30), archiveFolder && !trash ? "日期 / ARCHIVE" : "修改时间 / MODIFIED", small);
            bool isArchive = archiveFolder && !trash;
            List<DocumentRecord> documents = store.documents.FindAll(document => document.trashed == trash);
            int count = isArchive ? archive.pages.Length : documents.Count;
            Vector2 scroll = trash ? trashScroll : fileScroll;
            scroll = GUI.BeginScrollView(new Rect(list.x + 3, list.y + 34, list.width - 6, list.height - 37), scroll, new Rect(0, 0, list.width - 25, Mathf.Max(list.height - 40, count * 42)));
            for (int index = 0; index < count; index++)
            {
                string itemId = isArchive ? archive.pages[index].id : documents[index].id;
                string name = isArchive ? archive.pages[index].title : documents[index].name;
                string date = isArchive ? archive.pages[index].date : documents[index].modifiedAt;
                bool selected = itemId == (isArchive ? selectedArchiveId : selectedDocumentId);
                Rect rowRect = new Rect(0, index * 42, list.width - 25, 41);
                if (selected) Fill(rowRect, new Color(0.16f, 0.33f, 0.32f));
                else if (index % 2 == 1) Fill(rowRect, new Color(0.77f, 0.79f, 0.69f));
                GUIStyle rowStyle = new GUIStyle(row); if (selected) rowStyle.normal.textColor = new Color(0.95f, 0.95f, 0.82f);
                GUI.Label(new Rect(3, rowRect.y, list.width - 260, 41), (isArchive ? "▤  " : "▧  ") + name, rowStyle);
                GUIStyle dateStyle = new GUIStyle(small); if (selected) dateStyle.normal.textColor = rowStyle.normal.textColor;
                GUI.Label(new Rect(list.width - 238, rowRect.y, 210, 41), date, dateStyle);
                int clickCount = Event.current.clickCount;
                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    if (isArchive) selectedArchiveId = itemId;
                    else { selectedDocumentId = itemId; renameText = name; }
                    PlayClick();
                    if (clickCount > 1 && !trash) OpenSelectedFile();
                }
            }
            if (count == 0) GUI.Label(new Rect(22, 20, list.width - 60, 60), trash ? "垃圾堆为空。" : "这个目录中没有文件。", body);
            GUI.EndScrollView();
            if (trash) trashScroll = scroll; else fileScroll = scroll;
            float toolbarY = height - 104;
            if (trash)
            {
                DocumentRecord chosen = GetDocument(selectedDocumentId);
                bool valid = chosen != null && chosen.trashed;
                if (RetroButton(new Rect(166, toolbarY, 146, 34), "恢复到 Documents", valid)) Restore(selectedDocumentId);
                if (RetroButton(new Rect(322, toolbarY, 142, 34), "永久删除…", valid)) confirmationId = selectedDocumentId;
                GUI.Label(new Rect(166, toolbarY + 39, width - 191, 24), count + " 项  /  此处只处理你创建的虚拟文本，不会访问电脑上的其他文件。", small);
            }
            else if (isArchive)
            {
                if (RetroButton(new Rect(166, toolbarY, 153, 34), "在浏览器中阅读", !string.IsNullOrEmpty(selectedArchiveId))) OpenSelectedFile();
                GUI.Label(new Rect(166, toolbarY + 39, width - 191, 24), count + " 份档案  /  READ ONLY · 预置新闻不能修改或删除", small);
            }
            else
            {
                DocumentRecord chosen = GetDocument(selectedDocumentId);
                bool valid = chosen != null && !chosen.trashed;
                if (RetroButton(new Rect(166, toolbarY, 96, 32), "新建文本")) CreateDocument("未命名.txt", "");
                if (RetroButton(new Rect(270, toolbarY, 78, 32), "打开", valid)) OpenSelectedFile();
                if (RetroButton(new Rect(356, toolbarY, 103, 32), "移入垃圾堆", valid)) MoveToTrash(selectedDocumentId);
                renameText = RetroField(new Rect(470, toolbarY, width - 588, 32), renameText, "RenameDocument", 80);
                if (RetroButton(new Rect(width - 108, toolbarY, 94, 32), "重命名", valid)) RenameDocument(selectedDocumentId, renameText);
                GUI.Label(new Rect(166, toolbarY + 39, width - 191, 24), count + " 个文本文件  /  Ctrl+N 新建  /  单击选择，双击打开", small);
            }
        }

        void OpenSelectedFile()
        {
            if (archiveFolder)
            {
                ArchivePage page = Array.Find(archive.pages, entry => entry.id == selectedArchiveId);
                if (page != null) { OpenApp("browser"); Navigate(page.url); }
            }
            else
            {
                DocumentRecord document = GetDocument(selectedDocumentId);
                if (document != null && !document.trashed) OpenDocument(document.id);
            }
        }

        void DrawDeleteConfirmation(float width, float height)
        {
            Fill(new Rect(4, 36, width - 8, height - 40), new Color(0.03f, 0.07f, 0.055f, 0.73f));
            Rect box = new Rect(width * 0.5f - 260, height * 0.5f - 105, 520, 210);
            Bevel(box);
            Fill(new Rect(box.x + 4, box.y + 4, box.width - 8, 32), accentColor);
            GUI.Label(new Rect(box.x + 16, box.y + 4, box.width - 30, 32), "确认永久删除 / PERMANENT DELETE", title);
            DocumentRecord document = GetDocument(confirmationId);
            GUI.Label(new Rect(box.x + 20, box.y + 55, 480, 65), "永久删除 “" + (document == null ? "" : document.name) + "”？\n此操作不可撤销。", body);
            if (RetroButton(new Rect(box.x + 179, box.y + 153, 143, 35), "取消")) confirmationId = "";
            if (RetroButton(new Rect(box.x + 336, box.y + 153, 162, 35), "确认永久删除")) { DeletePermanently(confirmationId); confirmationId = ""; }
        }

        void DrawBrowser(float width, float height)
        {
            if (RetroButton(new Rect(12, 74, 72, 31), "← 后退", historyIndex > 0)) GoBack();
            if (RetroButton(new Rect(91, 74, 72, 31), "前进 →", historyIndex < history.Count - 1)) GoForward();
            if (RetroButton(new Rect(170, 74, 68, 31), "首页")) Navigate(HomeUrl);
            GUI.Label(new Rect(252, 75, 58, 30), "地址", small);
            address = RetroField(new Rect(301, 73, width - 395, 33), address, "BrowserAddress", 512);
            if (RetroButton(new Rect(width - 87, 74, 73, 31), "转到")) { Navigate(address); GUI.FocusControl(null); }
            Rect viewport = new Rect(12, 116, width - 24, height - 161);
            Bevel(viewport, true, paperColor);
            float contentWidth = viewport.width - 25;
            ArchivePage page = FindPage(browserUrl);
            bool home = string.Equals(browserUrl.TrimEnd('/'), HomeUrl, StringComparison.OrdinalIgnoreCase);
            float contentHeight;
            if (home) contentHeight = 291 + archive.pages.Length * 122 + 65;
            else if (page != null)
                contentHeight = 302 + heading.CalcHeight(new GUIContent(page.title ?? ""), contentWidth - 66) + body.CalcHeight(new GUIContent(page.body ?? ""), contentWidth - 86) + (page.links == null ? 0 : page.links.Length * 45) + 130 + (ArchiveImage(page) ? 275 : 0);
            else contentHeight = viewport.height - 8;
            browserScroll = GUI.BeginScrollView(new Rect(viewport.x + 3, viewport.y + 3, viewport.width - 6, viewport.height - 6), browserScroll, new Rect(0, 0, contentWidth, Mathf.Max(contentHeight, viewport.height - 8)));
            Fill(new Rect(0, 0, contentWidth, Mathf.Max(contentHeight, viewport.height)), paperColor);
            if (home) DrawBrowserHome(contentWidth);
            else if (page != null) DrawArticle(contentWidth, page);
            else DrawNotFound(contentWidth);
            GUI.EndScrollView();
        }

        void DrawBrowserHome(float width)
        {
            Fill(new Rect(0, 0, width, 32), new Color(0.19f, 0.27f, 0.23f));
            GUI.Label(new Rect(24, 0, width - 46, 32), "EARTH / PUBLIC ARCHIVE     •     OFFLINE MIRROR     •     READ ONLY", lightLabel);
            GUI.Label(new Rect(25, 46, width - 50, 58), archive.homeTitle, masthead);
            GUI.Label(new Rect(28, 107, width - 56, 29), archive.homeSubtitle, mono);
            Fill(new Rect(25, 147, width - 50, 3), inkColor);
            Fill(new Rect(25, 161, 5, 91), accentColor);
            GUIStyle announcement = new GUIStyle(heading) { fontSize = 22 };
            GUI.Label(new Rect(46, 163, width - 78, 31), "最后一次同步：" + archive.snapshotDate + "   /   地球链路中断", announcement);
            GUI.Label(new Rect(46, 204, width - 81, 52), "你正在查看出发前存入终端的公开信息快照。页面不会更新；历史报道中的承诺与推测，不代表今天的事实。", new GUIStyle(body) { fontSize = 16 });
            GUI.Label(new Rect(26, 266, width - 50, 26), "档案索引  /  DISPATCHES & HISTORICAL RECORDS", mono);
            float y = 306;
            foreach (ArchivePage page in archive.pages)
            {
                if (page.id == "home") continue;
                Fill(new Rect(25, y, width - 50, 1), new Color(0.46f, 0.50f, 0.42f));
                GUI.Label(new Rect(27, y + 9, width - 60, 22), (page.date ?? "") + "    /    " + (page.source ?? "") + "    /    " + (page.category ?? ""), small);
                if (TextLink(new Rect(26, y + 35, width - 58, 37), page.title + "  →")) Navigate(page.url);
                GUI.Label(new Rect(27, y + 78, width - 66, 35), page.summary ?? "", new GUIStyle(small) { wordWrap = true, alignment = TextAnchor.UpperLeft });
                y += 122;
            }
            GUI.Label(new Rect(27, y + 18, width - 54, 28), "END OF INDEX  /  ASTRA CIVIL INFORMATION SERVICE  /  LOCAL COPY", mono);
        }

        bool TextLink(Rect rectangle, string text)
        {
            if (rectangle.Contains(Event.current.mousePosition))
            {
                Fill(rectangle, new Color(0.22f, 0.35f, 0.29f, 0.09f));
                GUI.Label(rectangle, text, link);
                Fill(new Rect(rectangle.x, rectangle.yMax - 2, Mathf.Min(rectangle.width, link.CalcSize(new GUIContent(text)).x), 1), link.normal.textColor);
            }
            else GUI.Label(rectangle, text, link);
            if (!GUI.Button(rectangle, GUIContent.none, GUIStyle.none)) return false;
            PlayClick();
            return true;
        }

        void DrawArticle(float width, ArchivePage page)
        {
            Color publicationColor = new Color(0.19f, 0.27f, 0.23f);
            if ((page.url ?? "").Contains("history.local")) publicationColor = new Color(0.29f, 0.24f, 0.17f);
            if ((page.url ?? "").Contains("science.local")) publicationColor = new Color(0.12f, 0.26f, 0.31f);
            if ((page.url ?? "").Contains("civil.local") || (page.url ?? "").Contains("pigeon.local")) publicationColor = new Color(0.36f, 0.15f, 0.12f);
            Fill(new Rect(0, 0, width, 36), publicationColor);
            GUI.Label(new Rect(22, 0, width - 44, 36), (page.source ?? "EARTH ARCHIVE").ToUpperInvariant() + "   /   LOCAL SNAPSHOT", lightLabel);
            GUI.Label(new Rect(30, 51, width - 62, 28), (page.category ?? "PUBLIC RECORD") + "    •    " + (page.date ?? ""), mono);
            float headingHeight = heading.CalcHeight(new GUIContent(page.title ?? ""), width - 66);
            GUI.Label(new Rect(31, 91, width - 66, headingHeight + 9), page.title ?? "", heading);
            float y = 110 + headingHeight;
            Fill(new Rect(31, y, width - 63, 3), accentColor);
            y += 19;
            GUI.Label(new Rect(34, y, width - 70, 54), page.summary ?? "", new GUIStyle(body) { fontSize = 17, fontStyle = FontStyle.Bold });
            y += 84;
            Texture2D picture = ArchiveImage(page);
            if (picture)
            {
                Fill(new Rect(32, y, width - 64, 235), new Color(0.11f, 0.15f, 0.12f));
                GUI.DrawTexture(new Rect(38, y + 6, width - 76, 223), picture, ScaleMode.ScaleToFit, false);
                GUI.Label(new Rect(34, y + 240, width - 70, 26), "档案插图 / ARCHIVE IMAGE · 船载保存副本", small);
                y += 275;
            }
            float textHeight = body.CalcHeight(new GUIContent(page.body ?? ""), width - 86);
            GUI.Label(new Rect(43, y, width - 86, textHeight + 8), page.body ?? "", body);
            y += textHeight + 42;
            Fill(new Rect(31, y, width - 63, 1), inkColor);
            y += 20;
            GUI.Label(new Rect(32, y, width - 64, 28), "相关档案 / RELATED RECORDS", mono);
            y += 38;
            if (page.links != null)
                foreach (ArchiveLink item in page.links)
                {
                    if (TextLink(new Rect(32, y, width - 64, 36), "→ " + item.label)) Navigate(item.url);
                    y += 45;
                }
            if (TextLink(new Rect(32, y, width - 64, 38), "← 返回档案首页")) Navigate(HomeUrl);
            GUI.Label(new Rect(32, y + 55, width - 64, 28), "只读镜像 · 本页为游戏内虚构文献，历史资料与虚构时间线请以页面标注区分。", small);
        }

        void DrawNotFound(float width)
        {
            Fill(new Rect(0, 0, width, 35), accentColor);
            GUI.Label(new Rect(25, 0, width - 50, 35), "NETWORK SERVICE / 无外部连接", title);
            GUI.Label(new Rect(35, 73, width - 70, 60), "地址不在本地档案中", heading);
            GUI.Label(new Rect(35, 155, width - 70, 100), "此终端不连接真实互联网。请输入档案内的 .local 地址，或者返回首页选择新闻。\n\n输入的地址：" + browserUrl, body);
            if (TextLink(new Rect(35, 292, width - 70, 43), "← 打开地球公共信息档案")) Navigate(HomeUrl);
        }
    }
}
