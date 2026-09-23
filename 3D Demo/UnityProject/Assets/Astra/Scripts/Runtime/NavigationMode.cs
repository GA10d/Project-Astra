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
        public bool PresentationPending { get { return Session != null && Session.Engine.Pending != null && Registry.Find(Session.Engine.Pending.type).NeedsObservation; } }
        CabinComputer computer;
        CabinController controller;
        string selectedId, confirmation;
        bool focused = true;
        public bool automationMode;
        bool loaded;
        void Start() { Initialize(); }
        public void Initialize()
        {
            if (loaded) return; loaded = true;
            computer = GetComponent<CabinComputer>(); controller = GetComponent<CabinController>(); Director = GetComponent<SetpieceDirector>();
            Registry = EventRegistry.CreateDefault();
            string folder = Path.Combine(Application.persistentDataPath, computer.IsTestStorage ? "NavigationQA" : "Navigation");
            string[] args = Environment.GetCommandLineArgs(); int qa = Array.IndexOf(args, "--astra-navigation-qa");
            if (qa >= 0 && qa + 1 < args.Length) folder = Path.Combine(Path.GetFullPath(args[qa + 1]), "storage");
            SavePath = Path.Combine(folder, "voyage.json"); ConfigPath = Path.Combine(folder, "campaign.json");
            ImportPath = Path.Combine(Application.streamingAssetsPath, "Navigation", "campaign.json");
            try
            {
                Campaign = Parse(NavigationStorage.ReadLimited(File.Exists(ConfigPath) ? ConfigPath : ImportPath));
                Session = new NavigationSession(Campaign, Registry, SavePath);
                selectedId = Session.Engine.Node.id;
                Message = Session.Error ?? "选择亮起的相邻信标，规划下一次航行。";
            }
            catch (Exception e) { Message = "导航配置无法载入：" + e.Message + "。请修正导入文件后重新载入。"; }
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
                Campaign = next; Session = new NavigationSession(Campaign, Registry, SavePath);
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
            if (!loaded || !OwnsPresentation || !PresentationPending || Session.Engine.Observed || controller.IsPaused || (!focused && !automationMode)) return;
            var e = Session.Engine.Pending;
            if (Registry.Find(e.type).IsComplete(this, e))
            {
                if (Session.Commit("observed", e.id)) { Message = "演出已完成，返回导航模式选择处置方案。"; controller.ShowNotice(Message); }
                else { Message = Session.Error; OwnsPresentation = false; }
            }
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
            Director.ResetToDefault();
            if (reopen) { computer.Open(); computer.OpenApp("navigation"); }
        }
    }
}
