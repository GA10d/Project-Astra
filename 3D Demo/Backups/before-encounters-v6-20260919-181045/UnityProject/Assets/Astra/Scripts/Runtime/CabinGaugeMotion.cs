using System;
using UnityEngine;

namespace AstraCabin
{
    /// <summary>
    /// A slow, bounded instrument flutter about the authored spindle. The
    /// pointer's rest rotation is never accumulated into the next frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CabinGaugeMotion : MonoBehaviour
    {
        [Tooltip("Dial normal in the imported model's world coordinates.")]
        public Vector3 worldAxis = Vector3.forward;
        [Range(0f, 5f)] public float amplitudeDegrees = 2.4f;
        [Range(0.01f, 0.5f)] public float cyclesPerSecond = 0.085f;
        public int phaseSeed = 1;

        public float CurrentOffsetDegrees { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public Quaternion RestLocalRotation { get; private set; }
        public Vector3 LocalAxis { get; private set; }
        public bool Initialized { get { return initialized; } }

        private bool initialized;
        private double phase;
        private double rate;

        private void Awake() { Initialize(); }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            RestLocalRotation = transform.localRotation;
            Vector3 axis = worldAxis.sqrMagnitude > 0.0001f ? worldAxis.normalized : Vector3.forward;
            LocalAxis = transform.InverseTransformDirection(axis).normalized;
            // Explicit arithmetic keeps phase reproducible across machines.
            uint seed = unchecked((uint)phaseSeed * 1664525u + 1013904223u);
            phase = (seed & 65535u) / 65535.0 * Math.PI * 2.0;
            rate = 0.81 + ((seed >> 16) & 65535u) / 65535.0 * 0.38;
            Apply(0.0);
        }

        private void Update()
        {
            // Time.deltaTime is zero while the cabin pause menu is open.
            if (Time.deltaTime <= 0f) return;
            Initialize();
            Apply(ElapsedSeconds + Time.deltaTime);
        }

        /// <summary>Pure, deterministic offset function, bounded by amplitudeDegrees.</summary>
        public float EvaluateOffset(double seconds)
        {
            Initialize();
            double t = Math.Max(0.0, seconds);
            double w = t * Math.PI * 2.0 * Math.Max(0.01, cyclesPerSecond) * rate;
            double flutter = 0.68 * Math.Sin(w + phase)
                           + 0.24 * Math.Sin(w * 1.731 + phase * 0.71)
                           + 0.08 * Math.Sin(w * 4.137 + phase * 1.37);
            // Fade from the authored rest pose instead of jumping on load.
            double settle = 1.0 - Math.Exp(-t * 0.8);
            return (float)(flutter * settle * Mathf.Clamp(amplitudeDegrees, 0f, 5f));
        }

        private void Apply(double seconds)
        {
            ElapsedSeconds = Math.Max(0.0, seconds);
            CurrentOffsetDegrees = EvaluateOffset(ElapsedSeconds);
            transform.localRotation = RestLocalRotation * Quaternion.AngleAxis(CurrentOffsetDegrees, LocalAxis);
        }

        /// <summary>Absolute-time QA sample; deliberately does not change enabled/paused state.</summary>
        public void SetElapsedForTest(double seconds)
        {
            Initialize();
            Apply(seconds);
        }

        private void OnDisable()
        {
            if (!initialized) return;
            transform.localRotation = RestLocalRotation;
            CurrentOffsetDegrees = 0f;
        }
    }
}
