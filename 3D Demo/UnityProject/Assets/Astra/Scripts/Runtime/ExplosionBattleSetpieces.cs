using System.Collections.Generic;
using UnityEngine;

namespace AstraCabin
{
    /// <summary>Finite, resettable exterior actors. No per-frame object allocation or physics bodies.</summary>
    public sealed class ExplosionBattleSetpieces : MonoBehaviour
    {
        const int LongitudePieces = 12, LatitudePieces = 3, ShipCapacity = 28, LaserCapacity = 96, BurstCapacity = 14;
        public int FragmentCount { get { return fragments.Count; } }
        public int FighterCount { get { return fighters.Count; } }
        public int LaserCount { get { int n = 0; foreach (Beam b in beams) if (b.active) n++; return n; } }
        public int ExplosionCount { get; private set; }
        public bool ExplosionActive { get; private set; }
        public bool BattleActive { get; private set; }
        public Quaternion PlanetRotation { get; set; } = Quaternion.identity;
        public float Elapsed { get { return elapsed; } }
        public int ActiveBurstCount { get { int n = 0; foreach (Burst b in bursts) if (b.active) n++; return n; } }

        sealed class Fragment
        {
            public Transform transform;
            public Renderer shell;
            public LineRenderer seam;
            public Vector3 rest, velocity, axis;
            public Quaternion rotation;
            public float angularSpeed;
        }
        sealed class Fighter
        {
            public Transform transform, exhaust;
            public int faction, index;
            public Vector3 previous, velocity, anchor;
            public float phase, fireAt, respawnAt;
            public bool active = true;
        }
        sealed class Beam
        {
            public LineRenderer line;
            public Vector3 origin, velocity;
            public Color color;
            public float age, lifetime;
            public bool active;
        }
        sealed class Burst
        {
            public Transform root, flash, ring;
            public Renderer flashRenderer;
            public LineRenderer ringRenderer;
            public Transform[] debris;
            public Vector3[] velocities;
            public float age, scale;
            public bool active;
        }

        readonly List<Fragment> fragments = new List<Fragment>();
        readonly List<Fighter> fighters = new List<Fighter>();
        readonly List<Beam> beams = new List<Beam>();
        readonly List<Burst> bursts = new List<Burst>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Transform> dust = new List<Transform>();
        readonly List<Vector3> dustVelocity = new List<Vector3>();
        readonly List<Transform> smoke = new List<Transform>();
        readonly List<Renderer> smokeRenderers = new List<Renderer>();
        readonly List<Vector3> smokeVelocity = new List<Vector3>();
        SetpieceDirector director;
        Transform stage;
        Material glow, spriteGlow, orange, ash, shellMaterial, molten, smokeMaterial;
        Material red, blue, redTrim, blueTrim, engine;
        MaterialPropertyBlock propertyBlock;
        Transform planetaryRing, planetaryFlash;
        LineRenderer planetaryRingLine;
        Renderer planetaryFlashRenderer;
        Light planetLight;
        Vector3 planetPosition;
        float planetRadius, elapsed, nextBattleBurst, nextLaserSound;
        int nextBeam, nextBurst;
        bool detonated;
        System.Random random;

        public void Initialize(SetpieceDirector owner) { director = owner; }

