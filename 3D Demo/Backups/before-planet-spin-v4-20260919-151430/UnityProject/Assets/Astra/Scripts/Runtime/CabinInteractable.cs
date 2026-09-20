using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace AstraCabin
{
    public enum InteractionKind { Button, Switch, Lever, Hatch, Valve }

    [Serializable]
    public sealed class CabinBoolEvent : UnityEvent<bool> { }

    /// <summary>A physically separate control with reversible movement and a focused hover outline.</summary>
    [DisallowMultipleComponent]
    public sealed class CabinInteractable : MonoBehaviour
    {
        public string title = "操作";
        [TextArea] public string description = "";
        public InteractionKind kind = InteractionKind.Button;
        public Transform movingPart;
        public Vector3 motionAxis = Vector3.right;
        [Tooltip("Degrees for rotating parts; metres for pushbuttons.")]
        public float travel = 28f;
        public float duration = 0.28f;
        [Tooltip("The authored moving-part pose represents this initial state; startup never changes the authored pose.")]
        public bool startsOn;
        public bool toggleState = true;
        public Renderer[] highlightRenderers;
        public Shader outlineShader;
        public CabinBoolEvent onChanged = new CabinBoolEvent();
        public bool overrideSound;
        public CabinSound sound = CabinSound.Click;

        public bool IsOn { get; private set; }
        public bool IsBusy { get; private set; }
        public bool IsHovered { get; private set; }
        public int ActivationCount { get; private set; }

        private Vector3 restPosition;
        private Quaternion restRotation;
        private bool initialized;
        private Material outlineMaterial;
        private readonly List<GameObject> outlines = new List<GameObject>();

        private void Awake() { Initialize(); }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            if (!movingPart) movingPart = transform;
            restPosition = movingPart.localPosition;
            restRotation = movingPart.localRotation;
            IsOn = startsOn;
            if (kind != InteractionKind.Button) SetMotion(IsOn ? 1f : 0f);
        }

        /// <summary>Returns false when disabled or already animating; no duplicate callback is fired.</summary>
        public bool TryInteract()
        {
            if (!isActiveAndEnabled || IsBusy) return false;
            Initialize();
            StartCoroutine(AnimateInteraction());
            return true;
        }

        public void Interact() { TryInteract(); }

        private IEnumerator AnimateInteraction()
        {
            IsBusy = true;
            bool oldState = IsOn;
            IsOn = toggleState ? !IsOn : true;
            ActivationCount++;
            if (CabinAudio.Instance) CabinAudio.Instance.Play(overrideSound ? sound : SoundForKind(), transform.position);
            onChanged.Invoke(IsOn);
            float time = 0f;
            float animationTime = Mathf.Max(0.08f, duration);
            while (time < animationTime)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / animationTime);
                if (kind == InteractionKind.Button)
                {
                    // A quick depression followed by a slower sprung return; state remains latched electrically.
                    float press = t < 0.36f
                        ? Mathf.SmoothStep(0f, 1f, t / 0.36f)
                        : Mathf.SmoothStep(1f, 0f, (t - 0.36f) / 0.64f);
                    SetMotion(press);
                }
                else
                {
                    SetMotion(Mathf.Lerp(oldState ? 1f : 0f, IsOn ? 1f : 0f, Mathf.SmoothStep(0f, 1f, t)));
                }
                yield return null;
            }
            SetMotion(kind == InteractionKind.Button ? 0f : (IsOn ? 1f : 0f));
            if (!toggleState) IsOn = false;
            IsBusy = false;
        }

        private void SetMotion(float amount)
        {
            if (!movingPart) return;
            Vector3 axis = motionAxis.sqrMagnitude > 0.001f ? motionAxis.normalized : Vector3.right;
            if (kind == InteractionKind.Button)
                movingPart.localPosition = restPosition + axis * (travel * amount);
            else
                movingPart.localRotation = restRotation * Quaternion.AngleAxis(travel * (amount - (startsOn ? 1f : 0f)), axis);
        }

        private CabinSound SoundForKind()
        {
            switch (kind)
            {
                case InteractionKind.Switch: return CabinSound.Switch;
                case InteractionKind.Lever: return CabinSound.Lever;
                case InteractionKind.Hatch: return CabinSound.Hatch;
                case InteractionKind.Valve: return CabinSound.Valve;
                default: return CabinSound.Click;
            }
        }

        public void SetHover(bool enabled)
        {
            if (enabled && !isActiveAndEnabled) enabled = false;
            if (IsHovered == enabled) return;
            IsHovered = enabled;
            if (enabled && outlines.Count == 0) CreateOutlines();
            foreach (GameObject outline in outlines)
                if (outline) outline.SetActive(enabled);
        }

        private void CreateOutlines()
        {
            Shader shader = outlineShader ? outlineShader : Shader.Find("Astra/Outline");
            if (!shader) return;
            outlineMaterial = new Material(shader) { name = "Hover outline (instance)", hideFlags = HideFlags.DontSave };
            outlineMaterial.SetColor("_OutlineColor", new Color(1f, 0.76f, 0.12f, 1f));
            outlineMaterial.SetFloat("_OutlineWidth", 0.0055f);
            Renderer[] sources = highlightRenderers != null && highlightRenderers.Length > 0
                ? highlightRenderers : GetComponentsInChildren<Renderer>(true);
            foreach (Renderer source in sources)
            {
                if (!source || source.name.StartsWith("__HoverOutline", StringComparison.Ordinal)) continue;
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                var child = new GameObject("__HoverOutline") { hideFlags = HideFlags.DontSave, layer = 2 };
                child.transform.SetParent(source.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                Material[] materials = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
                for (int i = 0; i < materials.Length; i++) materials[i] = outlineMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                outlines.Add(child);
            }
        }

        private void OnDisable()
        {
            SetHover(false);
            StopAllCoroutines();
            IsBusy = false;
            if (initialized) SetMotion(kind == InteractionKind.Button ? 0f : (IsOn ? 1f : 0f));
        }

        private void OnDestroy()
        {
            foreach (GameObject outline in outlines) if (outline) Destroy(outline);
            if (outlineMaterial) Destroy(outlineMaterial);
        }
    }
}
