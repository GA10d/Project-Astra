using System;
using System.Linq;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    public sealed class FoolsWalker : IDisposable
    {
        readonly StomachMihuoerActor owner;
        readonly MihuoerMotion motion;
        readonly Transform[] thigh=new Transform[2],shin=new Transform[2],foot=new Transform[2];
        readonly Quaternion[] footRest=new Quaternion[2];
        readonly float[] side=new float[2];
        readonly FoolsSlope slope;
        public Transform Root {get;private set;}
        public Transform Head {get;private set;}
        public float FootError {get;private set;}
        float rootOffset;
        bool uphill;
        public FoolsWalker(StomachMihuoerActor actor,FoolsSlope path)
        {
            owner=actor;slope=path;Root=actor.BeginCinematic();
            try
            {
                motion=actor.StandingMotion;
                motion.Reset();
                var bones=Root.GetComponentInChildren<SkinnedMeshRenderer>().bones;
                Head=bones.First(t=>t.name=="head");
                for(int i=0;i<2;i++){string prefix=i==0?"L_":"R_";thigh[i]=bones.First(t=>t.name==prefix+"thigh");shin[i]=bones.First(t=>t.name==prefix+"shin");foot[i]=bones.First(t=>t.name==prefix+"foot");}
                motion.Sample(MihuoerPose.Idle,0);rootOffset=Vector3.Dot(Root.position-foot[0].position,slope.Up)+.07f;
                for(int i=0;i<2;i++){side[i]=Vector3.Dot(foot[i].position-Root.position,Root.right);footRest[i]=Quaternion.Inverse(Root.rotation)*foot[i].rotation;}
                Sample(0,false,MihuoerPose.Idle,0);
            }
            catch{owner.EndCinematic();throw;}
        }
        internal void Sample(float distance,bool walking,MihuoerPose pose,float dt)
        {
            const float stride=.78f;
            distance=Mathf.Clamp(distance,0,slope.Length);
            var direction=slope.Direction(distance);
            var ground=slope.Position(distance);
            Root.SetPositionAndRotation(ground+slope.Up*rootOffset,Quaternion.LookRotation(direction,slope.Up));

            float cycle=distance/stride;
            float gait=0,incline=0;
            if(walking)
            {
                var gradeSpan=slope.Position(distance+.7f)-slope.Position(distance-.7f);
                float grade=Vector3.Dot(gradeSpan,slope.Up)/Mathf.Max(.01f,gradeSpan.magnitude);
                uphill=grade>(uphill?.22f:.25f);
                incline=Mathf.InverseLerp(.19f,.30f,grade);
                float easeIn=Mathf.InverseLerp(0,.65f,distance);
                float easeOut=Mathf.InverseLerp(0,.70f,slope.Length-distance);
                gait=Mathf.SmoothStep(0,1,Mathf.Min(easeIn,easeOut));
            }

            motion.Sample(walking?(uphill?MihuoerPose.WalkUphill:MihuoerPose.WalkFlat):pose,dt,
                walking?Mathf.Repeat(cycle-.24f,1f):-1f);
            Root.position-=slope.Up*(.035f*gait);
            FootError=0;
            for(int i=0;i<2;i++)
            {
                float phase=cycle+i*.5f;
                float step=Mathf.Floor(phase);
                float swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.60f,1,phase-step));
                float station=(step-i*.5f+.3f+swing)*stride;
                var target=slope.Position(Mathf.Lerp(distance,station,gait))+Root.right*side[i];
                float swingHeight=Mathf.Sin(swing*Mathf.PI)*gait;
                target=slope.Ground(target)+slope.Up*(.07f+swingHeight*Mathf.Lerp(.13f,.17f,incline));
                PlantFoot(i,target,direction,swingHeight);
            }
        }
        internal void SampleStandingAt(Vector3 ground,Vector3 facing,MihuoerPose pose,float dt,Vector3 weightShift=default,float lowering=0f)
        {
            Root.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(facing,slope.Up).normalized,slope.Up);
            Root.position=ground+weightShift+slope.Up*(rootOffset-lowering);
            motion.Sample(pose,dt);
            FootError=0;
            for(int i=0;i<2;i++)
            {
                var target=slope.Ground(ground+Root.right*side[i])+slope.Up*.07f;
                PlantFoot(i,target,Root.forward,0);
            }
        }
        void PlantFoot(int index,Vector3 target,Vector3 direction,float swing)
        {
            var ankle=foot[index];
            var liftedRotation=ankle.rotation;
            Solve(thigh[index],shin[index],ankle,target,direction);
            ankle.rotation=Quaternion.Slerp(Root.rotation*footRest[index],liftedRotation,swing*.65f);
            FootError=Mathf.Max(FootError,Vector3.Distance(ankle.position,target));
        }
        internal static void Solve(Transform hip,Transform knee,Transform ankle,Vector3 target,Vector3 bend)
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
