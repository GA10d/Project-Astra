using System;
using System.Collections.Generic;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    // Scripted flight in normalized tactical-map coordinates. Does not touch CombatSimulation.
    public sealed class EscapeFlight
    {
        public sealed class Debris { public float encounter,x,y,size,seed;public bool ram,tree,hit;public Vector2 impact; }
        public readonly List<Debris> Obstacles=new List<Debris>();
        public float Time {get;private set;} public readonly float Duration,ExitDistance,Damage;
        public bool Paused;public bool Finished {get{return Time>=Duration;}}
        public float Progress {get{return Mathf.Clamp01(Time/Duration);}}
        public float Distance {get{return ExitDistance*(1-Progress);}}
        public float Hull {get;private set;}=100;
        public int Impacts {get;private set;}public float ImpactAge {get;private set;}=100;
        public Vector2 Leader {get;private set;}public Vector2 Player {get;private set;}
        public float LeaderAngle {get;private set;}public float PlayerAngle {get;private set;}
        public float Bank {get;private set;}
        const float ChoreographyDuration=42f;
        readonly float motionRate;
        public float MotionTime => Time * motionRate;
        public float MotionAge(float encounter) => (Time - encounter) * motionRate;
        const float Scroll=.19f,LeaderX=.59f,PlayerX=.45f;
        public EscapeFlight(float duration,float distance,float damage)
        {
            Duration=duration;ExitDistance=distance;Damage=damage;
            // Shorter edits speed up the full route, including its obstacle choreography.
            float routeDuration=Mathf.Max(ChoreographyDuration,duration);
            motionRate=routeDuration/duration;
            int count=Mathf.Max(6,Mathf.FloorToInt((routeDuration-8)/2.4f));
            for(int i=0;i<count;i++)
            {
                float t=Mathf.Lerp(4,routeDuration-4,(i+.5f)/count)/motionRate;bool ram=i%5==2;
                float lane=Center(LeaderX,t)+(ram?0:i%2==0?-.145f:.145f);
                Obstacles.Add(new Debris{encounter=t,seed=i*1.731f,ram=ram,tree=!ram&&i%2==0,size=ram?.26f:.047f,y=lane});
            }
            Sample();
        }
        public float Center(float x,float time){return .5f+Mathf.Sin((x+Scroll*time*motionRate)*4.7f)*.095f+Mathf.Sin((x+Scroll*time*motionRate)*9.1f+1)*.035f;}
        public float Ceiling(float x){return Center(x,Time)-(.31f+.035f*Mathf.Sin((x+Scroll*MotionTime)*13));}
        public float Floor(float x){return Center(x,Time)+(.31f+.028f*Mathf.Sin((x+Scroll*MotionTime)*11+2));}
        Vector2 LeaderAt(float time)
        {
            float y=Center(LeaderX,time)+.055f*Mathf.Sin(time*motionRate*1.4f);
            foreach(var o in Obstacles)
            {
                float d=(time-o.encounter)*motionRate;
                if(Mathf.Abs(d)>2)continue;
                float desired=o.ram?o.y:Center(LeaderX,time)+(o.y>Center(LeaderX,time)?-.10f:.10f);
                float weight=o.ram?Mathf.Exp(-d*d/0.30f):Mathf.Exp(-d*d/1.1f);
                y=Mathf.Lerp(y,desired,weight);
            }
            return new Vector2(LeaderX,Mathf.Clamp(y,.25f,.75f));
        }
        public Vector2 TowPoint(float time)
        {
            // A shorter wake gives the disabled craft visible towing inertia.
            var lead=LeaderAt(Mathf.Max(0,time-.65f/motionRate));return new Vector2(PlayerX,lead.y+.012f*Mathf.Sin(time*motionRate*2));
        }
        void Sample()
        {
            Leader=LeaderAt(Time);Player=TowPoint(Time);
            float sampleOffset=.05f/motionRate;
            LeaderAngle=Mathf.Clamp((LeaderAt(Time+sampleOffset).y-LeaderAt(Time-sampleOffset).y)*650,-28,28);
            PlayerAngle=Mathf.Clamp((TowPoint(Time+sampleOffset).y-TowPoint(Time-sampleOffset).y)*650,-24,24);
            Bank=Mathf.Clamp(PlayerAngle/24,-1,1);
            int rams=0;foreach(var o in Obstacles)if(o.ram)rams++;
            foreach(var o in Obstacles)
            {
                o.x=LeaderX-MotionAge(o.encounter)*Scroll;
                if(o.ram&&!o.hit&&Time>=o.encounter)
                {o.hit=true;o.impact=new Vector2(LeaderX,o.y);Impacts++;Hull=Mathf.Max(1,100-Damage*Impacts/Mathf.Max(1,rams));ImpactAge=Mathf.Max(0,MotionAge(o.encounter));}
            }
        }
        public void Tick(float dt)
        {
            if(Paused||Finished||float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0)return;
            float next=Mathf.Min(Duration,Time+dt);ImpactAge+=(next-Time)*motionRate;Time=next;Sample();
        }
    }
}
