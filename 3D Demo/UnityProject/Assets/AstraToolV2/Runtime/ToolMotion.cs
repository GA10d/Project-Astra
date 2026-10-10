using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace Astra.PerformanceToolV2
{
    // Manual Humanoid Playables, matching Demo/V2. Sampling is seekable and deterministic.
    public sealed class ToolMotion:MonoBehaviour
    {
        public ToolAvatarCatalog catalog;
        PlayableGraph graph;AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] players=new AnimationClipPlayable[2];
        readonly AnimationClip[] loaded=new AnimationClip[2];
        Animator animator;
        public Animator Animator=>animator?animator:GetComponentInChildren<Animator>();
        void Initialize()
        {
            if(graph.IsValid())return;
            animator=GetComponentInChildren<Animator>();
            if(!animator||!catalog)throw new InvalidOperationException("动作缺少骨架或动作库");
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create(name+" UAL");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer=AnimationMixerPlayable.Create(graph,2);
            AnimationPlayableOutput.Create(graph,"Quaternius Humanoid",animator).SetSourcePlayable(mixer);graph.Play();
        }
        void Set(int slot,string id,float time,float speed)
        {
            var clip=catalog.Clip(id);if(!clip)throw new ArgumentException("动作不存在: "+id);
            if(loaded[slot]!=clip)
            {
                if(players[slot].IsValid()){graph.Disconnect(mixer,slot);graph.DestroyPlayable(players[slot]);}
                loaded[slot]=clip;players[slot]=AnimationClipPlayable.Create(graph,clip);
                players[slot].SetApplyFootIK(true);players[slot].SetSpeed(0);graph.Connect(players[slot],0,mixer,slot);
            }
            float t=Mathf.Max(0,time*speed);t=clip.isLooping?Mathf.Repeat(t,clip.length):Mathf.Min(t,Mathf.Max(0,clip.length-.001f));
            players[slot].SetTime(t);players[slot].SetTime(t);
        }
        public void Sample(string id,float time,float speed=1)
        {Initialize();Set(0,id,time,speed);mixer.SetInputWeight(0,1);mixer.SetInputWeight(1,0);graph.Evaluate(0);}
        public void SampleTimeline(ToolUnit unit,string actorId,float time)
        {
            var action=unit.actions.LastOrDefault(a=>a.actorId==actorId&&a.start<=time&&a.start+a.duration>time);
            if(action==null){Sample("Idle_Loop",time);return;}
            float local=time-action.start;
            if(local>=.22f){Sample(action.actionId,local,action.speed);return;}
            var previous=unit.actions.LastOrDefault(a=>a!=action&&a.actorId==actorId&&a.start<action.start&&a.start+a.duration>=action.start-.001f);
            Initialize();Set(0,action.actionId,local,action.speed);
            Set(1,previous==null?"Idle_Loop":previous.actionId,previous==null?time:time-previous.start,previous==null?1:previous.speed);
            float blend=Mathf.SmoothStep(0,1,local/.22f);mixer.SetInputWeight(0,blend);mixer.SetInputWeight(1,1-blend);graph.Evaluate(0);
        }
        void OnDisable(){Release();}
        void OnDestroy(){Release();}
        void Release(){if(graph.IsValid())graph.Destroy();loaded[0]=loaded[1]=null;}
    }
}
