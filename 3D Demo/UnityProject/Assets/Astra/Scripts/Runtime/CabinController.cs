using UnityEngine;

namespace AstraCabin
{
    /// <summary>Fixed-centre, four-wall navigation with a free cursor and restrained mouse parallax.</summary>
    [DisallowMultipleComponent]
    public sealed class CabinController : MonoBehaviour
    {
        public Camera viewCamera;
        public Vector3 centerPosition = new Vector3(0f, 1.28f, 0f);
        [Range(45f, 100f)] public float fieldOfView = 75f;
        [Range(0.15f, 1.2f)] public float turnDuration = 0.48f;
        [Range(0f, 6f)] public float mouseLookDegrees = 3f;
        public float interactionDistance = 4.5f;
        public LayerMask interactionMask = ~0;
        public bool showUI = true;
        public bool automationMode;
        [HideInInspector] public Vector3 cinematicOffset;
        [HideInInspector] public Vector3 cinematicEuler;
        [HideInInspector] public Vector3 combatEuler, combatOffset;
        [HideInInspector] public float combatFov;
        [HideInInspector] public bool observationAllowed = true;
        [Header("Observation window / D wall")]
        public Vector3 windowObservationPosition = new Vector3(1.06f, 1.65f, 0f);
        [Range(45f, 85f)] public float windowFieldOfView = 68f;
        [Range(.3f, 2f)] public float observationDuration = .8f;
        [Range(0f, 7f)] public float windowLookDegrees = 4f;
        public string[] wallNames = { "A / 指令终端", "B / 制造与工具", "C / 生活物资", "D / 观察与交换" };

        public int CurrentWall { get; private set; }
        public float CurrentYaw { get { return baseRotation.eulerAngles.y; } }
        public Vector2 CurrentLook { get { return currentLook; } }
        public bool IsTurning { get; private set; }
        public bool IsPaused { get; private set; }
        public bool ComputerOpen { get; private set; }
        public bool IsObservingWindow { get { return observationRequested || observationBlend > 0f; } }
        public bool IsAtWindow { get { return observationRequested && observationBlend >= 1f; } }
        public bool IsObservationTransitioning { get { return observationRequested ? observationBlend < 1f : observationBlend > 0f; } }
        public float ObservationBlend { get { return observationBlend; } }
        public CabinInteractable Hovered { get { return hovered; } }

        private Quaternion baseRotation = Quaternion.identity;
        private Quaternion turnStart = Quaternion.identity;
        private Quaternion turnEnd = Quaternion.identity;
        private float turnElapsed;
        private Vector2 targetLook;
        private Vector2 currentLook;
        private CabinInteractable hovered;
        private Font uiFont;
        private GUIStyle smallStyle, titleStyle, hoverStyle, centeredStyle, buttonStyle;
        private float noticeUntil;
        private string notice = "";
        private uint noticeVersion;
        private bool ownsFont;
        private bool muted;
        private int modalChangedFrame = -1;
        private float observationBlend;
        private bool observationRequested;
        private int pendingObservationTurn;

        private void Awake()
        {
            if (!viewCamera) viewCamera = GetComponentInChildren<Camera>();
            if (!viewCamera) viewCamera = Camera.main;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SnapToWall(0);
        }

