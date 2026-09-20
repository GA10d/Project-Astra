using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AstraCabin
{
    // Only runs when explicitly launched with --astra-qa <output directory>.
    // Normal players incur no update loop or file writes.
    public sealed class CabinVerification : MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        readonly List<string> errors=new List<string>();
        CabinController controller; CabinSystems systems; string output; int failed;
        float started; bool finished;
        void Update()
        {
            if(output!=null && !finished && Time.realtimeSinceStartup-started>130)
            {
                results.Add("FAIL: QA watchdog timeout. "+string.Join("\n",errors));
                File.WriteAllLines(Path.Combine(output,"verification.txt"),results); finished=true; Application.Quit(3);
            }
        }
        void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors.Add(message+"\n"+stack);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"--astra-qa");
            if(index<0 || index+1>=args.Length) yield break;
            output=Path.GetFullPath(args[index+1]); Directory.CreateDirectory(output);
            started=Time.realtimeSinceStartup;
            Application.logMessageReceived+=OnLog;
            controller=GetComponent<CabinController>(); systems=GetComponent<CabinSystems>();
            controller.automationMode=true; Application.targetFrameRate=60;
            yield return new WaitForSecondsRealtime(2);
            Check("Nine independently interactive objects including computer and window",FindObjectsOfType<CabinInteractable>().Length==9);
            Check("Fixed central camera",Vector3.Distance(controller.viewCamera.transform.position,new Vector3(0,1.28f,0))<.001f);
            var screens=systems.screenRenderers;
            Check("Live CRT shader assigned",screens.Length>0 && screens.All(r=>r.sharedMaterials.Any(m=>m.shader.name=="Astra/CRT")));
            var titles=new[]{"01_A_Command","04_D_Observation","03_C_Habitation","02_B_Fabrication"};
            var hittable=new HashSet<string>();
            for(int wall=0;wall<4;wall++)
            {
                controller.SnapToWall(wall); yield return new WaitForSecondsRealtime(.35f);
                yield return Capture(titles[wall]);
                foreach(var item in FindObjectsOfType<CabinInteractable>())
                {
                    var collider=item.GetComponent<BoxCollider>();
                    Vector3 center=collider.transform.TransformPoint(collider.center);
                    var viewport=controller.viewCamera.WorldToViewportPoint(center);
                    if(viewport.z<=0 || viewport.x<0 || viewport.x>1 || viewport.y<0 || viewport.y>1) continue;
                    RaycastHit hit; Vector3 dir=center-controller.viewCamera.transform.position;
                    bool didHit=Physics.Raycast(controller.viewCamera.transform.position,dir.normalized,out hit,4.5f,~0,QueryTriggerInteraction.Ignore);
                    Check("Visible control raycast "+item.name,didHit && hit.collider.GetComponentInParent<CabinInteractable>()==item,
                        didHit?"nearest="+hit.collider.name:"miss");
                    if(didHit && hit.collider.GetComponentInParent<CabinInteractable>()==item) hittable.Add(item.name);
                }
            }
            Check("All nine controls accessible by real raycast",hittable.Count==9,"hit="+string.Join(",",hittable));
            controller.SnapToWall(0); controller.Turn(-1);
            yield return new WaitForSecondsRealtime(.7f);
            Check("Q wraps A to B",controller.CurrentWall==3 && Mathf.Abs(Mathf.DeltaAngle(controller.CurrentYaw,270))<.01f);
            controller.Turn(1); yield return new WaitForSecondsRealtime(.7f);
            Check("E wraps B to A",controller.CurrentWall==0 && Mathf.Abs(Mathf.DeltaAngle(controller.CurrentYaw,0))<.01f);
            for(int i=0;i<8;i++) { controller.Turn(1); yield return new WaitForSecondsRealtime(.55f); }
            Check("Repeated turns do not drift",controller.CurrentWall==0 && Mathf.Abs(Mathf.DeltaAngle(controller.CurrentYaw,0))<.01f);
            controller.SetLookForTest(Vector2.one); yield return new WaitForSecondsRealtime(.6f);
            Check("Mouse right and up look in the same direction",controller.viewCamera.transform.forward.x>0 && controller.viewCamera.transform.forward.y>0);
            Check("Mouse look remains subtle",Vector3.Angle(Vector3.forward,controller.viewCamera.transform.forward)<5);
            controller.SnapToWall(0);
            var light=Item("ACT_LightSwitch");
            controller.SetHoverForTest(light); yield return new WaitForSecondsRealtime(.1f);
            Check("Yellow hover outline",light.IsHovered && light.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterial && r.sharedMaterial.shader.name=="Astra/Outline"));
            yield return Capture("05_A_Hover");
            controller.ActivateHoveredForTest();
            Check("Repeated input during animation rejected",!light.TryInteract());
            yield return new WaitForSecondsRealtime(.45f);
            Check("Light switch turns cabin lights off",!systems.LightsOn && systems.cabinLights.All(l=>!l.enabled));
            Check("Emergency lighting remains on",systems.emergencyLights.All(l=>l.enabled));
            controller.SetHoverForTest(null); yield return Capture("06_A_LightsOff");
            Check("Hover outline clears when cursor leaves",!light.IsHovered && !light.GetComponentsInChildren<Renderer>().Any(r=>r.name=="__HoverOutline" && r.gameObject.activeInHierarchy));
            light.Interact(); yield return new WaitForSecondsRealtime(.45f);
            var power=Item("ACT_PowerLever"); var powerRotation=power.transform.localRotation;
            power.Interact(); yield return new WaitForSecondsRealtime(.45f);
            var block=new MaterialPropertyBlock(); screens[0].GetPropertyBlock(block);
            Check("Power lever animates and cuts CRT",!systems.MainPowerOn && Quaternion.Angle(powerRotation,power.transform.localRotation)>20 && block.GetFloat("_Power")==0);
            yield return Capture("07_A_MainPowerOff");
            power.Interact(); yield return new WaitForSecondsRealtime(.45f);
            controller.SnapToWall(3);
            var button=Item("ACT_PrintButton"); var rest=button.transform.localPosition;
            button.Interact(); yield return new WaitForSecondsRealtime(.08f);
            Check("Print button depresses",Vector3.Distance(rest,button.transform.localPosition)>.0001f);
            yield return new WaitForSecondsRealtime(.4f);
            Check("Print button springs back",Vector3.Distance(rest,button.transform.localPosition)<.0001f);
            var head=systems.printerHead.localPosition; yield return new WaitForSecondsRealtime(.5f);
            Check("Printer head moves in real geometry",systems.PrinterOn && Vector3.Distance(head,systems.printerHead.localPosition)>.001f);
            yield return Capture("08_B_PrinterRunning");
            controller.SnapToWall(2); var cabinet=Item("ACT_Cabinet"); var cabinetRotation=cabinet.transform.localRotation;
            cabinet.Interact(); yield return new WaitForSecondsRealtime(.8f);
            Check("Locker door has independent animation",Quaternion.Angle(cabinetRotation,cabinet.transform.localRotation)>40);
            Item("ACT_RadioKnob").Interact(); yield return new WaitForSecondsRealtime(.4f);
            Check("Radio control toggles system",systems.RadioOn);
            yield return Capture("09_C_LockerOpen");
            controller.SnapToWall(1); var hatch=Item("ACT_Hatch"); var hatchRotation=hatch.transform.localRotation;
            hatch.Interact(); yield return new WaitForSecondsRealtime(.8f);
            Check("Transfer hatch opens independently",Quaternion.Angle(hatchRotation,hatch.transform.localRotation)>40);
            controller.SetHoverForTest(hatch); yield return Capture("10_D_HatchOpen"); controller.SetHoverForTest(null);
            var stars=FindObjectOfType<LoopingStarfield>(); stars.animate=false;
            var old=stars.StarPositions[0]; stars.AdvanceTime(2);
            Check("Exterior stars are moving geometry",Vector3.Distance(old,stars.StarPositions[0])>.05f);
            stars.AdvanceTime(100000);
            Check("Star wrap stays inside volume",stars.StarPositions.All(p=>Mathf.Abs(p.x-stars.volumeCenter.x)<=stars.size.x*.5f+.001f && Mathf.Abs(p.y-stars.volumeCenter.y)<=stars.size.y*.5f+.001f && Mathf.Abs(p.z-stars.volumeCenter.z)<=stars.size.z*.5f+.001f));
            var valve=Item("ACT_Valve"); valve.Interact(); yield return new WaitForSecondsRealtime(.4f);
            Check("Ventilation valve toggles",!systems.AirOn);
            controller.SetPaused(true); Check("Pause stops simulation",Time.timeScale==0);
            controller.SetPaused(false); Check("Resume restores simulation",Time.timeScale==1);
            controller.SnapToWall(0);
            var computer=GetComponent<CabinComputer>(); computer.automationMode=true;
            var presentation=controller.viewCamera.GetComponent<CabinPresentation>();
            Check("Computer QA has isolated storage",computer.IsTestStorage && computer.PersistencePath.EndsWith(".qa.json"));
            controller.SetHoverForTest(Item("SCREEN_Main"));
            Check("Clicking CRT opens desktop",controller.ActivateHoveredForTest() && computer.IsOpen && controller.ComputerOpen);
            Check("Opening desktop clears outline",controller.Hovered==null);
            Check("Desktop blurs the real cabin camera",presentation.computerBlur && presentation.blurShader!=null);
            int heldWall=controller.CurrentWall; var heldPose=controller.viewCamera.transform.rotation;
            controller.Turn(1); controller.SetLookForTest(Vector2.one); yield return new WaitForSecondsRealtime(.6f);
            Check("Desktop blocks wall turns and mouse look",heldWall==controller.CurrentWall && Quaternion.Angle(heldPose,controller.viewCamera.transform.rotation)<.01f);
            yield return Capture("11_Computer_BackgroundBlur");
            var pages=computer.GetArchivePages();
            Check("All 18 offline pages are loaded",pages.Length==18);
            Check("Archive ids and URLs are unique",pages.Select(p=>p.id).Distinct().Count()==pages.Length && pages.Select(p=>p.url).Distinct().Count()==pages.Length);
            var urls=new HashSet<string>(pages.Select(p=>p.url));
            Check("Archive links resolve locally",pages.All(p=>p.links==null || p.links.All(l=>urls.Contains(l.url))));
            Check("Four archival illustrations load",pages.Where(p=>!string.IsNullOrEmpty(p.imageResource)).Count()==4 && pages.Where(p=>!string.IsNullOrEmpty(p.imageResource)).All(p=>Resources.Load<Texture2D>(p.imageResource)!=null));
            computer.OpenApp("browser"); computer.Navigate(pages[1].url); computer.Navigate(pages[2].url);
            Check("Browser back returns to previous article",computer.GoBack() && computer.CurrentUrl==pages[1].url);
            Check("Browser forward returns to next article",computer.GoForward() && computer.CurrentUrl==pages[2].url);
            computer.GoBack(); computer.Navigate(pages[3].url);
            Check("New navigation discards forward branch",!computer.GoForward());
            string note=computer.CreateDocument("QA_中文笔记.txt","这是自动测试文本。\nQ E M / saved locally.");
            Check("Create editable Unicode text",!string.IsNullOrEmpty(note) && computer.GetDocument(note).content.Contains("自动测试"));
            computer.UpdateEditorBuffer("QA_中文笔记.txt","中文编辑完成。\nQ E M / saved locally.");
            Check("Editor buffer saves changed content",computer.HasUnsavedChanges && computer.SaveCurrentDocument() && computer.GetDocument(note).content.Contains("中文编辑完成"));
            Check("Rename virtual document",computer.RenameDocument(note,"QA_航行日志.txt") && computer.GetDocument(note).name.StartsWith("QA_航行日志"));
            computer.Close(); computer.ReloadStore();
            Check("Document survives desktop close and disk reload",computer.GetDocument(note)!=null && computer.GetDocument(note).content.Contains("saved locally"));
            Check("Desktop close restores cabin input and sharp rendering",!computer.IsOpen && !controller.ComputerOpen && !presentation.computerBlur && !controller.IsPaused);
            computer.Open();
            Check("Trash preserves document content",computer.MoveToTrash(note) && computer.GetDocument(note).trashed && computer.GetDocument(note).content.Contains("中文编辑完成"));
            computer.ReloadStore(); Check("Trash state persists",computer.GetDocument(note).trashed);
            Check("Restore recovers document",computer.Restore(note) && !computer.GetDocument(note).trashed);
            Check("Live documents cannot be permanently deleted",!computer.DeletePermanently(note));
            computer.MoveToTrash(note); Check("Permanent deletion removes QA-created virtual note",computer.DeletePermanently(note) && computer.GetDocument(note)==null);
            computer.Close(); systems.SetMainPower(false);
            Check("Unpowered CRT cannot open desktop",!computer.Open() && !controller.ComputerOpen);
            systems.SetMainPower(true); computer.Open(); systems.SetMainPower(false); yield return null;
            Check("Power loss closes desktop safely",!computer.IsOpen && !presentation.computerBlur);
            systems.SetMainPower(true);
            yield return VerifyWindowView(computer,stars);
            yield return VerifyGaugeMotion();
            yield return VerifyPlanetRotation(stars);
            Check("No runtime exceptions",errors.Count==0,string.Join("\n",errors));
            results.Add("RESULT: "+(failed==0?"PASS":"FAIL")+"; failed="+failed);
            File.WriteAllLines(Path.Combine(output,"verification.txt"),results);
            Debug.Log("ASTRA_QA_COMPLETED failed="+failed);
            finished=true;
            Application.Quit(failed==0?0:2);
        }
        IEnumerator VerifyWindowView(CabinComputer computer,LoopingStarfield stars)
        {
            controller.SnapToWall(0);
            Check("Window approach is unavailable away from the observation wall",!controller.EnterWindowView() && !controller.IsObservingWindow);
            controller.SnapToWall(1);
            controller.SetPaused(true);
            Check("Paused cabin rejects window approach",!controller.EnterWindowView());
            controller.SetPaused(false);
            // Open through the public API while facing D so this exercises the
            // computer-modal guard, not the unrelated wrong-wall guard.
            computer.Open();
            Check("Desktop and window approach are mutually exclusive",!controller.EnterWindowView() && computer.IsOpen && controller.ComputerOpen);
            computer.Close(); controller.SnapToWall(0); controller.Turn(1);
            Check("Wall turn rejects window approach",!controller.EnterWindowView());
            yield return new WaitForSecondsRealtime(controller.turnDuration+.15f);
            controller.SnapToWall(1);

            var window=Item("OBS_Window");
            var windowCollider=window.GetComponent<BoxCollider>();
            Vector3 target=windowCollider.transform.TransformPoint(windowCollider.center);
            RaycastHit hit;
            bool rayHit=Physics.Raycast(controller.viewCamera.transform.position,(target-controller.viewCamera.transform.position).normalized,out hit,controller.interactionDistance,controller.interactionMask,QueryTriggerInteraction.Ignore);
            Check("Observation window is directly reachable by real camera ray",rayHit && hit.collider.GetComponentInParent<CabinInteractable>()==window,rayHit?hit.collider.name:"miss");
            controller.SetHoverForTest(window);
            yield return new WaitForSecondsRealtime(.08f);
            Check("Window frame receives yellow hover outline",window.IsHovered && window.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterial && r.sharedMaterial.shader.name=="Astra/Outline"));
            yield return Capture("12_D_WindowHover");
            Vector3 originalPosition=controller.viewCamera.transform.position;
            Quaternion originalRotation=controller.viewCamera.transform.rotation;
            float originalFov=controller.viewCamera.fieldOfView;
            Check("Clicking observation window starts approach",controller.ActivateHoveredForTest() && controller.IsObservingWindow && controller.IsObservationTransitioning);
            Check("Approach clears window hover",controller.Hovered==null && !window.IsHovered);
            yield return new WaitForSecondsRealtime(controller.observationDuration*.38f);
            Vector3 halfway=controller.viewCamera.transform.position;
            Check("Window camera approaches smoothly instead of teleporting",controller.ObservationBlend>0 && controller.ObservationBlend<1 && Vector3.Distance(halfway,originalPosition)>.02f && Vector3.Distance(halfway,controller.windowObservationPosition)>.02f);
            yield return Capture("13_D_WindowApproach");
            yield return new WaitForSecondsRealtime(controller.observationDuration+.15f);
            Check("Window approach reaches the authored eye position",controller.IsAtWindow && !controller.IsObservationTransitioning && Vector3.Distance(controller.viewCamera.transform.position,controller.windowObservationPosition)<.001f);
            Check("Window approach uses the observation field of view",Mathf.Abs(controller.viewCamera.fieldOfView-controller.windowFieldOfView)<.01f);
            Check("Window approach remains on the observation wall",controller.CurrentWall==1 && Mathf.Abs(Mathf.DeltaAngle(controller.CurrentYaw,90))<.01f);
            Check("Window mode rejects nested approach",!controller.EnterWindowView());
            Check("Window mode prevents opening the computer",!computer.Open() && !computer.IsOpen && !controller.ComputerOpen);
            var light=Item("ACT_LightSwitch"); int activations=light.ActivationCount;
            controller.SetHoverForTest(light);
            Check("Window mode prevents remote switch activation",!controller.ActivateHoveredForTest() && light.ActivationCount==activations);
            controller.SetHoverForTest(null);
            controller.SetLookForTest(Vector2.one);
            yield return new WaitForSecondsRealtime(.65f);
            Vector3 nearForward=controller.viewCamera.transform.forward;
            Check("Window mouse right and up move view in matching directions",nearForward.z<-.001f && nearForward.y>.001f);
            Check("Window mouse movement stays subtle",Vector3.Angle(Vector3.right,nearForward)<8);
            Check("Window mouse look does not translate through the glass",Vector3.Distance(controller.viewCamera.transform.position,controller.windowObservationPosition)<.001f);
            yield return Capture("14_D_WindowCloseView");

            stars.animate=true; var starBefore=stars.StarPositions[0];
            yield return new WaitForSecondsRealtime(.45f);
            Check("Exterior stars keep moving while observing",Vector3.Distance(starBefore,stars.StarPositions[0])>.005f);
            controller.SetLookForTest(Vector2.zero); yield return new WaitForSecondsRealtime(.65f);
            yield return Capture("14B_D_WindowCentered");
            controller.HandleEscape();
            Check("Escape starts a window return transition without pausing",controller.IsObservingWindow && controller.IsObservationTransitioning && !controller.IsPaused && Time.timeScale==1);
            yield return new WaitForSecondsRealtime(controller.observationDuration+.15f);
            Check("Leaving the window restores the precise central pose",!controller.IsObservingWindow && !controller.IsAtWindow && Vector3.Distance(controller.viewCamera.transform.position,originalPosition)<.001f && Quaternion.Angle(controller.viewCamera.transform.rotation,originalRotation)<.02f && Mathf.Abs(controller.viewCamera.fieldOfView-originalFov)<.01f);

            controller.EnterWindowView();
            yield return new WaitForSecondsRealtime(controller.observationDuration*.28f);
            Vector3 interruptedPosition=controller.viewCamera.transform.position;
            controller.HandleEscape();
            Check("Escape can reverse an unfinished approach without a teleport",controller.IsObservationTransitioning && !controller.IsPaused && Vector3.Distance(controller.viewCamera.transform.position,interruptedPosition)<.001f);
            yield return new WaitForSecondsRealtime(controller.observationDuration+.1f);
            Check("Interrupted approach returns precisely to the center",!controller.IsObservingWindow && Vector3.Distance(controller.viewCamera.transform.position,originalPosition)<.001f);

            bool noDrift=true;
            for(int cycle=0;cycle<3;cycle++)
            {
                noDrift &= controller.EnterWindowView();
                yield return new WaitForSecondsRealtime(controller.observationDuration+.08f);
                noDrift &= controller.IsAtWindow;
                controller.ExitWindowView();
                yield return new WaitForSecondsRealtime(controller.observationDuration+.08f);
                noDrift &= !controller.IsObservingWindow && Vector3.Distance(controller.viewCamera.transform.position,originalPosition)<.001f && Mathf.Abs(controller.viewCamera.fieldOfView-originalFov)<.01f;
            }
            Check("Repeated window approaches do not accumulate position or FOV drift",noDrift);
            foreach(int direction in new[]{-1,1})
            {
                controller.SnapToWall(1); controller.EnterWindowView();
                yield return new WaitForSecondsRealtime(controller.observationDuration+.08f);
                controller.Turn(direction);
                yield return new WaitForSecondsRealtime(controller.observationDuration*.3f);
                Check((direction<0?"Q":"E")+" returns from window before rotating",controller.CurrentWall==1 && !controller.IsTurning && controller.IsObservationTransitioning && Mathf.Abs(Mathf.DeltaAngle(controller.CurrentYaw,90))<.01f);
                yield return new WaitForSecondsRealtime(controller.observationDuration+controller.turnDuration+.2f);
                Check((direction<0?"Q":"E")+" completes one adjacent-wall turn after returning",controller.CurrentWall==(direction<0?0:2) && !controller.IsObservingWindow && !controller.IsTurning && Vector3.Distance(controller.viewCamera.transform.position,controller.centerPosition)<.001f);
            }
            controller.SnapToWall(1);
            yield return Capture("15_D_WindowReturned");
        }

        IEnumerator VerifyGaugeMotion()
        {
            var gauges=FindObjectsOfType<CabinGaugeMotion>().OrderBy(g=>g.name).ToArray();
            var expected=new[]{"GAUGE_A_O2","GAUGE_A_PWR","GAUGE_B_TEMP","GAUGE_D_PSI"};
            Check("All four instrument needles are independent animated objects",gauges.Length==4 && gauges.Select(g=>g.name).SequenceEqual(expected));
            if(gauges.Length==0) yield break;
            Check("Every gauge is initialized with a valid spindle axis",gauges.All(g=>g.Initialized && g.LocalAxis.sqrMagnitude>.99f));
            var positions=gauges.Select(g=>g.transform.localPosition).ToArray();
            var originalTimes=gauges.Select(g=>g.ElapsedSeconds).ToArray();
            bool bounded=true,rotating=true,reversible=true;
            foreach(var gauge in gauges)
            {
                float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
                for(int sample=0;sample<512;sample++)
                {
                    float value=gauge.EvaluateOffset(sample*13.37);
                    minimum=Mathf.Min(minimum,value); maximum=Mathf.Max(maximum,value);
                    bounded &= !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value)<=gauge.amplitudeDegrees+.001f;
                }
                gauge.SetElapsedForTest(10); Quaternion atTen=gauge.transform.localRotation;
                gauge.SetElapsedForTest(14);
                rotating &= Quaternion.Angle(atTen,gauge.transform.localRotation)>.05f && maximum-minimum>.5f;
                gauge.SetElapsedForTest(100000000);
                bounded &= Quaternion.Angle(gauge.RestLocalRotation,gauge.transform.localRotation)<=gauge.amplitudeDegrees+.03f;
                gauge.SetElapsedForTest(10);
                reversible &= Quaternion.Angle(atTen,gauge.transform.localRotation)<.03f;
            }
            Check("Gauge needles rotate as real geometry rather than changing a texture",rotating);
            Check("Gauge flutter stays within the authored small angular range",bounded && gauges.All(g=>g.amplitudeDegrees>0 && g.amplitudeDegrees<=5));
            Check("Long-running gauges return deterministically without angular drift",reversible);
            bool independent=true;
            for(int i=0;i<gauges.Length;i++) for(int j=i+1;j<gauges.Length;j++)
            {
                bool different=false;
                for(int sample=1;sample<=8;sample++) different |= Mathf.Abs(gauges[i].EvaluateOffset(sample*2.1)-gauges[j].EvaluateOffset(sample*2.1))>.05f;
                independent &= different;
            }
            Check("Gauge needles have independent non-synchronized movement",independent);
            Check("Gauge animation preserves all spindle positions",gauges.Select((g,i)=>Vector3.Distance(g.transform.localPosition,positions[i])<.000001f).All(same=>same));
            var before=gauges.Select(g=>g.transform.localRotation).ToArray();
            var maximumMovement=new float[gauges.Length];
            var liveTimes=gauges.Select(g=>g.ElapsedSeconds).ToArray();
            // A single two-frame sample can straddle a sinusoid turning point;
            // additionally Quaternion.Angle rounds sub-0.04-degree deltas to zero.
            // Observe a short interval and require actual mesh rotation + elapsed time.
            for(int sample=0;sample<8;sample++)
            {
                yield return new WaitForSecondsRealtime(.4f);
                for(int i=0;i<gauges.Length;i++) maximumMovement[i]=Mathf.Max(maximumMovement[i],Quaternion.Angle(gauges[i].transform.localRotation,before[i]));
            }
            Check("Gauge update loop animates all needles while the cabin runs",gauges.Select((g,i)=>maximumMovement[i]>.08f && g.ElapsedSeconds>liveTimes[i]+2).All(moved=>moved),string.Join(", ",gauges.Select((g,i)=>g.name+"="+maximumMovement[i].ToString("F3")+"deg")));
            controller.SetPaused(true);
            var pausedRotations=gauges.Select(g=>g.transform.localRotation).ToArray();
            var pausedTimes=gauges.Select(g=>g.ElapsedSeconds).ToArray();
            yield return new WaitForSecondsRealtime(.35f);
            Check("Pausing freezes gauge motion and simulation time",gauges.Select((g,i)=>Quaternion.Angle(g.transform.localRotation,pausedRotations[i])<.03f && Math.Abs(g.ElapsedSeconds-pausedTimes[i])<.000001).All(frozen=>frozen));
            controller.SetPaused(false);
            for(int i=0;i<gauges.Length;i++) gauges[i].SetElapsedForTest(originalTimes[i]);
            controller.SnapToWall(0);
            yield return Capture("16_A_AnimatedGauges");
        }

        IEnumerator VerifyPlanetRotation(LoopingStarfield stars)
        {
            var planet=FindObjectOfType<DistantPlanet>();
            Check("Exterior planet has a live rotation component",planet!=null);
            if(planet==null) yield break;
            Check("Planet rotates slowly at one revolution per five minutes",Mathf.Abs(planet.degreesPerSecond-1.2f)<.0001f);
            double originalElapsed=planet.ElapsedSeconds; bool originalAnimate=planet.animate;
            Vector3 originalPosition=planet.transform.localPosition,originalScale=planet.transform.localScale;
            planet.animate=true; planet.SetElapsedForTest(0);
            Quaternion firstRotation=planet.transform.localRotation;
            yield return new WaitForSecondsRealtime(.85f);
            Check("Planet Update visibly advances its real mesh rotation",planet.ElapsedSeconds>.5 && Quaternion.Angle(firstRotation,planet.transform.localRotation)>.5f);
            planet.animate=false;
            planet.SetElapsedForTest(60);
            float expectedAngle=planet.degreesPerSecond*60f;
            Quaternion expectedRotation=planet.RestRotation*Quaternion.AngleAxis(expectedAngle,Vector3.up);
            Check("Planet rotation follows the authored angular speed",Mathf.Abs(Mathf.DeltaAngle(planet.CurrentAngleDegrees,expectedAngle))<.01f && Quaternion.Angle(expectedRotation,planet.transform.localRotation)<.05f);
            planet.SetElapsedForTest(13.37); Quaternion atSample=planet.transform.localRotation;
            planet.SetElapsedForTest(100000000);
            float longExpected=(float)((100000000.0*(double)planet.degreesPerSecond)%360.0);
            bool longAccurate=Mathf.Abs(Mathf.DeltaAngle(planet.CurrentAngleDegrees,longExpected))<.02f;
            planet.SetElapsedForTest(13.37);
            Check("Long-running planet rotation is deterministic without accumulated drift",longAccurate && Quaternion.Angle(atSample,planet.transform.localRotation)<.05f);
            Check("Planet spins in place without changing position or size",Vector3.Distance(originalPosition,planet.transform.localPosition)<.000001f && Vector3.Distance(originalScale,planet.transform.localScale)<.000001f);
            planet.animate=true; controller.SetPaused(true);
            double pausedElapsed=planet.ElapsedSeconds; Quaternion pausedRotation=planet.transform.localRotation;
            yield return new WaitForSecondsRealtime(.35f);
            Check("Pausing freezes planet rotation",Math.Abs(planet.ElapsedSeconds-pausedElapsed)<.000001 && Quaternion.Angle(pausedRotation,planet.transform.localRotation)<.03f);
            controller.SetPaused(false);
            yield return new WaitForSecondsRealtime(.65f);
            Check("Resuming restarts planet rotation",planet.ElapsedSeconds>pausedElapsed+.4 && Quaternion.Angle(pausedRotation,planet.transform.localRotation)>.4f);

            controller.SnapToWall(1); controller.EnterWindowView(); controller.SetLookForTest(Vector2.zero);
            yield return new WaitForSecondsRealtime(controller.observationDuration+.7f);
            planet.animate=false; controller.SetPaused(true);
            // Remove moving foreground stars and freeze all simulation. This comparison
            // must measure the planet surface itself, not a changed star or camera pose.
            var starRenderers=stars.GetComponentsInChildren<Renderer>();
            var starVisibility=starRenderers.Select(r=>r.enabled).ToArray();
            foreach(var renderer in starRenderers) renderer.enabled=false;
            planet.SetElapsedForTest(0);
            yield return Capture("17_D_PlanetRotation_Start");
            Color32[] reference=RenderPlanetSample();
            yield return null;
            Color32[] repeat=RenderPlanetSample();
            float unchanged=PlanetChangedFraction(reference,repeat);
            Check("Planet image comparison has a stable fixed-camera baseline",unchanged<.01f,"changed="+unchanged.ToString("P2"));
            planet.SetElapsedForTest(60);
            yield return Capture("18_D_PlanetRotation_After60Seconds");
            Color32[] rotated=RenderPlanetSample();
            float changed=PlanetChangedFraction(reference,rotated);
            Check("Planet surface visibly rotates in the actual rendered window view",changed>.08f && changed>unchanged+.05f,"surface changed="+changed.ToString("P2")+"; baseline="+unchanged.ToString("P2"));
            for(int i=0;i<starRenderers.Length;i++) starRenderers[i].enabled=starVisibility[i];
            planet.SetElapsedForTest(originalElapsed); planet.animate=originalAnimate;
            controller.SetPaused(false); controller.ExitWindowView();
            yield return new WaitForSecondsRealtime(controller.observationDuration+.1f);
        }

        Color32[] RenderPlanetSample()
        {
            const int width=640,height=360;
            var camera=controller.viewCamera;
            var previousTarget=camera.targetTexture; var previousActive=RenderTexture.active;
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
                return image.GetPixels32();
            }
            finally
            {
                camera.targetTexture=previousTarget; RenderTexture.active=previousActive;
                RenderTexture.ReleaseTemporary(target); Destroy(image);
            }
        }

        static float PlanetChangedFraction(Color32[] first,Color32[] second)
        {
            const int width=640;
            int changed=0,total=0;
            // Bottom-origin pixels: a generous region inside the visible lower-right
            // planetary disk, excluding the window sill, UI, and upper empty sky.
            for(int y=72;y<162;y++) for(int x=288;x<544;x++)
            {
                int index=y*width+x;
                int difference=Math.Max(Math.Abs(first[index].r-second[index].r),Math.Max(Math.Abs(first[index].g-second[index].g),Math.Abs(first[index].b-second[index].b)));
                if(difference>8) changed++;
                total++;
            }
            return (float)changed/total;
        }

        CabinInteractable Item(string name) { return FindObjectsOfType<CabinInteractable>().First(i=>i.name==name); }
        void Check(string name,bool okay,string details="")
        {
            if(!okay) failed++;
            results.Add((okay?"PASS ":"FAIL ")+name+(details.Length>0?" | "+details:""));
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            // Hidden Windows players may not draw the swap chain; explicitly render the
            // real gameplay camera, including its image effect, into an offscreen target.
            const int width=1600,height=900;
            var camera=controller.viewCamera;
            var previousTarget=camera.targetTexture; var previousActive=RenderTexture.active;
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            camera.targetTexture=previousTarget; RenderTexture.active=previousActive;
            RenderTexture.ReleaseTemporary(target);
            var pixels=image.GetPixels32(); int minimum=255,maximum=0;
            for(int i=0;i<pixels.Length;i+=101) { int value=pixels[i].r+pixels[i].g+pixels[i].b; minimum=Math.Min(minimum,value); maximum=Math.Max(maximum,value); }
            Check("Rendered nonblank frame "+name,maximum-minimum>40);
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG()); Destroy(image);
        }
        void OnDestroy() { Application.logMessageReceived-=OnLog; }
    }
}
