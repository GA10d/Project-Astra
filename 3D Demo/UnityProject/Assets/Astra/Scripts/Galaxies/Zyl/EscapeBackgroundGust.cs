using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraCabin.Galaxies.Zyl
{
    sealed class EscapeBackgroundGust : IDisposable
    {
        const string ResourcePath = "Galaxies/Zyl/Escape/BackgroundGust";
        readonly List<Source> sources = new List<Source>();
        readonly Vector3 right, up, away;
        public float Duration { get; private set; }

        sealed class Source
        {
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public EscapeBackgroundDebris.Group Group;
            public GameObject Root;
            public Part[] Parts;
            public bool Split;
            public float StartTime = float.PositiveInfinity;
        }

        sealed class Part
        {
            public Transform Transform;
            public Vector3 Pivot, SpinAxis;
            public float Delay, FlightTime, Travel, Lift, Spin, Depth;
        }

        public EscapeBackgroundGust(Transform environment, Camera camera, Vector3 up,
            List<UnityEngine.Object> resources)
        {
            this.up = up;
            right = Vector3.ProjectOnPlane(camera.transform.right, up).normalized;
            away = Vector3.ProjectOnPlane(camera.transform.forward, up).normalized;
            var asset = Resources.Load<EscapeBackgroundDebris>(ResourcePath);
            if (!asset) return;
            var filters = environment.GetComponentsInChildren<MeshFilter>();
            Material woodCut = null, stoneCut = null;
            try
            {
                foreach (var group in asset.groups)
                {
                    var filter = Array.Find(filters, f => f.name == group.objectName && f.sharedMesh == group.source);
                    if (!filter || !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled) continue;
                    var source = new Source { Filter = filter, Renderer = renderer, Group = group };
                    sources.Add(source);
                    var root = new GameObject("Zyl16 | fractured " + filter.name);
                    source.Root = root;
                    root.SetActive(false);
                    root.transform.SetParent(filter.transform, false);
                    source.Parts = new Part[group.pieces.Length];

                    if (group.stone && !stoneCut)
                        stoneCut = CutMaterial("Fresh stone fracture", new Color(.44f,.38f,.31f), false, resources);
                    if (!group.stone && !woodCut)
                        woodCut = CutMaterial("Fresh split wood", new Color(.52f,.33f,.19f), true, resources);

                    for (int i = 0; i < source.Parts.Length; i++)
                    {
                        var baked = group.pieces[i];
                        var piece = new GameObject(baked.mesh.name);
                        piece.layer = ZylStomachEnvironment.EnvironmentLayer;
                        piece.transform.SetParent(root.transform, false);
                        piece.transform.localPosition = baked.pivot;
                        piece.AddComponent<MeshFilter>().sharedMesh = baked.mesh;
                        var partRenderer = piece.AddComponent<MeshRenderer>();
                        partRenderer.sharedMaterials = baked.mesh.subMeshCount > 1
                            ? new[] { renderer.sharedMaterial, group.stone ? stoneCut : woodCut }
                            : new[] { renderer.sharedMaterial };
                        partRenderer.shadowCastingMode = ShadowCastingMode.Off;

                        int seed = baked.objectIndex * 131 + i * 17;
                        var random = new System.Random(seed);
                        float Next() => (float)random.NextDouble();
                        Vector3 pivot = piece.transform.position;
                        Vector3 view = camera.WorldToViewportPoint(pivot);
                        Vector3 exit = camera.ViewportToWorldPoint(new Vector3(1.48f,.5f,view.z));
                        var part = new Part {
                            Transform = piece.transform, Pivot = pivot,
                            Delay = (group.stone ? .38f : .04f) + (baked.objectIndex % 5) * .075f + Next() * .10f,
                            FlightTime = group.stone ? Mathf.Lerp(1.58f,1.88f,Next()) : Mathf.Lerp(1.14f,1.63f,Next()),
                            Travel = Mathf.Max(5f, Vector3.Dot(exit - pivot, right)),
                            Lift = group.stone ? Mathf.Lerp(.28f,.67f,Next()) : Mathf.Lerp(.95f,2.0f,Next()),
                            Spin = group.stone ? Mathf.Lerp(95f,235f,Next()) : Mathf.Lerp(225f,610f,Next()),
                            SpinAxis = (away * (group.stone ? -1 : 1) + up * (Next() - .5f) + right * (Next() - .5f)).normalized,
                            Depth = Mathf.Lerp(.35f,1.8f,Next())
                        };
                        source.Parts[i] = part;
                        source.StartTime = Mathf.Min(source.StartTime, part.Delay);
                        Duration = Mathf.Max(Duration, part.Delay + part.FlightTime);
                    }
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        static Material CutMaterial(string name, Color color, bool wood, List<UnityEngine.Object> resources)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", .11f);
            const int size = 64;
            var grain = new Texture2D(size, size, TextureFormat.RGB24, true) { name = name + " grain" };
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.PingPong(x * 2f / (size - 1), 1f);
                float v = Mathf.PingPong(y * 2f / (size - 1), 1f);
                float coarse = Mathf.PerlinNoise(u * (wood ? 8f : 4f) + 1.7f, v * (wood ? 2f : 4f) + .3f);
                float fine = Mathf.PerlinNoise(u * 23f + 5f, v * 23f + 6f);
                float value = .48f + coarse * .32f + fine * .20f;
                colors[y * size + x] = new Color(value, value, value);
            }
            grain.SetPixels(colors);
            grain.Apply(true, true);
            material.mainTexture = grain;
            resources.Add(grain);
            resources.Add(material);
            return material;
        }

        public void Sample(float time)
        {
            if (time < 0f) return;
            foreach (var source in sources)
            {
                if (!source.Split)
                {
                    if (time < source.StartTime) continue;
                    source.Split = true;
                    if (source.Group.stationary) source.Filter.sharedMesh = source.Group.stationary;
                    else source.Renderer.enabled = false;
                    source.Root.SetActive(true);
                }
                foreach (var part in source.Parts)
                {
                    float age = time - part.Delay;
                    float u = Mathf.Clamp01(age / part.FlightTime);
                    part.Transform.gameObject.SetActive(u < 1f);
                    if (u >= 1f) continue;
                    float progress = u * u * (2f - u);
                    float liftProgress = source.Group.stone ? Mathf.InverseLerp(.17f,1f,u) : u;
                    part.Transform.position = part.Pivot + right * (progress * part.Travel)
                        + away * (progress * part.Depth) + up * (Mathf.Sin(liftProgress * 2.2f) * part.Lift);
                    part.Transform.rotation = Quaternion.AngleAxis(part.Spin * u, part.SpinAxis) * source.Root.transform.rotation;
                }
            }
        }

        public void Dispose()
        {
            foreach (var source in sources)
            {
                if (source.Root)
                {
                    source.Root.SetActive(false);
                    UnityEngine.Object.Destroy(source.Root);
                }
                if (!source.Split) continue;
                if (source.Filter) source.Filter.sharedMesh = source.Group.source;
                if (source.Renderer) source.Renderer.enabled = true;
            }
            sources.Clear();
        }
    }
}
