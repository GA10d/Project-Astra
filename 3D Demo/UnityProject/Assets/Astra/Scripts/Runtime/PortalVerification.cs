using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace AstraCabin {
public sealed class PortalVerification:MonoBehaviour {
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--astra-portal-qa")>=0)new GameObject("Portal QA").AddComponent<PortalVerification>();}
 IEnumerator Capture(string path){yield return new WaitForEndOfFrame();var camera=FindObjectOfType<SetpieceDirector>().controller.viewCamera;var old=camera.targetTexture;var active=RenderTexture.active;var target=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(image);}}
 List<string> checks=new List<string>();int failures;string output;
 void Check(string name,bool ok){checks.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
 IEnumerator Start(){var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"--astra-portal-qa")+1];Directory.CreateDirectory(output);yield return new WaitForSeconds(2);var d=FindObjectOfType<SetpieceDirector>();d.automationMode=true;d.controller.automationMode=true;d.computer.automationMode=true;
 var nav=d.computer.Navigation;nav.automationMode=true;nav.EnterGalaxy("zyl");
 var campaign=nav.Parse(File.ReadAllText(nav.GalaxyImportPath("zyl")));foreach(var e in campaign.events)if(e.id!="zyl_drifter"){e.type="choice";e.setpiece=0;e.duration=0;}
 Check("isolated QA storage",d.computer.IsTestStorage&&nav.SavePath.StartsWith(output));Check("import portal fixture",nav.ImportCampaign(JsonUtility.ToJson(campaign)));Check("reach first beacon",nav.Travel("engine_failure")&&nav.Choose("confirm"));Check("reach prayer beacon",nav.Travel("last_supper")&&nav.Choose("confirm"));Check("beacon starts mode 11",nav.Travel("drifter")&&d.ActiveMode==11);Check("cannot settle before View",!nav.Choose("confirm"));yield return new WaitForSeconds(1);Check("computer prompt waits for View",d.computer.IsOpen&&!d.portal.Started&&!d.portal.Complete);yield return Capture(Path.Combine(output,"01-prompt.png"));yield return new WaitForSeconds(1);
 if(Array.IndexOf(args,"--astra-portal-ui-check")>=0){yield return new WaitUntil(()=>d.portal.Started);yield return new WaitUntil(()=>d.portal.Complete);yield return new WaitForSeconds(1);d.computer.Open();d.computer.OpenApp("navigation");yield return new WaitForSecondsRealtime(120);Application.Quit();yield break;}
 d.portal.View();Check("View closes computer and enters window",!d.computer.IsOpen&&d.controller.IsObservingWindow);yield return new WaitForSeconds(2);yield return Capture(Path.Combine(output,"01-initial-portal.png"));d.controller.SetPaused(true);float time=d.portal.Elapsed;yield return new WaitForSecondsRealtime(1);Check("pause stops cinematic",Mathf.Abs(time-d.portal.Elapsed)<.01f);d.controller.SetPaused(false);
 yield return new WaitForSeconds(9);yield return Capture(Path.Combine(output,"02-opening.png"));yield return new WaitForSeconds(10);yield return Capture(Path.Combine(output,"03-absorption.png"));yield return new WaitForSeconds(.8f);yield return Capture(Path.Combine(output,"03a-transition.png"));Check("stars hidden during portal tunnel overlap",!d.stars.gameObject.activeSelf);yield return new WaitForSeconds(1);yield return Capture(Path.Combine(output,"03b-transition.png"));yield return new WaitForSeconds(3.2f);yield return Capture(Path.Combine(output,"04-transit.png"));Check("cannot settle during transit",!d.portal.Complete);yield return new WaitForSeconds(9);Check("arrival completes",d.portal.Complete);yield return null;Check("navigation observation committed",nav.Session.Engine.Observed);Check("crater texture loaded",Resources.Load<Texture2D>("PortalCraterAlbedo")!=null);yield return Capture(Path.Combine(output,"05-arrival.png"));var near=GameObject.Find("Near red planet").transform;var mid=GameObject.Find("Distant planet 1").transform;var far=GameObject.Find("Distant planet 2").transform;
 Check("three planets in destination",near.parent.childCount==3);
 Quaternion nr=near.localRotation,mr=mid.localRotation,fr=far.localRotation;
 yield return new WaitForSeconds(5);float ns=Quaternion.Angle(nr,near.localRotation),ms=Quaternion.Angle(mr,mid.localRotation),fs=Quaternion.Angle(fr,far.localRotation);
 Check("slow rotation continues after completion",ns>4&&ns<8&&ms>1&&fs>.3f);
 Check("near rotation faster than distant",ns>ms&&ms>fs);
 yield return Capture(Path.Combine(output,"06-arrival-spin.png"));
 d.controller.SetPaused(true);nr=near.localRotation;yield return new WaitForSecondsRealtime(.5f);Check("planet spin pauses",Quaternion.Angle(nr,near.localRotation)<.01f);d.controller.SetPaused(false);
 d.ResetToDefault();yield return null;Check("reset restores stars and default planet",d.stars.gameObject.activeSelf&&d.planet.gameObject.activeSelf&&!d.portal.Started);Check("replay available",d.TriggerMode(11)&&!d.portal.Complete);d.portal.View();d.TriggerMode(3);Check("switch to original repair cleans portal",d.CrisisActive&&!d.portal.Started);d.ResetToDefault();checks.Add("RESULT "+(failures==0?"PASS":"FAIL")+" checks="+(checks.Count));File.WriteAllLines(Path.Combine(output,"portal-verification.txt"),checks);Application.Quit(failures==0?0:1);
 }
}
}



