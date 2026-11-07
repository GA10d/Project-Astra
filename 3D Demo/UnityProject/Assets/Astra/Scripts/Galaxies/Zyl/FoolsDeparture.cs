using System;
using UnityEngine;
using AstraCabin.Navigation;
namespace AstraCabin.Galaxies.Zyl
{
    // Survives SetpieceDirector.ResetToDefault: settlement must remain hidden until the window view is ready.
    [DisallowMultipleComponent]
    public sealed class FoolsDeparture : MonoBehaviour
    {
        public enum Step {Idle,FadeToBlack,WaitForBlack,WaitForWindow,FadeFromBlack}
        public bool Active {get;private set;}
        public float Alpha {get;private set;}
        public Step Phase {get;private set;}
        public bool Committed {get;private set;}
        public string Error {get;private set;}
        public int CommitFrame {get;private set;}=-1;
        NavigationMode navigation;NavigationSession session;string eventId,choiceId;
        ZylScreenCurtain curtain;float clock,duration;
        public bool Begin(NavigationMode owner,string choice)
        {
            if(Active)return false;Error=null;
            if(!owner||owner.ActiveGalaxyId!="zyl"||owner.Session==null||owner.Session.Blocked)return false;
            var pending=owner.Session.Engine.Pending;
            if(pending==null||pending.type!="setpiece"||pending.setpiece!=SetpieceDirector.FoolsMode||owner.Session.Engine.ChoiceError(choice)!=null)return false;
            navigation=owner;session=owner.Session;eventId=pending.id;choiceId=choice;curtain=owner.GetComponent<ZylScreenCurtain>();
            if(!curtain){Error="结算黑幕组件尚未就绪。";return false;}
            duration=owner.Director.fools.SettlementFadeDuration;
            Active=true;Committed=false;Error=null;CommitFrame=-1;clock=0;Alpha=.001f;Phase=Step.FadeToBlack;curtain.ResetPaintConfirmation();return true;
        }
        void Update(){Tick(Time.deltaTime);}
        public void Tick(float dt)
        {
            if(!Active||!navigation||navigation.Director.controller.IsPaused)return;
            if(float.IsNaN(dt)||float.IsInfinity(dt))return;dt=Mathf.Max(0,dt);clock+=dt;
            switch(Phase)
            {
                case Step.FadeToBlack:
                    Alpha=Mathf.Max(.001f,Mathf.SmoothStep(0,1,Mathf.Clamp01(clock/duration)));
                    if(clock>=duration){Alpha=1;Phase=Step.WaitForBlack;clock=0;}
                    break;
                case Step.WaitForBlack:
                    if(curtain.OpaquePaintedFrame<0||Time.frameCount<=curtain.OpaquePaintedFrame)return;
                    if(navigation.Session!=session||session.Engine.Pending==null||session.Engine.Pending.id!=eventId)
                    {Error="航程或事件已改变，本次结算已取消。";Phase=Step.FadeFromBlack;clock=0;return;}
                    if(!navigation.CommitFoolsDeparture(session,eventId,choiceId))
                    {Error=navigation.Message;Phase=Step.FadeFromBlack;clock=0;return;}
                    Committed=true;CommitFrame=Time.frameCount;
                    var controller=navigation.Director.controller;
                    navigation.Director.computer.Close();controller.ExitWindowView();controller.SnapToWall(1);controller.ShowNotice("");controller.cinematicWindowLock=true;
                    Phase=Step.WaitForWindow;clock=0;
                    break;
                case Step.WaitForWindow:
                    if(Time.frameCount<=CommitFrame||curtain.OpaquePaintedFrame<CommitFrame)return;
                    Phase=Step.FadeFromBlack;clock=0;
                    break;
                case Step.FadeFromBlack:
                    Alpha=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(clock/duration));
                    if(clock>=duration)Clear();
                    break;
            }
        }
        public void Clear(){Alpha=0;Active=false;Phase=Step.Idle;if(navigation&&Committed)navigation.Director.controller.cinematicWindowLock=false;}
        void OnDisable(){Clear();}
        void OnDestroy(){Clear();}
    }
}
