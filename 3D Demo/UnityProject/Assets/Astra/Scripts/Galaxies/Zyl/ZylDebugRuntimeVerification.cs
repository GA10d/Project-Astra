#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AstraCabin.Navigation;
namespace AstraCabin.Galaxies.Zyl
{
    // Opt-in editor QA, instantiated only by the explicit Temp request consumed by the editor verifier.
    public sealed class ZylDebugRuntimeVerification : MonoBehaviour
    {
        string output;
        readonly List<string> checks=new List<string>();
        int failed;
        bool finishing,probe;
        NavigationMode nav;
        SetpieceDirector d;
        public void Begin(string folder){output=folder;Application.logMessageReceived+=OnLog;StartCoroutine(Run());}
        void Check(string name,bool ok){checks.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failed++;File.WriteAllLines(Path.Combine(output,"play-checks.txt"),checks);}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Exception){Check("runtime exception: "+message,false);Finish();}}
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        void Finish(){if(finishing)return;finishing=true;File.WriteAllText(Path.Combine(output,"play-result.txt"),(failed==0?"PASS":"FAIL")+" checks="+checks.Count);EditorApplication.isPlaying=false;}
        IEnumerator Capture(string name,bool expectBlack=false)
        {
            yield return new WaitForEndOfFrame();
            var rt=RenderTexture.GetTemporary(Screen.width,Screen.height,24);var old=RenderTexture.active;
            var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
                if(expectBlack){int max=0;foreach(var c in image.GetPixels32())max=Math.Max(max,Math.Max(c.r,Math.Max(c.g,c.b)));Check(name+" all RGB pixels <=2 (max="+max+")",max<=2);}
            }
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(image);}
        }
        void OnGUI()
        {
            if(!probe)return;GUI.depth=-1000;var color=GUI.color;var matrix=GUI.matrix;bool enabled=GUI.enabled;
            GUI.enabled=true;GUI.matrix=Matrix4x4.identity;GUI.color=Color.magenta;
            GUI.DrawTexture(new Rect(0,0,Screen.width,42),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width-42,0,42,Screen.height),Texture2D.whiteTexture);
            GUI.color=color;GUI.matrix=matrix;GUI.enabled=enabled;
        }
        IEnumerator Run()
        {
            yield return null;
            d=FindObjectOfType<SetpieceDirector>();nav=d.computer.Navigation;
            nav.automationMode=true;d.automationMode=true;d.controller.automationMode=true;d.computer.automationMode=true;
            Check("editor sandbox enabled",ZylEditorDebugSession.Enabled);
            Check("computer storage sandboxed",d.computer.PersistencePath.StartsWith(ZylEditorDebugSession.Root));
            var campaign=nav.Parse(File.ReadAllText(nav.GalaxyImportPath("zyl")));
            var guest=campaign.sectors.SelectMany(s=>s.nodes.Select(n=>new{sector=s,node=n})).First(x=>x.node.id=="guest");
            var routes=ZylDebugRoutePlanner.FindRoutes(campaign,guest.sector.id,"guest");Check("guest has four branch routes",routes.Count==4);
            var originalController=d.controller;var originalComputer=d.computer;
            foreach(var route in routes)
            {
                var request=ZylDebugRoutePlanner.Build(campaign,route);var engine=new NavigationEngine(campaign,EventRegistry.CreateDefault());
                bool valid=true;foreach(var command in request.prefix.commands){string error;valid&=engine.Apply(command,out error);}
                Check("prefix valid: "+route.label,valid&&engine.Pending==null&&engine.EngineBlocked&&!request.prefix.commands.Any(c=>c.kind=="observed"&&c.id=="zyl_guest"));
            }
            yield return nav.EditorApplyZylDebug(ZylDebugRoutePlanner.Build(campaign,routes.Last()));
            Check("selected beacon starts real encounter",nav.EditorDebugError==null&&nav.Session.Engine.Node.id=="guest"&&nav.Session.Engine.Pending.id=="zyl_guest"&&!nav.Session.Engine.Observed&&d.encounter.WaitingForView);
            Check("chosen route retained",routes.Last().steps.Where(s=>s.nodeId!="guest").All(s=>nav.Session.Engine.Visited.Contains(s.sectorId+"/"+s.nodeId)));
            Check("navigation storage sandboxed",nav.SavePath.StartsWith(ZylEditorDebugSession.Root));
            Check("blocked engine restored",nav.Session.Engine.EngineBlocked);
            d.encounter.View();d.encounter.AcceptCall();d.encounter.Reply();yield return new WaitForSeconds(.2f);
            yield return nav.EditorApplyZylDebug(ZylDebugRoutePlanner.Replay(campaign,nav.Session));
            Check("replay resets active event to entry",nav.EditorDebugError==null&&d.encounter.WaitingForView&&d.encounter.StoryTime==0&&!nav.Session.Engine.Observed);
            d.encounter.View();d.encounter.AcceptCall();d.encounter.Reply();
            yield return new WaitUntil(()=>d.encounter.LoadingEnvironment);
            probe=true;Check("curtain blocks input",ZylScreenCurtain.BlocksInput&&ZylScreenCurtain.IsOpaque);
            yield return Capture("01-black-call-and-probe",true);
            d.computer.Close();d.controller.SetPaused(true);
            yield return Capture("02-black-hud-and-pause",true);
            d.controller.SetPaused(false);probe=false;
            yield return new WaitUntil(()=>d.encounter.Complete);
            Check("ready before fade",nav.Exterior.Ready&&!d.computer.IsOpen&&d.controller.CurrentWall==1);
            yield return new WaitUntil(()=>d.encounter.TransitionAlpha<.6f);
            Check("fade runs after load",d.encounter.TransitionAlpha>0&&ZylScreenCurtain.BlocksInput);
            yield return Capture("03-stomach-fade");
            yield return new WaitUntil(()=>d.encounter.TransitionAlpha<=0);
            yield return Capture("04-stomach-revealed");
            Check("same ship retained",d.controller==originalController&&d.computer==originalComputer&&FindObjectsOfType<CabinController>().Length==1);
            Check("input released after fade",!ZylScreenCurtain.BlocksInput);
            yield return nav.EditorApplyZylDebug(ZylDebugRoutePlanner.Replay(campaign,nav.Session));
            Check("replay after swallow unloads stomach",nav.EditorDebugError==null&&!nav.Exterior.Ready&&!nav.Exterior.Busy&&d.encounter.WaitingForView);
            var prisoner=ZylDebugRoutePlanner.FindRoutes(campaign,guest.sector.id,"prisoner").First();
            yield return nav.EditorApplyZylDebug(ZylDebugRoutePlanner.Build(campaign,prisoner));
            Check("prisoner starts with stomach restored",nav.EditorDebugError==null&&nav.Session.Engine.Node.id=="prisoner"&&nav.Exterior.Ready&&nav.Session.Engine.Pending.type=="choice");
            var failure=ZylDebugRoutePlanner.FindRoutes(campaign,guest.sector.id,"engine_failure").First();
            yield return nav.EditorApplyZylDebug(ZylDebugRoutePlanner.Build(campaign,failure));
            Check("jump backwards cleans environment",nav.EditorDebugError==null&&!nav.Exterior.Ready&&d.ActiveMode==10&&d.CrisisActive&&!nav.Session.Engine.Observed);
            yield return nav.EditorApplyZylDebug(ZylDebugRoutePlanner.Replay(campaign,nav.Session));
            Check("repair replay resets three targets",nav.EditorDebugError==null&&d.CrisisActive&&d.RepairedCount==0&&d.RepairPoints.Length==3);
            Finish();
        }
    }
}
#endif
