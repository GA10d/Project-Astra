using System.Collections.Generic;
using UnityEngine;

namespace AstraCabin
{
    /// <summary>Original, self-contained geometry for the fourth demonstration event.</summary>
    public sealed class CrimsonEyeSetpiece : MonoBehaviour
    {
        public bool Active { get; private set; }
        public bool Warping { get; private set; }
        public int LavaPlanetCount { get { return planets.Count; } }
        public Transform EyeRoot { get; private set; }
        public Transform GeneratedRoot { get { return sceneRoot; } }
        public int TentacleCount { get { return tentacles.Count; } }
        public float Elapsed { get { return elapsed; } }
        const float WarpDuration = 2.15f;
        const int TubeRings = 17, TubeSides = 8;
        SetpieceDirector director;
        CabinController controller;
        Transform sceneRoot, destinationRoot, streakRoot;
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Transform> planets = new List<Transform>();
        readonly List<Tentacle> tentacles = new List<Tentacle>();
        readonly List<LineRenderer> streaks = new List<LineRenderer>();
        readonly List<Vector3> streakSeeds = new List<Vector3>();
        float elapsed;
        Material nebula, eyeMaterial, fleshMaterial;
        Camera observingCamera;

        sealed class Tentacle
        {
            public Mesh mesh;
            public Vector3[] vertices;
            public float angle, length, phase;
        }

        public void Initialize(SetpieceDirector value)
        {
            director = value;
            controller = FindObjectOfType<CabinController>();
            observingCamera = controller ? controller.viewCamera : Camera.main;
        }

        public void Show()
        {
            Clear();
            Active = true; Warping = true; elapsed = 0;
            sceneRoot = NewRoot("04 | Crimson jump", transform);
            destinationRoot = NewRoot("Red nebula and living planet", sceneRoot);
            BuildDestination();
            BuildStreaks();
            SetReveal(0);
            if (director) { director.Shake(.35f, 1.8f); director.PlayCue("warp", .82f); }
        }

