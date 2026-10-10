using System;
using System.Collections.Generic;
using UnityEngine;
using Astra.PerformanceToolV2;
using AstraCabin.Navigation;

namespace AstraCabin
{
    // The cabin remains the active scene; a private render layer isolates actors and lights.
    [DisallowMultipleComponent]
    public sealed partial class ConferenceMode : MonoBehaviour
    {
        const int Layer=30;
        public ToolPlayer Player {get;private set;}
        public bool HasSession => Player;
        public string EventId {get;private set;}
        public string Error {get;private set;}
        public bool UserPaused {get;private set;}
        public bool IsAdvancing => Player && computer.IsOpen && computer.CurrentApp=="meeting" && !controller.IsPaused && (focused||computer.Navigation.automationMode) && !UserPaused && Error==null;
        public RenderTexture Video => video;
        CabinComputer computer;CabinController controller;
        GameObject root;Camera lens;RenderTexture video;
        bool focused=true;string title,ending,pendingChoice;
        float elapsed;
        readonly Dictionary<Camera,int> cameras=new Dictionary<Camera,int>();
        readonly Dictionary<Light,int> lights=new Dictionary<Light,int>();
        void Awake(){computer=GetComponent<CabinComputer>();controller=GetComponent<CabinController>();}
        public void Begin(EventDefinition definition)
        {
            if(Player&&EventId==definition.id&&Error==null){UserPaused=false;return;}
            Stop();
            var package=Resources.Load<ToolPackage>("Performances/"+definition.performanceId);
            if(!package)throw new InvalidOperationException("找不到演出包 "+definition.performanceId+"，请同步 Tool V2 导出内容并重新构建");
            var failures=ToolValidation.Package(package);
            if(failures.Count>0)throw new InvalidOperationException(string.Join("；",failures));
            try
            {
                EventId=definition.id;title=definition.title;elapsed=0;UserPaused=false;Error=null;ending=null;ResetMeetingUI();
                foreach(var camera in FindObjectsOfType<Camera>()){cameras[camera]=camera.cullingMask;camera.cullingMask&=~(1<<Layer);}
                foreach(var light in FindObjectsOfType<Light>()){lights[light]=light.cullingMask;light.cullingMask&=~(1<<Layer);}
                root=new GameObject("ASTRA LINK performance");
                var cameraObject=new GameObject("Meeting video camera");cameraObject.transform.SetParent(root.transform,false);
                lens=cameraObject.AddComponent<Camera>();lens.enabled=false;lens.cullingMask=1<<Layer;lens.nearClipPlane=.05f;lens.farClipPlane=30;
                video=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){name="ASTRA LINK live video"};video.Create();lens.targetTexture=video;
                Player=root.AddComponent<ToolPlayer>();Player.autoPlay=false;Player.showGUI=false;Player.externalClock=true;Player.presentationLayer=Layer;Player.lens=lens;Player.package=package;
                Player.Completed+=OnCompleted;Player.Begin();
                if(Player.Failure!=null)throw new InvalidOperationException(Player.Failure);
                Render();
            }
            catch{Stop();throw;}
        }
        void OnCompleted(string node){ending=node;}
        void OnApplicationFocus(bool value){focused=value;}
        void Update()
        {
            if(!Player)return;
            Player.SetPaused(!IsAdvancing);
            if(!IsAdvancing){pendingChoice=null;return;}
            try
            {
                if(pendingChoice!=null){string slot=pendingChoice;pendingChoice=null;RecordReply(slot);Player.Choose(slot);}
                Player.Tick(Time.unscaledDeltaTime);elapsed+=Time.unscaledDeltaTime;TrackTranscript();
                if(Player.Failure!=null){Error=Player.Failure;return;}
                if(ending!=null){TryComplete();return;}
                Render();
            }
            catch(Exception ex){Error="会议已暂停："+ex.Message;}
        }
        void TryComplete()
        {
            if(!computer.Navigation.CompleteMeeting(EventId,ending))Error=computer.Navigation.Message;
        }
        void Render()
        {
            if(!lens)return;
            bool fog=RenderSettings.fog;
            try{RenderSettings.fog=false;lens.Render();}finally{RenderSettings.fog=fog;}
        }
        public void SetPaused(bool value){UserPaused=value;if(Player)Player.SetPaused(value||!IsAdvancing);}
        public void QueueChoice(string id){if(IsAdvancing)pendingChoice=id;}
        public void Stop()
        {
            if(Player){Player.Completed-=OnCompleted;Player.Stop();}
            if(root){root.SetActive(false);Destroy(root);}root=null;Player=null;lens=null;
            if(video){video.Release();Destroy(video);}video=null;
            foreach(var pair in cameras)if(pair.Key)pair.Key.cullingMask=pair.Value;cameras.Clear();
            foreach(var pair in lights)if(pair.Key)pair.Key.cullingMask=pair.Value;lights.Clear();
            EventId=null;ending=null;pendingChoice=null;Error=null;ResetMeetingUI();
        }
        void OnDestroy(){Stop();}
    }
}
