using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AstraCabin
{
    // Opt-in standalone acceptance runner. It uses the same simulation, UI and audio as players.
    public sealed class CombatVerification : MonoBehaviour
    {
        string output;
        int failures, errors;
        float started;
        bool finished;
        readonly List<string> report = new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--astra-combat-qa") < 0) return;
            var computer = FindObjectOfType<CabinComputer>();
            if (computer) computer.gameObject.AddComponent<CombatVerification>();
        }
        void Check(string name, bool condition, string detail = "")
        { report.Add((condition ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? " | " + detail : "")); if (!condition) failures++; }
        void Log(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; report.Add("ERROR " + text + "\n" + stack); } }
        void Update()
        { if (output != null && !finished && Time.realtimeSinceStartup - started > 100) { failures++; report.Add("FAIL watchdog"); Finish(); } }
        void Finish()
        {
            if (finished) return; finished = true;
            report.Add("RESULT " + (failures == 0 && errors == 0 ? "PASS" : "FAIL") + " failures=" + failures + " errors=" + errors);
            File.WriteAllLines(Path.Combine(output, "combat-verification.txt"), report);
            Application.logMessageReceived -= Log; Application.Quit(failures == 0 && errors == 0 ? 0 : 3);
        }
        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--astra-combat-qa");
            if (index < 0 || index + 1 >= args.Length) yield break;
            output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output); started = Time.realtimeSinceStartup;
            Application.logMessageReceived += Log; Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Screen.SetResolution(1600, 900, false);
            var computer = GetComponent<CabinComputer>(); var controller = GetComponent<CabinController>(); var systems = GetComponent<CabinSystems>();
            var app = computer.Combat; app.automationMode = true; controller.automationMode = true;
            yield return new WaitForSecondsRealtime(1.5f);
            Check("QA uses isolated document storage", computer.IsTestStorage);
            TestSimulation();
            controller.SnapToWall(0); Check("OS opens", computer.Open()); computer.OpenApp("combat");
            Check("Combat registered with desktop", computer.CurrentApp == "combat" && app.IsVisible);
            yield return Capture("01_Combat_Menu");
            app.StartSortie(false);
            var sound = GetComponent<CombatSound>();
            Check("Supplied BGM imported", sound.MusicLoaded);
            yield return new WaitForSecondsRealtime(.12f); float earlyGain = sound.MusicGain;
            Check("Music starts quietly", sound.MusicPlaying && earlyGain < .015f, earlyGain.ToString("F4"));
            yield return new WaitForSecondsRealtime(1.1f); float midGain = sound.MusicGain;
            yield return new WaitForSecondsRealtime(1.6f);
            Check("Music fades in gradually", midGain > earlyGain && sound.MusicGain > midGain && sound.MusicGain > .15f, earlyGain + " < " + midGain + " < " + sound.MusicGain);
            app.sim.velocity = Vector2.right * CombatSimulation.ThrustSpeed;
            yield return new WaitForSecondsRealtime(.12f);
            Check("Acceleration FOV eases in instead of snapping", controller.combatFov > 0 && controller.combatFov < 3);
            yield return new WaitForSecondsRealtime(1.2f);
            Check("Thrust increases actual camera FOV by about four degrees", controller.combatFov > 3.5f && controller.combatFov <= 4.01f && Mathf.Abs(controller.viewCamera.fieldOfView - controller.fieldOfView - controller.combatFov) < .01f);
            Check("Desktop follows acceleration FOV", computer.DesktopMotionMatrix.MultiplyVector(Vector3.right).magnitude < .96f);
            yield return Capture("10_Thrust_FOV");
            app.sim.velocity = Vector2.right * CombatSimulation.BoostSpeed;
            yield return new WaitForSecondsRealtime(1.3f);
            Check("Boost FOV is stronger and capped", controller.combatFov > 6.5f && controller.combatFov <= 7.01f);
            yield return Capture("11_Boost_FOV");
            app.sim.velocity = Vector2.right * CombatSimulation.CruiseSpeed;
            yield return new WaitForSecondsRealtime(.12f);
            Check("Deceleration FOV returns smoothly", controller.combatFov > 2 && controller.combatFov < 6.5f);
            yield return new WaitForSecondsRealtime(1.8f);
            Check("Cruise restores normal FOV", controller.combatFov < .05f);
            int wall = controller.CurrentWall; controller.Turn(1);
            Check("Combat isolates cabin turn input", controller.CurrentWall == wall && !controller.IsTurning);
            for (int i = 0; i < 100; i++) app.sim.Step(1f / 120, new CombatSimulation.Controls { turn = 1, fire = true });
            yield return new WaitForSecondsRealtime(.15f);
            Check("Turning drives cabin roll", Mathf.Abs(app.CabinRoll) > .1f, app.CabinRoll.ToString());
            CheckDesktopMotion(computer, controller, "Turning");
            yield return Capture("09_Desktop_Turning");
            app.sim.DamagePlayer(20); yield return new WaitForSecondsRealtime(.08f);
            Check("Damage drives real cabin lighting without damage lock", app.LightPulse > 0 && !systems.DamageLocked);
            CheckDesktopMotion(computer, controller, "Damage");
            yield return Capture("02_Combat_Active");
            app.Suspend(); Vector2 pausedPosition = app.sim.position; float pausedTime = app.sim.elapsed;
            for (int i = 0; i < 120; i++) app.sim.Step(1f / 120, new CombatSimulation.Controls { fire = true, thrust = true });
            Check("Pause freezes simulation", app.sim.position == pausedPosition && app.sim.elapsed == pausedTime);
            yield return new WaitForSecondsRealtime(.9f);
            Check("Pause fades music to silence", sound.MusicGain < .001f && !sound.MusicPlaying);
            Check("Pause restores cabin pose and light controls", controller.combatEuler == Vector3.zero && app.LightPulse == 0 && systems.MainPowerOn && systems.LightsOn);
            Check("Pause restores desktop transform", computer.DesktopMotionMatrix == Matrix4x4.identity);
            Check("Pause restores FOV", controller.combatFov == 0 && Mathf.Abs(controller.viewCamera.fieldOfView - controller.fieldOfView) < .01f);
            yield return Capture("03_Combat_Paused");
            app.sim.Resume(); yield return new WaitForSecondsRealtime(.2f);
            Check("Resume fades BGM back in", sound.MusicPlaying && sound.MusicGain > 0 && sound.MusicGain < .06f);
            computer.OpenApp("editor"); yield return null;
            Check("Switching app pauses combat", app.sim.phase == CombatSimulation.Phase.Paused && !app.IsLive);
            Check("Other apps retain stable desktop", computer.DesktopMotionMatrix == Matrix4x4.identity);
            computer.OpenApp("combat"); app.sim.Resume(); app.reducedMotion = true; app.sim.angularVelocity = 245; app.sim.velocity = Vector2.right * CombatSimulation.BoostSpeed; app.sim.DamagePlayer(20);
            yield return new WaitForSecondsRealtime(.15f);
            Check("Reduced motion suppresses cabin movement and flashes", controller.combatEuler == Vector3.zero);
            Check("Reduced motion suppresses desktop movement", computer.DesktopMotionMatrix == Matrix4x4.identity);
            Check("Reduced motion suppresses FOV", controller.combatFov == 0);
            app.reducedMotion = false;
            // Progress using ordinary aiming/fire inputs; no damage/HP overrides.
            app.StartSortie(false); RunAutopilot(app.sim, 1);
            Check("Real weapons can clear sector one", app.sim.phase == CombatSimulation.Phase.Upgrade, Summary(app.sim));
            yield return Capture("04_Combat_Upgrade");
            if (app.sim.phase == CombatSimulation.Phase.Upgrade) app.sim.Upgrade(0);
            RunAutopilot(app.sim, 2);
            Check("Real weapons can clear heavy escort", app.sim.phase == CombatSimulation.Phase.Upgrade, Summary(app.sim));
            if (app.sim.phase == CombatSimulation.Phase.Upgrade) app.sim.Upgrade(0);
            yield return new WaitForSecondsRealtime(.2f);
            yield return Capture("05_Combat_Boss");
            RunAutopilot(app.sim, 3);
            Check("Mission victory through ordinary flight and weapons", app.sim.phase == CombatSimulation.Phase.Won, Summary(app.sim));
            yield return Capture("06_Combat_Result");
            app.StartSortie(false); app.sim.DamagePlayer(1000);
            Check("Hull depletion gives defeat", app.sim.phase == CombatSimulation.Phase.Lost && app.sim.hull == 0);
            yield return Capture("07_Combat_Defeat");
            app.StartSortie(true); Check("Retry resets resources and score", app.sim.hull == 100 && app.sim.score == 0 && app.sim.missiles == 4);
            for (int i = 0; i < 100; i++) app.sim.Step(1f / 120, new CombatSimulation.Controls { turn = 1, fire = true, thrust = true });
            app.sim.DamagePlayer(20); app.sim.velocity = Vector2.right * CombatSimulation.BoostSpeed; app.sim.boostTime = .3f;
            yield return new WaitForSecondsRealtime(.2f);
            Check("Practice retains flying and weapons", app.sim.elapsed > .5f && app.sim.shots > 0 && app.sim.angle != 0);
            Check("Practice disables all physical feedback", !app.PhysicalFeedbackEnabled && controller.combatEuler == Vector3.zero && controller.combatOffset == Vector3.zero && controller.combatFov == 0 && app.LightPulse == 0 && app.CabinRoll == 0);
            Check("Practice keeps desktop and zoom stable", computer.DesktopMotionMatrix == Matrix4x4.identity && Mathf.Abs(app.FieldZoom - .83f) < .001f && Mathf.Abs(controller.viewCamera.fieldOfView - controller.fieldOfView) < .01f);
            Check("Practice leaves cabin lighting on", systems.MainPowerOn && systems.LightsOn && !systems.DamageLocked);
            yield return Capture("12_Practice_No_Physical_Feedback");
            app.StartSortie(false);
            Screen.SetResolution(1280, 720, false); yield return new WaitForSecondsRealtime(.7f);
            app.sim.angularVelocity = -245;
            yield return new WaitForSecondsRealtime(.4f);
            CheckDesktopMotion(computer, controller, "1280x720 opposite turn");
            yield return Capture("08_Combat_1280x720");
            computer.Close(); yield return new WaitForSecondsRealtime(.9f);
            Check("Exit restores OS and music state", !computer.IsOpen && !controller.ComputerOpen && !sound.MusicPlaying && controller.combatEuler == Vector3.zero);
            Check("Exit clears desktop transform", computer.DesktopMotionMatrix == Matrix4x4.identity);
            Check("Exit clears FOV", controller.combatFov == 0);
            Finish();
        }
        void CheckDesktopMotion(CabinComputer computer, CabinController controller, string context)
        {
            Matrix4x4 motion = computer.DesktopMotionMatrix;
            Vector3 right = motion.MultiplyVector(Vector3.right);
            float screenRoll = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;
            Check(context + " desktop shares cabin roll", Mathf.Abs(screenRoll) > .1f && Mathf.Abs(Mathf.DeltaAngle(screenRoll, controller.combatEuler.z)) < .001f);
            Vector3 point = new Vector3(Screen.width * .78f, Screen.height * .32f, 0);
            Vector3 restored = motion.inverse.MultiplyPoint3x4(motion.MultiplyPoint3x4(point));
            Check(context + " pointer transform round-trips", Vector3.Distance(restored, point) < .01f);
            Vector3 center = new Vector3(Screen.width * .5f, Screen.height * .5f, 0);
            float focal = Screen.height * .5f / Mathf.Tan(controller.viewCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            float expectedY = -Mathf.Tan(controller.combatEuler.x * Mathf.Deg2Rad) * focal;
            Check(context + " desktop shares camera recoil", Mathf.Abs(motion.MultiplyPoint3x4(center).y - center.y - expectedY) < .001f);
        }
        static string Summary(CombatSimulation s) { return "phase=" + s.phase + " sector=" + s.sector + " kills=" + s.kills + " hull=" + s.hull + " shield=" + s.shield + " time=" + s.elapsed; }
        static void RunAutopilot(CombatSimulation s, int sector)
        {
            for (int i = 0; i < 120 * 180 && s.phase == CombatSimulation.Phase.Running && s.sector == sector; i++)
            {
                CombatSimulation.Enemy closest = null; float best = float.MaxValue;
                foreach (var e in s.enemies) { float d = (e.position - s.position).sqrMagnitude; if (d < best) { best = d; closest = e; } }
                Vector2 aim = closest == null ? Vector2.zero : closest.position + closest.velocity * Mathf.Sqrt(best) / 1080;
                float heading = CombatSimulation.Bearing(aim - s.position);
                float error = Mathf.Abs(Mathf.DeltaAngle(s.angle, heading));
                var input = new CombatSimulation.Controls { turn = error < 2 ? 0 : Mathf.Sign(Mathf.DeltaAngle(s.angle, heading)), brake = error > 25 || best < 220 * 220, fire = error < 9, missile = s.lockProgress >= 1, boost = s.shield < 12 && s.boostCooldown <= 0 && i % 30 == 0 };
                s.Step(1f / 120, input);
                // In the live player these cues are drained each rendered frame.
                if (i % 8 == 0) s.cues.Clear();
            }
        }
        void TestSimulation()
        {
            var s = new CombatSimulation(); s.Start(true);
            foreach (float targetAngle in new[] { -35f, 35f, -45f, 45f, 180f })
            {
                s.Start(true); s.enemies.Clear();
                var target = new CombatSimulation.Enemy { kind = 3, position = CombatSimulation.Direction(targetAngle) * 400, hp = 200, maxHp = 200 };
                s.enemies.Add(target); s.Step(1f / 120, new CombatSimulation.Controls());
                Check("Lock cone at " + targetAngle + " degrees", (s.lockedTarget == target) == (Mathf.Abs(targetAngle) < 40));
            }
            s.Start(true);
            s.Step(1f / 120, new CombatSimulation.Controls { turn = 1 });
            Check("Steering responds on the first tick", s.angle > 0);
            Check("Velocity retains controlled inertia", Mathf.Abs(CombatSimulation.Bearing(s.velocity) - s.angle) > .5f);
            for (int i = 0; i < 120; i++) s.Step(1f / 120, new CombatSimulation.Controls { brake = true });
            Check("Brake converges to low-speed flight", s.velocity.magnitude < 105);
            float oldAngle = s.angle; s.Step(1f / 120, new CombatSimulation.Controls { turn = -1 });
            Check("D turns the nose clockwise", s.angle < oldAngle);
            float energy = s.energy; s.Step(1f / 120, new CombatSimulation.Controls { boost = true });
            Check("Boost consumes energy and grants a brief dodge", s.energy < energy - 30 && s.invulnerable > 0 && s.boostTime > 0);
            float hp = s.hull + s.shield; s.DamagePlayer(100);
            Check("Dodge ignores damage", s.hull + s.shield == hp);
            s.Start(true); bool overheated = false;
            for (int i = 0; i < 120 * 5; i++) { s.Step(1f / 120, new CombatSimulation.Controls { fire = true }); overheated |= s.overheated; }
            Check("Sustained firing overheats", overheated);
            for (int i = 0; i < 120 * 3; i++) s.Step(1f / 120, new CombatSimulation.Controls());
            Check("Heat recovers without firing", !s.overheated && s.heat == 0);
            Check("Swept collisions catch high-speed crossing", CombatSimulation.SegmentHit(new Vector2(-100, 0), new Vector2(100, 0), Vector2.zero, 3));
            Check("Swept collisions reject near misses", !CombatSimulation.SegmentHit(new Vector2(-100, 8), new Vector2(100, 8), Vector2.zero, 3));
            s.Start(true); s.enemies.Clear(); s.enemies.Add(new CombatSimulation.Enemy { kind = 3, position = new Vector2(120, 0), hp = 18, maxHp = 18 });
            s.Step(1f / 120, new CombatSimulation.Controls { fire = true });
            for (int i = 0; i < 24; i++) s.Step(1f / 120, new CombatSimulation.Controls());
            Check("Projectile deals damage and awards kill score", s.kills == 1 && s.score > 0 && s.hits == 1);
            s.Start(true); s.enemies.Clear(); s.enemies.Add(new CombatSimulation.Enemy { kind = 3, position = new Vector2(400, 0), hp = 200, maxHp = 200 });
            int ammo = s.missiles; s.Step(1f / 120, new CombatSimulation.Controls { missile = true });
            Check("Missile cannot fire before lock", s.missiles == ammo);
            for (int i = 0; i < 90; i++) s.Step(1f / 120, new CombatSimulation.Controls { brake = true });
            s.Step(1f / 120, new CombatSimulation.Controls { missile = true });
            Check("Locked missile consumes exactly one ammo", s.lockProgress >= 1 && s.missiles == ammo - 1);
            s.Start(); s.DamagePlayer(30); Check("Shield absorbs damage before hull", s.shield == 30 && s.hull == 100);
            s.enemies.Clear();
            for (int i = 0; i < 120; i++) s.Step(1f / 120, new CombatSimulation.Controls());
            s.DamagePlayer(40); Check("Shield overflow damages hull", s.shield == 0 && s.hull == 90);
            s.Start(true); for (int i = 0; i < 120 * 18; i++) s.Step(1f / 120, new CombatSimulation.Controls { thrust = true });
            Check("Flight remains inside bounded arena", s.position.magnitude <= CombatSimulation.ArenaRadius + .01f);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
    }
}
