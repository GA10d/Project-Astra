using UnityEngine;

namespace AstraCabin
{
    public sealed class DistantPlanet : MonoBehaviour
    {
        public float degreesPerSecond = 0.45f;
        void Update() { transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self); }
    }
}