        void Update()
        {
            if (!Active || (controller && controller.IsPaused)) return;
            float dt = Time.deltaTime;
            elapsed += dt;
            if (Warping)
            {
                float t = Mathf.Clamp01(elapsed / WarpDuration);
                SetReveal(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.48f, 1, t)));
                AnimateStreaks(t);
                if (t >= 1)
                {
                    Warping = false;
                    streakRoot.gameObject.SetActive(false);
                    if (director) { director.Shake(.2f, .5f); director.PlayCue("monster", .65f); }
                }
            }
            foreach (Material m in materials) if (m && m.HasProperty("_Clock")) m.SetFloat("_Clock", elapsed);
            for (int i = 0; i < planets.Count; i++) planets[i].Rotate(Vector3.up, dt * (.26f + .045f * i), Space.Self);
            if (EyeRoot)
            {
                EyeRoot.localPosition = new Vector3(45, 2.4f + Mathf.Sin(elapsed * .18f) * .3f, 0);
                Vector3 target = observingCamera ? observingCamera.transform.position : new Vector3(0, 1.5f, 0);
                EyeRoot.rotation = Quaternion.FromToRotation(Vector3.left, (target - EyeRoot.position).normalized);
                // The iris is on a genuine sphere. The small pulse is breathing, not a billboard scale effect.
                EyeRoot.localScale = Vector3.one * (1 + Mathf.Sin(elapsed * .39f) * .008f);
            }
            foreach (Tentacle t in tentacles) AnimateTentacle(t, elapsed);
        }

        public void Clear()
        {
            Active = false; Warping = false; elapsed = 0;
            if (sceneRoot) { sceneRoot.gameObject.SetActive(false); Destroy(sceneRoot.gameObject); }
            foreach (Mesh m in meshes) if (m) Destroy(m);
            foreach (Material m in materials) if (m) Destroy(m);
            materials.Clear(); meshes.Clear(); planets.Clear(); tentacles.Clear(); streaks.Clear(); streakSeeds.Clear();
            sceneRoot = destinationRoot = streakRoot = EyeRoot = null;
            nebula = eyeMaterial = fleshMaterial = null;
        }

        void OnDestroy() { Clear(); }

        void BuildDestination()
        {
            nebula = MakeMaterial("Astra/Setpieces/CrimsonNebula", "Crimson dust volume");
            var dome = NewRoot("Curved red nebula horizon", destinationRoot).gameObject;
            dome.AddComponent<MeshFilter>().sharedMesh = MakeDome();
            dome.AddComponent<MeshRenderer>().sharedMaterial = nebula;

            Vector4[] positions = {
                new Vector4(34, 12, -19, 5.5f), new Vector4(48, -13, -22, 8),
                new Vector4(62, 21, -23, 8), new Vector4(44, 13, 22, 6.2f),
                new Vector4(57, -17, 23, 9), new Vector4(72, 29, 7, 7.5f),
                new Vector4(85, -32, -4, 10), new Vector4(90, 4, 46, 8),
                new Vector4(83, 2, -42, 5.2f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                Vector4 p = positions[i];
                Material lava = MakeMaterial("Astra/Setpieces/CrimsonLava", "Molten geology " + i);
                lava.SetFloat("_Seed", i * 1.73f + .47f);
                lava.SetColor("_Glow", i % 3 == 0 ? new Color(1.6f, .27f, .013f) : new Color(1.25f, .095f, .008f));
                var planet = Primitive("Lava planet " + (i + 1), PrimitiveType.Sphere, destinationRoot, lava);
                planet.localPosition = new Vector3(p.x, p.y, p.z);
                planet.localScale = Vector3.one * (p.w * 2);
                planet.localRotation = Quaternion.Euler(i * 31, i * 67, i * 17);
                planets.Add(planet);
            }
            EyeRoot = NewRoot("Planet-scale eye entity", destinationRoot);
            EyeRoot.localPosition = new Vector3(45, 2.4f, 0);
            eyeMaterial = MakeMaterial("Astra/Setpieces/CrimsonEye", "Living spherical eye");
            fleshMaterial = MakeMaterial("Astra/Setpieces/CrimsonEye", "Vascular ring and tendrils");
            fleshMaterial.SetFloat("_FleshOnly", 1);
            var eyeball = Primitive("Spherical cornea and vertical pupil", PrimitiveType.Sphere, EyeRoot, eyeMaterial);
            eyeball.localScale = Vector3.one * 18;
            var ring = NewRoot("Thick fleshy orbital ring", EyeRoot).gameObject;
            ring.AddComponent<MeshFilter>().sharedMesh = MakeTorus(9.1f, 1.28f);
            ring.AddComponent<MeshRenderer>().sharedMaterial = fleshMaterial;
            for (int i = 0; i < 14; i++)
            {
                var t = new Tentacle { angle = i * Mathf.PI * 2 / 14, length = 6.3f + (i % 4) * .85f, phase = i * 1.618f };
                t.vertices = new Vector3[TubeRings * TubeSides];
                t.mesh = new Mesh { name = "Living tube " + i };
                t.mesh.MarkDynamic();
                t.mesh.vertices = t.vertices;
                t.mesh.triangles = TubeTriangles(TubeRings, TubeSides);
                meshes.Add(t.mesh);
                var go = NewRoot("Animated tendril " + (i + 1), EyeRoot).gameObject;
                go.AddComponent<MeshFilter>().sharedMesh = t.mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = fleshMaterial;
                AnimateTentacle(t, 0);
                tentacles.Add(t);
            }
        }

        void BuildStreaks()
        {
            streakRoot = NewRoot("Three-dimensional jump star trails", sceneRoot);
            Material material = MakeMaterial("Sprites/Default", "Jump starlight");
            for (int i = 0; i < 96; i++)
            {
                float angle = i * 2.399963f;
                float radius = 2.4f + (i % 13) * .59f;
                streakSeeds.Add(new Vector3(i * 1.37f, Mathf.Cos(angle) * radius + 1.5f, Mathf.Sin(angle) * radius));
                var line = NewRoot("Jump trail " + i, streakRoot).gameObject.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = false; line.positionCount = 2;
                line.widthMultiplier = .035f + (i % 3) * .009f;
                line.startColor = new Color(.2f, .55f, .7f, .6f);
                line.endColor = new Color(.3f, .6f, .7f, 0);
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                streaks.Add(line);
            }
            AnimateStreaks(0);
        }

        void AnimateStreaks(float t)
        {
            float stretch = Mathf.Sin(t * Mathf.PI);
            for (int i = 0; i < streaks.Count; i++)
            {
                Vector3 seed = streakSeeds[i];
                float x = 3.5f + Mathf.Repeat(seed.x - elapsed * (38 + 85 * stretch), 85);
                streaks[i].SetPosition(0, new Vector3(x, seed.y, seed.z));
                streaks[i].SetPosition(1, new Vector3(x + 1 + stretch * 38, seed.y, seed.z));
                Color c = Color.Lerp(new Color(.28f, .64f, .82f), new Color(.8f, .1f, .045f), Mathf.SmoothStep(0, 1, t));
                c.a = Mathf.Clamp01(stretch * 1.5f);
                streaks[i].startColor = c;
                c.a = 0; streaks[i].endColor = c;
            }
        }

        void AnimateTentacle(Tentacle t, float time)
        {
            for (int j = 0; j < TubeRings; j++)
            {
                float u = j / (float)(TubeRings - 1);
                Vector3 center = TentaclePoint(t, u, time);
                Vector3 tangent = (TentaclePoint(t, Mathf.Min(u + .012f, 1.01f), time) - TentaclePoint(t, u - .012f, time)).normalized;
                Vector3 side = Vector3.Cross(tangent, Vector3.right).normalized;
                Vector3 up = Vector3.Cross(side, tangent).normalized;
                float width = Mathf.Lerp(.83f, .035f, Mathf.Pow(u, .72f));
                for (int k = 0; k < TubeSides; k++)
                {
                    float a = k * Mathf.PI * 2 / TubeSides;
                    t.vertices[j * TubeSides + k] = center + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * width;
                }
            }
            t.mesh.vertices = t.vertices;
            t.mesh.RecalculateNormals();
            t.mesh.RecalculateBounds();
        }

        static Vector3 TentaclePoint(Tentacle t, float u, float time)
        {
            float wave = Mathf.Sin(u * 4.4f + t.phase + time * .53f);
            float angle = t.angle + (wave * .24f + Mathf.Sin(time * .16f + t.phase) * .1f) * u;
            float radial = 8.9f + t.length * u - 1.7f * u * u;
            return new Vector3(Mathf.Sin(u * 5 + t.phase + time * .37f) * 2.4f * u + .5f, Mathf.Sin(angle) * radial, Mathf.Cos(angle) * radial);
        }

        void SetReveal(float reveal)
        {
            foreach (Material m in materials) if (m && m.HasProperty("_Reveal")) m.SetFloat("_Reveal", reveal);
        }

        Material MakeMaterial(string shaderName, string name)
        {
            Shader shader = Shader.Find(shaderName);
            if (!shader) shader = Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = name };
            materials.Add(m); return m;
        }

        static Transform NewRoot(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }

        static Transform Primitive(string name, PrimitiveType type, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false);
            var collider = go.GetComponent<Collider>(); if (collider) { collider.enabled = false; Destroy(collider); }
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            return go.transform;
        }

        Mesh MakeTorus(float major, float minor)
        {
            const int rows = 96, cols = 14;
            var vertices = new Vector3[rows * cols];
            for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++)
            {
                float a = i * Mathf.PI * 2 / rows, b = j * Mathf.PI * 2 / cols;
                float r = major + Mathf.Cos(b) * minor;
                vertices[i * cols + j] = new Vector3(Mathf.Sin(b) * minor, Mathf.Sin(a) * r, Mathf.Cos(a) * r);
            }
            var triangles = new int[rows * cols * 6];
            int p = 0;
            for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++)
            {
                int a = i * cols + j, b = ((i + 1) % rows) * cols + j, c = i * cols + (j + 1) % cols, d = ((i + 1) % rows) * cols + (j + 1) % cols;
                triangles[p++] = a; triangles[p++] = c; triangles[p++] = b;
                triangles[p++] = c; triangles[p++] = d; triangles[p++] = b;
            }
            var mesh = new Mesh { name = "Muscular orbital torus", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh); return mesh;
        }

        Mesh MakeDome()
        {
            const int columns = 40, rows = 32;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var triangles = new int[columns * rows * 6];
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
                vertices[y * (columns + 1) + x] = new Vector3(1, Mathf.Lerp(-3.4f, 3.4f, y / (float)rows), Mathf.Lerp(-3.4f, 3.4f, x / (float)columns)).normalized * 115;
            int p = 0;
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            {
                int a = y * (columns + 1) + x, b = a + 1, c = a + columns + 1, d = c + 1;
                triangles[p++] = a; triangles[p++] = b; triangles[p++] = c;
                triangles[p++] = b; triangles[p++] = d; triangles[p++] = c;
            }
            var mesh = new Mesh { name = "Curved crimson horizon", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh); return mesh;
        }

        static int[] TubeTriangles(int rows, int cols)
        {
            var indices = new int[(rows - 1) * cols * 6]; int p = 0;
            for (int i = 0; i < rows - 1; i++) for (int j = 0; j < cols; j++)
            {
                int a = i * cols + j, b = a + cols, c = i * cols + (j + 1) % cols, d = c + cols;
                indices[p++] = a; indices[p++] = b; indices[p++] = c;
                indices[p++] = c; indices[p++] = b; indices[p++] = d;
            }
            return indices;
        }
    }
}
