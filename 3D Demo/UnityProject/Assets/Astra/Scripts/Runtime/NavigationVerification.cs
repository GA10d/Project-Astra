using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AstraCabin.Navigation;

namespace AstraCabin
{
    public sealed class NavigationVerification : MonoBehaviour
    {
        string output;
        int failures, errors;
        float started;
        bool finished;
        readonly List<string> report = new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--astra-navigation-manual") >= 0) return;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--astra-navigation-qa") >= 0)
                FindObjectOfType<CabinComputer>().gameObject.AddComponent<NavigationVerification>();
        }
        void Check(string name, bool value) { report.Add((value ? "PASS " : "FAIL ") + name); if (!value) failures++; }
        void Log(string text, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; report.Add("ERROR " + text + "\n" + trace); } }
        void Update() { if (output != null && !finished && Time.realtimeSinceStartup - started > 300) { Check("watchdog", false); Finish(); } }
        void Finish()
        {
            if (finished) return; finished = true;
            report.Add("RESULT " + (failures == 0 && errors == 0 ? "PASS" : "FAIL") + " failures=" + failures + " errors=" + errors);
            File.WriteAllLines(Path.Combine(output, "navigation-verification.txt"), report);
            Application.logMessageReceived -= Log; Application.Quit(failures == 0 && errors == 0 ? 0 : 3);
        }
        static Campaign Clone(Campaign c) { return JsonUtility.FromJson<Campaign>(JsonUtility.ToJson(c)); }
        void Invalid(Campaign source, EventRegistry registry, string name, Action<Campaign> mutate)
        { var c = Clone(source); mutate(c); Check("Reject " + name, CampaignValidator.Validate(c, registry).Count > 0); }
        void ModelTests(Campaign c, EventRegistry r)
        {
            Check("Default campaign validates", CampaignValidator.Validate(c, r).Count == 0);
            Invalid(c, r, "future schema", x => x.schemaVersion = 2);
            Invalid(c, r, "duplicate resource", x => x.resources[1].id = x.resources[0].id);
            Invalid(c, r, "negative starting resource", x => x.resources[0].initial = -1);
            Invalid(c, r, "unknown handler", x => x.events[0].type = "missing");
            Invalid(c, r, "invalid setpiece", x => x.events[0].setpiece = 10);
            Invalid(c, r, "NaN duration", x => x.events[0].duration = float.NaN);
            Invalid(c, r, "null event", x => x.events[0] = null);
            Invalid(c, r, "null option", x => x.events[0].choices[0] = null);
            Invalid(c, r, "duplicate option", x => x.events[0].choices[1].id = x.events[0].choices[0].id);
            Invalid(c, r, "missing free escape", x => x.events[0].choices[1].costs = new[] { new Amount { resourceId = "fuel", amount = 1 } });
            Invalid(c, r, "unknown resource", x => x.travelCosts[0].resourceId = "missing");
            Invalid(c, r, "negative cost", x => x.travelCosts[0].amount = -1);
            Invalid(c, r, "duplicate cost", x => x.travelCosts = new[] { x.travelCosts[0], x.travelCosts[0] });
            Invalid(c, r, "dangling edge", x => x.sectors[0].nodes[0].next[0] = "missing");
            Invalid(c, r, "cycle", x => x.sectors[0].nodes[1].next = new[] { "entry" });
            Invalid(c, r, "unreachable node", x => x.sectors[0].nodes[0].next = new[] { "signal_a" });
            Invalid(c, r, "duplicate node", x => x.sectors[0].nodes[1].id = "entry");
            Invalid(c, r, "NaN position", x => x.sectors[0].nodes[1].x = float.NaN);
            Invalid(c, r, "out-of-range position", x => x.sectors[0].nodes[1].y = 2);
            Invalid(c, r, "dead end", x => x.sectors[0].nodes[1].next = new string[0]);
            Invalid(c, r, "exit with edge", x => x.sectors[0].nodes[7].next = new[] { "entry" });
            Invalid(c, r, "missing event", x => x.sectors[0].nodes[1].eventId = "missing");
            Invalid(c, r, "event on entry", x => x.sectors[0].nodes[0].eventId = "show_1");
            Invalid(c, r, "sector cycle", x => x.sectors[2].nextSectorId = "sector_1");
            Invalid(c, r, "unreachable sector", x => x.sectors[0].nextSectorId = "");
            for (int branch = 0; branch < 3; branch++)
            {
                var s = new NavigationSession(c, r, Path.Combine(output, "model-" + branch + ".json")); s.Restart();
                Check("No shortcut to exit " + branch, !s.Commit("travel", "exit"));
                Check("Cannot advance before exit " + branch, !s.Commit("advance", "sector_1"));
                for (int sector = 0; sector < 3; sector++)
                {
                    Check("Reach branch " + branch + "/" + sector, s.Commit("travel", "signal_" + (char)('a' + branch)));
                    int fuel = s.Engine.Balances["fuel"];
                    Check("Unresolved event locks route", !s.Commit("travel", "salvage") && s.Engine.Balances["fuel"] == fuel);
                    Check("Unwatched show locks reward", !s.Commit("choose", s.Engine.Pending.choices.Last().id));
                    Check("Observe once", s.Commit("observed", s.Engine.Pending.id) && !s.Commit("observed", s.Engine.Pending.id));
                    var resume = new NavigationSession(c, r, s.Path);
                    Check("Resume pending event and resources", !resume.Blocked && resume.Engine.Observed && resume.Engine.Balances["fuel"] == fuel);
                    string pick = s.Engine.Pending.choices.Last().id;
                    Check("Reward commits once", s.Commit("choose", pick) && !s.Commit("choose", pick));
                    Check("Resource choice resolves", s.Commit("travel", "salvage") && s.Commit("choose", "fuel"));
                    Check("Relay resolves", s.Commit("travel", "relay") && s.Commit("choose", "keep"));
                    Check("Sector transition", s.Commit("travel", "exit") && s.Commit("advance", s.Engine.Sector.id));
                }
                Check("Voyage completes and locks", s.Engine.Complete && !s.Commit("advance", s.Engine.Sector.id));
                Check("Completed save reloads", new NavigationSession(c, r, s.Path).Engine.Complete);
            }
            var poor = Clone(c); poor.resources[0].initial = 0;
            var engine = new NavigationEngine(poor, r); string why;
            Check("Insufficient fuel leaves position intact", !engine.Apply(new Command { kind = "travel", id = "signal_a" }, out why) && engine.Node.id == "entry");
            var rich = Clone(c); rich.resources[0].initial = rich.resources[0].capacity;
            engine = new NavigationEngine(rich, r);
            engine.Apply(new Command { kind = "travel", id = "signal_a" }, out why); engine.Apply(new Command { kind = "observed", id = "show_1" }, out why);
            engine.Apply(new Command { kind = "choose", id = "record" }, out why); engine.Apply(new Command { kind = "travel", id = "salvage" }, out why);
            engine.Apply(new Command { kind = "choose", id = "fuel" }, out why);
            Check("Reward capped at capacity", engine.Balances["fuel"] == rich.resources[0].capacity);
            var corruptPath = Path.Combine(output, "corrupt.json"); File.WriteAllText(corruptPath, "{broken");
            var broken = new NavigationSession(c, r, corruptPath);
            Check("Corrupt save protected", broken.Blocked && !broken.Commit("travel", "signal_a") && File.ReadAllText(corruptPath) == "{broken");
            Check("Explicit restart backs up corrupt save", broken.Restart() && Directory.GetFiles(output, "corrupt.json.preserved-*").Length > 0);
            var altered = Clone(c); altered.name += " changed";
            Check("Config fingerprint mismatch protected", new NavigationSession(altered, r, corruptPath).Blocked);
            var failPath = Path.Combine(output, "blocked-write.json"); Directory.CreateDirectory(failPath);
            var diskFailure = new NavigationSession(c, r, failPath);
            Check("Failed disk commit leaves state unchanged", !diskFailure.Commit("travel", "signal_a") && diskFailure.Engine.Node.id == "entry" && diskFailure.Save.commands.Count == 0);
        }
        IEnumerator GalaxyTests(NavigationMode app, CabinComputer computer)
        {
            string testSave = app.SavePath, testConfig = app.ConfigPath;
            Check("Navigation initially opens the galaxy selector", app.IsGalaxyOverview);
            Check("Exactly the requested galaxy slots exist", NavigationMode.GalaxyIds.SequenceEqual(new[] { "test", "zyl", "gzw" }));
            Check("test uses canonical campaign and original voyage paths", app.ImportPath == Path.Combine(Application.streamingAssetsPath, "Navigation", "test", "campaign.json") && testSave == Path.Combine(output, "storage", "voyage.json") && testConfig == Path.Combine(output, "storage", "campaign.json"));
            Check("test content exists and developer slots are empty", app.GalaxyHasContent("test") && !app.GalaxyHasContent("zyl") && !app.GalaxyHasContent("gzw"));
            Screen.SetResolution(1600, 900, false); yield return new WaitForSecondsRealtime(.6f);
            yield return Capture("00_Galaxies_900p");
            Screen.SetResolution(1280, 720, false); yield return new WaitForSecondsRealtime(.3f);
            yield return Capture("00_Galaxies_720p");
            Screen.SetResolution(1600, 900, false); yield return new WaitForSecondsRealtime(.3f);
            app.RequestEnterGalaxy("test");
            Check("Selecting a planet starts the same UI transition", app.IsGalaxyTransitioning);
            Check("Rapid planet clicks cannot replace an active transition", !app.RequestEnterGalaxy("zyl") && !app.RequestGalaxyOverview());
            yield return new WaitForSecondsRealtime(.15f); yield return Capture("00_Enter_Test_Transition");
            yield return new WaitForSecondsRealtime(.8f);
            Check("test transition opens the existing tower", !app.IsGalaxyTransitioning && !app.IsGalaxyOverview && app.ActiveGalaxyId == "test" && app.Session != null);
            app.Restart();
            string preservedTest = File.ReadAllText(testSave);
            var source = Clone(app.Campaign);
            app.RequestGalaxyOverview(); yield return new WaitForSecondsRealtime(.8f);
            app.RequestEnterGalaxy("gzw"); computer.OpenApp("browser"); computer.OpenApp("navigation");
            yield return new WaitForSecondsRealtime(.8f);
            Check("Reopening navigation cancels an interrupted transition", app.IsGalaxyOverview && !app.IsGalaxyTransitioning && app.ActiveGalaxyId == "test" && File.ReadAllText(testSave) == preservedTest);
            bool originalMotion = computer.Combat.reducedMotion; computer.Combat.reducedMotion = true;
            Check("Reduced motion still starts a galaxy transition", app.RequestEnterGalaxy("test") && app.IsGalaxyTransitioning);
            yield return new WaitForSecondsRealtime(.06f); yield return Capture("00_Reduced_Motion_Transition");
            yield return new WaitForSecondsRealtime(.4f);
            Check("Reduced motion finishes entering the same test tower", !app.IsGalaxyTransitioning && !app.IsGalaxyOverview && app.ActiveGalaxyId == "test" && File.ReadAllText(testSave) == preservedTest);
            app.RequestGalaxyOverview(); yield return new WaitForSecondsRealtime(.4f);
            Check("Reduced motion returns to the same selector", app.IsGalaxyOverview && !app.IsGalaxyTransitioning);
            computer.Combat.reducedMotion = originalMotion;
            var slotConfig = new Dictionary<string, string>(); var slotSave = new Dictionary<string, string>(); var slotData = new Dictionary<string, string>();
            foreach (string slot in new[] { "zyl", "gzw" })
            {
                app.RequestGalaxyOverview(); yield return new WaitForSecondsRealtime(.8f);
                Check("Returning from tower opens the selector " + slot, app.IsGalaxyOverview && !app.IsGalaxyTransitioning);
                app.RequestEnterGalaxy(slot); yield return new WaitForSecondsRealtime(.8f);
                Check("Empty slot has no fabricated levels " + slot, app.ActiveGalaxyId == slot && !app.IsGalaxyOverview && app.Session == null && app.Campaign == null);
                Check("Each slot has its own configuration and voyage paths " + slot, app.ConfigPath == Path.Combine(output, "storage", slot, "campaign.json") && app.SavePath == Path.Combine(output, "storage", slot, "voyage.json") && app.ImportPath == Path.Combine(Application.streamingAssetsPath, "Navigation", slot, "campaign.json"));
                Check("Empty slot actions do not create a voyage " + slot, !app.Travel("signal_a") && !app.Restart() && !File.Exists(app.SavePath) && !File.Exists(app.ConfigPath));
                slotConfig[slot] = app.ConfigPath; slotSave[slot] = app.SavePath;
                yield return Capture("00_Reserved_" + slot);
                Check("Reserved slot rejects invalid import with feedback " + slot, !app.ImportCampaign("{}") && app.Session == null && app.IsGalaxyReserved && app.Message.StartsWith("导入失败") && !File.Exists(app.ConfigPath) && !File.Exists(app.SavePath));
                if (slot == "gzw") yield return Capture("00_Reserved_Import_Error");
            }
            NavigationStorage.WriteAtomic(slotConfig["zyl"], "{}");
            app.RequestGalaxyOverview(); yield return new WaitForSecondsRealtime(.8f);
            app.RequestEnterGalaxy("zyl"); yield return new WaitForSecondsRealtime(.8f);
            Check("Invalid configured slot shows an error instead of reserved content", app.Session == null && !app.IsGalaxyReserved && app.Message.StartsWith("导航配置无法载入") && File.ReadAllText(slotConfig["zyl"]) == "{}" && !File.Exists(slotSave["zyl"]));
            yield return Capture("00_Invalid_Galaxy_Config");
            File.Delete(slotConfig["zyl"]);
            app.RequestGalaxyOverview(); yield return new WaitForSecondsRealtime(.8f);
            app.RequestEnterGalaxy("gzw"); yield return new WaitForSecondsRealtime(.8f);
            app.RequestGalaxyOverview(); yield return new WaitForSecondsRealtime(.8f);
            app.RequestEnterGalaxy("zyl"); yield return new WaitForSecondsRealtime(.8f);
            Check("Removing only the QA error fixture restores the reserved slot", app.Session == null && app.IsGalaxyReserved && !app.GalaxyHasContent("zyl"));
            app.RequestGalaxyOverview(); yield return new WaitForSecondsRealtime(.8f);
            app.RequestEnterGalaxy("test"); yield return new WaitForSecondsRealtime(.8f);
            Check("Returning to test restores its existing save", app.Session != null && app.SavePath == testSave && app.ConfigPath == testConfig && File.ReadAllText(testSave) == preservedTest && app.Session.Engine.Node.id == "entry");
            foreach (string slot in new[] { "zyl", "gzw" })
            {
                app.RequestGalaxyOverview(); yield return new WaitForSecondsRealtime(.8f);
                app.RequestEnterGalaxy(slot); yield return new WaitForSecondsRealtime(.8f);
                var fixture = Clone(source); fixture.id = "qa_" + slot; fixture.name = "QA " + slot;
                Check("Developer campaign imports into only its selected slot " + slot, app.ImportCampaign(JsonUtility.ToJson(fixture)) && app.GalaxyHasContent(slot) && app.Campaign.id == fixture.id);
                string node = slot == "zyl" ? "signal_a" : "signal_b";
                Check("Developer slot commits its own progress " + slot, app.Session.Commit("travel", node) && app.Session.Commit("observed", app.Session.Engine.Pending.id) && app.Session.Commit("choose", app.Session.Engine.Pending.choices.Last().id));
                slotData[slot] = File.ReadAllText(slotSave[slot]);
                Check("Developer progress leaves test save untouched " + slot, File.ReadAllText(testSave) == preservedTest);
            }
            foreach (string slot in new[] { "test", "zyl", "gzw" })
            {
                app.RequestGalaxyOverview(); yield return new WaitForSecondsRealtime(.8f);
                app.RequestEnterGalaxy(slot); yield return new WaitForSecondsRealtime(.8f);
                string node = slot == "test" ? "entry" : slot == "zyl" ? "signal_a" : "signal_b";
                Check("Switching galaxies restores independent progress " + slot, app.Session != null && app.Session.Engine.Node.id == node && File.ReadAllText(app.SavePath) == (slot == "test" ? preservedTest : slotData[slot]));
            }
            // Only remove fixtures created by this suite in its isolated storage directory.
            foreach (string slot in new[] { "zyl", "gzw" }) { File.Delete(slotConfig[slot]); File.Delete(slotSave[slot]); }
            app.RequestGalaxyOverview(); yield return new WaitForSecondsRealtime(.8f);
            app.RequestEnterGalaxy("test"); yield return new WaitForSecondsRealtime(.8f);
            Check("Removing QA fixtures restores both reserved slots", !app.GalaxyHasContent("zyl") && !app.GalaxyHasContent("gzw") && File.ReadAllText(testSave) == preservedTest);
            Check("Unknown galaxy cannot replace the active session", !app.EnterGalaxy("missing") && app.ActiveGalaxyId == "test" && app.Session != null);
        }
        IEnumerator MeetingGalaxyTests(NavigationMode app, CabinComputer computer)
        {
            app.Restart();
            Check("Existing meeting branch opens inside the terminal", app.Travel("conference") && computer.CurrentApp == "meeting" && computer.Meeting.HasSession && !app.CanSwitchGalaxy);
            if (!computer.Meeting.HasSession) yield break;
            var player = computer.Meeting.Player; var session = app.Session;
            string save = File.ReadAllText(app.SavePath); int data = app.Session.Engine.Balances["data"];
            computer.OpenApp("navigation");
            Check("Returning from meeting opens its current tower", !app.IsGalaxyOverview && app.ActiveGalaxyId == "test");
            app.RequestGalaxyOverview(); yield return new WaitForSecondsRealtime(.8f);
            app.RequestEnterGalaxy("zyl"); yield return new WaitForSecondsRealtime(.8f);
            Check("Pending meeting cannot switch galaxies or replace its save", app.ActiveGalaxyId == "test" && ReferenceEquals(app.Session, session) && computer.Meeting.Player == player && File.ReadAllText(app.SavePath) == save && !app.CanSwitchGalaxy);
            app.RequestEnterGalaxy("test"); yield return new WaitForSecondsRealtime(.8f);
            Check("Selecting test resumes the pending meeting tower", !app.IsGalaxyOverview && app.Session.Engine.Pending.id == "relay_meeting");
            Check("Meeting continues the same player after galaxy navigation", app.BeginPresentation() && computer.CurrentApp == "meeting" && computer.Meeting.Player == player);
            yield return Capture("00_Test_Meeting_Resume");
            // Follow the authored silence branch to its real End node; reward is durable.
            player.Seek(18.9f); yield return new WaitForSecondsRealtime(.3f);
            Check("Meeting retains its authored alternate branch", player.CurrentNodeId == "contact");
            player.Seek(6.9f); yield return new WaitForSecondsRealtime(.3f);
            Check("Meeting reaches the final interactive choice", player.CurrentNodeId == "archive" && player.IsChoosing);
            player.Tick(25); yield return null; yield return null;
            Check("Meeting End returns to test tower without losing galaxy selection", computer.CurrentApp == "navigation" && !app.IsGalaxyOverview && app.ActiveGalaxyId == "test" && !computer.Meeting.HasSession && app.Session.Engine.Pending == null && app.CanSwitchGalaxy);
            Check("Meeting reward remains committed exactly once", app.Session.Engine.Balances["data"] == data + 3 && app.Session.Save.commands.Last().outcome == "deferred" && !app.Session.Commit("performance", "relay_meeting", "deferred"));
            yield return Capture("00_Test_Meeting_Return");
            app.Restart();
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--astra-navigation-qa");
            output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output); started = Time.realtimeSinceStartup;
            Application.logMessageReceived += Log; Application.runInBackground = true; Application.targetFrameRate = 60;
            Screen.SetResolution(1280, 720, false);
            yield return new WaitForSecondsRealtime(1.2f);
            var computer = GetComponent<CabinComputer>(); var controller = GetComponent<CabinController>(); var app = computer.Navigation;
            controller.automationMode = true; app.automationMode = true; app.Initialize();
            Check("Navigation component and valid content", app.Session != null); if (app.Session == null) { Finish(); yield break; }
            Check("QA isolated from player documents and voyage", computer.IsTestStorage && app.SavePath.StartsWith(output));
            app.Restart(); controller.SnapToWall(0); computer.Open(); computer.OpenApp("navigation");
            Check("Navigation registered in OS", computer.IsOpen && computer.CurrentApp == "navigation");
            yield return GalaxyTests(app, computer);
            ModelTests(app.Campaign, app.Registry);
            // Recreate the player surface after a hidden/background launch before readback.
            Screen.SetResolution(1600, 900, false); yield return new WaitForSecondsRealtime(.6f);
            yield return Capture("01_Navigation_Map");
            Screen.SetResolution(1280, 720, false); yield return new WaitForSecondsRealtime(.3f); yield return Capture("02_Navigation_720p");
            Screen.SetResolution(1600, 900, false); yield return new WaitForSecondsRealtime(.3f);
            string original = File.ReadAllText(app.SavePath); var activeCampaign = app.Campaign;
            Check("Invalid import leaves active campaign and save intact", !app.ImportCampaign("{}") && ReferenceEquals(app.Campaign, activeCampaign) && File.ReadAllText(app.SavePath) == original);
            yield return MeetingGalaxyTests(app, computer);
            for (int branch = 0; branch < 3; branch++)
            {
                app.Restart();
                for (int sector = 0; sector < 3; sector++)
                {
                    computer.Open(); computer.OpenApp("navigation");
                    if (app.IsGalaxyOverview) { app.RequestEnterGalaxy("test"); yield return new WaitForSecondsRealtime(.8f); }
                    int mode = sector * 3 + branch + 1;
                    Check("Navigation triggers real show " + mode, app.Travel("signal_" + (char)('a' + branch)) && app.Director.ActiveMode == mode && !computer.IsOpen);
                    Check("No early show reward " + mode, !app.Choose(app.Session.Engine.Pending.choices.Last().id));
                    if (mode == 1)
                    {
                        var pendingSession = app.Session; string pendingSave = File.ReadAllText(app.SavePath);
                        Check("Pending physical show blocks cross-galaxy switching", !app.CanSwitchGalaxy && !app.EnterGalaxy("gzw") && ReferenceEquals(app.Session, pendingSession) && app.ActiveGalaxyId == "test" && File.ReadAllText(app.SavePath) == pendingSave);
                    }
                    controller.SnapToWall(1);
                    if (mode == 3)
                    {
                        yield return new WaitForSecondsRealtime(4.2f);
                        Check("Repair event waits for physical repairs", !app.Session.Engine.Observed && app.Director.CrisisActive);
                        Check("Repair hammer picked up", app.Director.TakeHammer());
                        for (int crack = 0; crack < 3; crack++) { app.Director.RepairCrack(crack); yield return new WaitForSecondsRealtime(1.1f); }
                    }
                    float deadline = Time.realtimeSinceStartup + app.Session.Engine.Pending.duration + 3;
                    while (!app.Session.Engine.Observed && Time.realtimeSinceStartup < deadline) yield return null;
                    Check("Show completion gate " + mode, app.Session.Engine.Observed);
                    yield return Capture("Show_" + mode);
                    controller.SnapToWall(0); computer.Open(); computer.OpenApp("navigation");
                    Check("Returning from physical show preserves the test tower " + mode, !app.IsGalaxyOverview && app.ActiveGalaxyId == "test");
                    // Existing OS alerts have their own ACK overlay; preserve their actual state.
                    if (mode == 1) yield return Capture("03_Event_Choices");
                    computer.OpenApp("combat"); Check("Pending navigation show blocks competing combat " + mode, computer.CurrentApp == "navigation");
                    Check("Settle event and restore cabin " + mode, app.Choose(app.Session.Engine.Pending.choices.Last().id) && !app.OwnsPresentation && app.Director.ActiveMode == 0 && !app.Director.CrisisActive && computer.IsOpen);
                    Check("Settling physical show keeps its tower visible " + mode, !app.IsGalaxyOverview && app.ActiveGalaxyId == "test");
                    app.Travel("salvage");
                    if (mode == 1)
                    {
                        yield return Capture("04_Resource_Choice");
                        var pendingChoice = app.Session;
                        Check("Pending text choice also blocks cross-galaxy switching", !app.CanSwitchGalaxy && !app.EnterGalaxy("zyl") && ReferenceEquals(app.Session, pendingChoice) && app.Session.Engine.Pending.id == "salvage");
                    }
                    app.Choose("fuel");
                    app.Travel("relay"); app.Choose("keep"); app.Travel("exit"); app.Advance();
                }
                Check("Integrated full voyage " + branch, app.Session.Engine.Complete);
            }
            yield return Capture("05_Voyage_Complete");
            Check("Valid import starts fresh voyage", app.ImportCampaign(JsonUtility.ToJson(app.Campaign)) && app.Session.Engine.Node.id == "entry" && app.Session.Save.commands.Count == 0);
            computer.OpenApp("combat"); Check("Combat remains accessible after settlement", computer.CurrentApp == "combat");
            app.Restart(); Finish();
        }
        IEnumerator Capture(string name)
        {
            // State changes made by a coroutine must reach a rendered frame first.
            // In particular, model replay can overrun the first frame on startup.
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
    }
}
