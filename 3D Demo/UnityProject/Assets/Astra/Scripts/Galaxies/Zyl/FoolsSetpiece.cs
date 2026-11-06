using System;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    public sealed class FoolsSetpiece : MonoBehaviour
    {
        public enum Step {WaitingForEnvironment,Playing,Finished,Error}
        public bool Active {get;private set;}
        public bool Complete {get;private set;}
        public string Error {get;private set;}
        public Step Phase {get;private set;}
        public FoolsStage Stage {get;private set;}
        public float Elapsed {get;private set;}
        SetpieceDirector director;
        bool capturedUI,oldUI;
        ZylStomachEnvironment Exterior {get{return director&&director.computer&&director.computer.Navigation?director.computer.Navigation.Exterior:null;}}
        public void Initialize(SetpieceDirector owner){director=owner;}
        public void Show()
        {
            Clear();Active=true;Phase=Step.WaitingForEnvironment;
            if(!director.computer.Navigation||director.computer.Navigation.ActiveGalaxyId!="zyl"){Fail("都是傻瓜：请先进入zyl胃内航程。");return;}
            director.computer.Close();director.controller.SnapToWall(1);director.controller.cinematicWindowLock=true;
        }
        public void Resume(){if(!Active||Complete)return;if(Phase==Step.Error){Show();return;}director.computer.Close();director.controller.cinematicWindowLock=true;}
        void Fail(string message){Error=message;Phase=Step.Error;RestoreView();director.controller.ShowNotice(message);}
        void Update(){Tick(Time.deltaTime);}
        public void Tick(float dt)
        {
            if(!Active||Complete||Phase==Step.Error)return;
            if(capturedUI)director.controller.showUI=oldUI&&director.controller.IsPaused;
            if(director.controller.IsPaused||director.computer.IsOpen)return;
            var exterior=Exterior;
            if(!exterior){Fail("都是傻瓜：胃内环境不可用。");return;}
            if(exterior.Error!=null){Fail(exterior.Error);return;}
            if(Phase==Step.WaitingForEnvironment)
            {
                if(!exterior.Ready||exterior.Busy||ZylScreenCurtain.BlocksInput)return;
                if(!exterior.MihuoerActor||!exterior.MihuoerShipAnchor){Fail("都是傻瓜：胃内飞船缺失。");return;}
                try
                {
                    exterior.MihuoerActor.SetSeatedOnCraft(true);exterior.MihuoerActor.Speaking=false;
                    Stage=new FoolsStage(exterior.MihuoerActor.Craft,exterior.MihuoerShipAnchor.transform.parent,exterior.AuthoredToRuntime,director.controller.viewCamera);
                    oldUI=director.controller.showUI;capturedUI=true;director.controller.showUI=false;Stage.Camera.enabled=true;Phase=Step.Playing;
                }
                catch(Exception e){Fail(e.Message);return;}
            }
            Elapsed=Mathf.Min(FoolsStage.Duration,Elapsed+Mathf.Max(0,dt));Stage.Sample(Elapsed);
            if(Elapsed>=FoolsStage.Duration)
            {
                Complete=true;Phase=Step.Finished;RestoreView();director.controller.ExitWindowView();director.controller.SnapToWall(0);
                director.computer.Open();director.computer.OpenApp("navigation");
            }
        }
        void RestoreView(){if(Stage!=null&&Stage.Camera)Stage.Camera.enabled=false;if(director){director.controller.cinematicWindowLock=false;if(capturedUI)director.controller.showUI=oldUI;}capturedUI=false;}
        public void Clear(){RestoreView();if(Stage!=null)Stage.Dispose();Stage=null;Active=Complete=false;Error=null;Elapsed=0;Phase=Step.WaitingForEnvironment;}
        void OnDisable(){Clear();}
        void OnDestroy(){Clear();}
    }
}
