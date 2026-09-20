using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraCabin
{
    /// <summary>A continuous three-dimensional migration; no camera or ship-power ownership.</summary>
    public sealed class StarWhaleSetpiece : MonoBehaviour
    {
        const int ExteriorLayer = 26;
        const float OrbitSpeed = Mathf.PI * 2f / 56f;
        static readonly float[] BodyStations = { -4.8f, -4.2f, -3.4f, -2.5f, -1.3f, 0, 1.3f, 2.4f, 3.15f, 3.5f, 3.65f };
        static readonly float[] BodyRadii = { .006f, .13f, .24f, .39f, .68f, .9f, .99f, .84f, .60f, .31f, .008f };
        public bool Active { get; private set; }
        public float Elapsed { get; private set; }
        public Transform GeneratedRoot { get { return root; } }
        public Transform HeroWhale { get; private set; }
        public int WhaleCount { get { return whales.Count; } }
        public int FollowerCount { get { return followers.Count; } }
        public float WingsUnfurled { get; private set; }

        SetpieceDirector director;
        CabinController controller;
        Transform root;
        Light cabinReflection;
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Whale> whales = new List<Whale>();
        readonly List<Transform> followers = new List<Transform>();
        Material skin, membrane, glow, dark;
        Mesh bodyMesh, membraneMesh, followerMesh, dorsalMesh, flukeMesh;
        float nextSong;

        sealed class Whale
        {
            public Transform body, leftWing, rightWing, tail;
            public float phase, size, depth, altitude;
        }

        public void Initialize(SetpieceDirector value)
        {
            director = value;
            controller = value ? value.controller : FindObjectOfType<CabinController>();
        }

        public void Show()
        {
            Clear();
            Active = true; Elapsed = 0; nextSong = 2.2f;
            root = NewRoot("07 | Pelagic migration", transform);
            skin = MaterialFor("Astra/Setpieces/WhaleSkin", "Slate-blue living hide");
            membrane = MaterialFor("Astra/Setpieces/WhaleMembrane", "Translucent folded wing tissue");
            glow = MaterialFor("Astra/Setpieces/WhaleGlow", "Bioluminescent organs");
            glow.SetColor("_Color", new Color(.16f, 1.35f, 1.65f, 1));
            dark = MaterialFor("Astra/Setpieces/WhaleGlow", "Mouth and eyelid shadow");
            dark.SetColor("_Color", new Color(.008f, .018f, .027f, 1));
            bodyMesh = MakeBody();
            membraneMesh = MakeWing();
            dorsalMesh = MakeDorsal();
            flukeMesh = MakeFluke();
            followerMesh = MakeFollower();

            // The first animal is deliberately near enough to occlude stars and fill the aperture.
            AddWhale("Matriarch | eleven-metre silhouette", 1.42f, 0, 0, 2.65f);
            HeroWhale = whales[0].body;
            for (int i = 0; i < 6; i++)
                AddWhale("Migrating whale " + (i + 1), .57f + (i % 3) * .15f,
                    -.52f - i * .22f, 12 + (i % 3) * 8.5f, 2.3f + (i % 2 == 0 ? 1 : -1) * (2.4f + i * .38f));

            for (int i = 0; i < 120; i++)
            {
                Transform fish = MeshObject("Wake lantern " + (i + 1), root, followerMesh, glow);
                float size = .11f + (i % 7) * .019f;
                fish.localScale = Vector3.one * size;
                followers.Add(fish);
            }
            Transform lightRoot = NewRoot("Blue light transmitted through window", root);
            lightRoot.gameObject.layer = 0;
            lightRoot.localPosition = new Vector3(.92f, 1.74f, 0);
            cabinReflection = lightRoot.gameObject.AddComponent<Light>();
            cabinReflection.type = LightType.Point;
            cabinReflection.color = new Color(.12f, .69f, 1);
            cabinReflection.range = 4.4f;
            cabinReflection.shadows = LightShadows.None;
            cabinReflection.cullingMask = ~(1 << ExteriorLayer);
            Animate(0);
        }

        void Update()
        {
            if (!Active || (controller && controller.IsPaused)) return;
            Elapsed += Time.deltaTime;
            Animate(Elapsed);
            if (Elapsed >= nextSong)
            {
                if (director) director.PlayCue("whale", nextSong < 3 ? .65f : .4f);
                nextSong += 32;
            }
        }

        void Animate(float time)
        {
            WingsUnfurled = Mathf.SmoothStep(.045f, 1, Mathf.InverseLerp(1.6f, 7.2f, time));
            skin.SetFloat("_Clock", time);
            membrane.SetFloat("_Clock", time);
            glow.SetFloat("_Clock", time);
            for (int i = 0; i < whales.Count; i++)
            {
                Whale whale = whales[i];
                float angle = 1.15f - time * OrbitSpeed + whale.phase;
                Vector3 velocity = new Vector3(-19 * Mathf.Sin(angle), 0, -35 * Mathf.Cos(angle));
                whale.body.localPosition = OrbitPoint(angle, whale.depth, whale.altitude);
                whale.body.localRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up)
                    * Quaternion.Euler(Mathf.Sin(time * .23f + i) * 3, 0, Mathf.Sin(time * .19f + i * .9f) * 5);
                float flap = Mathf.Sin(time * .68f + i * 1.6f) * 10;
                float fold = Mathf.Lerp(74, 7, WingsUnfurled);
                whale.leftWing.localRotation = Quaternion.Euler(0, 0, fold + flap);
                whale.rightWing.localRotation = Quaternion.Euler(0, 0, -fold - flap);
                whale.leftWing.localScale = new Vector3(WingsUnfurled, 1, 1);
                whale.rightWing.localScale = new Vector3(-WingsUnfurled, 1, 1);
                whale.tail.localRotation = Quaternion.Euler(12 + Mathf.Sin(time * .75f + i) * 14, Mathf.Sin(time * .5f + i) * 9, 0);
            }
            for (int i = 0; i < followers.Count; i++)
            {
                float trail = .34f + i / 120f * .9f;
                float angle = 1.15f - time * OrbitSpeed + trail;
                Vector3 p = OrbitPoint(angle, 0, 2.65f);
                float swirl = i * 2.39996f + time * .63f;
                float spread = .45f + (i % 13) * .12f;
                p.x += Mathf.Cos(swirl) * spread;
                p.y += Mathf.Sin(swirl) * spread;
                p.z += Mathf.Sin(i * 3.1f) * .35f;
                followers[i].localPosition = p;
                Vector3 velocity = new Vector3(-19 * Mathf.Sin(angle), .12f * Mathf.Sin(swirl), -35 * Mathf.Cos(angle));
                followers[i].localRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up)
                    * Quaternion.Euler(0, 0, Mathf.Sin(time * 3.3f + i) * 24);
            }
            // This lamp adds reflected animal light only; CabinSystems retains complete power ownership.
            float proximity = Mathf.Exp(-Mathf.Pow(HeroWhale.localPosition.z / 12, 2));
            float nearSide = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(11, 27, HeroWhale.localPosition.x));
            cabinReflection.intensity = (.06f + 1.25f * proximity * nearSide) * WingsUnfurled;
        }

        static Vector3 OrbitPoint(float angle, float depth, float altitude)
        {
            return new Vector3(27 - 19 * Mathf.Cos(angle) + depth,
                altitude + .5f * Mathf.Sin(angle * 2), 35 * Mathf.Sin(angle));
        }

        void AddWhale(string name, float size, float phase, float depth, float altitude)
        {
            var whale = new Whale { body = NewRoot(name, root), size = size, phase = phase, depth = depth, altitude = altitude };
            whale.body.localScale = Vector3.one * size;
            MeshObject("Continuous head, throat and tapered body", whale.body, bodyMesh, skin);
            MeshObject("Dorsal keel", whale.body, dorsalMesh, skin);
            whale.leftWing = NewRoot("Port living wing", whale.body);
            whale.rightWing = NewRoot("Starboard living wing", whale.body);
            whale.leftWing.localPosition = new Vector3(.58f, -.05f, .6f);
            whale.rightWing.localPosition = new Vector3(-.58f, -.05f, .6f);
            BuildWing(whale.leftWing);
            BuildWing(whale.rightWing);
            whale.tail = NewRoot("Articulated fluke peduncle", whale.body);
            whale.tail.localPosition = new Vector3(0, 0, -4.2f);
            MeshObject("Broad crescent tail", whale.tail, flukeMesh, skin);
            for (int side = -1; side <= 1; side += 2)
            {
                Sphere("Obsidian eye socket", whale.body, new Vector3(side * .80f, .18f, 2.35f), new Vector3(.12f, .17f, .23f), dark);
                Sphere("Luminous watchful eye", whale.body, new Vector3(side * .885f, .185f, 2.35f), new Vector3(.034f, .10f, .125f), glow);
                var mouth = new Vector3[24];
                for (int k = 0; k < mouth.Length; k++)
                {
                    float u = k / (float)(mouth.Length - 1);
                    float z = Mathf.Lerp(1.0f, 3.43f, u);
                    float radius = RadiusAt(z);
                    mouth[k] = new Vector3(side * radius * .89f, -.15f - .1f * Mathf.Sin(u * Mathf.PI), z);
                }
                Line("Long curved jaw seam", whale.body, mouth, .027f, dark);
                for (int j = 0; j < 7; j++)
                {
                    float z = 2.5f - j * .5f;
                    float radius = RadiusAt(z);
                    var points = new Vector3[14];
                    for (int k = 0; k < points.Length; k++)
                    {
                        float a = Mathf.Lerp(-.7f, -1.9f, k / (float)(points.Length - 1));
                        points[k] = new Vector3(side * Mathf.Cos(a) * radius * 1.015f,
                            Mathf.Sin(a) * radius * .74f - .035f, z - .18f * Mathf.Sin(a));
                    }
                    Line("Ventral photophore rib " + j, whale.body, points, .018f, glow);
                }
            }
            whales.Add(whale);
        }

        void BuildWing(Transform parent)
        {
            MeshObject("Thin semitransparent wing membrane", parent, membraneMesh, membrane);
            for (int rib = 0; rib < 5; rib++)
            {
                var points = new Vector3[13];
                float v = rib / 4f;
                for (int j = 0; j < points.Length; j++) points[j] = WingPoint(j / 12f, v);
                Line("Flexible wing spar " + rib, parent, points, rib == 0 ? .041f : .023f, skin);
            }
            var edge = new Vector3[29];
            for (int j = 0; j < edge.Length; j++) edge[j] = WingPoint(1, j / 28f);
            Line("Luminous scalloped wing edge", parent, edge, .019f, glow);
        }

        static Vector3 WingPoint(float u, float v)
        {
            float span = 3.1f * u;
            float front = .85f - u * 1.65f;
            float rear = -.85f - 2.8f * u + Mathf.Sin(u * Mathf.PI) * .7f;
            float z = Mathf.Lerp(front, rear, v);
            float y = .24f * Mathf.Sin(u * Mathf.PI) - .21f * u + .18f * Mathf.Sin(v * Mathf.PI) * Mathf.Sin(u * Mathf.PI);
            span *= 1 - .18f * Mathf.Sin(v * Mathf.PI);
            return new Vector3(span, y, z);
        }

        Mesh MakeWing()
        {
            const int rows = 19, cols = 23;
            var vertices = new Vector3[rows * cols]; var uv = new Vector2[vertices.Length];
            for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++)
            {
                float u = i / (float)(rows - 1), v = j / (float)(cols - 1);
                vertices[i * cols + j] = WingPoint(u, v); uv[i * cols + j] = new Vector2(u, v);
            }
            return MeshFor("Curved living membrane surface", vertices, GridIndices(rows, cols, false), uv);
        }

        Mesh MakeBody()
        {
            const int rows = 81, cols = 40;
            var vertices = new Vector3[rows * cols]; var uv = new Vector2[vertices.Length];
            for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++)
            {
                float u = i / (float)(rows - 1), a = j * Mathf.PI * 2 / cols;
                float z = Mathf.Lerp(-4.8f, 3.65f, u), radius = RadiusAt(z);
                vertices[i * cols + j] = new Vector3(Mathf.Cos(a) * radius,
                    Mathf.Sin(a) * radius * .74f + .055f * Mathf.Sin(u * Mathf.PI), z);
                uv[i * cols + j] = new Vector2(u, j / (float)cols);
            }
            return MeshFor("Smooth fusiform whale body", vertices, GridIndices(rows, cols, true), uv);
        }

        static float RadiusAt(float z)
        {
            for (int i = 0; i < BodyStations.Length - 1; i++)
                if (z <= BodyStations[i + 1]) return Mathf.Lerp(BodyRadii[i], BodyRadii[i + 1], Mathf.SmoothStep(0, 1, Mathf.InverseLerp(BodyStations[i], BodyStations[i + 1], z)));
            return BodyRadii[BodyRadii.Length - 1];
        }

        Mesh MakeDorsal()
        {
            var vertices = new[] {
                new Vector3(-.07f,.50f,.1f),new Vector3(-.03f,1.42f,-1.5f),new Vector3(-.04f,.30f,-2.6f),
                new Vector3(.07f,.50f,.1f),new Vector3(.03f,1.42f,-1.5f),new Vector3(.04f,.30f,-2.6f)};
            return MeshFor("Swept solid dorsal fin", vertices, new[] {0,1,2,5,4,3,0,3,4,0,4,1,1,4,5,1,5,2,2,5,3,2,3,0}, null);
        }

        Mesh MakeFluke()
        {
            const int rows = 35, cols = 9;
            var vertices = new Vector3[rows * cols]; var uv = new Vector2[vertices.Length];
            for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++)
            {
                float x = Mathf.Lerp(-1.7f, 1.7f, i / (float)(rows - 1)), s = Mathf.Abs(x) / 1.7f;
                float v = j / (float)(cols - 1);
                float leading = -.06f - s * .37f;
                float trailing = -.55f - .92f * Mathf.Sin(s * Mathf.PI * .82f);
                vertices[i * cols + j] = new Vector3(x, .12f * Mathf.Sin(v * Mathf.PI) * (1 - s), Mathf.Lerp(leading, trailing, v));
                uv[i * cols + j] = new Vector2(i / (float)(rows - 1), v);
            }
            // Skin is double-sided, so a thin fluke remains visible from both sides of the pass.
            return MeshFor("Crescent fluke surface", vertices, GridIndices(rows, cols, false), uv);
        }

        Mesh MakeFollower()
        {
            var v = new[] {new Vector3(0,0,1.2f),new Vector3(-.3f,.19f,0),new Vector3(.3f,.19f,0),new Vector3(0,-.2f,0),
                new Vector3(0,0,-1.4f),new Vector3(-1.3f,.26f,-.4f),new Vector3(1.3f,.26f,-.4f)};
            return MeshFor("Tiny living lantern ray", v, new[]{0,1,2,0,3,1,0,2,3,1,4,2,1,3,4,3,2,4,0,5,1,1,5,4,0,2,6,2,4,6}, null);
        }

        static int[] GridIndices(int rows, int cols, bool wrap)
        {
            int width = wrap ? cols : cols - 1;
            var triangles = new int[(rows - 1) * width * 6]; int p = 0;
            for (int i = 0; i < rows - 1; i++) for (int j = 0; j < width; j++)
            {
                int a = i * cols + j, b = a + cols, c = i * cols + (j + 1) % cols, d = c + cols;
                triangles[p++] = a; triangles[p++] = c; triangles[p++] = b;
                triangles[p++] = c; triangles[p++] = d; triangles[p++] = b;
            }
            return triangles;
        }

        Mesh MeshFor(string name, Vector3[] vertices, int[] triangles, Vector2[] uv)
        {
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            if (uv != null) mesh.uv = uv;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh); return mesh;
        }

        Material MaterialFor(string shaderName, string name)
        {
            Shader shader = Shader.Find(shaderName);
            if (!shader) shader = Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = name }; materials.Add(m); return m;
        }

        static Transform NewRoot(string name, Transform parent)
        {
            var go = new GameObject(name) { layer = ExteriorLayer };
            go.transform.SetParent(parent, false); return go.transform;
        }

        static Transform MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            Transform t = NewRoot(name, parent);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = t.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return t;
        }

        static void Sphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.layer = ExteriorLayer;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale * 2;
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        static void Line(string name, Transform parent, Vector3[] points, float width, Material material)
        {
            var line = NewRoot(name, parent).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.positionCount = points.Length; line.SetPositions(points);
            line.widthMultiplier = width; line.numCornerVertices = 3; line.numCapVertices = 3;
            line.sharedMaterial = material; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
        }

        public void Clear()
        {
            Active = false; Elapsed = 0; WingsUnfurled = 0;
            if (root) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            foreach (Mesh mesh in meshes) if (mesh) Destroy(mesh);
            foreach (Material material in materials) if (material) Destroy(material);
            meshes.Clear(); materials.Clear(); whales.Clear(); followers.Clear();
            root = HeroWhale = null; cabinReflection = null;
            bodyMesh = membraneMesh = followerMesh = dorsalMesh = flukeMesh = null;
            skin = membrane = glow = dark = null;
        }

        void OnDestroy() { Clear(); }
    }
}
