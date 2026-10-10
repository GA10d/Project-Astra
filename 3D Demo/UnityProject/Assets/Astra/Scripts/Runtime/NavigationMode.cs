using System;
using System.IO;
using System.Linq;
using UnityEngine;
using AstraCabin.Navigation;

namespace AstraCabin
{
    [DisallowMultipleComponent]
    public sealed partial class NavigationMode : MonoBehaviour
    {
        public NavigationSession Session { get; private set; }
        public SetpieceDirector Director { get; private set; }
        public EventRegistry Registry { get; private set; }
        public Campaign Campaign { get; private set; }
        public bool OwnsPresentation { get; private set; }
        public string Message { get; private set; }
        public string SavePath { get; private set; }
        public string ImportPath { get; private set; }
        public string ConfigPath { get; private set; }
        public string ActiveGalaxyId { get; private set; } = "test";
        public bool IsGalaxyOverview { get; private set; } = true;
        public bool IsGalaxyReserved { get; private set; }
        public bool CanSwitchGalaxy { get { return Session == null || Session.Engine.Pending == null; } }
        public static readonly string[] GalaxyIds = { "test", "zyl", "gzw" };
        public bool PresentationPending { get { return Session != null && Session.Engine.Pending != null && Registry.Find(Session.Engine.Pending.type).NeedsObservation; } }
        CabinComputer computer;
        CabinController controller;
        string selectedId, confirmation;
        bool focused = true;
        public bool automationMode;
        bool loaded;
        string storageRoot;
        void Start() { Initialize(); }
        public void Initialize()
        {
            if (loaded) return; loaded = true;
            computer = GetComponent<CabinComputer>(); controller = GetComponent<CabinController>(); Director = GetComponent<SetpieceDirector>();
            Registry = EventRegistry.CreateDefault();
            string folder = Path.Combine(Application.persistentDataPath, computer.IsTestStorage ? "NavigationQA" : "Navigation");
            string[] args = Environment.GetCommandLineArgs(); int qa = Array.IndexOf(args, "--astra-navigation-qa");
            int meetingQA = Array.IndexOf(args, "--astra-meeting-qa");
            if(meetingQA>=0)qa=meetingQA;
            if (qa >= 0 && qa + 1 < args.Length) folder = Path.Combine(Path.GetFullPath(args[qa + 1]), "storage");
            if(Array.IndexOf(args,"--astra-meeting-demo")>=0)folder=Path.Combine(Application.persistentDataPath,"NavigationMeetingDemo");
            storageRoot = folder;
            LoadGalaxy("test");
        }
        string GalaxyStorageFolder(string id) { return id == "test" ? storageRoot : Path.Combine(storageRoot, id); }
        public string GalaxyImportPath(string id)
        {
            if (!GalaxyIds.Contains(id)) throw new ArgumentException("未知星系");
            return Path.Combine(Application.streamingAssetsPath, "Navigation", id, "campaign.json");
        }
        public bool GalaxyHasContent(string id)
        {
            Initialize();
            if (!GalaxyIds.Contains(id)) return false;
            return File.Exists(Path.Combine(GalaxyStorageFolder(id), "campaign.json")) || File.Exists(GalaxyImportPath(id)) ||
                (id == "test" && File.Exists(Path.Combine(Application.streamingAssetsPath, "Navigation", "campaign.json")));
        }
        void LoadGalaxy(string id)
        {
            ActiveGalaxyId = id;
            string folder = GalaxyStorageFolder(id);
            // test keeps the existing files so every previous voyage remains resumable.
            SavePath = Path.Combine(folder, "voyage.json"); ConfigPath = Path.Combine(folder, "campaign.json");
            ImportPath = GalaxyImportPath(id);
            string source = File.Exists(ConfigPath) ? ConfigPath : ImportPath;
            if (!File.Exists(source) && id == "test") source = Path.Combine(Application.streamingAssetsPath, "Navigation", "campaign.json");
            Session = null; Campaign = null; IsGalaxyReserved = !File.Exists(source);
            selectedId = null; confirmation = null; showJournal = false;
            detailScroll = journalScroll = Vector2.zero;
            if (IsGalaxyReserved) { Message = "此星系的航路尚未开放。"; return; }
            try
            {
                Campaign = Parse(NavigationStorage.ReadLimited(source));
                Session = new NavigationSession(Campaign, Registry, SavePath);
                selectedId = Session.Engine.Node.id;
                Message = Session.Error ?? "选择亮起的相邻信标，规划下一次航行。";
            }
            catch (Exception e) { Message = "导航配置无法载入：" + e.Message + "。请修正导入文件后重新载入。"; }
        }
        public bool EnterGalaxy(string id)
        {
            Initialize();
            if (!GalaxyIds.Contains(id)) { Message = "未知星系。"; return false; }
            if (id != ActiveGalaxyId && !CanSwitchGalaxy) { Message = "请先完成当前星系的事件，再切换星系。"; return false; }
            if (id != ActiveGalaxyId || Session == null) LoadGalaxy(id);
            IsGalaxyOverview = false; confirmation = null; showJournal = false;
            return true;
        }
        public bool ReturnToGalaxies()
        {
            Initialize(); IsGalaxyOverview = true; confirmation = null; showJournal = false; return true;
        }
        public void OnAppOpened()
        {
            Initialize();
            galaxyTransition = false;
            // Returning from a physical event must keep its settlement controls in view.
            if (!PresentationPending) ReturnToGalaxies();
            else IsGalaxyOverview = false;
        }
        public Campaign Parse(string json)
        {
            if (json == null || System.Text.Encoding.UTF8.GetByteCount(json) > CampaignValidator.MaxBytes) throw new InvalidDataException("配置超过 2 MB");
            var c = JsonUtility.FromJson<Campaign>(json);
            var errors = CampaignValidator.Validate(c, Registry);
            if (errors.Count > 0) throw new InvalidDataException(string.Join("；", errors.Take(5)));
            return c;
        }
        // The importer validates before touching either the active campaign or voyage.
        // The UI's explicit confirmation states that importing starts a fresh voyage.
        public bool ImportCampaign(string json)
        {
            try
            {
                var next = Parse(json);
                NavigationStorage.Archive(ConfigPath); NavigationStorage.Archive(SavePath);
                string previousConfig = File.Exists(ConfigPath) ? NavigationStorage.ReadLimited(ConfigPath) : null;
                NavigationStorage.WriteAtomic(ConfigPath, JsonUtility.ToJson(next, true));
                try
                {
                    NavigationStorage.WriteAtomic(SavePath, JsonUtility.ToJson(new VoyageSave { fingerprint = CampaignValidator.Fingerprint(next) }, true));
                }
                catch
                {
                    // Recover the old config if the second file cannot be committed. A crash
                    // between replacements is also caught by the save fingerprint on startup.
                    if (previousConfig != null) NavigationStorage.WriteAtomic(ConfigPath, previousConfig);
                    else if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
                    throw;
                }
                Campaign = next; Session = new NavigationSession(Campaign, Registry, SavePath); IsGalaxyReserved = false;
                StopPresentation(); selectedId = Session.Engine.Node.id;
                Message = "已导入「" + Campaign.name + "」，旧航程已备份。"; return true;
            }
            catch (Exception e) { Message = "导入失败：" + e.Message; return false; }
        }
        public bool Travel(string id)
        {
            if (Session == null || !Session.Commit("travel", id)) { Message = Session == null ? "尚无有效配置" : Session.Error; return false; }
            selectedId = id; detailScroll = Vector2.zero; Message = "已抵达 · " + Session.Engine.Node.name;
            if (PresentationPending) BeginPresentation();
            return true;
        }
        public bool BeginPresentation()
        {
            if (!PresentationPending || Session.Blocked) return false;
            try
            {
                var e = Session.Engine.Pending;
                if(e.type=="performance")
                {
                    Registry.Find(e.type).Begin(this,e);OwnsPresentation=true;
                    computer.Open();computer.OpenApp("meeting");return true;
                }
                if (OwnsPresentation && Director.ActiveMode == e.setpiece) { computer.Close(); return true; }
                OwnsPresentation = true;
                Registry.Find(e.type).Begin(this, e);
                computer.Close();
                controller.ShowNotice(e.setpiece == 3 ? "导航事件：前往 B 墙取维修锤，修复 D 墙裂痕" : "导航事件：Q / E 转向 D 墙观察，结束后返回导航模式");
                return true;
            }
            catch (Exception e) { OwnsPresentation = false; Message = "演出未启动，可重试：" + e.Message; return false; }
        }
        void OnApplicationFocus(bool value) { focused = value; }
        void Update()
        {
            TickGalaxyTransition();
            if (!loaded || !OwnsPresentation || !PresentationPending || Session.Engine.Observed || controller.IsPaused || (!focused && !automationMode)) return;
            var e = Session.Engine.Pending;
            if(e.type=="performance")return; // Meeting commits its end and reward in one durable command.
            if (Registry.Find(e.type).IsComplete(this, e))
            {
                if (Session.Commit("observed", e.id)) { Message = "演出已完成，返回导航模式选择处置方案。"; controller.ShowNotice(Message); }
                else { Message = Session.Error; OwnsPresentation = false; }
            }
        }
        public bool CompleteMeeting(string eventId,string endNode)
        {
            var meeting=computer.Meeting;
            if(!meeting || !meeting.Player || !meeting.Player.IsFinished || meeting.EventId!=eventId || meeting.Player.CurrentNodeId!=endNode)
            {Message="会议尚未到达有效结束节点。";return false;}
            if(Session==null || !Session.Commit("performance",eventId,endNode))
            {Message=Session==null?"尚无有效配置":Session.Error;return false;}
            OwnsPresentation=false;computer.Meeting.Stop();computer.FinishMeeting();
            detailScroll=Vector2.zero;Message="会议已结束，事件完成。选择下一个信标继续航行。";return true;
        }
        public bool Choose(string id)
        {
            if (Session == null || !Session.Commit("choose", id)) { Message = Session == null ? "尚无有效配置" : Session.Error; return false; }
            StopPresentation(); detailScroll = Vector2.zero; Message = "处置已记录，资源已结算。选择下一个信标继续航行。"; return true;
        }
        public bool Advance()
        {
            if (Session == null || !Session.Commit("advance", Session.Engine.Sector.id)) { Message = Session == null ? "尚无有效配置" : Session.Error; return false; }
            selectedId = Session.Engine.Node.id; Message = Session.Engine.Complete ? "本次航程已完成。" : "已进入「" + Session.Engine.Sector.name + "」。"; return true;
        }
        public bool Restart()
        {
            if (Session == null || !Session.Restart()) { Message = Session == null ? "请先导入有效配置" : Session.Error; return false; }
            StopPresentation(); selectedId = Session.Engine.Node.id; Message = "已开始新的航程，旧存档已备份。"; return true;
        }
        void StopPresentation()
        {
            if (!OwnsPresentation) return;
            OwnsPresentation = false; bool reopen = computer.IsOpen;
            if(computer.Meeting && computer.Meeting.HasSession){computer.Meeting.Stop();computer.FinishMeeting();return;}
            Director.ResetToDefault();
            if (reopen) { computer.Open(); computer.OpenApp("navigation"); }
        }
    }
}
