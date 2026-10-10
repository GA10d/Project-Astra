using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Astra.PerformanceToolV2;
using AstraCabin.Navigation;

namespace AstraCabin
{
    public sealed class MeetingVerification : MonoBehaviour
    {
        string output;readonly List<string> lines=new List<string>();int failures,errors;bool finished;float start;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--astra-meeting-qa")>=0||Array.IndexOf(args,"--astra-meeting-demo")>=0)
                FindObjectOfType<CabinComputer>().gameObject.AddComponent<MeetingVerification>();
        }
        void Check(string title,bool ok){lines.Add((ok?"PASS ":"FAIL ")+title);if(!ok)failures++;}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;lines.Add("ERROR "+message+"\n"+trace);}}
        void Update(){if(output!=null&&!finished&&Time.realtimeSinceStartup-start>150){Check("Watchdog",false);Finish();}}
        void Finish(){if(finished)return;finished=true;lines.Add("RESULT "+(failures+errors==0?"PASS":"FAIL")+" failures="+failures+" errors="+errors);File.WriteAllLines(Path.Combine(output,"meeting-verification.txt"),lines);Application.logMessageReceived-=Log;Application.Quit(failures+errors==0?0:3);}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--astra-meeting-qa");
            yield return null;
            var computer=GetComponent<CabinComputer>();var controller=GetComponent<CabinController>();var nav=computer.Navigation;var meeting=computer.Meeting;
            nav.Initialize();
            if(index<0){controller.SnapToWall(0);computer.Open();computer.OpenApp("navigation");yield break;}
            output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(output);start=Time.realtimeSinceStartup;Application.logMessageReceived+=Log;
            Application.runInBackground=true;Application.targetFrameRate=60;controller.automationMode=true;nav.automationMode=true;
            Screen.SetResolution(1600,900,false);yield return new WaitForSecondsRealtime(1);
            Check("QA saves isolated",computer.IsTestStorage&&nav.SavePath.StartsWith(output));
            Check("Campaign and imported ToolPackage validate",nav.Session!=null&&ToolValidation.Package(Resources.Load<ToolPackage>("Performances/relay")).Count==0);
            if(nav.Session==null){Finish();yield break;}
            nav.Restart();controller.SnapToWall(0);computer.Open();computer.OpenApp("navigation");yield return Capture("01_Map");
            int fuel=nav.Session.Engine.Balances["fuel"],data=nav.Session.Engine.Balances["data"];
            int mask=Camera.main.cullingMask;Vector3 position=Camera.main.transform.position;
            Check("Warp opens meeting inside terminal",nav.Travel("conference")&&computer.IsOpen&&computer.CurrentApp=="meeting"&&meeting.HasSession);
            Check("Travel charged exactly once",nav.Session.Engine.Balances["fuel"]==fuel-1);
            Check("No early reward",!nav.CompleteMeeting("relay_meeting","published")&&!nav.Session.Commit("observed","relay_meeting")&&!nav.Choose("complete")&&nav.Session.Engine.Balances["data"]==data);
            yield return new WaitForSecondsRealtime(1.5f);yield return Capture("02_Conference");
            Check("Speaker view defaults to full video without a sidebar",meeting.SidePanel=="");
            int transcriptCount=meeting.TranscriptCount;
            meeting.TogglePanel("participants");yield return Capture("08_Participants_Drawer");
            meeting.TogglePanel("participants");Check("Participants drawer can close",meeting.SidePanel=="");
            meeting.TogglePanel("chat");yield return Capture("09_Meeting_Chat");
            Check("Chat contains the actual performed dialogue",meeting.TranscriptCount>0&&meeting.LastTranscriptText.Contains("探测舱"));
            meeting.TogglePanel("chat");meeting.ToggleCaptions();yield return Capture("10_Captions_Off");
            Check("Caption toggle preserves the session and transcript",!meeting.CaptionsVisible&&meeting.HasSession&&meeting.TranscriptCount>=transcriptCount);
            meeting.ToggleCaptions();meeting.SetPaused(false);
            var player=meeting.Player;float time=player.CurrentTime;
            computer.OpenApp("browser");yield return new WaitForSecondsRealtime(.3f);Check("Switching apps freezes timeline",Mathf.Abs(time-player.CurrentTime)<.05f);
            computer.OpenApp("meeting");meeting.SetPaused(true);time=player.CurrentTime;yield return new WaitForSecondsRealtime(.3f);Check("Manual pause freezes timeline",Mathf.Abs(time-player.CurrentTime)<.01f);
            computer.Close();yield return new WaitForSecondsRealtime(.3f);Check("Exiting terminal retains unresolved meeting",meeting.HasSession&&nav.Session.Engine.Pending!=null&&Mathf.Abs(time-player.CurrentTime)<.01f);
            computer.Open();nav.BeginPresentation();player.Seek(5.1f);yield return null;
            meeting.ToggleCaptions();yield return Capture("11_Choice_With_Captions_Off");
            Check("Disabling captions does not hide interactive choices",!meeting.CaptionsVisible&&player.IsChoosing&&player.CurrentChoices.Length==2);
            meeting.ToggleCaptions();yield return Capture("03_Interactive_Choice");
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);yield return Capture("04_Conference_720p");Screen.SetResolution(1600,900,false);
            player.Stats["credits"]=0;meeting.QueueChoice("repair");yield return null;yield return null;Check("Unaffordable choice stays on current node",player.CurrentNodeId=="opening");
            player.Stats["credits"]=3;meeting.QueueChoice("repair");yield return null;yield return null;Check("Choice effects and condition branch use ToolPlayer",player.CurrentNodeId=="repair"&&player.Stats["credits"]==1);
            Check("Selected reply appears in meeting chat",meeting.LastTranscriptText=="使用维修券，恢复中继");
            yield return new WaitForSecondsRealtime(7.4f);Check("Global choice is interactive",player.CurrentNodeId=="archive"&&player.IsChoosing);yield return Capture("05_Final_Choice");
            Directory.CreateDirectory(nav.SavePath+".tmp");
            meeting.QueueChoice("publish");yield return null;yield return null;yield return null;
            Check("Failed durable save retains meeting and no reward",meeting.HasSession&&meeting.Error!=null&&nav.Session.Engine.Pending!=null&&nav.Session.Engine.Balances["data"]==data);
            Directory.Delete(nav.SavePath+".tmp");
            Check("Completed result can retry without replay",nav.CompleteMeeting("relay_meeting","published"));
            yield return null;
            Check("End closes software and returns navigation",!meeting.HasSession&&computer.IsOpen&&computer.CurrentApp=="navigation"&&nav.Session.Engine.Pending==null);
            Check("Completion reward committed once",nav.Session.Engine.Balances["data"]==data+3&&!nav.Session.Commit("performance","relay_meeting","published"));
            Check("Private rendering cleaned up",Camera.main.cullingMask==mask&&Camera.main.transform.position==position&&meeting.Video==null);
            var loaded=new NavigationSession(nav.Campaign,nav.Registry,nav.SavePath);
            Check("Completion and result survive reload",!loaded.Blocked&&loaded.Engine.Pending==null&&loaded.Engine.Balances["data"]==data+3&&loaded.Save.commands.Last().outcome=="published");
            yield return Capture("06_Back_To_Map");
            nav.Restart();computer.OpenApp("navigation");nav.Travel("conference");yield return null;player=meeting.Player;
            player.Seek(18.9f);yield return new WaitForSecondsRealtime(.3f);Check("Silence follows authored branch",player.CurrentNodeId=="contact");yield return new WaitForSecondsRealtime(1);yield return Capture("07_Alien_Branch");
            player.Seek(6.9f);yield return new WaitForSecondsRealtime(.3f);Check("Alternate branch reaches final choice",player.CurrentNodeId=="archive");
            player.Tick(25);yield return null;yield return null;
            Check("Global silence reaches End and settles",!meeting.HasSession&&nav.Session.Engine.Pending==null&&nav.Session.Save.commands.Last().outcome=="deferred");
            nav.Restart();nav.Travel("conference");yield return null;
            Check("Reload leaves interrupted meeting pending",new NavigationSession(nav.Campaign,nav.Registry,nav.SavePath).Engine.Pending?.id=="relay_meeting");
            player=meeting.Player;var broken=ScriptableObject.CreateInstance<ToolGraph>();broken.entryNode="bad";broken.nodes.Add(new ToolGraphNode{id="bad",kind=ToolNodeKind.Condition});
            var package=ScriptableObject.CreateInstance<ToolPackage>();package.library=player.package.library;package.graph=broken;player.package=package;player.Begin();yield return null;
            Check("Broken graph cannot complete event",!player.IsFinished&&player.Failure!=null&&nav.Session.Engine.Pending!=null);
            nav.Restart();Destroy(package);Destroy(broken);yield return null;
            // Keep existing cabin setpieces routed to the external observation flow.
            computer.Open();computer.OpenApp("navigation");Check("Existing setpiece integration retained",nav.Travel("signal_a")&&!computer.IsOpen&&nav.Director.ActiveMode==1);
            nav.Restart();Finish();
        }
        IEnumerator Capture(string name)
        {
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);
        }
    }
}
