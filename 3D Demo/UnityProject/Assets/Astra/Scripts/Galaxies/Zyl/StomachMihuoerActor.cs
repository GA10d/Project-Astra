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
        Animation animationPlayer;
        SetpieceDirector director;
        float clock;
        Transform standingActor,seatedActor;
        Animation standingAnimation,seatedAnimation;
        Quaternion craftAuthoredLocalRotation;
        public bool SeatedOnCraft {get;private set;}
        public bool Speaking {get;set;}
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
            animationPlayer=actor.GetComponentInChildren<Animation>();
            if(!animationPlayer||animationPlayer[StandingClip]==null)throw new InvalidOperationException("胃内米霍尔缺少站立沮丧循环："+StandingClip);
            var surface=Resources.Load<Material>("Galaxies/Zyl/Encounter/MihuoerSurface");if(!surface)throw new InvalidOperationException("米霍尔材质资源缺失");
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterial=surface;
            foreach(var container in new[]{factory.GeneratedRoot,Actor})foreach(var t in container.GetComponentsInChildren<Transform>(true))t.gameObject.layer=ZylStomachEnvironment.EnvironmentLayer;
            foreach(var light in factory.GeneratedRoot.GetComponentsInChildren<Light>(true)){light.cullingMask=1<<ZylStomachEnvironment.EnvironmentLayer;light.intensity*=.25f;}
            var timer=owner.computer.Navigation.Countdown;timer.ReloadParameters();MihuoerCountdownSkin.Attach(actor.gameObject,timer,timer.Parameters,true);
            standingActor=Actor;standingAnimation=animationPlayer;Sample();
        }
        public void SetSeatedOnCraft(bool seated)
        {
            if(seated&&!seatedActor)
            {
                var model=Resources.Load<GameObject>("Galaxies/Zyl/Encounter/Mihuoer");if(!model)throw new InvalidOperationException("米霍尔坐姿资源缺失");
                seatedActor=Instantiate(model,Craft,false).transform;seatedActor.name="Mihuoer | seated on craft";seatedActor.localPosition=new Vector3(0,.06f,-.12f);seatedActor.localRotation=Quaternion.Euler(0,180,0);seatedActor.localScale=Vector3.one*1.12f;
                seatedAnimation=seatedActor.GetComponentInChildren<Animation>();if(!seatedAnimation||seatedAnimation["SeatedTalk"]==null||seatedAnimation["Think"]==null)throw new InvalidOperationException("米霍尔坐姿循环缺失");
                foreach(var t in seatedActor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=ZylStomachEnvironment.EnvironmentLayer;
                var timer=director.computer.Navigation.Countdown;MihuoerCountdownSkin.Attach(seatedActor.gameObject,timer,timer.Parameters);
            }
            SeatedOnCraft=seated;standingActor.gameObject.SetActive(!seated);if(seatedActor)seatedActor.gameObject.SetActive(seated);Actor=seated?seatedActor:standingActor;animationPlayer=seated?seatedAnimation:standingAnimation;
            if(seated){var position=director.controller.viewCamera.transform.position;position.y=Craft.position.y;var direction=position-Craft.position;if(direction.sqrMagnitude>.0001f)Craft.rotation=Quaternion.LookRotation(-direction.normalized,Vector3.up);}
            else Craft.localRotation=craftAuthoredLocalRotation;
            Sample();
        }
        void Update(){if(!director||director.controller.IsPaused)return;clock+=Time.deltaTime;Sample();}
        void Sample()
        {
            if(!animationPlayer)return;string clip=SeatedOnCraft?(Speaking?"SeatedTalk":"Think"):StandingClip;var state=animationPlayer[clip];if(state==null)return;
            animationPlayer.enabled=false;animationPlayer.Play(clip);state.speed=0;state.time=Mathf.Repeat(clock,Mathf.Max(.1f,state.length));animationPlayer.Sample();
        }
    }
}
