using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using AstraCabin.Navigation;

namespace AstraCabin.Galaxies.Zyl
{
    /// <summary>Owns only the additive exterior. The existing cabin and navigation session survive.</summary>
    [DisallowMultipleComponent]
    public sealed class ZylStomachEnvironment : MonoBehaviour
    {
        public const string ScenePath = "Assets/Astra/Scenes/Zyl/StomachRefuge.unity";
        public const int EnvironmentLayer = 25;
        public bool Ready { get; private set; }
        public StomachMihuoerAnchor MihuoerAnchor {get;private set;}
        public StomachMihuoerShipAnchor MihuoerShipAnchor {get;private set;}
        public StomachMihuoerActor MihuoerActor {get;private set;}
        public float CabinYaw {get;private set;}
        Matrix4x4 authoredLandingPose;
        bool savedTalkCompleted,orientationDirty;
        public float CabinYawTowardMihuoer(){if(!MihuoerAnchor)return CabinYaw;var direction=MihuoerAnchor.transform.position;direction.y=0;return CabinYaw+Vector3.SignedAngle(Vector3.right,direction,Vector3.up);}
        public void SetCabinYaw(float yaw){
            if(!environmentRoot)return;CabinYaw=yaw;var rotation=Quaternion.Euler(0,-yaw,0);
            var initial=authoredLandingPose.inverse;environmentRoot.transform.SetPositionAndRotation(rotation*initial.MultiplyPoint3x4(Vector3.zero),rotation*Quaternion.Inverse(authoredLandingPose.rotation));
            AuthoredToRuntime=Matrix4x4.Rotate(rotation)*initial;
            foreach(var renderer in environmentRoot.GetComponentsInChildren<Renderer>(true))for(int i=0;i<renderer.sharedMaterials.Length;i++){var material=renderer.sharedMaterials[i];if(!material||!material.HasProperty("_UseBreathFrameMatrix"))continue;var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);block.SetMatrix("_BreathWorldToAuth",AuthoredToRuntime.inverse);block.SetMatrix("_BreathAuthToWorld",AuthoredToRuntime);renderer.SetPropertyBlock(block,i);}
        }
        bool EncounterCurtainOwned { get { return navigation && navigation.Director.encounter && navigation.Director.encounter.LoadingEnvironment; } }
        public bool Busy { get; private set; }
        public string Error { get; private set; }
        public Matrix4x4 AuthoredToRuntime { get; private set; }
        NavigationMode navigation;
        NavigationSession enteringSession;
        VoyageSave evaluatedSave;
        string evaluatedGalaxy;
        bool savedWantsStomach, desired, captured, prepared, destroying;
        Scene environment;
        GameObject environmentRoot;
        StomachEnvironmentSettings settings;
        float curtain;
        public float CurtainAlpha {get{return Error!=null?1:curtain;}}
        public void RetryLoad(){Error=null;}
#if UNITY_EDITOR
        public void EditorRefreshForDebug(){enteringSession=null;evaluatedSave=null;RefreshDesired();}
#endif
        Color ambient, background, fogColor;
        AmbientMode ambientMode;
        Material skybox;
        bool fog, planetActive, starsActive;
        FogMode fogMode;
        float fogDensity, fogStart, fogEnd, cameraFar;
        int cameraMask;
        CameraClearFlags clearFlags;

