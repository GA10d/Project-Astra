using System;
using System.Linq;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    public sealed class FoolsWalker : IDisposable
    {
        readonly StomachMihuoerActor owner;
        readonly Animation animation;
        readonly Transform[] thigh=new Transform[2],shin=new Transform[2],foot=new Transform[2],arms=new Transform[2];
        readonly Quaternion[] footRest=new Quaternion[2];
        readonly float[] side=new float[2];
        readonly FoolsSlope slope;
        public Transform Root {get;private set;}
        public Transform Head {get;private set;}
        public float FootError {get;private set;}
        float rootOffset;
        public FoolsWalker(StomachMihuoerActor actor,FoolsSlope path)
        {
            owner=actor;slope=path;Root=actor.BeginCinematic();
            try
            {
                animation=Root.GetComponentInChildren<Animation>();
                foreach(var name in new[]{"StandingIdle","StandingTalk01"})
                {
                    var prefab=Resources.Load<GameObject>("Galaxies/Zyl/Standing/"+name);var source=prefab?prefab.GetComponentInChildren<Animation>():null;
                    if(!source||!source.GetClip(name))throw new InvalidOperationException("上坡演出缺少站立片段："+name);if(!animation.GetClip(name))animation.AddClip(source.GetClip(name),name);
                }
                var bones=Root.GetComponentInChildren<SkinnedMeshRenderer>().bones;
                Head=bones.First(t=>t.name=="head");
                for(int i=0;i<2;i++){string prefix=i==0?"L_":"R_";thigh[i]=bones.First(t=>t.name==prefix+"thigh");shin[i]=bones.First(t=>t.name==prefix+"shin");foot[i]=bones.First(t=>t.name==prefix+"foot");arms[i]=bones.First(t=>t.name==prefix+"upperarm");}
                SampleBase(false,0);rootOffset=Vector3.Dot(Root.position-foot[0].position,slope.Up)+.07f;
                for(int i=0;i<2;i++){side[i]=Vector3.Dot(foot[i].position-Root.position,Root.right);footRest[i]=Quaternion.Inverse(Root.rotation)*foot[i].rotation;}
                Sample(0,false,false,0);
            }
            catch{owner.EndCinematic();throw;}
        }
        void SampleBase(bool speaking,float time)
        {
            string name=speaking?"StandingTalk01":"StandingIdle";animation.enabled=false;animation.Play(name);var state=animation[name];state.speed=0;state.time=Mathf.Repeat(time,Mathf.Max(.1f,state.length));animation.Sample();
        }
        public void Sample(float distance,bool walking,bool speaking,float time)
        {
            distance=Mathf.Clamp(distance,0,slope.Length);var direction=slope.Direction(distance);Root.rotation=Quaternion.LookRotation(direction,slope.Up);
            Root.position=slope.Position(distance)+slope.Up*(rootOffset-(walking?.055f:0)+(walking?.012f*Mathf.Sin(distance/1.05f*Mathf.PI*4):0));SampleBase(speaking,time);
            FootError=0;
            for(int i=0;i<2;i++)
            {
                const float stride=1.05f;float phase=distance/stride+i*.5f;float step=Mathf.Floor(phase),u=phase-step;
                float station=(step-i*.5f)*stride+stride*.3f;
                float swing=walking?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.60f,1,u)):0;
                float targetDistance=walking?station+swing*stride:distance;
                var target=slope.Position(Mathf.Clamp(targetDistance,0,slope.Length))+Root.right*side[i];target=slope.Ground(target)+slope.Up*(.07f+(walking?Mathf.Sin(swing*Mathf.PI)*.16f:0));
                Solve(thigh[i],shin[i],foot[i],target,direction);
                foot[i].rotation=Root.rotation*footRest[i];FootError=Mathf.Max(FootError,Vector3.Distance(foot[i].position,target));
                if(walking)arms[i].rotation=Quaternion.AngleAxis(Mathf.Sin(phase*Mathf.PI*2)*13,Root.right)*arms[i].rotation;
            }
            owner.Speaking=speaking;
        }
        public void SampleStandingAt(Vector3 ground,Vector3 facing,bool speaking,float time)
        {
            Root.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(facing,slope.Up).normalized,slope.Up);
            Root.position=ground+slope.Up*rootOffset;SampleBase(speaking,time);FootError=0;
            for(int i=0;i<2;i++)
            {
                var target=slope.Ground(ground+Root.right*side[i])+slope.Up*.07f;
                Solve(thigh[i],shin[i],foot[i],target,Root.forward);foot[i].rotation=Root.rotation*footRest[i];
                FootError=Mathf.Max(FootError,Vector3.Distance(foot[i].position,target));
            }
            owner.Speaking=speaking;
        }
        static void Solve(Transform hip,Transform knee,Transform ankle,Vector3 target,Vector3 bend)
        {
            var origin=hip.position;float a=Vector3.Distance(origin,knee.position),b=Vector3.Distance(knee.position,ankle.position);var delta=target-origin;
            float d=Mathf.Clamp(delta.magnitude,.001f,a+b-.001f);var along=delta.normalized;
            var outward=Vector3.ProjectOnPlane(bend,along).normalized;float x=(a*a-b*b+d*d)/(2*d);float y=Mathf.Sqrt(Mathf.Max(0,a*a-x*x));var desired=origin+along*x+outward*y;
            hip.rotation=Quaternion.FromToRotation(knee.position-origin,desired-origin)*hip.rotation;
            knee.rotation=Quaternion.FromToRotation(ankle.position-knee.position,target-knee.position)*knee.rotation;
        }
        public void Dispose(){if(owner)owner.EndCinematic();}
    }
}
