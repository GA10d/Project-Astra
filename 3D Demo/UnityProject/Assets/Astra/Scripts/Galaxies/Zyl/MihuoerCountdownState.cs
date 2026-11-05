using System;
using System.Collections.Generic;
using System.IO;
namespace AstraCabin.Galaxies.Zyl
{
    [Serializable] public sealed class CountdownAction
    {
        public string kind="none";
        public double seconds;
        public static readonly string[] Kinds={"none","set","setStart","start","pause","resume","add","subtract","stopHide","show","hide","freezeDisplay","unfreezeDisplay"};
        public static bool NeedsSeconds(string kind){return kind=="set"||kind=="setStart"||kind=="add"||kind=="subtract"||kind=="freezeDisplay";}
        public static void Validate(CountdownAction action)
        {
            if(action==null)return;
            if(Array.IndexOf(Kinds,action.kind)<0)throw new InvalidDataException("未知倒计时操作");
            if(NeedsSeconds(action.kind)&&(double.IsNaN(action.seconds)||double.IsInfinity(action.seconds)||action.seconds<0||action.seconds>5999))throw new InvalidDataException("计时操作时间必须为0–99:59");
        }
    }
    [Serializable] public sealed class MihuoerCountdownSnapshot
    {
        public int schemaVersion=1;
        public double remaining=300,frozenSeconds;
        public bool running,visible,displayFrozen,zeroRaised,controlled;
        public List<string> consumedActions=new List<string>();
        public void Validate()
        {
            if(schemaVersion!=1||!Valid(remaining)||!Valid(frozenSeconds)||consumedActions==null||consumedActions.Count>2048)throw new InvalidDataException("倒计时存档格式无效");
            foreach(string key in consumedActions)if(string.IsNullOrEmpty(key)||key.Length>128)throw new InvalidDataException("倒计时操作记录无效");
        }
        static bool Valid(double n){return !double.IsNaN(n)&&!double.IsInfinity(n)&&n>=0&&n<=5999;}
    }
    public sealed class MihuoerCountdownClock
    {
        public readonly MihuoerCountdownSnapshot State;
        public double DisplaySeconds {get{return State.displayFrozen?State.frozenSeconds:State.remaining;}}
        public MihuoerCountdownClock(MihuoerCountdownSnapshot state){state.Validate();State=state;}
        public bool Tick(double seconds)
        {
            if(!State.running||seconds<=0||double.IsNaN(seconds)||double.IsInfinity(seconds))return false;
            State.remaining=Math.Max(0,State.remaining-seconds);
            if(State.remaining>0)return false;
            State.running=false;return CheckZeroReached();
        }
        public bool CheckZeroReached(){if(State.remaining>0||State.zeroRaised)return false;State.running=false;State.zeroRaised=true;return true;}
        public bool Apply(CountdownAction action,string onceKey=null)
        {
            CountdownAction.Validate(action);if(action==null||action.kind=="none")return false;
            if(onceKey!=null){if(State.consumedActions.Contains(onceKey))return false;if(onceKey.Length==0||onceKey.Length>128||State.consumedActions.Count>=2048)throw new InvalidDataException("倒计时操作记录已满或标识无效");}
            switch(action.kind)
            {
                case "set":State.remaining=action.seconds;break;
                case "setStart":State.remaining=action.seconds;State.running=action.seconds>0;break;
                case "start":case "resume":State.running=State.remaining>0;break;
                case "pause":State.running=false;break;
                case "add":State.remaining=Math.Min(5999,State.remaining+action.seconds);break;
                case "subtract":State.remaining=Math.Max(0,State.remaining-action.seconds);break;
                case "stopHide":State.running=false;State.visible=false;break;
                case "show":State.visible=true;break;
                case "hide":State.visible=false;break;
                case "freezeDisplay":State.frozenSeconds=action.seconds;State.displayFrozen=true;break;
                case "unfreezeDisplay":State.displayFrozen=false;break;
            }
            State.controlled=true;if(State.remaining>0)State.zeroRaised=false;else State.running=false;
            if(onceKey!=null)State.consumedActions.Add(onceKey);return true;
        }
        public static string Format(double seconds){int value=(int)Math.Ceiling(Math.Max(0,Math.Min(5999,seconds)));return (value/60).ToString("00")+":"+(value%60).ToString("00");}
    }
}
