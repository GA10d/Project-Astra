using System;
using UnityEngine;
using UnityEngine.Events;
using AstraCabin.Navigation;
namespace AstraCabin.Galaxies.Zyl
{
    /// <summary>One clock per active zyl voyage. Presentation objects are read-only views of this service.</summary>
    [DisallowMultipleComponent]
    public sealed class MihuoerCountdown : MonoBehaviour
    {
        NavigationMode navigation;
        NavigationSession bound;
        MihuoerCountdownClock clock;
        float saveElapsed;
        bool dirty;
        public string Error {get;private set;}
        public UnityEvent ReachedZero=new UnityEvent();
        public MihuoerCountdownClock Clock {get{EnsureBound();return clock;}}
        public ZylPresentationParameters Parameters {get;private set;}
        public void Initialize(NavigationMode owner){navigation=owner;}
        public void ReloadParameters(){Parameters=ZylPresentationParameters.Load();}
        bool CanFlush()
        {
            return MihuoerCountdownPersistence.CanFlush(bound,navigation?navigation.Session:null,clock);
        }
        void EnsureBound()
        {
            if(!navigation)return;
            var session=navigation.ActiveGalaxyId=="zyl"?navigation.Session:null;
            if(session==bound&&clock!=null&&ReferenceEquals(session.Save.mihuoerCountdown,clock.State))return;
            if(CanFlush())Flush();bound=null;clock=null;
            if(session==null||session.Blocked)return;
            Parameters=ZylPresentationParameters.Load();bound=session;
            var state=session.Save.mihuoerCountdown??new MihuoerCountdownSnapshot{remaining=Parameters.countdownInitialSeconds,visible=Parameters.countdownInitiallyVisible>.5f};
            clock=new MihuoerCountdownClock(state);session.Save.mihuoerCountdown=state;dirty=true;saveElapsed=0;
        }
        void Update()
        {
            EnsureBound();if(clock==null)return;
            var controller=navigation.Director?navigation.Director.controller:null;
            if(controller&&controller.IsPaused)return;
            bool ticking=clock.State.running;
            if(clock.Tick(Time.deltaTime)){dirty=true;ReachedZero.Invoke();}
            if(ticking)dirty=true;
            saveElapsed+=Time.unscaledDeltaTime;if(dirty&&saveElapsed>=5)Flush();
        }
        public bool Apply(CountdownAction action,string onceKey=null)
        {
            EnsureBound();if(clock==null)return false;
            try
            {
                bool changed=clock.Apply(action,onceKey);if(changed){dirty=true;if(clock.CheckZeroReached())ReachedZero.Invoke();Flush();}return changed;
            }
            catch(Exception e){Error=e.Message;Debug.LogError("米霍尔倒计时："+Error);return false;}
        }
        public void BeginEncounter(string eventId)
        {
            // Reveal on first appearance; replays retain time and explicit later hide/pause actions.
            Apply(new CountdownAction{kind="show"},eventId+"/appearance/show");
            Apply(new CountdownAction{kind="start"},eventId+"/appearance/start");
        }
        public void BeginStomachTalk(string eventId)
        {
            EnsureBound();if(clock==null||Parameters.countdownStartOnTalk<.5f)return;
            if(!clock.State.controlled){clock.State.remaining=Parameters.countdownInitialSeconds;clock.State.visible=Parameters.countdownInitiallyVisible>.5f;}
            Apply(new CountdownAction{kind="show"},eventId+"/activation/show");Apply(new CountdownAction{kind="start"},eventId+"/activation/start");
        }
        public void Flush()
        {
            if(!CanFlush()||!dirty)return;
            if(bound.StoreMihuoerCountdown(clock.State)){dirty=false;saveElapsed=0;Error=null;}
            else if(Error!=bound.Error){Error=bound.Error;Debug.LogWarning("米霍尔倒计时未保存："+Error);}
        }
        public void SetRemainingSeconds(float seconds){Apply(new CountdownAction{kind="set",seconds=seconds});}
        public void SetAndStart(float seconds){Apply(new CountdownAction{kind="setStart",seconds=seconds});}
        public void StartCountdown(){Apply(new CountdownAction{kind="start"});}
        public void PauseCountdown(){Apply(new CountdownAction{kind="pause"});}
        public void ResumeCountdown(){Apply(new CountdownAction{kind="resume"});}
        public void AddSeconds(float seconds){Apply(new CountdownAction{kind="add",seconds=seconds});}
        public void SubtractSeconds(float seconds){Apply(new CountdownAction{kind="subtract",seconds=seconds});}
        public void ShowCountdown(){Apply(new CountdownAction{kind="show"});}
        public void HideCountdown(){Apply(new CountdownAction{kind="hide"});}
        public void StopAndHide(){Apply(new CountdownAction{kind="stopHide"});}
        public void FreezeDisplayAt(float seconds){Apply(new CountdownAction{kind="freezeDisplay",seconds=seconds});}
        public void UnfreezeDisplay(){Apply(new CountdownAction{kind="unfreezeDisplay"});}
        void OnApplicationPause(bool paused){if(paused)Flush();}
        void OnApplicationQuit(){Flush();}
        void OnDisable(){Flush();}
    }
}
