using System;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    /// <summary>Environment-owned visitor. It survives a setpiece reset and disappears only with the stomach scene.</summary>
    public sealed class StomachMihuoerActor : MonoBehaviour
    {
        public const string StandingClip="StandingDejected";
        public const string StandingResource="Galaxies/Zyl/Standing/StandingDejected";
        VisitorStationSetpieces factory;
        MihuoerMotion standingMotion, seatedMotion;
        Transform referenceSeatedActor;
        SetpieceDirector director;
        float clock;
        Transform standingActor,seatedActor;
        Animation seatedAnimation;
        bool referenceSeat, cinematicReferenceSeat;
        internal static readonly Vector3 ReferenceSeat=new Vector3(0,.286f,-.16f);
        Quaternion craftAuthoredLocalRotation;
        public bool SeatedOnCraft {get;private set;}
        public bool CinematicControlled {get;private set;}
        bool cinematicSeated;Vector3 cinematicLocalPosition;Quaternion cinematicLocalRotation,cinematicCraftRotation;
        public Transform BeginCinematic()
        {
            if(CinematicControlled)return standingActor;
            cinematicSeated=SeatedOnCraft;cinematicReferenceSeat=referenceSeat;cinematicLocalPosition=standingActor.localPosition;cinematicLocalRotation=standingActor.localRotation;cinematicCraftRotation=Craft.rotation;
            SetSeatedOnCraft(false);Craft.rotation=cinematicCraftRotation;CinematicControlled=true;return standingActor;
        }
        public void EndCinematic()
        {
            if(!CinematicControlled)return;CinematicControlled=false;Pose=MihuoerPose.Slumped;
            if(!standingActor||!Craft||(cinematicSeated&&!(cinematicReferenceSeat?referenceSeatedActor:seatedActor)))return;
            standingMotion.Reset();
            if(standingActor){standingActor.localPosition=cinematicLocalPosition;standingActor.localRotation=cinematicLocalRotation;}
            SetSeatedOnCraft(cinematicSeated,cinematicReferenceSeat);Craft.rotation=cinematicCraftRotation;
        }
        internal MihuoerMotion StandingMotion => standingMotion;
        internal MihuoerPose Pose {get;set;}=MihuoerPose.Slumped;
        public Transform Craft {get;private set;}
        public Transform Actor {get;private set;}
        public void Initialize(SetpieceDirector owner,Transform characterAnchor,Transform shipAnchor)
        {
            director=owner;factory=gameObject.AddComponent<VisitorStationSetpieces>();factory.Initialize(owner);factory.ShowVisitor();factory.enabled=false;
            factory.GeneratedRoot.SetParent(transform,false);Craft=factory.VisitorPilot.parent;factory.VisitorPilot.gameObject.SetActive(false);
            Craft.name="Mihuoer ship | independent craft";Craft.SetPositionAndRotation(shipAnchor.position,shipAnchor.rotation);Craft.localScale=Vector3.one*1.08f;
            craftAuthoredLocalRotation=Craft.localRotation;
            var model=Resources.Load<GameObject>(StandingResource);if(!model)throw new InvalidOperationException("肚中会谈缺少米霍尔角色资源");
            var actor=new GameObject("Mihuoer standing root").transform;actor.SetParent(transform,false);Instantiate(model,actor,false);Actor=actor;actor.name="Mihuoer | independent character";actor.SetPositionAndRotation(characterAnchor.position,characterAnchor.rotation);actor.localScale=Vector3.one*(1.12f*1.08f);
            var animation=actor.GetComponentInChildren<Animation>();
            if(!animation||animation[StandingClip]==null)throw new InvalidOperationException("胃内米霍尔缺少站立沮丧循环："+StandingClip);
            var surface=Resources.Load<Material>("Galaxies/Zyl/Encounter/MihuoerSurface");if(!surface)throw new InvalidOperationException("米霍尔材质资源缺失");
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterial=surface;
            foreach(var container in new[]{factory.GeneratedRoot,Actor})foreach(var t in container.GetComponentsInChildren<Transform>(true))t.gameObject.layer=ZylStomachEnvironment.EnvironmentLayer;
            foreach(var light in factory.GeneratedRoot.GetComponentsInChildren<Light>(true)){light.cullingMask=1<<ZylStomachEnvironment.EnvironmentLayer;light.intensity*=.25f;}
            var timer=owner.computer.Navigation.Countdown;timer.ReloadParameters();MihuoerCountdownSkin.Attach(actor.gameObject,timer,timer.Parameters,true);
            standingActor=Actor;standingMotion=new MihuoerMotion(animation);Sample(0);
        }
        public void SetSeatedOnCraft(bool seated, bool useReferenceSeat=false)
        {
            if(seated&&useReferenceSeat&&!referenceSeatedActor)
            {
                var model=Resources.Load<GameObject>(StandingResource);
                referenceSeatedActor=Instantiate(model,Craft,false).transform;
                referenceSeatedActor.name="Mihuoer | reference seated on craft";
                referenceSeatedActor.localPosition=ReferenceSeat;
                referenceSeatedActor.localRotation=Quaternion.Euler(0,180,0);
                referenceSeatedActor.localScale=Vector3.one*1.12f;
                var surface=Resources.Load<Material>("Galaxies/Zyl/Encounter/MihuoerSurface");
                foreach(var renderer in referenceSeatedActor.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterial=surface;
                foreach(var t in referenceSeatedActor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=ZylStomachEnvironment.EnvironmentLayer;
                var timer=director.computer.Navigation.Countdown;
                MihuoerCountdownSkin.Attach(referenceSeatedActor.gameObject,timer,timer.Parameters,true);
                seatedMotion=new MihuoerMotion(referenceSeatedActor.GetComponentInChildren<Animation>());
            }
            if(seated&&!useReferenceSeat&&!seatedActor)
            {
                var model=Resources.Load<GameObject>("Galaxies/Zyl/Encounter/Mihuoer");if(!model)throw new InvalidOperationException("米霍尔坐姿资源缺失");
                seatedActor=Instantiate(model,Craft,false).transform;seatedActor.name="Mihuoer | seated on craft";seatedActor.localPosition=new Vector3(0,.06f,-.12f);seatedActor.localRotation=Quaternion.Euler(0,180,0);seatedActor.localScale=Vector3.one*1.12f;
                seatedAnimation=seatedActor.GetComponentInChildren<Animation>();if(!seatedAnimation||seatedAnimation["Think"]==null)throw new InvalidOperationException("米霍尔坐姿循环缺失");
                foreach(var t in seatedActor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=ZylStomachEnvironment.EnvironmentLayer;
                var timer=director.computer.Navigation.Countdown;MihuoerCountdownSkin.Attach(seatedActor.gameObject,timer,timer.Parameters);
            }
            referenceSeat=seated&&useReferenceSeat;
            SeatedOnCraft=seated;
            standingActor.gameObject.SetActive(!seated);
            if(seatedActor)seatedActor.gameObject.SetActive(seated&&!referenceSeat);
            if(referenceSeatedActor)referenceSeatedActor.gameObject.SetActive(seated&&referenceSeat);
            Actor=seated?(referenceSeat?referenceSeatedActor:seatedActor):standingActor;
            Pose=seated?MihuoerPose.ChairListen:MihuoerPose.Slumped;
            if(seated){var position=director.controller.viewCamera.transform.position;position.y=Craft.position.y;var direction=position-Craft.position;if(direction.sqrMagnitude>.0001f)Craft.rotation=Quaternion.LookRotation(-direction.normalized,Vector3.up);}
            else Craft.localRotation=craftAuthoredLocalRotation;
            Sample(0);
        }
        void Update()
        {
            if(!director||director.controller.IsPaused||CinematicControlled)return;
            if(director.computer.IsOpen&&(director.stomachTalk.Active&&!director.stomachTalk.Complete
                ||director.printingRequest.Active&&!director.printingRequest.Complete))return;
            clock+=Time.deltaTime;
            Sample(Time.deltaTime);
        }
        void Sample(float dt)
        {
            if(!SeatedOnCraft) { standingMotion?.Sample(Pose,dt);return; }
            if(referenceSeat) { seatedMotion.Sample(Pose,dt);return; }

            // Keep the original seated performance in the fixed pole/ceiling shots.
            var state=seatedAnimation["Think"];
            seatedAnimation.enabled=false;
            seatedAnimation.Play("Think");
            state.speed=0;
            state.time=Mathf.Repeat(clock,state.length);
            seatedAnimation.Sample();
        }
    }
}