        public void ShowPlanetExplosion(Vector3 originalPlanetPosition, float originalRadius, Material planetMaterial)
        {
            Clear();
            CreateStage("SETPIECE_01_PlanetFracture");
            random = new System.Random(1043);
            ExplosionActive = true;
            planetPosition = originalPlanetPosition;
            planetRadius = Mathf.Max(1f, originalRadius);
            shellMaterial = MakeMaterial("Astra/FracturedPlanet", "Original ocean / fractured crust", new Color(.035f, .08f, .12f));
            if (planetMaterial && planetMaterial.HasProperty("_Color")) shellMaterial.SetColor("_Color", planetMaterial.GetColor("_Color"));
            molten = MakeMaterial("Astra/MoltenInterior", "Charred mantle with fine molten fissures", new Color(.075f, .05f, .038f));
            for (int latitude = 0; latitude < LatitudePieces; latitude++)
                for (int longitude = 0; longitude < LongitudePieces; longitude++) CreateFragment(longitude, latitude);

            planetaryRingLine = MakeRing("Expanding planetary shock front", stage, glow, 144, .075f);
            planetaryRing = planetaryRingLine.transform;
            planetaryRing.position = planetPosition;
            // The shockwave plane faces the cabin, but is slightly tilted for depth.
            planetaryRing.rotation = Quaternion.LookRotation(new Vector3(-1f, .15f, .1f));
            planetaryRing.gameObject.SetActive(false);
            planetaryFlash = MakeQuad("White-orange detonation flash", stage, spriteGlow);
            planetaryFlash.position = planetPosition + Vector3.left * planetRadius * .18f;
            planetaryFlash.rotation = Quaternion.LookRotation(Vector3.right);
            planetaryFlashRenderer = planetaryFlash.GetComponent<Renderer>();
            planetaryFlash.gameObject.SetActive(false);
            GameObject lightObject = new GameObject("Local detonation light"); lightObject.transform.SetParent(stage, false);
            planetLight = lightObject.AddComponent<Light>();
            planetLight.type = LightType.Point; planetLight.color = new Color(1f, .31f, .075f);
            planetLight.range = 45f; planetLight.intensity = 0f; lightObject.transform.position = planetPosition + Vector3.left * planetRadius;

            // Every debris particle has a fixed actor; no allocation occurs during the effect.
            for (int i = 0; i < 84; i++)
            {
                Vector3 direction = UnitVector();
                Transform p = Primitive("Ejecta / " + i, i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere,
                    stage, Vector3.zero, Vector3.one * Range(.045f, .22f), i % 4 == 0 ? orange : ash);
                p.position = planetPosition + direction * planetRadius * .3f;
                p.rotation = Quaternion.Euler(Range(0, 360), Range(0, 360), Range(0, 360));
                dust.Add(p); dustVelocity.Add(direction * Range(2f, 6.7f)); p.gameObject.SetActive(false);
            }
            smokeMaterial = MakeMaterial("Astra/SetpieceGlow", "Cooling vapor / soft alpha", new Color(.05f, .032f, .018f, .16f));
            smokeMaterial.SetFloat("_Radial", 1f);
            smokeMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            smokeMaterial.renderQueue = 2995;
            for (int i = 0; i < 12; i++)
            {
                Transform p = MakeQuad("Drifting vapor cloud / " + i, stage, smokeMaterial);
                smoke.Add(p); smokeRenderers.Add(p.GetComponent<Renderer>());
                smokeVelocity.Add(UnitVector() * Range(1f, 3.2f)); p.gameObject.SetActive(false);
            }
        }

        public void ShowBattle()
        {
            Clear();
            CreateStage("SETPIECE_05_RedBlueDogfight");
            random = new System.Random(55019);
            BattleActive = true;
            red = MakeMaterial("Unlit/Color", "Red faction / crimson armor", new Color(.24f, .045f, .04f));
            blue = MakeMaterial("Unlit/Color", "Blue faction / navy armor", new Color(.045f, .11f, .25f));
            redTrim = MakeMaterial("Unlit/Color", "Red faction / identification glow", new Color(2.4f, .11f, .055f));
            blueTrim = MakeMaterial("Unlit/Color", "Blue faction / identification glow", new Color(.045f, .8f, 2.3f));
            engine = MakeMaterial("Unlit/Color", "Fusion engine cores", new Color(.6f, 1.5f, 2.1f));
            for (int i = 0; i < ShipCapacity; i++) CreateFighter(i);
            for (int i = 0; i < LaserCapacity; i++)
            {
                LineRenderer line = MakeLine("Pooled pulse beam / " + i, stage, glow, .085f);
                line.positionCount = 2; line.gameObject.SetActive(false);
                beams.Add(new Beam { line = line });
            }
            for (int i = 0; i < BurstCapacity; i++) CreateBurst(i);
            nextBattleBurst = 1.3f;
        }

