using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AstraCabin
{
    /// <summary>Opt-in end-to-end demonstration verification. Never runs in ordinary play.</summary>
    public sealed class SetpieceVerification : MonoBehaviour
    {
        readonly List<string> results = new List<string>();
        readonly List<string> errors = new List<string>();
        CabinController controller;
        CabinSystems systems;
        CabinComputer computer;
        SetpieceDirector director;
        DistantPlanet defaultPlanet;
        ExplosionBattleSetpieces battle;
        VisitorStationSetpieces visitor;
        CrimsonEyeSetpiece crimson;
        StarWhaleSetpiece whales;
        MirrorProbeSetpiece mirror;
        SortingStationSetpiece sorting;
        int baselineLightCount;
        string output;
        int failed;
        bool finished;
        float started;

        void Update()
        {
            if (output == null || finished || Time.realtimeSinceStartup - started < 240) return;
            Check("Verification watchdog completed within 240 seconds", false);
            Finish(3);
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
        }

        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--astra-setpiece-qa");
            if (index < 0 || index + 1 >= args.Length) yield break;
            output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
            started = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnLog;
            Application.targetFrameRate = 60;
            controller = GetComponent<CabinController>();
            systems = GetComponent<CabinSystems>();
            computer = GetComponent<CabinComputer>();
            director = GetComponent<SetpieceDirector>();
            if (!controller || !systems || !director || !computer)
            {
                Check("Director and cabin components exist", false); Finish(2); yield break;
            }
            controller.automationMode = true; controller.SetPaused(false);
            computer.automationMode = true;
            bool isolatedStorage = Path.GetFileName(computer.PersistencePath) == "astra-computer-v1.qa.json";
            Check("Computer tests use isolated QA storage, never personal documents", isolatedStorage);
            if (!isolatedStorage) { Finish(2); yield break; }
            yield return new WaitForSecondsRealtime(1.5f);
            defaultPlanet = FindObjectOfType<DistantPlanet>();
            battle = FindObjectOfType<ExplosionBattleSetpieces>();
            visitor = FindObjectOfType<VisitorStationSetpieces>();
            crimson = FindObjectOfType<CrimsonEyeSetpiece>();
            whales = FindObjectOfType<StarWhaleSetpiece>();
            mirror = FindObjectOfType<MirrorProbeSetpiece>();
            sorting = FindObjectOfType<SortingStationSetpiece>();
            Check("All six setpiece modules are installed", battle && visitor && crimson && whales && mirror && sorting);
            Check("Default planet exists", defaultPlanet);
            if (!battle || !visitor || !crimson || !whales || !mirror || !sorting || !defaultPlanet) { Finish(2); yield break; }
            baselineLightCount = FindObjectsOfType<Light>().Length;
            int baselineControls = FindObjectsOfType<CabinInteractable>().Length;
            Check("Initial state contains only the original nine interactions", baselineControls == 9);
            Check("Invalid numeric mode is rejected", !director.TriggerMode(10) && director.ActiveMode == 0);
            controller.SnapToWall(1);
            yield return Capture("00_Default");

            // 1: the planet becomes independent solid fragments, not a flat flash overlay.
            Check("Key 1 activates planetary breakup", director.TriggerMode(1) && director.ActiveMode == 1);
            yield return new WaitForSecondsRealtime(.15f);
            Check("Original solid planet is hidden during breakup", !PlanetVisible());
            Check("Planet explosion uses many solid fragments", battle.ExplosionActive && battle.FragmentCount >= 12, "fragments=" + battle.FragmentCount);
            yield return new WaitForSecondsRealtime(1.05f);
            Check("Planet detonation produces an explosion", battle.ExplosionCount > 0);
            Check("Planet detonation shakes the actual camera", director.ShakeAmount > .01f && (controller.cinematicEuler.sqrMagnitude > .00001f || controller.cinematicOffset.sqrMagnitude > .000001f));
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("01_Planet_Breakup");
            controller.EnterWindowView();
            yield return new WaitForSecondsRealtime(controller.observationDuration + .1f);
            while (director.ModeTime < 4.1f) yield return null;
            yield return Capture("01_D_Close_Planet_Fragments_After4Seconds");
            director.ResetToDefault(); yield return null;
            Check("Planetary breakup resources clear on zero", !battle.ExplosionActive && battle.FragmentCount == 0 && PlanetVisible());

            // 2: arrival, readable physical monitor alert and a moving, three-dimensional pilot.
            controller.SnapToWall(1); // Exterior screenshots choose their view explicitly; events do not.
            Check("Key 2 activates the visitor", director.TriggerMode(2));
            yield return new WaitForSecondsRealtime(1.8f);
            Check("A physical UFO, pilot and waving hand exist", visitor.VisitorActive && visitor.GeneratedRoot && visitor.VisitorPilot && visitor.VisitorWaveHand);
            Check("Visitor alert is present on the physical monitor", director.AlertRoot && director.AlertRoot.activeInHierarchy && !string.IsNullOrEmpty(director.AlertTitle));
            yield return Capture("02_D_Central_Visitor");
            controller.SnapToWall(0); yield return Capture("02_A_UFO_Monitor_Alert");
            Check("Visitor warning is queued as a computer desktop modal", computer.HasPendingEventAlert && computer.eventAlertTitle == "不明飞行物靠近");
            Check("Powered computer opens with its visitor alert pending", computer.Open() && computer.IsOpen && controller.ComputerOpen && computer.HasPendingEventAlert);
            yield return null;
            computer.Close();
            Check("Closing the alert desktop returns input to the cabin", !computer.IsOpen && !controller.ComputerOpen);
            systems.SetMainPower(false);
            yield return null; yield return null;
            Check("Physical monitor popup vanishes when terminal loses power", !systems.ScreenHasPower && director.AlertRoot && !director.AlertRoot.activeInHierarchy);
            Check("An unpowered monitor cannot open its computer desktop", !computer.Open() && !computer.IsOpen);
            systems.SetMainPower(true);
            yield return null; yield return null;
            Check("Physical monitor popup returns with restored power", systems.ScreenHasPower && director.AlertRoot && director.AlertRoot.activeInHierarchy);
            controller.SnapToWall(1); controller.EnterWindowView();
            yield return new WaitForSecondsRealtime(1f);
            Quaternion handRotation = visitor.VisitorWaveHand ? visitor.VisitorWaveHand.rotation : Quaternion.identity;
            Vector3 handPosition = visitor.VisitorWaveHand ? visitor.VisitorWaveHand.position : Vector3.zero;
            yield return new WaitForSecondsRealtime(.36f);
            Check("Alien pilot really waves between frames", visitor.VisitorWaveHand && (Quaternion.Angle(handRotation, visitor.VisitorWaveHand.rotation) > 1 || Vector3.Distance(handPosition, visitor.VisitorWaveHand.position) > .01f));
            yield return Capture("02_D_Close_Visitor_Wave");
            director.ResetToDefault(); yield return new WaitForSecondsRealtime(.15f);
            Check("Visitor is completely removed on zero", !visitor.VisitorActive && !visitor.GeneratedRoot && !visitor.VisitorPilot);

            // 3: an explicit three-second blackout, a locked electrical circuit, then repair gameplay.
            controller.SnapToWall(1);
            Check("Key 3 starts a damage crisis", director.TriggerMode(3) && director.CrisisActive);
            yield return new WaitForSecondsRealtime(.12f);
            Check("Impact immediately locks the failed power circuit", systems.DamageLocked && !systems.MainPowerOn && !systems.LightsOn);
            Check("All normal and emergency lamps go dark initially", systems.cabinLights.All(l => !l.enabled) && systems.emergencyLights.All(l => !l.enabled));
            Check("Alarm does not start before the delayed second impact", !systems.EmergencyAlarm);
            Check("Blackout has no premature alarm audio", director.eventAudio && !director.eventAudio.AlarmPlaying);
            Check("Computer is unavailable during the initial complete blackout", !computer.Open() && !computer.IsOpen);
            Check("Initial impact shakes the camera", director.ShakeAmount > .1f);
            systems.SetLights(true); systems.SetMainPower(true);
            Check("Light and power commands cannot bypass damage lock", !systems.MainPowerOn && !systems.LightsOn);
            var powerLever = FindObjectsOfType<CabinInteractable>().FirstOrDefault(i => i.name == "ACT_PowerLever");
            if (powerLever)
            {
                powerLever.TryInteract(); yield return new WaitForSecondsRealtime(.4f);
                powerLever.TryInteract(); yield return new WaitForSecondsRealtime(.4f);
            }
            Check("Operating the real power lever cannot restore failed lights", powerLever && systems.DamageLocked && !systems.MainPowerOn && systems.cabinLights.All(l => !l.enabled));
            yield return Capture("03_D_Impact_Blackout");
            while (director.ModeTime < 2.7f) yield return null;
            Check("Blackout is still silent before the three-second deadline", !systems.EmergencyAlarm);
            while (director.ModeTime < 3.18f) yield return null;
            Check("Second impact starts emergency alarm after three seconds", systems.EmergencyAlarm && director.ShakeAmount > .01f);
            Check("The emergency siren is actually playing audio", director.eventAudio && director.eventAudio.AlarmPlaying);
            Check("Impact warning has the requested exact text", director.AlertTitle == "舱体遭遇撞击");
            Check("Screen receives emergency power while main circuit stays locked", systems.ScreenHasPower && !systems.MainPowerOn && systems.DamageLocked);
            Check("Impact warning is queued as a computer desktop modal", computer.HasPendingEventAlert && computer.eventAlertTitle == "舱体遭遇撞击");
            float minimumPulse = systems.EmergencyPulse, maximumPulse = minimumPulse;
            float pulseUntil = Time.realtimeSinceStartup + 1;
            while (Time.realtimeSinceStartup < pulseUntil)
            {
                minimumPulse = Mathf.Min(minimumPulse, systems.EmergencyPulse);
                maximumPulse = Mathf.Max(maximumPulse, systems.EmergencyPulse);
                yield return null;
            }
            Check("Red emergency lights visibly pulse", maximumPulse - minimumPulse > .2f, "range=" + minimumPulse.ToString("F2") + ".." + maximumPulse.ToString("F2"));
            Check("Exactly three repairable cracks are spawned", director.RepairPoints != null && director.RepairPoints.Length == 3 && director.RepairPoints.All(i => i));
            Check("Cannot repair cracks with bare hands", !director.HasHammer && !director.RepairCrack(0) && director.RepairedCount == 0);
            Check("Unsafe near-window observation is blocked during crisis", !controller.EnterWindowView());
            float brightPhaseDeadline = Time.realtimeSinceStartup + 2f;
            while (systems.EmergencyPulse <= .75f && Time.realtimeSinceStartup < brightPhaseDeadline) yield return null;
            yield return Capture("03_D_Three_Cracks_Alarm");
            controller.SnapToWall(0); yield return Capture("03_A_Impact_Monitor_Alert");
            Check("Emergency battery powers the interactive computer despite failed main circuit", computer.Open() && computer.IsOpen && computer.HasPendingEventAlert && !systems.MainPowerOn && systems.EmergencyAlarm);
            yield return null;
            computer.Close();
            Check("Emergency computer closes without ending the repair crisis", !computer.IsOpen && !controller.ComputerOpen && director.CrisisActive && director.eventAudio.AlarmPlaying);
            controller.SnapToWall(3);
            yield return new WaitForSecondsRealtime(.2f);
            CabinInteractable hammer = director.HammerRack ? director.HammerRack.GetComponentInChildren<CabinInteractable>() : null;
            Check("Hammer is physically reachable on the tool wall", CanRaycast(hammer));
            yield return Capture("03_B_Tool_Wall_Hammer");
            bool hammerClick = ActivateThroughRay(hammer);
            yield return new WaitForSecondsRealtime(.55f);
            Check("Real hammer click equips the repair tool", hammerClick && director.HasHammer);
            controller.SetHoverForTest(null);
            controller.SnapToWall(1);
            yield return Capture("03_D_Hammer_Equipped");
            for (int i = 0; i < 3; i++)
            {
                CabinInteractable crack = director.RepairPoints != null && i < director.RepairPoints.Length ? director.RepairPoints[i] : null;
                Check("Crack " + (i + 1) + " is reachable by actual player raycast", CanRaycast(crack));
                int before = director.RepairedCount;
                bool accepted = ActivateThroughRay(crack);
                yield return new WaitForSecondsRealtime(.12f);
                Check("Crack " + (i + 1) + " requires the hammer animation before completion", accepted && director.RepairedCount == before);
                yield return new WaitForSecondsRealtime(.52f);
                Check("Crack " + (i + 1) + " is repaired exactly once", director.RepairedCount == before + 1);
                controller.SetHoverForTest(null);
            }
            Check("All three repairs end the crisis", director.RepairedCount == 3 && !director.CrisisActive && !systems.DamageLocked);
            Check("Successful repair clears alarm and restores normal lights", !systems.EmergencyAlarm && systems.MainPowerOn && systems.LightsOn && systems.cabinLights.All(l => l.enabled));
            Check("Completing repairs stops the real siren audio", director.eventAudio && !director.eventAudio.AlarmPlaying);
            Check("Window observation is available after repair", controller.observationAllowed);
            yield return Capture("03_D_Crisis_Repaired");
            director.ResetToDefault(); yield return new WaitForSecondsRealtime(.15f);
            Check("Reset removes temporary hammer and crack interactions", !director.HasHammer && FindObjectsOfType<CabinInteractable>().Length == baselineControls);

            // 4: a finite jump reaches an actual spherical eye and independent molten worlds.
            controller.SnapToWall(1); controller.EnterWindowView();
            yield return new WaitForSecondsRealtime(1f);
            Check("Key 4 begins a real warp transition", director.TriggerMode(4) && crimson.Active && crimson.Warping);
            yield return new WaitForSecondsRealtime(.82f);
            yield return Capture("04_D_Crimson_Warp");
            yield return new WaitForSecondsRealtime(1.6f);
            Check("Warp completes rather than remaining in a transition", crimson.Active && !crimson.Warping);
            Check("Crimson system has nine separate molten planets", crimson.LavaPlanetCount == 9);
            Check("Planet-scale eye has fourteen animated mesh tentacles", crimson.EyeRoot && crimson.TentacleCount == 14);
            Check("Default planet is absent from the crimson destination", !PlanetVisible());
            MeshFilter tentacleMesh = crimson.EyeRoot ? crimson.EyeRoot.GetComponentsInChildren<MeshFilter>().FirstOrDefault(m => m.name.StartsWith("Animated tendril")) : null;
            Vector3 tentacleSample = tentacleMesh ? tentacleMesh.sharedMesh.vertices[32] : Vector3.zero;
            yield return new WaitForSecondsRealtime(.4f);
            Check("Eye creature tendrils deform their real geometry", tentacleMesh && Vector3.Distance(tentacleSample, tentacleMesh.sharedMesh.vertices[32]) > .001f);
            yield return Capture("04_D_Crimson_Eye_And_Lava_Worlds");
            float heldTime = crimson.Elapsed;
            controller.SetPaused(true); yield return new WaitForSecondsRealtime(.3f);
            Check("Cinematic exterior respects gameplay pause", Mathf.Abs(crimson.Elapsed - heldTime) < .0001f);
            controller.SetPaused(false);

            // 5: red and blue craft move, fire visible laser segments, then explode.
            controller.SnapToWall(1); controller.EnterWindowView();
            yield return new WaitForSecondsRealtime(controller.observationDuration + .1f);
            Check("Key 5 replaces the crimson scene with a battlefield", director.TriggerMode(5));
            yield return new WaitForSecondsRealtime(.3f);
            Check("Previous eye scene is released when switching directly", !crimson.Active && !crimson.GeneratedRoot);
            Check("Battlefield has numerous distinct fighters", battle.BattleActive && battle.FighterCount >= 20, "fighters=" + battle.FighterCount);
            var redFighter = FindObjectsOfType<Transform>().FirstOrDefault(t => t.name.StartsWith("RED / Needle"));
            var blueFighter = FindObjectsOfType<Transform>().FirstOrDefault(t => t.name.StartsWith("BLUE / Twin-boom"));
            Check("Both red and blue factions are present", redFighter && blueFighter);
            Vector3 fighterPosition = redFighter ? redFighter.position : Vector3.zero;
            int peakLasers = 0; bool redLaser = false, blueLaser = false, battlefieldShake = false;
            float battleStarted = Time.realtimeSinceStartup;
            float battleUntil = battleStarted + 11f;
            while (Time.realtimeSinceStartup < battleUntil)
            {
                peakLasers = Mathf.Max(peakLasers, battle.LaserCount);
                battlefieldShake |= director.ShakeAmount > .01f;
                foreach (LineRenderer line in FindObjectsOfType<LineRenderer>())
                {
                    if (!line.enabled || line.positionCount != 2) continue;
                    Color c = line.startColor;
                    redLaser |= c.r > c.b * 2 && c.r > .5f;
                    blueLaser |= c.b > c.r * 2 && c.b > .5f;
                }
                if (Time.realtimeSinceStartup - battleStarted >= 6f && battlefieldShake && battle.ExplosionCount >= 2) break;
                yield return null;
            }
            Check("Dogfighting craft move through three-dimensional space", redFighter && Vector3.Distance(fighterPosition, redFighter.position) > .2f);
            Check("Battle continuously emits multiple laser bolts", peakLasers >= 4, "peak=" + peakLasers);
            Check("Red and blue factions fire matching colored lasers", redLaser && blueLaser);
            Check("Battle generates recurring craft explosions", battle.ExplosionCount >= 2, "explosions=" + battle.ExplosionCount);
            Check("Nearby battlefield blasts shake the cabin", battlefieldShake);
            yield return Capture("05_D_Dogfight_Red_And_Blue");

            // 6: a physical station surrounds a varied, moving refuelling queue.
            controller.SnapToWall(1); controller.EnterWindowView();
            yield return new WaitForSecondsRealtime(controller.observationDuration + .1f);
            Check("Key 6 changes the exterior to a fuel station", director.TriggerMode(6));
            yield return new WaitForSecondsRealtime(1.2f);
            Check("Battle ships and lasers are removed on station arrival", !battle.BattleActive && battle.FighterCount == 0 && battle.LaserCount == 0);
            Check("Refuelling station has a visible generated structure", visitor.StationActive && visitor.GeneratedRoot);
            Check("Eight ships queue for refuelling", visitor.QueuedShipCount == 8, "queued=" + visitor.QueuedShipCount);
            yield return Capture("06_D_Orbital_Fuel_Station");

            yield return VerifySevenToNine();

            director.ResetToDefault(); yield return new WaitForSecondsRealtime(.2f);
            CheckDefault("Final zero reset", baselineControls);
            yield return Capture("07_Default_Restored");
            // Switch before animations complete, including abandoning an unrepaired impact.
            for (int cycle = 0; cycle < 2; cycle++)
            {
                foreach (int mode in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 })
                {
                    bool changed = director.TriggerMode(mode);
                    yield return new WaitForSecondsRealtime(.06f);
                    Check("Rapid switch cycle " + (cycle + 1) + " mode " + mode, changed && director.ActiveMode == mode);
                }
                yield return null;
                CheckDefault("Rapid switch cleanup " + (cycle + 1), baselineControls);
            }
            yield return VerifyEventViewPreservation(baselineControls);
            Check("No shader, runtime, or assertion errors were logged", errors.Count == 0, string.Join("\n", errors));
            Finish(failed == 0 ? 0 : 2);
        }

        IEnumerator VerifyEventViewPreservation(int baselineControls)
        {
            // Events change the exterior and cabin systems, never select the player's wall.
            for (int wall = 0; wall < 4; wall++)
            {
                for (int mode = 1; mode <= 9; mode++)
                {
                    director.ResetToDefault();
                    controller.SnapToWall(wall);
                    controller.SetLookForTest(new Vector2(.35f, -.2f));
                    yield return null;
                    Vector3 position = controller.viewCamera.transform.position;
                    Quaternion rotation = controller.viewCamera.transform.rotation;
                    Vector2 look = controller.CurrentLook;
                    float yaw = controller.CurrentYaw;
                    bool changed = director.TriggerMode(mode);
                    string label = "Wall " + wall + " event " + mode;
                    Check(label + " activates without selecting another wall", changed && director.ActiveMode == mode && controller.CurrentWall == wall);
                    Check(label + " preserves the camera pose at the instant of triggering", SameCameraPose(position, rotation) && Vector2.Distance(look, controller.CurrentLook) < .0001f);
                    yield return null;
                    // Camera shake is intentional; compare navigation yaw rather than shaken rotation.
                    Check(label + " leaves navigation orientation unchanged on the next frame", controller.CurrentWall == wall && !controller.IsTurning && Mathf.Abs(Mathf.DeltaAngle(yaw, controller.CurrentYaw)) < .001f);
                }
            }

            for (int mode = 1; mode <= 9; mode++)
            {
                director.ResetToDefault(); controller.SnapToWall(2);
                controller.Turn(1);
                yield return new WaitForSecondsRealtime(controller.turnDuration * .25f);
                int targetWall = controller.CurrentWall;
                float yaw = controller.CurrentYaw;
                Vector3 position = controller.viewCamera.transform.position;
                Quaternion rotation = controller.viewCamera.transform.rotation;
                Check("Turning setup for event " + mode + " is genuinely in progress", controller.IsTurning && targetWall == 3 && Mathf.Abs(Mathf.DeltaAngle(yaw, targetWall * 90f)) > .1f);
                director.TriggerMode(mode);
                Check("Event " + mode + " does not snap or cancel an active wall turn", controller.IsTurning && controller.CurrentWall == targetWall && Mathf.Abs(Mathf.DeltaAngle(yaw, controller.CurrentYaw)) < .001f && SameCameraPose(position, rotation));
                yield return new WaitForSecondsRealtime(controller.turnDuration + .1f);
                Check("Event " + mode + " lets the original wall turn finish normally", !controller.IsTurning && controller.CurrentWall == targetWall && Mathf.Abs(Mathf.DeltaAngle(controller.CurrentYaw, targetWall * 90f)) < .001f);
            }

            for (int mode = 1; mode <= 9; mode++)
            {
                director.ResetToDefault(); controller.SnapToWall(1);
                controller.EnterWindowView();
                controller.SetLookForTest(new Vector2(-.28f, .16f));
                yield return new WaitForSecondsRealtime(controller.observationDuration + .1f);
                Check("Near-window setup for event " + mode + " is complete", controller.IsAtWindow);
                Vector3 position = controller.viewCamera.transform.position;
                Quaternion rotation = controller.viewCamera.transform.rotation;
                director.TriggerMode(mode);
                Check("Near-window event " + mode + " never teleports or turns the camera", controller.CurrentWall == 1 && SameCameraPose(position, rotation));
                if (mode == 3)
                {
                    Check("Impact requests a smooth retreat instead of instant recentering", controller.IsObservingWindow && controller.IsObservationTransitioning && !controller.IsAtWindow && controller.ObservationBlend > .99f);
                    yield return new WaitForSecondsRealtime(controller.observationDuration * .35f);
                    Check("Impact retreat passes through an intermediate near-window pose", controller.CurrentWall == 1 && controller.ObservationBlend > 0 && controller.ObservationBlend < 1 && !controller.observationAllowed);
                    yield return new WaitForSecondsRealtime(controller.observationDuration + .3f);
                    Check("Impact retreat finishes at the center without changing the viewed wall", !controller.IsObservingWindow && controller.CurrentWall == 1 && Vector3.Distance(controller.viewCamera.transform.position, controller.centerPosition) < .001f);
                }
                else
                {
                    Check("Event " + mode + " preserves completed near-window observation", controller.IsAtWindow && controller.ObservationBlend >= 1f);
                    yield return null;
                    Check("Event " + mode + " does not restart the approach on its next frame", controller.IsAtWindow && controller.CurrentWall == 1 && controller.ObservationBlend >= 1f);
                }
            }

            // Preserve an in-flight approach/retreat too, not just its two stable endpoints.
            director.ResetToDefault(); controller.SnapToWall(1); controller.EnterWindowView();
            yield return new WaitForSecondsRealtime(controller.observationDuration * .35f);
            float approachingBlend = controller.ObservationBlend;
            Vector3 approachPosition = controller.viewCamera.transform.position;
            Quaternion approachRotation = controller.viewCamera.transform.rotation;
            director.TriggerMode(2);
            Check("Visitor trigger preserves an in-flight window approach", approachingBlend > 0 && approachingBlend < 1 && Mathf.Abs(controller.ObservationBlend - approachingBlend) < .0001f && SameCameraPose(approachPosition, approachRotation));
            yield return new WaitForSecondsRealtime(controller.observationDuration + .1f);
            Check("Window approach still finishes after an event trigger", controller.IsAtWindow && controller.CurrentWall == 1);
            controller.ExitWindowView();
            yield return new WaitForSecondsRealtime(controller.observationDuration * .35f);
            float retreatingBlend = controller.ObservationBlend;
            Vector3 retreatPosition = controller.viewCamera.transform.position;
            Quaternion retreatRotation = controller.viewCamera.transform.rotation;
            director.TriggerMode(6);
            Check("Station trigger preserves an in-flight window retreat", retreatingBlend > 0 && retreatingBlend < 1 && Mathf.Abs(controller.ObservationBlend - retreatingBlend) < .0001f && SameCameraPose(retreatPosition, retreatRotation));
            yield return new WaitForSecondsRealtime(controller.observationDuration + .1f);
            Check("Window retreat still finishes after an event trigger", !controller.IsObservingWindow && controller.CurrentWall == 1);

            director.ResetToDefault(); yield return new WaitForSecondsRealtime(.2f);
            CheckDefault("View preservation regression cleanup", baselineControls);
            Check("Zero deliberately keeps its default A-wall reset behavior", controller.CurrentWall == 0 && Mathf.Abs(Mathf.DeltaAngle(controller.CurrentYaw, 0)) < .001f);
        }

        bool SameCameraPose(Vector3 position, Quaternion rotation)
        {
            return Vector3.Distance(position, controller.viewCamera.transform.position) < .0001f && Quaternion.Angle(rotation, controller.viewCamera.transform.rotation) < .01f;
        }

        IEnumerator VerifySevenToNine()
        {
            director.ResetToDefault(); controller.SnapToWall(1);
            Check("Key 7 starts the migration without turning the player", director.TriggerMode(7) && director.ActiveMode == 7 && controller.CurrentWall == 1);
            yield return new WaitForSecondsRealtime(.4f);
            Check("Whale migration contains multiple real creatures", whales.Active && whales.GeneratedRoot && whales.HeroWhale && whales.WhaleCount >= 3);
            Check("Whale wake contains a school of small luminous animals", whales.FollowerCount >= 20, "followers=" + whales.FollowerCount);
            Check("Whales replace the original exterior", !PlanetVisible() && !visitor.StationActive);
            Vector3 initialWhalePosition = whales.HeroWhale ? whales.HeroWhale.position : Vector3.zero;
            while (whales.Elapsed < 3.1f) yield return null;
            yield return Capture("19_D_Whale_Migration_Arrival");
            while (whales.Elapsed < 10.1f) yield return null;
            Check("Hero whale physically crosses the exterior", whales.HeroWhale && Vector3.Distance(initialWhalePosition, whales.HeroWhale.position) > 1f);
            Check("Whales add a visibly lit temporary cabin reflection", whales.GeneratedRoot && whales.GeneratedRoot.GetComponentsInChildren<Light>(true).Any(l => (l.cullingMask & 1) != 0 && l.intensity > .4f));
            yield return Capture("20_D_Whale_Blue_Cabin_Reflection");
            controller.EnterWindowView(); yield return new WaitForSecondsRealtime(controller.observationDuration + .1f);
            yield return Capture("21_D_Close_Whale_And_Followers");
            float whaleTime = whales.Elapsed; controller.SetPaused(true); yield return new WaitForSecondsRealtime(.22f);
            Check("Whale animation stops in the pause menu", Mathf.Abs(whales.Elapsed - whaleTime) < .001f);
            controller.SetPaused(false);
            director.ResetToDefault(); yield return null;
            Check("Whale scene and its blue reflection lights are fully removed", !whales.Active && !whales.GeneratedRoot && FindObjectsOfType<Light>().Length == baselineLightCount);

            controller.SnapToWall(1); controller.SetLookForTest(Vector2.zero);
            Check("Key 8 starts the duplicate probe encounter", director.TriggerMode(8) && mirror.Active);
            yield return new WaitForSeconds(.8f);
            Check("Duplicate probe contains a physical pilot and lever", mirror.GeneratedRoot && mirror.PilotHead && mirror.MirrorLever);
            Check("Mirror scene accumulates delayed player samples", mirror.SampleCount > 0);
            Check("Duplicate ship is reported on the monitor", director.AlertTitle != null && director.AlertTitle.Contains("重复"));
            Vector2 startingLook = mirror.AppliedLook;
            controller.SetLookForTest(new Vector2(.8f, .5f));
            yield return new WaitForSeconds(.18f);
            Check("Duplicate pilot does not instantly copy new mouse look", Vector2.Distance(startingLook, mirror.AppliedLook) < .03f);
            yield return new WaitForSeconds(.65f);
            Check("Duplicate pilot copies player look after a real delay", Vector2.Distance(startingLook, mirror.AppliedLook) > .15f, "look=" + mirror.AppliedLook + "; elapsed=" + mirror.Elapsed + "; sample=" + mirror.LastAppliedSampleTime);
            systems.SetLights(false); yield return new WaitForSeconds(.18f);
            Check("Mirror cabin lights initially retain their delayed state", mirror.MirrorLightsOn);
            yield return new WaitForSeconds(.65f);
            Check("Mirror cabin lights follow the player's light switch after delay", !mirror.MirrorLightsOn);
            systems.SetLights(true); yield return new WaitForSeconds(.7f);
            Check("Duplicate cabin can relight during the imitation phase", mirror.MirrorLightsOn);
            controller.SetLookForTest(Vector2.zero);
            yield return Capture("22_D_Duplicate_Probe_Imitation");
            controller.EnterWindowView(); yield return new WaitForSecondsRealtime(controller.observationDuration + .1f);
            yield return Capture("23_D_Close_Duplicate_Pilot");
            while (mirror.Elapsed < 13.2f) yield return null;
            Check("Duplicate pilot enters the independent uncanny phase", mirror.AnticipationPhase);
            Check("Independent pilot really switches off the duplicate's lamps", !mirror.MirrorLightsOn);
            Check("Mirror blackout never switches off the player's actual power", systems.MainPowerOn && systems.LightsOn && !systems.DamageLocked);
            yield return Capture("24_D_Duplicate_Independent_Blackout");
            float mirrorTime = mirror.Elapsed; controller.SetPaused(true); yield return new WaitForSecondsRealtime(.22f);
            Check("Duplicate pilot pauses along with the cabin", Mathf.Abs(mirror.Elapsed - mirrorTime) < .001f);
            controller.SetPaused(false);

            Check("Key 9 replaces the mirror scene with a sorting station", director.TriggerMode(9) && sorting.Active);
            yield return null;
            Check("Switching to sorting removes the duplicate pilot and samples", !mirror.Active && !mirror.GeneratedRoot && mirror.SampleCount == 0);
            Check("Sorting station contains many separate human probes", sorting.GeneratedRoot && sorting.ProbeCount >= 8, "probes=" + sorting.ProbeCount);
            Check("Sorting process has an articulated stamping arm", sorting.StampArm);
            Check("Shipping label is not applied before the scanning sequence", !sorting.StampApplied);
            while (sorting.Elapsed < 4.2f) yield return null;
            yield return Capture("25_D_Sorting_Clamp_And_Scan");
            while (sorting.Elapsed < 10f) yield return null;
            Check("Robot physically applies the biological inventory label", sorting.StampApplied);
            yield return Capture("26_D_Sorting_Label_Close");
            while (sorting.Elapsed < 17f) yield return null;
            Check("Neighbouring dark probe is routed into recycling", sorting.RecycledProbeRouted);
            Check("Sorting never locks navigation or disables actual ship power", controller.observationAllowed && systems.MainPowerOn && systems.LightsOn && !systems.DamageLocked);
            controller.ExitWindowView(); yield return new WaitForSecondsRealtime(controller.observationDuration + .1f);
            yield return Capture("27_D_Sorting_Route_And_Probe_Queue");
            while (sorting.Elapsed < 24.1f) yield return null;
            Check("Sorting releases the player's capsule for redeployment", sorting.ReleasedForRedeployment);
            yield return Capture("28_D_Sorting_Released_For_Redeployment");
            float sortingTime = sorting.Elapsed; controller.SetPaused(true); yield return new WaitForSecondsRealtime(.22f);
            Check("Sorting conveyor and robot respect gameplay pause", Mathf.Abs(sorting.Elapsed - sortingTime) < .001f);
            controller.SetPaused(false);
            Check("All new exterior materials have supported shaders", FindObjectsOfType<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m).All(m => m.shader && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader"));
            director.ResetToDefault(); yield return null;
            Check("Zero removes sorting props, stamp and temporary lighting", !sorting.Active && !sorting.GeneratedRoot && FindObjectsOfType<Light>().Length == baselineLightCount);
        }

        bool PlanetVisible()
        {
            return defaultPlanet && defaultPlanet.gameObject.activeInHierarchy && defaultPlanet.GetComponentsInChildren<Renderer>().Any(r => r.enabled && r.gameObject.activeInHierarchy);
        }

        void CheckDefault(string label, int controls)
        {
            Check(label + " restores default planet and exterior", director.ActiveMode == 0 && PlanetVisible() && !battle.ExplosionActive && !battle.BattleActive && !visitor.VisitorActive && !visitor.StationActive && !crimson.Active && !whales.Active && !mirror.Active && !sorting.Active);
            Check(label + " clears warnings and temporary assets", !director.CrisisActive && !director.HasHammer && !director.AlertRoot && !visitor.GeneratedRoot && !crimson.GeneratedRoot && FindObjectsOfType<CabinInteractable>().Length == controls);
            Check(label + " restores main power and cancels alarm", !systems.DamageLocked && !systems.EmergencyAlarm && systems.MainPowerOn && systems.LightsOn);
            Check(label + " stops alarm audio and removes pending desktop alerts", director.eventAudio && !director.eventAudio.AlarmPlaying && !computer.HasPendingEventAlert && !computer.IsOpen);
            Check(label + " returns camera to a stable central pose", !controller.IsObservingWindow && director.ShakeAmount < .001f && controller.cinematicOffset.sqrMagnitude < .000001f && controller.cinematicEuler.sqrMagnitude < .000001f && Vector3.Distance(controller.viewCamera.transform.position, controller.centerPosition) < .001f);
            Check(label + " removes encounter geometry and restores original light count", !whales.GeneratedRoot && !mirror.GeneratedRoot && !sorting.GeneratedRoot && FindObjectsOfType<Light>().Length == baselineLightCount);
        }

        bool CanRaycast(CabinInteractable item)
        {
            if (!item || !item.isActiveAndEnabled) return false;
            Collider collider = item.GetComponentInChildren<Collider>();
            if (!collider) return false;
            Physics.SyncTransforms();
            Vector3 origin = controller.viewCamera.transform.position;
            Vector3 center = collider.bounds.center;
            Vector3 viewport = controller.viewCamera.WorldToViewportPoint(center);
            RaycastHit hit;
            bool visible = viewport.z > 0 && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1;
            bool intersects = Physics.Raycast(origin, (center - origin).normalized, out hit, controller.interactionDistance, controller.interactionMask, QueryTriggerInteraction.Ignore);
            return visible && intersects && hit.collider.GetComponentInParent<CabinInteractable>() == item;
        }

        bool ActivateThroughRay(CabinInteractable item)
        {
            if (!CanRaycast(item)) return false;
            controller.SetHoverForTest(item);
            return controller.ActivateHoveredForTest();
        }

        void Check(string label, bool passed, string detail = "")
        {
            if (!passed) failed++;
            results.Add((passed ? "PASS " : "FAIL ") + label + (string.IsNullOrEmpty(detail) ? "" : " | " + detail));
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            const int width = 1600, height = 900;
            Camera camera = controller.viewCamera;
            RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                float renderStarted = Time.realtimeSinceStartup;
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                float renderMilliseconds = (Time.realtimeSinceStartup - renderStarted) * 1000f;
                results.Add("INFO " + name + " | offscreen render and readback=" + renderMilliseconds.ToString("F1") + " ms; resolution=" + width + "x" + height);
                Color32[] pixels = image.GetPixels32(); int minimum = 765, maximum = 0;
                for (int i = 0; i < pixels.Length; i += 101)
                {
                    int value = pixels[i].r + pixels[i].g + pixels[i].b;
                    minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
                }
                Check("Camera rendered nonblank frame " + name, maximum - minimum > 30);
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                File.WriteAllLines(Path.Combine(output, "setpiece-verification.txt"), results);
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target); Destroy(image);
            }
        }

        void Finish(int exitCode)
        {
            if (finished) return;
            finished = true;
            if (errors.Count > 0) results.Add("Logged errors:\n" + string.Join("\n", errors));
            int checkCount = results.Count(line => line.StartsWith("PASS ") || line.StartsWith("FAIL "));
            results.Add("RESULT " + (failed == 0 && errors.Count == 0 ? "PASS" : "FAIL") + " | checks=" + checkCount + " failures=" + failed + " errors=" + errors.Count);
            File.WriteAllLines(Path.Combine(output, "setpiece-verification.txt"), results);
            Debug.Log("Setpiece verification completed: " + failed + " failed; " + errors.Count + " runtime errors.");
            if (controller) controller.SetPaused(false);
            Application.Quit(exitCode);
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
