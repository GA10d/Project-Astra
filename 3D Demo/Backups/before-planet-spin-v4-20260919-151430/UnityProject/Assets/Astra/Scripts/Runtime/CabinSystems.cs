using System.Collections.Generic;
using UnityEngine;

namespace AstraCabin
{
    /// <summary>Small, deterministic cabin simulation. UI controls connect through persistent bool events.</summary>
    [DisallowMultipleComponent]
    public sealed class CabinSystems : MonoBehaviour
    {
        public Light[] cabinLights;
        public Light[] emergencyLights;
        public Renderer[] cabinLampRenderers;
        public Renderer[] screenRenderers;
        public Transform printerHead;
        public Vector3 printerMotionAxis = Vector3.right;
        public float printerTravel = 0.26f;
        public float printerCyclesPerSecond = 0.32f;
        public Renderer printerStatusRenderer;
        public Renderer radioIndicatorRenderer;
        public Renderer airIndicatorRenderer;
        public Light printerLight;
        public Transform ventilationFan;
        public float fanDegreesPerSecond = 110f;
        public bool initialLightsOn = true;
        public bool initialMainPowerOn = true;
        public bool initialAirOn = true;
        public bool initialPrinterOn;
        public bool initialRadioOn;

        public bool LightsOn { get; private set; }
        public bool MainPowerOn { get; private set; }
        public bool PrinterOn { get; private set; }
        public bool RadioOn { get; private set; }
        public bool AirOn { get; private set; }
        public float PrinterPhase { get; private set; }

        private readonly Dictionary<Light, float> initialIntensities = new Dictionary<Light, float>();
        private readonly Dictionary<Renderer, Color> indicatorColors = new Dictionary<Renderer, Color>();
        private readonly Dictionary<Renderer, Color> lampEmissions = new Dictionary<Renderer, Color>();
        private readonly Dictionary<Renderer, Color> lampColors = new Dictionary<Renderer, Color>();
        private MaterialPropertyBlock properties;
        private Vector3 printerRest;
        private bool initialized;

        private void Awake() { Initialize(); }
        private void Start() { Refresh(); }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            properties = new MaterialPropertyBlock();
            LightsOn = initialLightsOn;
            MainPowerOn = initialMainPowerOn;
            AirOn = initialAirOn;
            PrinterOn = initialPrinterOn;
            RadioOn = initialRadioOn;
            CacheLights(cabinLights);
            CacheLights(emergencyLights);
            if (printerLight) initialIntensities[printerLight] = printerLight.intensity;
            if (printerHead) printerRest = printerHead.localPosition;
        }

        private void CacheLights(Light[] lights)
        {
            if (lights == null) return;
            foreach (Light light in lights)
                if (light && !initialIntensities.ContainsKey(light)) initialIntensities.Add(light, light.intensity);
        }

        private void Update()
        {
            if (MainPowerOn && PrinterOn && printerHead)
            {
                PrinterPhase += Time.deltaTime * printerCyclesPerSecond * Mathf.PI * 2f;
                if (PrinterPhase > Mathf.PI * 200f) PrinterPhase -= Mathf.PI * 200f;
                Vector3 axis = printerMotionAxis.sqrMagnitude > 0.001f ? printerMotionAxis.normalized : Vector3.right;
                printerHead.localPosition = printerRest + axis * ((1f - Mathf.Cos(PrinterPhase)) * 0.5f * printerTravel);
            }
            if (MainPowerOn && AirOn && ventilationFan)
                ventilationFan.Rotate(Vector3.forward, fanDegreesPerSecond * Time.deltaTime, Space.Self);
        }

        public void SetLights(bool value) { Initialize(); LightsOn = value; Refresh(); }
        public void SetMainPower(bool value) { Initialize(); MainPowerOn = value; Refresh(); }
        public void SetPrinter(bool value) { Initialize(); PrinterOn = value; Refresh(); }
        public void SetRadio(bool value) { Initialize(); RadioOn = value; Refresh(); }
        public void SetAir(bool value) { Initialize(); AirOn = value; Refresh(); }

        public void Refresh()
        {
            Initialize();
            SetLightGroup(cabinLights, MainPowerOn && LightsOn, 1f);
            SetLampSurfaces(MainPowerOn && LightsOn);
            // The emergency circuit is independent of both the main-light switch and the main isolator.
            SetLightGroup(emergencyLights, true, MainPowerOn ? 0.18f : 1f);
            if (printerLight)
            {
                printerLight.enabled = MainPowerOn && PrinterOn;
                float original;
                if (initialIntensities.TryGetValue(printerLight, out original)) printerLight.intensity = original;
            }
            if (screenRenderers != null)
            {
                foreach (Renderer screen in screenRenderers)
                {
                    if (!screen) continue;
                    screen.GetPropertyBlock(properties);
                    properties.SetFloat("_Power", MainPowerOn ? 1f : 0f);
                    properties.SetColor("_EmissionColor", MainPowerOn ? new Color(0.18f, 0.75f, 0.43f) : Color.black);
                    screen.SetPropertyBlock(properties);
                    properties.Clear();
                }
            }
            SetIndicator(printerStatusRenderer, MainPowerOn && PrinterOn);
            SetIndicator(radioIndicatorRenderer, MainPowerOn && RadioOn);
            SetIndicator(airIndicatorRenderer, MainPowerOn && AirOn);
            if (CabinAudio.Instance) CabinAudio.Instance.SetAmbientEnabled(MainPowerOn && AirOn);
        }

        private void SetLightGroup(Light[] lights, bool enabled, float multiplier)
        {
            if (lights == null) return;
            foreach (Light light in lights)
            {
                if (!light) continue;
                if (!initialIntensities.ContainsKey(light)) initialIntensities[light] = light.intensity;
                light.enabled = enabled;
                light.intensity = initialIntensities[light] * multiplier;
            }
        }

        private void SetIndicator(Renderer renderer, bool enabled)
        {
            if (!renderer) return;
            Color color;
            if (!indicatorColors.TryGetValue(renderer, out color))
            {
                Material material = renderer.sharedMaterial;
                color = material && material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : new Color(0.2f, 0.9f, 0.4f);
                if (color.maxColorComponent < 0.01f) color = new Color(0.2f, 0.9f, 0.4f);
                indicatorColors[renderer] = color;
            }
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_EmissionColor", enabled ? color : Color.black);
            properties.SetColor("_Color", enabled ? color : color * 0.13f);
            renderer.SetPropertyBlock(properties);
            properties.Clear();
        }

        private void SetLampSurfaces(bool enabled)
        {
            if (cabinLampRenderers == null) return;
            foreach (Renderer renderer in cabinLampRenderers)
            {
                if (!renderer) continue;
                Material material = renderer.sharedMaterial;
                if (!lampEmissions.ContainsKey(renderer))
                {
                    lampEmissions[renderer] = material && material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : new Color(1f, 0.6f, 0.2f);
                    lampColors[renderer] = material && material.HasProperty("_Color") ? material.GetColor("_Color") : new Color(0.8f, 0.65f, 0.3f);
                }
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_EmissionColor", enabled ? lampEmissions[renderer] : Color.black);
                properties.SetColor("_Color", enabled ? lampColors[renderer] : lampColors[renderer] * 0.15f);
                renderer.SetPropertyBlock(properties);
                properties.Clear();
            }
        }
    }
}
