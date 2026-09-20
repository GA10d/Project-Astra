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
            if(output!=null && !finished && Time.realtimeSinceStartup-started>100)
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
            Check("Seven independently interactive objects",FindObjectsOfType<CabinInteractable>().Length==7);
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
            Check("All seven controls accessible by real raycast",hittable.Count==7,"hit="+string.Join(",",hittable));
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
            Check("No runtime exceptions",errors.Count==0,string.Join("\n",errors));
            results.Add("RESULT: "+(failed==0?"PASS":"FAIL")+"; failed="+failed);
            File.WriteAllLines(Path.Combine(output,"verification.txt"),results);
            Debug.Log("ASTRA_QA_COMPLETED failed="+failed);
            finished=true;
            Application.Quit(failed==0?0:2);
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
