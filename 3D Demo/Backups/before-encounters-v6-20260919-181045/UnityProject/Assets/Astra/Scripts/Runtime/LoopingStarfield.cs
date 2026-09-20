using System;
using UnityEngine;

namespace AstraCabin
{
    /// <summary>
    /// True 3D stars outside the viewport. Position, volume and velocity are LOCAL
    /// to this object. For a D wall facing -X, leave rotation at identity and put
    /// this transform at the window center. The default volume extends outward.
    /// </summary>
    public class LoopingStarfield : MonoBehaviour
    {
        public Vector3 volumeCenter = new Vector3(-18f, 0f, 0f);
        public Vector3 size = new Vector3(26f, 24f, 30f);
        public Vector3 velocity = new Vector3(0f, -0.028f, 0.20f);
        [Range(8, 2048)] public int starCount = 360;
        public int seed = 1943;
        [Tooltip("Assign in the generated scene so the player build retains this shader.")]
        public Shader starShader;
        [Range(0.01f, 0.4f)] public float pointSize = 0.075f;
        public bool animate = true;
        public Vector3[] StarPositions { get { return positions; } }
        public bool Initialized { get { return particles != null && positions != null; } }

        Vector3[] positions;
        ParticleSystem.Particle[] points;
        ParticleSystem particles;
        Material material;
        Texture2D sprite;

        void Start() { if (!Initialized) Initialize(); }

        public void Initialize()
        {
            starCount = Mathf.Clamp(starCount, 8, 2048);
            size = new Vector3(Mathf.Max(0.01f,size.x),Mathf.Max(0.01f,size.y),Mathf.Max(0.01f,size.z));
            particles = GetComponent<ParticleSystem>();
            if (particles == null) particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = starCount;
            main.startSpeed = 0f;
            main.startLifetime = 100000f;
            main.startSize = pointSize;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            if (material == null)
            {
                Shader shader = starShader != null ? starShader : Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Particles/Additive");
                material = new Material(shader) { name = "Astra_Runtime_StarPoints" };
                sprite = MakeRadialSprite();
                material.mainTexture = sprite;
                if (material.HasProperty("_Color")) material.SetColor("_Color",Color.white);
                if (material.HasProperty("_TintColor")) material.SetColor("_TintColor",Color.white);
                material.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_ZWrite",0);
                material.SetOverrideTag("RenderType","Transparent");
                material.renderQueue = 3000;
                material.EnableKeyword("_ALPHABLEND_ON");
            }
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.minParticleSize = 0.0002f;
            renderer.maxParticleSize = 0.008f;
            positions = new Vector3[starCount];
            points = new ParticleSystem.Particle[starCount];
            var random = new System.Random(seed);
            for (int i = 0; i < starCount; i++)
            {
                positions[i] = volumeCenter + Vector3.Scale(size,new Vector3(
                    (float)random.NextDouble()-0.5f,(float)random.NextDouble()-0.5f,(float)random.NextDouble()-0.5f));
                float brightness = Mathf.Lerp(0.32f,0.95f,(float)random.NextDouble());
                bool dust = i % 17 == 0;
                points[i] = new ParticleSystem.Particle
                {
                    position = positions[i],
                    startSize = pointSize * Mathf.Lerp(0.45f,1.45f,(float)random.NextDouble())*(dust?1.35f:1f),
                    startColor = dust ? new Color(0.55f,0.69f,0.67f,0.24f) : new Color(brightness*0.87f,brightness*0.94f,brightness,0.92f),
                    startLifetime = 100000f,
                    remainingLifetime = 100000f,
                    velocity = Vector3.zero,
                    randomSeed = (uint)(i+1)
                };
            }
            particles.SetParticles(points,points.Length);
            // Simulation is owned by AdvanceTime, so no engine drift or finite lifetime.
            particles.Pause(true);
        }

        void Update() { if (animate) AdvanceTime(Time.deltaTime); }

        /// <summary>Deterministic, frame-rate-independent local-space drift and wrap.</summary>
        public void AdvanceTime(float deltaTime)
        {
            if (!Initialized) Initialize();
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            Vector3 step = velocity * deltaTime;
            Vector3 minimum = volumeCenter-size*0.5f;
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i] + step;
                p.x = minimum.x+Mathf.Repeat(p.x-minimum.x,Mathf.Max(0.01f,size.x));
                p.y = minimum.y+Mathf.Repeat(p.y-minimum.y,Mathf.Max(0.01f,size.y));
                p.z = minimum.z+Mathf.Repeat(p.z-minimum.z,Mathf.Max(0.01f,size.z));
                positions[i] = p;
                points[i].position = p;
            }
            particles.SetParticles(points,points.Length);
        }

        static Texture2D MakeRadialSprite()
        {
            const int width = 32;
            var texture = new Texture2D(width,width,TextureFormat.RGBA32,false);
            texture.name = "Astra_Procedural_PointSprite";
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[width*width];
            for (int y = 0; y < width; y++) for (int x = 0; x < width; x++)
            {
                Vector2 p = (new Vector2(x+0.5f,y+0.5f)/width-new Vector2(0.5f,0.5f))*2f;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f-p.magnitude),2.8f);
                pixels[y*width+x] = new Color(1f,1f,1f,alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false,true);
            return texture;
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (sprite != null) Destroy(sprite);
        }
    }
}