        private void Update()
        {
            if (automationMode || ComputerOpen || modalChangedFrame == Time.frameCount) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { HandleEscape(); return; }
            if (IsPaused) return;
            if (IsObservingWindow && Input.GetMouseButtonDown(1)) { ExitWindowView(); return; }
            if (Input.GetKeyDown(KeyCode.Q)) Turn(-1);
            if (Input.GetKeyDown(KeyCode.E)) Turn(1);
            if (Input.GetKeyDown(KeyCode.M)) ToggleMute();
            float x = (Input.mousePosition.x / Mathf.Max(1, Screen.width) - 0.5f) * 2f;
            float y = (Input.mousePosition.y / Mathf.Max(1, Screen.height) - 0.5f) * 2f;
            targetLook = new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f));
            // Raycast and click are resolved after LateUpdate applies the latest camera pose.
        }

        private void LateUpdate()
        {
            if (!viewCamera) return;
            if (!IsPaused && !ComputerOpen)
            {
                observationBlend = Mathf.MoveTowards(observationBlend, observationRequested ? 1f : 0f, Time.deltaTime / Mathf.Max(.01f, observationDuration));
                if (!IsObservingWindow && pendingObservationTurn != 0)
                {
                    int direction = pendingObservationTurn; pendingObservationTurn = 0; Turn(direction);
                }
                if (IsTurning)
                {
                    turnElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(turnElapsed / Mathf.Max(0.01f, turnDuration));
                    baseRotation = Quaternion.Slerp(turnStart, turnEnd, t * t * (3f - 2f * t));
                    if (t >= 1f) { baseRotation = turnEnd; IsTurning = false; }
                }
                float smooth = 1f - Mathf.Exp(-10f * Time.deltaTime);
                currentLook = Vector2.Lerp(currentLook, targetLook, smooth);
            }
            float approach = Mathf.SmoothStep(0f, 1f, observationBlend);
            float lookDegrees = Mathf.Lerp(mouseLookDegrees, windowLookDegrees, approach);
            viewCamera.transform.position = Vector3.Lerp(centerPosition, windowObservationPosition, approach);
            viewCamera.transform.rotation = baseRotation * Quaternion.Euler(-currentLook.y * lookDegrees, currentLook.x * lookDegrees, 0f);
            viewCamera.transform.position += viewCamera.transform.rotation * cinematicOffset;
            viewCamera.transform.rotation *= Quaternion.Euler(cinematicEuler);
            viewCamera.transform.position += viewCamera.transform.rotation * combatOffset;
            viewCamera.transform.rotation *= Quaternion.Euler(combatEuler);
            viewCamera.fieldOfView = Mathf.Lerp(fieldOfView, windowFieldOfView, approach) + combatFov;
            if (!automationMode && !IsPaused && !ComputerOpen && modalChangedFrame != Time.frameCount)
            {
                if (IsObservingWindow) { ChangeHovered(null); return; }
                UpdateHover();
                if (Input.GetMouseButtonDown(0)) ActivateHoveredForTest();
            }
        }

        /// <summary>+1 turns clockwise (E), -1 counter-clockwise (Q). Transitions never accumulate Euler drift.</summary>
        public void Turn(int direction)
        {
            if (direction == 0 || IsTurning || IsPaused || ComputerOpen) return;
            if (IsObservingWindow)
            {
                ExitWindowView(); pendingObservationTurn = direction > 0 ? 1 : -1; return;
            }
            CurrentWall = (CurrentWall + (direction > 0 ? 1 : 3)) % 4;
            turnStart = baseRotation;
            turnEnd = Quaternion.Euler(0f, CurrentWall * 90f, 0f);
            turnElapsed = 0f;
            IsTurning = true;
            ChangeHovered(null);
            if (CabinAudio.Instance) CabinAudio.Instance.Play(CabinSound.Turn, centerPosition);
        }

        public void SnapToWall(int wall)
        {
            observationBlend = 0f; observationRequested = false; pendingObservationTurn = 0;
            CurrentWall = ((wall % 4) + 4) % 4;
            baseRotation = Quaternion.Euler(0f, CurrentWall * 90f, 0f);
            turnStart = turnEnd = baseRotation;
            IsTurning = false;
            targetLook = currentLook = Vector2.zero;
            ChangeHovered(null);
            if (viewCamera)
            {
                viewCamera.transform.SetPositionAndRotation(centerPosition, baseRotation);
                viewCamera.fieldOfView = fieldOfView;
            }
        }

        public bool EnterWindowView()
        {
            if (!observationAllowed) { ShowNotice("舱体受损 / 修复裂痕后才能近窗观察"); return false; }
            if (CurrentWall != 1 || IsTurning || IsPaused || ComputerOpen || IsObservingWindow) return false;
            observationRequested = true; pendingObservationTurn = 0;
            modalChangedFrame = Time.frameCount;
            ChangeHovered(null);
            if (CabinAudio.Instance) CabinAudio.Instance.Play(CabinSound.Turn, centerPosition);
            return true;
        }

        public void SetWindowView(bool active) { if (active) EnterWindowView(); else ExitWindowView(); }

        public void ExitWindowView()
        {
            if (!IsObservingWindow) return;
            observationRequested = false; pendingObservationTurn = 0;
            modalChangedFrame = Time.frameCount;
            ChangeHovered(null);
        }

        public void HandleEscape()
        {
            if (ComputerOpen) return; // The desktop owns Escape while it is open.
            if (IsObservingWindow) { ExitWindowView(); return; }
            SetPaused(!IsPaused);
        }

        public void SetLookForTest(Vector2 normalized)
        {
            targetLook = new Vector2(Mathf.Clamp(normalized.x, -1f, 1f), Mathf.Clamp(normalized.y, -1f, 1f));
        }

        public void SetHoverForTest(CabinInteractable target) { ChangeHovered(target); }

        public bool ActivateHoveredForTest()
        {
            if (IsPaused || IsTurning || ComputerOpen || IsObservingWindow || !hovered) return false;
            // Opening the desktop clears hover synchronously inside the event callback.
            var item = hovered;
            uint previousNoticeVersion = noticeVersion;
            bool accepted = item.TryInteract();
            // Preserve contextual failures or instructions emitted by the interaction callback.
            if (accepted && !ComputerOpen && !IsObservingWindow && noticeVersion == previousNoticeVersion)
                ShowNotice(item.title + (item.toggleState ? (item.IsOn ? " / ON" : " / OFF") : ""));
            return accepted;
        }

        private void UpdateHover()
        {
            if (IsTurning || Input.mousePosition.x < 0f || Input.mousePosition.x > Screen.width || Input.mousePosition.y < 0f || Input.mousePosition.y > Screen.height)
            { ChangeHovered(null); return; }
            Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            // The nearest solid collider wins, including non-interactive walls and furniture.
            CabinInteractable target = Physics.Raycast(ray, out hit, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInParent<CabinInteractable>() : null;
            if (target && !target.isActiveAndEnabled) target = null;
            ChangeHovered(target);
        }

        private void ChangeHovered(CabinInteractable target)
        {
            if (hovered == target) return;
            if (hovered) hovered.SetHover(false);
            hovered = target;
            if (hovered) hovered.SetHover(true);
        }

        public void ShowNotice(string message)
        {
            unchecked { noticeVersion++; }
            notice = message;
            noticeUntil = Time.unscaledTime + 2.4f;
        }

        public void SetComputerMode(bool active)
        {
            if (active && IsObservingWindow) return;
            ComputerOpen = active;
            modalChangedFrame = Time.frameCount;
            ChangeHovered(null);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (paused) ChangeHovered(null);
        }

        public void ToggleMute()
        {
            muted = !muted;
            if (CabinAudio.Instance) CabinAudio.Instance.SetMuted(muted);
            ShowNotice(muted ? "AUDIO / 静音" : "AUDIO / 声音开启");
        }

        private void EnsureStyles()
        {
            if (smallStyle != null) return;
            try { uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 18); }
            catch (System.Exception) { uiFont = null; }
            ownsFont = uiFont != null;
            // GUI.skin.font is Unity's valid built-in fallback across 2022 LTS and Unity 6.
            // It avoids relying on version-specific names such as Arial.ttf / LegacyRuntime.ttf.
            if (!uiFont) uiFont = GUI.skin.font;
            smallStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 15, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            smallStyle.normal.textColor = new Color(0.69f, 0.72f, 0.65f);
            titleStyle = new GUIStyle(smallStyle) { fontSize = 19 };
            titleStyle.normal.textColor = new Color(0.9f, 0.89f, 0.76f);
            hoverStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            hoverStyle.normal.textColor = new Color(1f, 0.78f, 0.21f);
            centeredStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = uiFont, fontSize = 18, alignment = TextAnchor.MiddleCenter };
            buttonStyle.normal.textColor = new Color(0.95f, 0.9f, 0.71f);
        }

        private static void Panel(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void OnGUI()
        {
            if (!showUI || ComputerOpen) return;
            EnsureStyles();
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            float w = Screen.width / scale;
            float h = Screen.height / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            Color panel = new Color(0.035f, 0.043f, 0.039f, 0.87f);
            Color line = new Color(0.59f, 0.54f, 0.31f, 0.7f);
            Panel(new Rect(28f, 25f, 340f, 69f), panel);
            Panel(new Rect(28f, 25f, 3f, 69f), line);
            GUI.Label(new Rect(44f, 31f, 305f, 27f), "ASTRA  /  CABIN 04", titleStyle);
            string wall = wallNames != null && wallNames.Length > CurrentWall ? wallNames[CurrentWall] : "WALL " + CurrentWall;
            GUI.Label(new Rect(44f, 59f, 305f, 25f), IsObservingWindow ? "D / 舷窗近距观察" : wall, smallStyle);
            GUI.Label(new Rect(w - 283f, 29f, 254f, 25f), "PIGEON PROGRAM  //  20XX", smallStyle);

            Panel(new Rect(28f, h - 62f, 280f, 37f), panel);
            GUI.Label(new Rect(44f, h - 59f, 255f, 31f), IsObservingWindow ? "[ Q / E ]  退回并转向" : "[ Q ]  左转      右转  [ E ]", smallStyle);
            Panel(new Rect(w - 255f, h - 62f, 227f, 37f), panel);
            GUI.Label(new Rect(w - 241f, h - 59f, 206f, 31f), IsObservingWindow ? "[ ESC / 右键 ]  退回" : "[ M ] 声音   [ ESC ] 暂停", smallStyle);

            if (hovered && !IsTurning && !IsPaused)
            {
                Panel(new Rect(w * 0.5f - 245f, h - 92f, 490f, 66f), panel);
                Panel(new Rect(w * 0.5f - 245f, h - 92f, 490f, 2f), new Color(0.9f, 0.68f, 0.11f));
                GUI.Label(new Rect(w * 0.5f - 232f, h - 87f, 464f, 29f), hovered.title, hoverStyle);
                string hint = hovered.IsBusy ? "机械动作中…" : "鼠标左键  /  " + (hovered.toggleState ? (hovered.IsOn ? "当前开启" : "当前关闭") : (string.IsNullOrEmpty(hovered.description) ? "进入电脑" : hovered.description));
                GUI.Label(new Rect(w * 0.5f - 230f, h - 60f, 460f, 27f), hint, centeredStyle);
            }
            else if (IsObservingWindow && !IsPaused)
                GUI.Label(new Rect(w * .5f - 260f, h - 65f, 520f, 34f), IsAtWindow ? "移动鼠标观察外景  /  ESC 返回舱室" : "靠近 / 离开观察窗…", centeredStyle);
            else if (!IsPaused && Time.unscaledTime < noticeUntil)
                GUI.Label(new Rect(w * 0.5f - 260f, h - 65f, 520f, 34f), notice, centeredStyle);

            if (IsPaused)
            {
                Panel(new Rect(0f, 0f, w, h), new Color(0.01f, 0.02f, 0.02f, 0.77f));
                Rect box = new Rect(w * 0.5f - 215f, h * 0.5f - 179f, 430f, 358f);
                Panel(box, new Color(0.08f, 0.10f, 0.085f, 0.98f));
                Panel(new Rect(box.x, box.y, box.width, 3f), line);
                GUI.Label(new Rect(box.x + 20f, box.y + 19f, 390f, 38f), "SYSTEM HOLD / 已暂停", hoverStyle);
                GUI.Label(new Rect(box.x + 20f, box.y + 57f, 390f, 35f), "Q / E 切换墙面 · 鼠标查看 · 左键操作", centeredStyle);
                if (GUI.Button(new Rect(box.x + 45f, box.y + 108f, 340f, 44f), "继续 / RESUME", buttonStyle)) SetPaused(false);
                if (GUI.Button(new Rect(box.x + 45f, box.y + 166f, 340f, 44f), muted ? "开启声音 / SOUND ON" : "静音 / MUTE", buttonStyle)) ToggleMute();
                var presentation = viewCamera ? viewCamera.GetComponent<CabinPresentation>() : null;
                if (presentation && GUI.Button(new Rect(box.x + 45f, box.y + 224f, 340f, 44f), "画面 / " + presentation.LookLabel, buttonStyle)) presentation.CycleLook();
                if (GUI.Button(new Rect(box.x + 45f, box.y + 282f, 340f, 44f), "退出 / EXIT", buttonStyle))
                {
                    Time.timeScale = 1f;
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                }
            }
            GUI.matrix = previous;
        }

        private void OnDisable()
        {
            ChangeHovered(null);
            if (IsPaused) Time.timeScale = 1f;
        }

        private void OnDestroy() { if (ownsFont && uiFont) Destroy(uiFont); }
    }
}