        public void Initialize(NavigationMode owner)
        {
            navigation = owner;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        // Uses the existing durable observed command: no second save file, new reward, or schema.
        public static bool HasSwallowedVoyage(string galaxy, NavigationSession session)
        {
            if (galaxy != "zyl" || session == null || session.Blocked) return false;
            return session.Save.commands.Any(c => c.kind == "observed" &&
                session.Engine.Campaign.events.Any(e => e.id == c.id && e.type == "setpiece" && e.setpiece == SetpieceDirector.EncounterMode));
        }

        public bool AwaitSwallowEnvironment()
        {
            if (!navigation || navigation.ActiveGalaxyId != "zyl" || !navigation.OwnsPresentation ||
                navigation.Session == null || navigation.Session.Engine.Pending == null ||
                navigation.Session.Engine.Pending.setpiece != SetpieceDirector.EncounterMode) return true;
            enteringSession = navigation.Session;
            RefreshDesired();
            return Ready;
        }

        void Update() { if (navigation) RefreshDesired(); }
        void RefreshDesired()
        {
            if (evaluatedSave != navigation.Session?.Save || evaluatedGalaxy != navigation.ActiveGalaxyId)
            {
                evaluatedSave = navigation.Session?.Save; evaluatedGalaxy = navigation.ActiveGalaxyId;
                savedWantsStomach = HasSwallowedVoyage(evaluatedGalaxy, navigation.Session);
                savedTalkCompleted=evaluatedGalaxy=="zyl"&&navigation.Session!=null&&navigation.Session.Save.commands.Any(c=>c.kind=="observed"&&navigation.Session.Engine.Campaign.events.Any(e=>e.id==c.id&&e.type=="setpiece"&&e.setpiece==SetpieceDirector.StomachTalkMode));orientationDirty=true;
            }
            // Restart/import replace Save; retain the transient request only during the actual black ending.
            bool entering = enteringSession == navigation.Session && navigation.ActiveGalaxyId == "zyl" &&
                navigation.OwnsPresentation && navigation.Director.encounter && (navigation.Director.encounter.LoadingEnvironment || navigation.Director.encounter.Complete);
            desired = savedWantsStomach || entering;
            if (!entering) enteringSession = null;
            if (!desired && Error != null) Error = null;
            if (!Busy && Error == null && desired != Ready) StartCoroutine(ChangeEnvironment());
            if(Ready&&!Busy&&orientationDirty&&(!navigation.Director.stomachTalk||!navigation.Director.stomachTalk.Active)){SetCabinYaw(savedTalkCompleted?CabinYawTowardMihuoer():0);orientationDirty=false;}
            if (!Busy && Error == null) curtain = Mathf.MoveTowards(curtain, 0, Time.deltaTime / 1.5f);
        }

        IEnumerator ChangeEnvironment()
        {
            Busy = true; curtain = EncounterCurtainOwned ? 0 : 1;
            if (desired)
            {
                CaptureExterior();
                prepared = false;
                AsyncOperation operation = null;
                try { operation = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive); }
                catch (Exception e) { Fail(e.Message); }
                if (operation != null) yield return operation;
                if (Error == null && !prepared) Fail("胃内环境缺少停靠锚点，或场景未包含在构建中。");
                if (Error == null)
                {
                    Ready = true;
                    ApplyExterior();
                    // Allow the observation command to become durable before revealing the exterior.
                    yield return null;
                }
                else
                {
                    if (environment.IsValid() && environment.isLoaded) yield return SceneManager.UnloadSceneAsync(environment);
                    RestoreExterior();
                }
            }
            else
            {
                Ready = false;
                if (environmentRoot) environmentRoot.SetActive(false);
                RestoreExterior();
                if (environment.IsValid() && environment.isLoaded) yield return SceneManager.UnloadSceneAsync(environment);
                environmentRoot = null; settings = null;MihuoerAnchor=null;MihuoerShipAnchor=null;MihuoerActor=null;CabinYaw=0; environment = default;
            }
            Busy = false;
        }

