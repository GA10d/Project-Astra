using UnityEngine;

namespace AstraCabin
{
    // At most two spot shadow maps, selected by cabin turn rather than mouse look.
    [RequireComponent(typeof(Camera))]
    public sealed class CabinLightBudget : MonoBehaviour
    {
        public Light[] keyLights;
        CabinController controller;
        float[] strengths;
        void Awake()
        {
            controller = FindObjectOfType<CabinController>();
            if (keyLights == null) return;
            strengths = new float[keyLights.Length];
            for (int i = 0; i < keyLights.Length; i++)
                strengths[i] = keyLights[i] ? keyLights[i].shadowStrength : 1f;
        }
        void OnPreCull()
        {
            if (keyLights == null) return;
            // CurrentYaw excludes mouse parallax, observation movement and shaking.
            Vector3 forward = Quaternion.Euler(0, controller ? controller.CurrentYaw : 0, 0) * Vector3.forward;
            Vector3 origin = controller ? controller.centerPosition : Vector3.zero;
            int first = -1, second = -1;
            float best = float.NegativeInfinity, next = float.NegativeInfinity;
            for (int i = 0; i < keyLights.Length; i++)
            {
                if (!keyLights[i] || !keyLights[i].enabled) continue;
                Vector3 direction = keyLights[i].transform.position - origin;
                direction.y = 0;
                float score = Vector3.Dot(forward, direction.normalized);
                if (score > best) { second = first; next = best; first = i; best = score; }
                else if (score > next) { second = i; next = score; }
            }
            for (int i = 0; i < keyLights.Length; i++)
            {
                if (!keyLights[i]) continue;
                float score = i == first ? best : i == second ? next : 0;
                // Fade to zero before recycling a side-wall slot during Q/E turns.
                float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01(score / .35f));
                keyLights[i].shadowStrength = strengths[i] * fade;
                keyLights[i].shadows = fade > .001f ? LightShadows.Soft : LightShadows.None;
            }
        }
    }
}
