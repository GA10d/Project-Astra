using System;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    /// <summary>Environment-owned visitor. It survives a setpiece reset and disappears only with the stomach scene.</summary>
    public sealed class StomachMihuoerActor : MonoBehaviour
    {
        VisitorStationSetpieces factory;
        Animation animationPlayer;
        SetpieceDirector director;
        float clock;
        public bool Speaking {get;set;}
        public Transform Craft {get;private set;}
        public Transform Actor {get;private set;}
        public void Initialize(SetpieceDirector owner,Transform characterAnchor,Transform shipAnchor)
        {
            director=owner;factory=gameObject.AddComponent<VisitorStationSetpieces>();factory.Initialize(owner);factory.ShowVisitor();factory.enabled=false;
            factory.GeneratedRoot.SetParent(transform,false);Craft=factory.VisitorPilot.parent;factory.VisitorPilot.gameObject.SetActive(false);
            Craft.name="Mihuoer ship | independent craft";Craft.SetPositionAndRotation(shipAnchor.position,shipAnchor.rotation);Craft.localScale=Vector3.one*1.08f;
            var model=Resources.Load<GameObject>("Galaxies/Zyl/Encounter/Mihuoer");if(!model)throw new InvalidOperationException("肚中会谈缺少米霍尔角色资源");
            var actor=Instantiate(model,transform).transform;Actor=actor;actor.name="Mihuoer | independent character";actor.SetPositionAndRotation(characterAnchor.position,characterAnchor.rotation);actor.localScale=Vector3.one*(1.12f*1.08f);
            animationPlayer=actor.GetComponentInChildren<Animation>();
            foreach(var container in new[]{factory.GeneratedRoot,Actor})foreach(var t in container.GetComponentsInChildren<Transform>(true))t.gameObject.layer=ZylStomachEnvironment.EnvironmentLayer;
            foreach(var light in factory.GeneratedRoot.GetComponentsInChildren<Light>(true)){light.cullingMask=1<<ZylStomachEnvironment.EnvironmentLayer;light.intensity*=.25f;}
            var timer=owner.computer.Navigation.Countdown;timer.ReloadParameters();MihuoerCountdownSkin.Attach(actor.gameObject,timer,timer.Parameters);
            Sample();
        }
        void Update(){if(!director||director.controller.IsPaused)return;clock+=Time.deltaTime;Sample();}
        void Sample()
        {
            if(!animationPlayer)return;string clip=Speaking?"SeatedTalk":"Think";var state=animationPlayer[clip];if(state==null)return;
            animationPlayer.enabled=false;animationPlayer.Play(clip);state.speed=0;state.time=Mathf.Repeat(clock,Mathf.Max(.1f,state.length));animationPlayer.Sample();
        }
    }
}
