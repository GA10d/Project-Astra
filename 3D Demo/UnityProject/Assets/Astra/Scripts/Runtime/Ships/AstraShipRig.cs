using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace AstraCabin.Ships
{
    public enum AstraShipUsage { PlayerShip, Scenery }
    public interface IAstraShipWorldRebaseAware { void OnShipWorldRebased(Matrix4x4 previousWorldToCurrentWorld); }

    /// <summary>A single-player cabin package. Rebase before legacy world-coordinate systems wake.</summary>
    [DefaultExecutionOrder(-10000), DisallowMultipleComponent]
    public sealed class AstraShipRig : MonoBehaviour
    {
        [Header("Placement")]
        public AstraShipUsage usage = AstraShipUsage.PlayerShip;
        public bool rotateGravityWithScene = true;
        [Header("Bound prefab references")]
        public GameObject systemsRoot;
        public Transform hull;
        public Camera cockpitCamera;
        public UnityEvent onReady = new UnityEvent();
        public static AstraShipRig Active { get; private set; }
        public bool Ready { get; private set; }
        public Matrix4x4 PreviousWorldToCurrentWorld { get; private set; } = Matrix4x4.identity;
        public Pose AuthoredPose { get; private set; }
        Vector3 savedGravity;
        bool ownsGravity;
        static readonly int UseMatrix = Shader.PropertyToID("_UseBreathFrameMatrix");
        static readonly int WorldToAuth = Shader.PropertyToID("_BreathWorldToAuth");
        static readonly int AuthToWorld = Shader.PropertyToID("_BreathAuthToWorld");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = null; }

        void Awake()
        {
            if (usage == AstraShipUsage.Scenery) { MakeScenery(); return; }
            if ((transform.lossyScale - Vector3.one).sqrMagnitude > .00001f)
            { Debug.LogError("ASTRA Ship: keep the player ship and its parents at scale (1,1,1).", this); MakeScenery(); return; }
            if (!systemsRoot || !hull || !cockpitCamera)
            { Debug.LogError("ASTRA Ship: prefab bindings are incomplete; restore the prefab references.", this); MakeScenery(); return; }
            if (Active && Active != this)
            { Debug.LogWarning("ASTRA Ship: only one player ship is active; this additional instance becomes scenery.", this); MakeScenery(); return; }
            // Retire every other authored player package before their child Awake methods run.
            Active = this;
            foreach (var other in FindObjectsOfType<AstraShipRig>())
                if (other != this) other.MakeScenery();
            var externalControllers = FindObjectsOfType<CabinController>().Where(c => !c.transform.IsChildOf(transform)).ToArray();
            if (externalControllers.Length != 0)
            { Debug.LogError("ASTRA Ship: the scene already has an independent cabin controller. Remove that rig before placing a player ship.", this); Active = null; MakeScenery(); return; }

            AuthoredPose = new Pose(transform.position, transform.rotation);
            var oldFromNew = Matrix4x4.TRS(AuthoredPose.position, AuthoredPose.rotation, Vector3.one);
            PreviousWorldToCurrentWorld = oldFromNew.inverse;
            var roots = gameObject.scene.GetRootGameObjects();
            // Shader material property blocks preserve per-instance authored world frames.
            foreach (var root in roots)
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (!material || !material.HasProperty(UseMatrix)) continue;
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, i);
                    var oldFrame = Matrix4x4.TRS(material.GetVector("_BreathFrameOffset"), Quaternion.Euler(0, material.GetFloat("_BreathFrameYaw"), 0), Vector3.one);
                    var newFrame = oldFrame * oldFromNew;
                    block.SetFloat(UseMatrix, 1); block.SetMatrix(WorldToAuth, newFrame); block.SetMatrix(AuthToWorld, newFrame.inverse);
                    renderer.SetPropertyBlock(block, i);
                }
            }
            foreach (var root in roots)
            {
                root.transform.SetPositionAndRotation(PreviousWorldToCurrentWorld.MultiplyPoint3x4(root.transform.position), Quaternion.Inverse(AuthoredPose.rotation) * root.transform.rotation);
            }
            // Disable competing scene cameras/listeners, without touching cameras generated later by apps.
            foreach (var camera in FindObjectsOfType<Camera>())
                if (!camera.transform.IsChildOf(transform)) camera.enabled = false;
            foreach (var listener in FindObjectsOfType<AudioListener>())
                if (!listener.transform.IsChildOf(transform)) listener.enabled = false;
            if (rotateGravityWithScene)
            { savedGravity = Physics.gravity; Physics.gravity = PreviousWorldToCurrentWorld.MultiplyVector(savedGravity); ownsGravity = true; }
            foreach (var component in roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
                if (component is IAstraShipWorldRebaseAware aware) aware.OnShipWorldRebased(PreviousWorldToCurrentWorld);
            Ready = true;
        }
        IEnumerator Start() { if (!Ready) yield break; yield return null; if (Ready) onReady.Invoke(); }
        void MakeScenery()
        {
            usage = AstraShipUsage.Scenery; Ready = false;
            if (systemsRoot) systemsRoot.SetActive(false);
            foreach (var c in GetComponentsInChildren<Camera>(true)) c.gameObject.SetActive(false);
            foreach (var b in GetComponentsInChildren<CabinInteractable>(true)) b.enabled = false;
            foreach (var b in GetComponentsInChildren<CabinGaugeMotion>(true)) b.enabled = false;
            foreach (var l in GetComponentsInChildren<Light>(true)) l.enabled = false;
        }
        void OnDestroy()
        {
            if (Active != this) return;
            if (ownsGravity) Physics.gravity = savedGravity;
            Active = null;
        }
        public Vector3 AuthoredPointToRuntime(Vector3 point) => PreviousWorldToCurrentWorld.MultiplyPoint3x4(point);
        public Vector3 RuntimePointToAuthored(Vector3 point) => PreviousWorldToCurrentWorld.inverse.MultiplyPoint3x4(point);
    }
}


