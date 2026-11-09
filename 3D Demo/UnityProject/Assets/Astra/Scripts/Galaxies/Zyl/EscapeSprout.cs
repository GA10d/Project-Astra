using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AstraCabin.Galaxies.Zyl
{
    // Runtime prop and left-hand choreography for the grave scene.
    sealed class EscapeSprout
    {
        public const float GripTime = 1.15f;
        public const float LiftEnd = 2.65f;
        internal const string PlantedName = "Zyl16 | planted flesh sprout";
        internal const float CrownScale = .86f, ContactRise = .245f, CrownSink = .185f;
        internal const float PlantedHeight = .145f + ContactRise - CrownSink;
        internal static Quaternion PlantedRotation => Quaternion.Euler(5,11,-8);
        const int Rings = 15, Sides = 8, Roots = 7;
        readonly FoolsWalker walker;
        readonly Vector3 ground, actorGround, facing, up, reachDirection;
        readonly Transform baseRoot, crown, spine, chest, neck, upperArm, forearm, hand, balanceArm;
        readonly Transform[][] fingers;
        readonly Vector3 plantedGrip, gripOffset;
        readonly Mesh roots;
        readonly MeshRenderer tetherRenderer;
        readonly Material crownSurface;
        readonly ZylPresentationParameters settings;
        float wavePhase;
        readonly Vector3[] rootVertices = new Vector3[Roots * Rings * Sides];
        readonly float[] rootAngles = { 0f, .4f, 1.55f, 2.58f, 3.57f, 4.62f, 5.53f };
        readonly float[] rootSpread = { .04f, .35f, .24f, .36f, .28f, .34f, .26f };

        public EscapeSprout(Transform parent, FoolsWalker actor, Vector3 soil, Vector3 standingGround,
            Vector3 standingFacing, Vector3 vertical, List<UnityEngine.Object> resources, Transform planted, ZylPresentationParameters settings)
        {
            walker = actor;
            this.settings = settings;
            ground = soil;
            actorGround = standingGround;
            facing = standingFacing;
            up = vertical;
            reachDirection = Vector3.ProjectOnPlane(ground - actorGround, up).normalized;
            baseRoot = Create("Zyl16 | rooted flesh sprout", parent);
            baseRoot.SetPositionAndRotation(ground, planted ? planted.rotation : Quaternion.LookRotation(reachDirection, up));
            plantedGrip = baseRoot.TransformPoint(Vector3.up * PlantedHeight);
            Mesh fleshMesh;
            Material flesh;
            if (planted)
            {
                fleshMesh = planted.GetComponentInChildren<MeshFilter>().sharedMesh;
                flesh = planted.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            }
            else
            {
                var texture = FleshTexture();
                flesh = FleshMaterial(texture);
                fleshMesh = CrownMesh();
                resources.Add(texture);
                resources.Add(flesh);
                resources.Add(fleshMesh);
            }

            var rootSurface = new Material(flesh) { name = "Sprout | pale root fibers", mainTexture = null };
            rootSurface.color = new Color(.78f, .46f, .35f);
            rootSurface.SetFloat("_Glossiness", .43f);
            resources.Add(rootSurface);

            crown = Create("Sprout | lobed crown", baseRoot);
            var shader = Resources.Load<Shader>("Galaxies/Zyl/Escape/SproutWave");
            if (!shader) throw new InvalidOperationException("肉芽起伏着色器缺失。");
            crownSurface = new Material(flesh) { name = "Sprout | rippling flesh", shader = shader };
            resources.Add(crownSurface);
            crownSurface.SetVector("_WaveCenter", fleshMesh.bounds.center);
            AdvanceWave(float.NegativeInfinity, 0f);
            Render(crown, fleshMesh, crownSurface);
            crown.localScale = Vector3.one * CrownScale;

            var tether = Create("Sprout | soil-anchored roots", baseRoot);
            roots = new Mesh { name = "Sprout | continuous branching roots" };
            roots.MarkDynamic();
            resources.Add(roots);
            roots.vertices = rootVertices;
            var indices = new List<int>();
            for (int strand = 0; strand < Roots; strand++)
            for (int ring = 0; ring < Rings - 1; ring++)
            for (int side = 0; side < Sides; side++)
            {
                int a = (strand * Rings + ring) * Sides + side;
                int b = (strand * Rings + ring) * Sides + (side + 1) % Sides;
                indices.AddRange(new[] { a, b, a + Sides, b, b + Sides, a + Sides });
            }
            roots.SetTriangles(indices, 0);
            Render(tether, roots, rootSurface);
            tetherRenderer = tether.GetComponent<MeshRenderer>();

            var bones = walker.Root.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            Transform Bone(string name) => bones.First(bone => bone.name == name);
            spine = Bone("spine");
            chest = Bone("chest");
            neck = Bone("neck");
            var leftShoulder = Bone("L_upperarm");
            var rightShoulder = Bone("R_upperarm");
            float ReachDistance(Transform shoulder) => Vector3.ProjectOnPlane(shoulder.position - ground, up).sqrMagnitude;
            bool useLeft = ReachDistance(leftShoulder) <= ReachDistance(rightShoulder);
            string handPrefix = useLeft ? "L_" : "R_";
            upperArm = useLeft ? leftShoulder : rightShoulder;
            balanceArm = useLeft ? rightShoulder : leftShoulder;
            forearm = Bone(handPrefix + "forearm");
            hand = Bone(handPrefix + "hand");
            var middle = Bone(handPrefix + "middle_01");
            gripOffset = hand.InverseTransformPoint(Vector3.Lerp(hand.position, middle.position, .78f));
            fingers = new[] { "index", "middle", "ring", "pinky" }.Select(finger =>
                Enumerable.Range(1, 3).Select(joint => Bone(handPrefix + finger + "_0" + joint)).ToArray()).ToArray();
            PoseProp(plantedGrip, 0f);
        }

        internal void Sample(float age, MihuoerPose pose, float dt)
        {
            AdvanceWave(age, dt);
            float reach = Ease(age / .90f);
            float lift = Ease((age - GripTime) / (LiftEnd - GripTime));
            float crouch = reach * (1f - lift);
            var standingDirection = Vector3.ProjectOnPlane(facing, up).normalized;
            var turning = Vector3.Slerp(standingDirection, reachDirection, .40f * crouch);
            var basePose = age >= 0f && age < LiftEnd + .35f ? MihuoerPose.Idle : pose;
            walker.SampleStandingAt(actorGround, turning, basePose, dt, reachDirection * (.14f * crouch), .29f * crouch);
            if (age < 0f) return;

            Vector3 bendAxis = Vector3.Cross(up, reachDirection);
            spine.rotation = Quaternion.AngleAxis(30f * crouch, bendAxis) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(16f * crouch, bendAxis) * chest.rotation;
            neck.rotation = Quaternion.AngleAxis(-8f * crouch, bendAxis) * neck.rotation;
            Vector3 outward = Vector3.ProjectOnPlane(upperArm.position - spine.position, up).normalized;
            float balanceSign = Mathf.Sign(Vector3.Dot(balanceArm.position - spine.position, walker.Root.right));
            balanceArm.rotation = Quaternion.AngleAxis(balanceSign * 12f * crouch, walker.Root.forward) * balanceArm.rotation;

            Vector3 heldTarget = actorGround + up * .94f + reachDirection * .61f;
            Vector3 target = Vector3.Lerp(plantedGrip, heldTarget, lift);
            target += reachDirection * (.11f * Mathf.Sin(lift * Mathf.PI));
            Quaternion start = hand.rotation;
            Vector3 fingersDirection = hand.TransformPoint(gripOffset) - hand.position;
            Vector3 desiredDirection = Vector3.Slerp(-up, (up * .82f + reachDirection * .42f).normalized, lift);
            Quaternion wrist = Quaternion.FromToRotation(fingersDirection, desiredDirection) * start;
            wrist = Quaternion.Slerp(start, wrist, reach);
            Vector3 gripFromWrist = Quaternion.Inverse(start) * (hand.TransformPoint(gripOffset) - hand.position);
            Vector3 wristTarget = Vector3.Lerp(hand.position, target - wrist * gripFromWrist, reach);
            FoolsWalker.Solve(upperArm, forearm, hand, wristTarget, outward - up * .35f);
            hand.rotation = wrist;

            float clench = Ease((age - .83f) / .31f);
            foreach (var finger in fingers)
            {
                var inward = -outward - up * .22f;
                var axis = Vector3.Cross(finger[1].position - finger[0].position, inward).normalized;
                for (int i = 0; i < finger.Length; i++)
                    finger[i].rotation = Quaternion.AngleAxis(clench * (i == 0 ? 26f : 39f), axis) * finger[i].rotation;
            }

            float grasp = Ease((age - GripTime) / .15f);
            PoseProp(Vector3.Lerp(plantedGrip, hand.TransformPoint(gripOffset), grasp), lift);
        }

        internal void AdvanceWave(float age, float dt)
        {
            float pulled = Ease((age - LiftEnd) / .45f);
            float amplitude = Mathf.Lerp(settings.escapeSproutWaveAmplitude, settings.escapeSproutPulledWaveAmplitude, pulled);
            float frequency = Mathf.Lerp(settings.escapeSproutWaveHz, settings.escapeSproutPulledWaveHz, pulled);
            wavePhase = Mathf.Repeat(wavePhase + dt * frequency * (2f * Mathf.PI), 200f * Mathf.PI);
            crownSurface.SetFloat("_WaveAmplitude", amplitude * .01f);
            crownSurface.SetFloat("_WavePhase", wavePhase);
        }

        void PoseProp(Vector3 grip, float lift)
        {
            tetherRenderer.enabled = lift > .001f;
            crown.position = grip;
            crown.localRotation = Quaternion.Euler(5f + 18f * lift, 11f, -8f - 12f * lift);
            Vector3 top = baseRoot.InverseTransformPoint(crown.TransformPoint(Vector3.up * ((.07f - ContactRise) / CrownScale)));
            Vector3 MainStem(float u)
            {
                Vector3 buried = new Vector3(.04f, -.19f, 0);
                Vector3 surface = new Vector3(.0224f, -.014f, 0);
                if (u <= .34f) return Vector3.Lerp(buried, surface, u / .34f);
                float t = (u - .34f) / .66f;
                return Vector3.Lerp(surface, top, t) + Vector3.right *
                    (lift * (.085f * Mathf.Sin(Mathf.PI * t) + .035f * Mathf.Sin(2f * Mathf.PI * t)));
            }
            for (int strand = 0; strand < Roots; strand++)
            {
                float angle = rootAngles[strand];
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 buried = radial * rootSpread[strand] - Vector3.up * (.19f + .025f * strand);
                Vector3 collar = MainStem(Mathf.Lerp(.29f, .55f + .052f * strand, Ease(lift * 3f)));
                Vector3 atSoil = radial * (rootSpread[strand] * .56f) - Vector3.up * .014f;
                Vector3 bulge = radial * (lift * .13f);
                Vector3 Point(float u)
                {
                    if (strand == 0) return MainStem(u);
                    if (u <= .34f) return Vector3.Lerp(buried, atSoil, u / .34f);
                    float t = (u - .34f) / .66f;
                    return Vector3.Lerp(atSoil, collar, t) + bulge * Mathf.Sin(Mathf.PI * t) +
                        radial * (.035f * lift * Mathf.Sin(2f * Mathf.PI * t));
                }

                for (int ring = 0; ring < Rings; ring++)
                {
                    float u = ring / (Rings - 1f);
                    Vector3 center = Point(u);
                    Vector3 direction = (Point(Mathf.Min(1f, u + .025f)) - Point(Mathf.Max(0f, u - .025f))).normalized;
                    Vector3 side = Vector3.Cross(direction, new Vector3(radial.z, 0, -radial.x)).normalized;
                    Vector3 binormal = Vector3.Cross(direction, side);
                    float width = strand == 0 ? Mathf.Lerp(.006f, .041f, u) : Mathf.Lerp(.002f, .015f, u);
                    for (int j = 0; j < Sides; j++)
                    {
                        float a = j * (Mathf.PI * 2f / Sides);
                        rootVertices[(strand * Rings + ring) * Sides + j] =
                            center + (side * Mathf.Cos(a) + binormal * Mathf.Sin(a)) * width;
                    }
                }
            }
            roots.vertices = rootVertices;
            roots.RecalculateNormals();
            roots.RecalculateBounds();
        }

        internal static Material FleshMaterial(Texture2D texture)
        {
            var flesh = new Material(Shader.Find("Standard (Specular setup)")) { name = "Sprout | moist flesh", mainTexture = texture };
            flesh.SetColor("_SpecColor", new Color(.17f, .12f, .105f));
            flesh.SetFloat("_Glossiness", .62f);
            return flesh;
        }

        internal static Mesh CrownMesh()
        {
            const int bands = 24, sides = 44;
            var positions = new Vector3[(bands + 1) * (sides + 1)];
            var uv = new Vector2[positions.Length];
            var triangles = new List<int>();
            var lumps = new Vector3[29];
            for (int i = 0; i < lumps.Length; i++)
            {
                float y = (i + .5f) * 2f / lumps.Length - 1f, a = i * 2.399963f;
                float r = Mathf.Sqrt(1f - y * y);
                lumps[i] = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
            }
            for (int row = 0; row <= bands; row++)
            for (int col = 0; col <= sides; col++)
            {
                float u = col / (float)sides, v = row / (float)bands;
                float theta = v * Mathf.PI, phi = u * 2f * Mathf.PI;
                var direction = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                float bump = 0f;
                foreach (var lobe in lumps)
                {
                    float d = Vector3.Dot(direction, lobe);
                    if (d > .72f) bump = Mathf.Max(bump, Mathf.SmoothStep(0f, 1f, (d - .72f) / .28f));
                }
                float scale = .80f + bump * .29f + .03f * Mathf.Sin(direction.x * 13f + direction.y * 9f);
                var point = Vector3.Scale(direction * scale, new Vector3(.192f, .205f, .153f));
                point.x += .037f * direction.y;
                point.y += .22f - ContactRise / CrownScale;
                int index = row * (sides + 1) + col;
                positions[index] = point;
                uv[index] = new Vector2(u,v);
                if (row == bands || col == sides) continue;
                int next = index + sides + 1;
                if (row > 0) triangles.AddRange(new[] { index, index + 1, next });
                if (row < bands - 1) triangles.AddRange(new[] { index + 1, next + 1, next });
            }

            var mesh = new Mesh { name = "Sprout | lobed fleshy head" };
            mesh.vertices = positions;
            mesh.uv = uv;
            mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();
            var normals = mesh.normals;
            for (int row = 1; row < bands; row++)
            {
                int first = row * (sides + 1);
                normals[first] = normals[first + sides] = (normals[first] + normals[first + sides]).normalized;
            }
            for (int col = 0; col <= sides; col++)
            {
                normals[col] = Vector3.up;
                normals[bands * (sides + 1) + col] = Vector3.down;
            }
            mesh.normals = normals;
            return mesh;
        }

        internal static Texture2D FleshTexture()
        {
            const int width = 128, height = 64;
            var texture = new Texture2D(width,height,TextureFormat.RGB24,true) { name = "Sprout | mottled rose and capillaries" };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1), v = y / (float)(height - 1), a = u * 2f * Mathf.PI;
                float noise = Mathf.PerlinNoise(4f + Mathf.Cos(a) * 2f + v * 5f, 5f + Mathf.Sin(a) * 2f - v * 4f);
                float vein = Mathf.Pow(Mathf.Max(0, Mathf.Cos(a * 9f + v * 14f + noise * 6f)), 26f);
                var color = Color.Lerp(new Color(.46f,.16f,.19f), new Color(.89f,.52f,.49f), noise);
                pixels[y * width + x] = Color.Lerp(color, new Color(.52f,.23f,.18f), vein * .33f);
            }
            texture.SetPixels(pixels);
            texture.Apply(true,true);
            return texture;
        }

        static Transform Create(string name, Transform parent)
        {
            var item = new GameObject(name);
            item.layer = ZylStomachEnvironment.EnvironmentLayer;
            item.transform.SetParent(parent, false);
            return item.transform;
        }

        static void Render(Transform item, Mesh mesh, Material material)
        {
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static float Ease(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
    }
}