        void Fail(string reason)
        {
            Error = "胃内环境加载失败：" + reason;
            Debug.LogError(Error);
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Finish ownership even if a restart/galaxy switch arrived while the async load was pending.
            if (!Busy || scene.path != ScenePath) return;
            environment = scene;
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
            settings = roots.SelectMany(r => r.GetComponentsInChildren<StomachEnvironmentSettings>(true)).FirstOrDefault();
            if (!settings || roots.Any(r => r.GetComponentInChildren<CabinController>(true))) return;
            StomachCabinAnchor.Ensure(settings);MihuoerAnchor=StomachMihuoerAnchor.Ensure(settings);MihuoerShipAnchor=StomachMihuoerShipAnchor.Ensure(settings,MihuoerAnchor);roots=scene.GetRootGameObjects();
            var pose = Matrix4x4.TRS(settings.transform.position, settings.transform.rotation, Vector3.one);authoredLandingPose=pose;CabinYaw=0;
            AuthoredToRuntime = pose.inverse;
            environmentRoot = new GameObject("StomachRefuge | additive exterior");
            SceneManager.MoveGameObjectToScene(environmentRoot, scene);
            foreach (var root in roots)
            {
                root.transform.SetParent(environmentRoot.transform, true);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = EnvironmentLayer;
                foreach (var light in root.GetComponentsInChildren<Light>(true)) light.cullingMask = 1 << EnvironmentLayer;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    var material = renderer.sharedMaterials[i];
                    if (!material || !material.HasProperty("_UseBreathFrameMatrix")) continue;
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, i);
                    block.SetFloat("_UseBreathFrameMatrix", 1);
                    block.SetMatrix("_BreathWorldToAuth", pose); block.SetMatrix("_BreathAuthToWorld", pose.inverse);
                    renderer.SetPropertyBlock(block, i);
                }
            }
            environmentRoot.transform.SetPositionAndRotation(AuthoredToRuntime.MultiplyPoint3x4(Vector3.zero), Quaternion.Inverse(pose.rotation));
            var actorRoot=new GameObject("Mihuoer | persistent stomach visitor");actorRoot.transform.SetParent(environmentRoot.transform,false);
            MihuoerActor=actorRoot.AddComponent<StomachMihuoerActor>();MihuoerActor.Initialize(navigation.Director,MihuoerAnchor.transform,MihuoerShipAnchor.transform);
            prepared = true;orientationDirty=true;
            ApplyExterior();
        }

        void CaptureExterior()
        {
            if (captured) return;
            var director = navigation.Director; var camera = director.controller.viewCamera;
            ambient = RenderSettings.ambientLight; ambientMode = RenderSettings.ambientMode; skybox = RenderSettings.skybox;
            fog = RenderSettings.fog; fogColor = RenderSettings.fogColor; fogMode = RenderSettings.fogMode;
            fogDensity = RenderSettings.fogDensity; fogStart = RenderSettings.fogStartDistance; fogEnd = RenderSettings.fogEndDistance;
            background = camera.backgroundColor; clearFlags = camera.clearFlags; cameraFar = camera.farClipPlane; cameraMask = camera.cullingMask;
            // Encounter12 already hides its planet. Default exterior should be restored on leaving this environment.
            planetActive = director.planet != null; starsActive = director.stars && director.stars.gameObject.activeSelf;
            captured = true;
        }

        // Called by the shared director immediately after its default reset, before any frame renders.
        public void ApplyExterior()
        {
            if (!prepared || !settings || !navigation) return;
            var director = navigation.Director; var camera = director.controller.viewCamera;
            if (director.planet) director.planet.gameObject.SetActive(false);
            if (director.stars) director.stars.gameObject.SetActive(false);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = settings.fogColor;
            camera.farClipPlane = settings.cameraFar; camera.cullingMask |= 1 << EnvironmentLayer;
            RenderSettings.ambientMode = settings.ambientMode; RenderSettings.ambientLight = settings.ambient;
            RenderSettings.skybox = settings.skybox; RenderSettings.fog = settings.fog; RenderSettings.fogColor = settings.fogColor;
            RenderSettings.fogMode = settings.fogMode; RenderSettings.fogDensity = settings.fogDensity;
            RenderSettings.fogStartDistance = settings.fogStart; RenderSettings.fogEndDistance = settings.fogEnd;
        }

        void RestoreExterior()
        {
            prepared = false;
            if (!captured || !navigation || !navigation.Director) return;
            var director = navigation.Director; var camera = director.controller.viewCamera;
            if (!camera) { captured = false; return; } // Play Mode exit can destroy the camera before this component.
            if (director.planet) director.planet.gameObject.SetActive(planetActive);
            if (director.stars) director.stars.gameObject.SetActive(starsActive);
            camera.backgroundColor = background; camera.clearFlags = clearFlags; camera.farClipPlane = cameraFar; camera.cullingMask = cameraMask;
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient; RenderSettings.skybox = skybox;
            RenderSettings.fog = fog; RenderSettings.fogColor = fogColor; RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity; RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
            captured = false;
        }

        void OnDestroy()
        {
            if (destroying) return; destroying = true;
            SceneManager.sceneLoaded -= SceneLoaded;
            if (environmentRoot) environmentRoot.SetActive(false);
            RestoreExterior();
            if (environment.IsValid() && environment.isLoaded) SceneManager.UnloadSceneAsync(environment);
        }
    }
}