        void CreateStage(string name)
        {
            GameObject root = new GameObject(name); stage = root.transform; stage.SetParent(transform, false);
            propertyBlock = new MaterialPropertyBlock();
            glow = MakeMaterial("Astra/SetpieceGlow", "Additive shock fronts and beams", Color.white);
            spriteGlow = MakeMaterial("Astra/SetpieceGlow", "Radial explosion glow", Color.white);
            spriteGlow.SetFloat("_Radial", 1f);
            orange = MakeMaterial("Unlit/Color", "Glowing ejected metal", new Color(1.8f, .22f, .02f));
            ash = MakeMaterial("Unlit/Color", "Scorched floating debris", new Color(.12f, .07f, .055f));
        }

        void CreateFragment(int longitude, int latitude)
        {
            const int resolution = 9;
            Vector3 center = SpherePoint((longitude + .5f) / LongitudePieces, (latitude + .5f) / LatitudePieces) * .63f;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var outerTriangles = new List<int>(); var insideTriangles = new List<int>();
            // A paired shell plus four radial cut faces makes a genuinely closed curved solid.
            for (int layer = 0; layer < 2; layer++)
                for (int v = 0; v <= resolution; v++)
                    for (int u = 0; u <= resolution; u++)
                    {
                        Vector3 p = PatchPoint(longitude, latitude, u / (float)resolution, v / (float)resolution);
                        float radius = layer == 0 ? 1f : .28f;
                        vertices.Add(p * radius - center); normals.Add(layer == 0 ? p.normalized : -p.normalized);
                    }
            int side = resolution + 1, count = side * side;
            for (int v = 0; v < resolution; v++)
                for (int u = 0; u < resolution; u++)
                {
                    int a = v * side + u, b = a + 1, c = a + side, d = c + 1;
                    // Shared spherical positions retain the exact original planet silhouette.
                    Tri(outerTriangles, a, b, c); Tri(outerTriangles, b, d, c);
                    Tri(insideTriangles, a + count, c + count, b + count); Tri(insideTriangles, b + count, c + count, d + count);
                }
            // Cut faces are separately indexed, preserving sharp normals at the crust break.
            for (int edge = 0; edge < 4; edge++)
                for (int i = 0; i < resolution; i++)
                {
                    int a = EdgeIndex(edge, i, resolution), b = EdgeIndex(edge, i + 1, resolution);
                    AddCutFace(vertices, normals, insideTriangles, vertices[a], vertices[b], vertices[b + count], vertices[a + count]);
                }
            Mesh mesh = new Mesh { name = "Closed spherical fragment " + latitude + "-" + longitude };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.subMeshCount = 2;
            mesh.SetTriangles(outerTriangles, 0); mesh.SetTriangles(insideTriangles, 1); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
            GameObject obj = new GameObject("Crust wedge " + latitude + "-" + longitude);
            obj.transform.SetParent(stage, false); obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { shellMaterial, molten };
            obj.transform.localScale = Vector3.one * planetRadius * 2f;
            Vector3 rest = planetPosition + PlanetRotation * center * planetRadius * 2f;
            obj.transform.SetPositionAndRotation(rest, PlanetRotation);
            propertyBlock.Clear(); propertyBlock.SetVector("_FragmentOrigin", center); renderer.SetPropertyBlock(propertyBlock);
            Vector3 direction = (PlanetRotation * center).normalized;
            // The original planet sits low/right of the window. An asymmetric blast plume
            // ejects material upward and toward the view's middle without relocating the intact sphere.
            Vector3 ejectaVelocity = direction * Range(2.2f, 3.9f)
                + Vector3.up * Range(1.5f, 2.25f) + Vector3.forward * Range(.65f, 1.2f);
            Fragment fragment = new Fragment { transform = obj.transform, rest = rest, shell = renderer,
                rotation = PlanetRotation, velocity = ejectaVelocity, axis = UnitVector(), angularSpeed = Range(-31f, 31f) };
            // Thin glowing seam lines on the shell read as connected fault lines before separation.
            LineRenderer seam = MakeLine("Igniting fault line", obj.transform, glow, .010f);
            seam.useWorldSpace = false; seam.positionCount = resolution * 4; seam.loop = true;
            for (int edge = 0; edge < 4; edge++)
                for (int i = 0; i < resolution; i++)
                {
                    Vector3 point = vertices[EdgeIndex(edge, i, resolution)] + center;
                    seam.SetPosition(edge * resolution + i, point * 1.002f - center);
                }
            seam.gameObject.SetActive(false); fragment.seam = seam; fragments.Add(fragment);
        }

