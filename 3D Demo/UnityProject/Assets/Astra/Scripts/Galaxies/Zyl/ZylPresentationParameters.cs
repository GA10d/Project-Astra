using System;
using System.IO;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl {
 [Serializable] public sealed class ZylPresentationParameters {
  public int schemaVersion=1;
  public float stomachWaterHold=1.5f,stomachTurnDuration=6f;
  public MihuoerDialogueLine[] stomachDialogue=new[]{new MihuoerDialogueLine{text="我们被吞进它的肚子里了。",duration=5f,replies=new[]{"这里到底是什么地方？"}},new MihuoerDialogueLine{text="先别慌，我们得想办法出去。",duration=6f,replies=new[]{"先看看周围吧。"}}};
  public MihuoerDialogueLine[] freeDialogue; // null means legacy config; an explicit empty array means no free dialogue.
  public void NormalizeDialogue(){if(freeDialogue==null)freeDialogue=new[]{new MihuoerDialogueLine{text=explainText,duration=explainDuration},new MihuoerDialogueLine{text=thinkText,duration=600f}};}
  public string greetingText="你好，我是米霍尔。";
  public string playerReplyText="你要做什么？";
  public string explainText="我想……我可能要请你离开这里。我没有恶意，但是这件事说起来十分复杂，";
  public string thinkText="我需要想想这件事应该从哪说起？也许应该从十年前，我居住的地方非常糟糕……";
  public string concernText="你怎么了？你看起来好像不太好";
  public float repairAlarmDelay=3.0f;
  public float repairStrikeDuration=0.48f;
  public float repairImpactStrength=0.95f;
  public float repairImpactDuration=1.35f;
  public float portalApproachDuration=20.0f;
  public float portalInitialDistance=38.0f;
  public float portalNearDistance=2.8f;
  public float portalInitialOpening=0.2f;
  public float portalFinalOpening=1.65f;
  public float portalOpenStart=5.0f;
  public float portalOpenEnd=15.0f;
  public float portalWarpStart=21.5f;
  public float portalGateEnd=23.0f;
  public float portalArrival=32.0f;
  public float portalComplete=35.0f;
  public float portalTunnelSpeed=24.0f;
  public float portalShake=0.18f;
  public float planetDiameter0=46.0f;
  public float planetSpin0=1.2f;
  public float planetDiameter1=7.0f;
  public float planetSpin1=0.45f;
  public float planetDiameter2=4.5f;
  public float planetSpin2=0.18f;
  public float visitorArrivalDuration=4.0f;
  public float visitorInvitationTime=5.2f;
  public float actorBlendDuration=0.55f;
  public float explainDuration=10.0f;
  public float beastScale=640.0f;
  public float initialClearance=18000.0f;
  public float finalClearance=120.0f;
  public float approachDuration=44.0f;
  public float mouthTriggerDistance=16500.0f;
  public float mouthPlaybackSpeed=0.5f;
  public float swallowDistance=180.0f;
  public float swallowDuration=8.0f;
  public float swallowAdvance=160.0f;
  public float concernDistance=3750.0f;
  public float videoFov=72.0f;
  public float videoFarClip=40000.0f;
  public float starCount=1400.0f;
  public float starSize=42.0f;
  public float coldLight=0.42f;
  public float mouthSearchLight=2.2f;
  public float mouthBounceLight=0.65f;
  public float loadDistance=250.0f;
  public float blackHoldDuration=1.0f;
  public float fadeInDuration=2.0f;
  public static ZylPresentationParameters Load(bool enabled=true) {
   var result=new ZylPresentationParameters(); if(!enabled){result.NormalizeDialogue();return result;}
   try {var file=Path.Combine(Application.streamingAssetsPath,"Navigation/zyl/presentation.json");if(!File.Exists(file)){result.NormalizeDialogue();return result;}JsonUtility.FromJsonOverwrite(File.ReadAllText(file),result);result.Validate();return result;}
   catch(Exception e){Debug.LogError("zyl演出参数无效，整份回退默认值："+e.Message);var fallback=new ZylPresentationParameters();fallback.NormalizeDialogue();return fallback;}
  }
  public void Validate(){
   NormalizeDialogue();MihuoerDialogueLine.Validate(freeDialogue);MihuoerDialogueLine.Validate(stomachDialogue);Check("stomachWaterHold",stomachWaterHold,0,10);Check("stomachTurnDuration",stomachTurnDuration,1,30);
   CheckText("greetingText",greetingText,300);
   CheckText("playerReplyText",playerReplyText,160);
   CheckText("concernText",concernText,1000);
   if(schemaVersion!=1)throw new InvalidDataException("不支持的演出参数版本");
   Check("repairAlarmDelay",repairAlarmDelay,0.0f,15.0f);
   Check("repairStrikeDuration",repairStrikeDuration,0.2f,2.0f);
   Check("repairImpactStrength",repairImpactStrength,0.0f,1.0f);
   Check("repairImpactDuration",repairImpactDuration,0.05f,5.0f);
   Check("portalApproachDuration",portalApproachDuration,5.0f,60.0f);
   Check("portalInitialDistance",portalInitialDistance,10.0f,150.0f);
   Check("portalNearDistance",portalNearDistance,2.0f,8.0f);
   Check("portalInitialOpening",portalInitialOpening,0.05f,1.0f);
   Check("portalFinalOpening",portalFinalOpening,1.0f,4.0f);
   Check("portalOpenStart",portalOpenStart,0.0f,30.0f);
   Check("portalOpenEnd",portalOpenEnd,1.0f,50.0f);
   Check("portalWarpStart",portalWarpStart,5.0f,70.0f);
   Check("portalGateEnd",portalGateEnd,6.0f,75.0f);
   Check("portalArrival",portalArrival,10.0f,100.0f);
   Check("portalComplete",portalComplete,11.0f,120.0f);
   Check("portalTunnelSpeed",portalTunnelSpeed,1.0f,80.0f);
   Check("portalShake",portalShake,0.0f,1.0f);
   Check("planetDiameter0",planetDiameter0,1.0f,80.0f);
   Check("planetSpin0",planetSpin0,0.0f,10.0f);
   Check("planetDiameter1",planetDiameter1,1.0f,80.0f);
   Check("planetSpin1",planetSpin1,0.0f,10.0f);
   Check("planetDiameter2",planetDiameter2,1.0f,80.0f);
   Check("planetSpin2",planetSpin2,0.0f,10.0f);
   Check("visitorArrivalDuration",visitorArrivalDuration,1.0f,15.0f);
   Check("visitorInvitationTime",visitorInvitationTime,1.0f,30.0f);
   Check("actorBlendDuration",actorBlendDuration,0.1f,2.0f);
   Check("beastScale",beastScale,80.0f,1000.0f);
   Check("initialClearance",initialClearance,1000.0f,30000.0f);
   Check("finalClearance",finalClearance,20.0f,200.0f);
   Check("approachDuration",approachDuration,15.0f,120.0f);
   Check("mouthTriggerDistance",mouthTriggerDistance,1000.0f,30000.0f);
   Check("mouthPlaybackSpeed",mouthPlaybackSpeed,0.2f,1.0f);
   Check("swallowDistance",swallowDistance,30.0f,1000.0f);
   Check("swallowDuration",swallowDuration,1.0f,20.0f);
   Check("swallowAdvance",swallowAdvance,10.0f,600.0f);
   Check("concernDistance",concernDistance,1500.0f,10000.0f);
   Check("videoFov",videoFov,40.0f,100.0f);
   Check("videoFarClip",videoFarClip,20000.0f,80000.0f);
   Check("starCount",starCount,100.0f,4000.0f);
   Check("starSize",starSize,5.0f,100.0f);
   Check("coldLight",coldLight,0.0f,2.0f);
   Check("mouthSearchLight",mouthSearchLight,0.3f,5.0f);
   Check("mouthBounceLight",mouthBounceLight,0.0f,3.0f);
   Check("loadDistance",loadDistance,50.0f,1500.0f);
   Check("blackHoldDuration",blackHoldDuration,0.3f,5.0f);
   Check("fadeInDuration",fadeInDuration,0.5f,8.0f);
   if(starCount!=Mathf.Floor(starCount))throw new InvalidDataException("starCount必须是整数");
   if(finalClearance>=swallowDistance)throw new InvalidDataException("finalClearance必须小于swallowDistance");
   if(finalClearance>=loadDistance)throw new InvalidDataException("finalClearance必须小于loadDistance");
   if(swallowDistance>=loadDistance)throw new InvalidDataException("swallowDistance必须小于loadDistance");
   if(loadDistance>=initialClearance)throw new InvalidDataException("loadDistance必须小于initialClearance");
   if(portalOpenStart>=portalOpenEnd)throw new InvalidDataException("portalOpenStart必须小于portalOpenEnd");
   if(portalOpenEnd>=portalApproachDuration)throw new InvalidDataException("portalOpenEnd必须小于portalApproachDuration");
   if(portalApproachDuration>=portalWarpStart)throw new InvalidDataException("portalApproachDuration必须小于portalWarpStart");
   if(portalWarpStart>=portalGateEnd)throw new InvalidDataException("portalWarpStart必须小于portalGateEnd");
   if(portalGateEnd>=portalArrival)throw new InvalidDataException("portalGateEnd必须小于portalArrival");
   if(portalArrival>=portalComplete)throw new InvalidDataException("portalArrival必须小于portalComplete");
   if(portalNearDistance>=portalInitialDistance)throw new InvalidDataException("portalNearDistance必须小于portalInitialDistance");
   if(portalInitialOpening>=portalFinalOpening)throw new InvalidDataException("portalInitialOpening必须小于portalFinalOpening");
   if(visitorArrivalDuration>=visitorInvitationTime)throw new InvalidDataException("visitorArrivalDuration必须小于visitorInvitationTime");
  }
  static void CheckText(string key,string value,int max){
   if(string.IsNullOrWhiteSpace(value)||value.Length>max)throw new InvalidDataException(key+"台词不能为空且不能超过"+max+"个UTF-16字符");
   foreach(char c in value)if(char.IsControl(c)&&c!='\n'&&c!='\r'&&c!='\t')throw new InvalidDataException(key+"含不支持的控制字符");
  }
  static void Check(string key,float value,float min,float max){if(float.IsNaN(value)||float.IsInfinity(value)||value<min||value>max)throw new InvalidDataException(key+"超出允许范围");}
 }
}
