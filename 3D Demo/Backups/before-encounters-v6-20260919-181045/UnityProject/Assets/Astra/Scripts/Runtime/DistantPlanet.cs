using UnityEngine;

namespace AstraCabin
{
    public sealed class DistantPlanet : MonoBehaviour
    {
        [Tooltip("Slow visible axial rotation: 1.2 degrees/second = one turn in five minutes.")]
        [Range(0f, 3f)] public float degreesPerSecond = 1.2f;
        public bool animate = true;
        public double ElapsedSeconds { get; private set; }
        public float CurrentAngleDegrees { get; private set; }
        public Quaternion RestRotation { get; private set; }
        bool initialized;

        void Awake() { Initialize(); }
        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            RestRotation = transform.localRotation;
        }
        void Update()
        {
            if (animate && Time.deltaTime > 0f) SetElapsedForTest(ElapsedSeconds + Time.deltaTime);
        }

        // An absolute, double-precision clock avoids accumulating transform drift.
        // Planet.shader samples its clouds in object space, so the visible surface
        // rotates with the sphere while the light direction remains fixed in space.
        public void SetElapsedForTest(double seconds)
        {
            Initialize();
            ElapsedSeconds = double.IsNaN(seconds) || double.IsInfinity(seconds) ? 0 : System.Math.Max(0, seconds);
            CurrentAngleDegrees = (float)((ElapsedSeconds * degreesPerSecond) % 360.0);
            transform.localRotation = RestRotation * Quaternion.AngleAxis(CurrentAngleDegrees, Vector3.up);
        }
    }
}