        static int EdgeIndex(int edge, int i, int resolution)
        {
            int side = resolution + 1;
            if (edge == 0) return i;
            if (edge == 1) return i * side + resolution;
            if (edge == 2) return resolution * side + resolution - i;
            return (resolution - i) * side;
        }
        static void Tri(List<int> triangles, int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
        static void AddCutFace(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count; Vector3 normal = Vector3.Cross(c - a, b - a).normalized;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++) normals.Add(normal);
            Tri(triangles, start, start + 2, start + 1); Tri(triangles, start, start + 3, start + 2);
        }
        static Vector3 PatchPoint(int longitude, int latitude, float u, float v)
        {
            float lon = (longitude + u) / LongitudePieces;
            float lat = (latitude + v) / LatitudePieces;
            // Warped shared boundaries avoid the appearance of a perfect orange-peel grid.
            float warpedLon = lon + Mathf.Sin(lat * Mathf.PI) * Mathf.Sin(lat * 11f + lon * Mathf.PI * 2f) * .016f;
            float warpedLat = lat + Mathf.Sin(lat * Mathf.PI) * Mathf.Sin(lon * Mathf.PI * 6f + lat * 7f) * .032f;
            return SpherePoint(warpedLon, warpedLat);
        }
        static Vector3 SpherePoint(float u, float v)
        {
            float theta = u * Mathf.PI * 2f, phi = v * Mathf.PI;
            return new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)) * .5f;
        }

        void Update()
        {
            if ((!ExplosionActive && !BattleActive) || Time.deltaTime <= 0f) return;
            float dt = Time.deltaTime; elapsed += dt;
            if (ExplosionActive) UpdatePlanet(dt);
            if (BattleActive) UpdateBattle(dt);
        }

        void UpdatePlanet(float dt)
        {
            float heat = Mathf.Clamp01(elapsed / .95f);
            if (elapsed < 1.15f)
            {
                foreach (Fragment fragment in fragments)
                {
                    fragment.seam.gameObject.SetActive(elapsed > .12f);
                    fragment.seam.startColor = fragment.seam.endColor = new Color(3f, .4f + heat * .4f, .04f, heat);
                    fragment.seam.widthMultiplier = .008f + heat * .060f;
                }
            }
            if (elapsed >= .96f && !detonated)
            {
                detonated = true; ExplosionCount++;
                planetaryRing.gameObject.SetActive(true); planetaryFlash.gameObject.SetActive(true);
                foreach (Transform p in dust) p.gameObject.SetActive(true);
                if (director) { director.Shake(.9f, 2.1f); director.PlayCue("explosion", 1f); }
            }
            if (!detonated) return;
            float t = Mathf.Max(0f, elapsed - .96f);
            // A short impulse instead of a multi-second ease-in makes the whole body visibly
            // disintegrate by the third second; the asymptote keeps prolonged play safe.
            float expansion = 1.65f * (t - .12f * (1f - Mathf.Exp(-t / .12f)));
            float remainingMass = Mathf.Lerp(1f, .60f, 1f - Mathf.Exp(-t * .8f));
            molten.SetFloat("_Heat", Mathf.Lerp(1f, .36f, 1f - Mathf.Exp(-t / 8f)));
            foreach (Fragment fragment in fragments)
            {
                // Nothing can cross the cabin plane, even after leaving the effect running for hours.
                float drift = 22f * (1f - Mathf.Exp(-expansion / 22f));
                Vector3 travel = fragment.velocity * drift;
                fragment.transform.position = fragment.rest + travel;
                Vector3 p = fragment.transform.position; p.x = Mathf.Max(planetRadius + 4.5f, p.x); fragment.transform.position = p;
                fragment.transform.rotation = Quaternion.AngleAxis(fragment.angularSpeed * t, fragment.axis) * fragment.rotation;
                // The large crust keeps its silhouette while shedding its incandescent outer debris.
                fragment.transform.localScale = Vector3.one * planetRadius * 2f * remainingMass;
                fragment.seam.startColor = fragment.seam.endColor = new Color(2.4f, .25f, .01f, Mathf.Clamp01(1f - t / 14f));
            }
            for (int i = 0; i < dust.Count; i++)
            {
                Vector3 direction = dustVelocity[i];
                dust[i].position = planetPosition + direction * (planetRadius * .04f + 14f * (1f - Mathf.Exp(-t / 4f)))
                    + new Vector3(0, 1.8f, .8f) * Mathf.Min(t, 10f);
                Vector3 p = dust[i].position; p.x = Mathf.Max(7f, p.x); dust[i].position = p;
                dust[i].Rotate(new Vector3(14f, 23f, 7f) * dt, Space.Self);
            }
            for (int i = 0; i < smoke.Count; i++)
            {
                smoke[i].gameObject.SetActive(t > .4f && t < 24f);
                smoke[i].position = planetPosition + smokeVelocity[i] * (1.5f + 8f * (1f - Mathf.Exp(-t / 8f)));
                Vector3 p = smoke[i].position; p.x = Mathf.Max(8f, p.x); smoke[i].position = p;
                smoke[i].rotation = Quaternion.LookRotation((p - new Vector3(0, 1.5f, 0)).normalized);
                smoke[i].localScale = Vector3.one * (1.6f + Mathf.Min(t, 18f) * .3f);
                SetColor(smokeRenderers[i], new Color(.065f, .043f, .025f, Mathf.Clamp01(t / 2f) * Mathf.Clamp01(1f - t / 24f) * .16f));
            }
            float ringSize = planetRadius * (1.2f + t * 2.3f);
            planetaryRing.localScale = Vector3.one * ringSize;
            planetaryRingLine.widthMultiplier = Mathf.Lerp(.025f, .005f, Mathf.Clamp01(t / 6f));
            planetaryRingLine.startColor = planetaryRingLine.endColor = new Color(2f, .5f, .07f, Mathf.Clamp01(1f - t / 5.5f));
            planetaryRing.gameObject.SetActive(t < 5.5f);
            planetaryFlash.localScale = Vector3.one * planetRadius * (2.7f + t * .8f);
            float flash = Mathf.Exp(-t * 5f) * 1.05f;
            SetColor(planetaryFlashRenderer, new Color(2.2f, 1.35f, .6f, flash));
            planetaryFlash.gameObject.SetActive(t < 1.2f);
            planetLight.intensity = flash * 3.7f;
        }

        void CreateFighter(int index)
        {
            int faction = index % 2; Material armor = faction == 0 ? red : blue, trim = faction == 0 ? redTrim : blueTrim;
            GameObject actor = new GameObject((faction == 0 ? "RED / Needle interceptor " : "BLUE / Twin-boom fighter ") + index);
            actor.transform.SetParent(stage, false);
            float size = index < 8 ? Range(.65f, .9f) : Range(.46f, .72f);
            actor.transform.localScale = Vector3.one * size;
            Primitive("Armored fuselage", PrimitiveType.Cube, actor.transform, Vector3.zero, new Vector3(.32f, .19f, 1.5f), armor);
            Primitive("Canopy", PrimitiveType.Sphere, actor.transform, new Vector3(0, .12f, .21f), new Vector3(.23f, .17f, .43f), trim);
            if (faction == 0)
            {
                MakeWing(actor.transform, "Swept delta wing", new[] { new Vector3(-1f, 0, -.7f), new Vector3(0, 0, .52f), new Vector3(1f, 0, -.7f) }, armor);
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform rail = Primitive("Crimson wingtip cannon", PrimitiveType.Cube, actor.transform,
                        new Vector3(side * .7f, .03f, -.43f), new Vector3(.09f, .09f, .8f), trim);
                    rail.localRotation = Quaternion.Euler(0, side * -14f, 0);
                }
                Primitive("Dorsal blade", PrimitiveType.Cube, actor.transform, new Vector3(0, .28f, -.45f), new Vector3(.06f, .52f, .6f), trim);
            }
            else
            {
                Primitive("Crosswing", PrimitiveType.Cube, actor.transform, new Vector3(0, 0, -.18f), new Vector3(1.9f, .09f, .25f), armor);
                for (int side = -1; side <= 1; side += 2)
                {
                    Primitive("Twin engine boom", PrimitiveType.Cube, actor.transform, new Vector3(side * .74f, 0, -.19f), new Vector3(.18f, .24f, 1.5f), armor);
                    Primitive("Azure boom light", PrimitiveType.Cube, actor.transform, new Vector3(side * .74f, .14f, -.22f), new Vector3(.11f, .035f, .95f), trim);
                    Primitive("Boom engine", PrimitiveType.Sphere, actor.transform, new Vector3(side * .74f, 0, -.95f), new Vector3(.18f, .18f, .34f), engine);
                }
            }
            Transform exhaust = Primitive("Pulsing drive plume", PrimitiveType.Sphere, actor.transform,
                new Vector3(0, 0, -.93f), new Vector3(.18f, .18f, .52f), trim);
            Fighter fighter = new Fighter { transform = actor.transform, exhaust = exhaust, index = index, faction = faction,
                phase = Range(0, Mathf.PI * 2f), fireAt = Range(.2f, 2f), anchor = new Vector3(Range(19f, 50f), Range(-8f, 12f), Range(-23f, 23f)) };
            fighter.previous = FlightPosition(fighter, 0); actor.transform.position = fighter.previous; fighters.Add(fighter);
        }

        void MakeWing(Transform parent, string name, Vector3[] points, Material material)
        {
            Mesh mesh = new Mesh { name = name }; mesh.vertices = new[] { points[0], points[1], points[2], points[0], points[1], points[2] };
            mesh.triangles = new[] { 0, 1, 2, 5, 4, 3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
            GameObject obj = new GameObject(name); obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        Vector3 FlightPosition(Fighter fighter, float time)
        {
            float t = time * (.25f + (fighter.index % 5) * .026f) + fighter.phase;
            return fighter.anchor + new Vector3(Mathf.Sin(t * .83f) * 8f,
                Mathf.Sin(t * 1.3f) * 5.5f + Mathf.Cos(t * 2.7f) * 1.5f, Mathf.Cos(t) * 15f);
        }

        void UpdateBattle(float dt)
        {
            foreach (Fighter fighter in fighters)
            {
                if (!fighter.active)
                {
                    if (elapsed < fighter.respawnAt) continue;
                    fighter.active = true; fighter.transform.gameObject.SetActive(true);
                    fighter.previous = FlightPosition(fighter, elapsed);
                }
                Vector3 next = FlightPosition(fighter, elapsed);
                fighter.velocity = (next - fighter.previous) / Mathf.Max(.0001f, dt);
                fighter.transform.position = next;
                Vector3 tangent = FlightPosition(fighter, elapsed + .03f) - next;
                if (tangent.sqrMagnitude > .00001f)
                    fighter.transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up) * Quaternion.Euler(0, 0, Mathf.Sin(elapsed + fighter.phase) * 28f);
                fighter.previous = next;
                fighter.exhaust.localScale = new Vector3(.18f, .18f, .4f + Mathf.Sin(elapsed * 18f + fighter.phase) * .14f);
                if (elapsed >= fighter.fireAt)
                {
                    Fighter target = fighters[(fighter.index + 1 + (fighter.index % 3) * 2) % fighters.Count];
                    FireBeam(fighter.transform.position + fighter.transform.forward * .55f, target.transform.position + target.velocity * .1f,
                        fighter.faction == 0 ? new Color(3f, .10f, .03f) : new Color(.03f, .72f, 3f));
                    fighter.fireAt = elapsed + Range(.75f, 2.2f);
                }
            }
            foreach (Beam beam in beams)
            {
                if (!beam.active) continue;
                beam.age += dt;
                if (beam.age >= beam.lifetime) { beam.active = false; beam.line.gameObject.SetActive(false); continue; }
                Vector3 point = beam.origin + beam.velocity * beam.age;
                beam.line.SetPosition(0, point); beam.line.SetPosition(1, point - beam.velocity.normalized * 1.9f);
            }
            if (elapsed >= nextBattleBurst)
            {
                int victimIndex = random.Next(fighters.Count); Fighter victim = fighters[victimIndex];
                if (victim.active)
                {
                    SpawnBurst(victim.transform.position, Range(.75f, 1.4f));
                    victim.active = false; victim.respawnAt = elapsed + Range(4f, 7f); victim.transform.gameObject.SetActive(false);
                }
                if (ExplosionCount % 3 == 0)
                {
                    // Foreground flak sells the cabin being in the middle, without ever clipping through it.
                    SpawnBurst(new Vector3(Range(8f, 13f), Range(-2f, 7f), Range(-8f, 8f)), .85f);
                    if (director) director.Shake(.2f, .48f);
                }
                nextBattleBurst = elapsed + Range(1.7f, 3.1f);
            }
            foreach (Burst burst in bursts) if (burst.active) UpdateBurst(burst, dt);
        }

        void FireBeam(Vector3 origin, Vector3 target, Color color)
        {
            Beam beam = beams[nextBeam++ % beams.Count]; Vector3 direction = target - origin;
            float distance = direction.magnitude; if (distance < .1f) return;
            beam.active = true; beam.origin = origin; beam.velocity = direction.normalized * 36f;
            beam.age = 0; beam.lifetime = Mathf.Clamp(distance / 36f, .18f, 2.8f); beam.color = color;
            beam.line.startColor = beam.line.endColor = color; beam.line.gameObject.SetActive(true);
            beam.line.SetPosition(0, origin); beam.line.SetPosition(1, origin - direction.normalized * 1.9f);
            if (elapsed > nextLaserSound)
            {
                nextLaserSound = elapsed + .28f;
                if (director) director.PlayCue("laser", .12f);
            }
        }

        void CreateBurst(int index)
        {
            GameObject obj = new GameObject("Pooled explosion " + index); obj.transform.SetParent(stage, false);
            Burst burst = new Burst { root = obj.transform, debris = new Transform[8], velocities = new Vector3[8] };
            burst.flash = MakeQuad("Fireball glow", obj.transform, spriteGlow); burst.flashRenderer = burst.flash.GetComponent<Renderer>();
            burst.ringRenderer = MakeRing("Pressure wave", obj.transform, glow, 48, .045f); burst.ring = burst.ringRenderer.transform;
            for (int i = 0; i < burst.debris.Length; i++)
                burst.debris[i] = Primitive("Tumbling hull plate " + i, PrimitiveType.Cube, obj.transform, Vector3.zero,
                    new Vector3(Range(.05f, .19f), Range(.03f, .06f), Range(.09f, .35f)), i < 4 ? orange : ash);
            bursts.Add(burst); obj.SetActive(false);
        }

        void SpawnBurst(Vector3 position, float scale)
        {
            Burst burst = bursts[nextBurst++ % bursts.Count]; burst.root.gameObject.SetActive(true);
            burst.root.position = position; burst.age = 0; burst.scale = scale; burst.active = true;
            burst.flash.rotation = Quaternion.LookRotation((position - new Vector3(0, 1.5f, 0)).normalized);
            burst.ring.rotation = burst.flash.rotation;
            for (int i = 0; i < burst.debris.Length; i++)
            {
                burst.debris[i].localPosition = Vector3.zero; burst.debris[i].localRotation = Quaternion.Euler(Range(0, 360), Range(0, 360), Range(0, 360));
                burst.velocities[i] = UnitVector() * Range(.9f, 3f) * scale;
            }
            ExplosionCount++;
            if (director) director.PlayCue("explosion", .2f);
        }

        void UpdateBurst(Burst burst, float dt)
        {
            burst.age += dt;
            if (burst.age > 3.5f) { burst.active = false; burst.root.gameObject.SetActive(false); return; }
            float t = burst.age;
            burst.flash.localScale = Vector3.one * burst.scale * (3f + t * 4f);
            SetColor(burst.flashRenderer, new Color(3f, .42f + .8f * Mathf.Exp(-t * 3f), .07f, Mathf.Exp(-t * 3f)));
            burst.ring.localScale = Vector3.one * burst.scale * (1f + t * 4f);
            burst.ringRenderer.startColor = burst.ringRenderer.endColor = new Color(2.2f, .4f, .035f, Mathf.Clamp01(1f - t / 1.6f));
            for (int i = 0; i < burst.debris.Length; i++)
            {
                burst.debris[i].localPosition = burst.velocities[i] * t;
                burst.debris[i].Rotate(new Vector3(73, 27, 49) * dt, Space.Self);
            }
        }

        Material MakeMaterial(string shaderName, string name, Color color)
        {
            Shader shader = Shader.Find(shaderName);
            if (!shader) shader = Shader.Find("Unlit/Color");
            Material material = new Material(shader) { name = name };
            material.color = color; ownedMaterials.Add(material); return material;
        }
        static Transform Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            Collider collider = obj.GetComponent<Collider>(); if (collider) { collider.enabled = false; Destroy(collider); }
            obj.GetComponent<Renderer>().sharedMaterial = material; return obj.transform;
        }
        Transform MakeQuad(string name, Transform parent, Material material)
        {
            return Primitive(name, PrimitiveType.Quad, parent, Vector3.zero, Vector3.one, material);
        }
        static LineRenderer MakeLine(string name, Transform parent, Material material, float width)
        {
            GameObject obj = new GameObject(name); obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.widthMultiplier = width; line.numCapVertices = 2; line.numCornerVertices = 2;
            line.useWorldSpace = true; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false; line.startColor = line.endColor = Color.white; return line;
        }
        static LineRenderer MakeRing(string name, Transform parent, Material material, int segments, float width)
        {
            LineRenderer line = MakeLine(name, parent, material, width); line.useWorldSpace = false;
            line.positionCount = segments; line.loop = true;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
            }
            return line;
        }
        void SetColor(Renderer renderer, Color color)
        {
            propertyBlock.Clear(); propertyBlock.SetColor("_Color", color); renderer.SetPropertyBlock(propertyBlock);
        }
        float Range(float min, float max) { return min + (max - min) * (float)random.NextDouble(); }
        Vector3 UnitVector()
        {
            float z = Range(-1, 1), angle = Range(0, Mathf.PI * 2f), radius = Mathf.Sqrt(Mathf.Max(0, 1 - z * z));
            return new Vector3(radius * Mathf.Cos(angle), z, radius * Mathf.Sin(angle));
        }

        public void Clear()
        {
            ExplosionActive = BattleActive = false; elapsed = 0; detonated = false; ExplosionCount = 0;
            nextBeam = nextBurst = 0; nextBattleBurst = nextLaserSound = 0;
            if (stage) { stage.gameObject.SetActive(false); Destroy(stage.gameObject); } stage = null;
            foreach (Mesh mesh in ownedMeshes) if (mesh) Destroy(mesh);
            foreach (Material material in ownedMaterials) if (material) Destroy(material);
            ownedMeshes.Clear(); ownedMaterials.Clear(); fragments.Clear(); fighters.Clear(); beams.Clear(); bursts.Clear(); dust.Clear(); dustVelocity.Clear();
            smoke.Clear(); smokeRenderers.Clear(); smokeVelocity.Clear();
            planetaryRing = planetaryFlash = null; planetaryRingLine = null; planetaryFlashRenderer = null; planetLight = null;
        }
        void OnDestroy() { Clear(); }
    }
}
