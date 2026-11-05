using UnityEngine;
using UnityEngine.Rendering;

namespace AstraCabin.Galaxies.Zyl
{
    /// <summary>The transform marks the cabin origin in the authored, ship-free environment.</summary>
    public sealed class StomachEnvironmentSettings : MonoBehaviour
    {
        public Color ambient = new Color(.19f, .16f, .17f);
        public AmbientMode ambientMode = AmbientMode.Flat;
        public bool fog = true;
        public Color fogColor = new Color(.105f, .052f, .059f);
        public FogMode fogMode = FogMode.ExponentialSquared;
        public float fogDensity = .005f, fogStart, fogEnd = 300;
        public float cameraFar = 1800;
        public Material skybox;
    }
}
