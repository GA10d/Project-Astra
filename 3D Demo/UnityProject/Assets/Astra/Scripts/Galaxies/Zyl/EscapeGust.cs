using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraCabin.Galaxies.Zyl
{
    // Streams individual pieces of the actual refuge meshes through the grave shot.
    sealed class EscapeGust
    {
        const int Capacity = 52;
        const float Interval = 1f / 30f;
        readonly Piece[] pool;
        readonly float halfFov;

        sealed class Piece
        {
            public GameObject Object;
            public int Number = -1;
            public float Duration, Depth, Height, Size, Spin, Drift, Phase;
            public Vector3 Axis;
        }

        public EscapeGust(Transform parent, Transform environment, Camera camera, List<UnityEngine.Object> resources)
        {
            halfFov = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            var variants = new List<(Mesh mesh, Material material)>();
            var filters = environment.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh &&
                f.sharedMesh.isReadable && f.GetComponent<MeshRenderer>()?.sharedMaterial)
                .OrderBy(f => f.name, StringComparer.Ordinal).ToArray();
            AddVariants("Bark_Grade2_Zone0_", filters, variants, resources);
            AddVariants("Stone_Grade1_Zone0_", filters, variants, resources);

            if (variants.Count == 0)
            {
                pool = Array.Empty<Piece>();
                return;
            }

            var origin = new GameObject("Zyl16 | windborne refuge debris").transform;
            origin.SetParent(parent);
            origin.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            pool = new Piece[Capacity];
            for (int i = 0; i < pool.Length; i++)
            {
                var variant = variants[i * 5 % variants.Count];
                var item = new GameObject("Wind fragment " + (i + 1));
                item.layer = ZylStomachEnvironment.EnvironmentLayer;
                item.transform.SetParent(origin, false);
                item.AddComponent<MeshFilter>().sharedMesh = variant.mesh;
                var renderer = item.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = variant.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                item.SetActive(false);
                pool[i] = new Piece { Object = item };
            }
        }

        public void Sample(float time, float aspect)
        {
            if (pool.Length == 0) return;
            int newest = Mathf.FloorToInt(time / Interval);
            for (int i = 0; i < pool.Length; i++)
            {
                var piece = pool[i];
                int number = newest - ((newest - i + Capacity) % Capacity);
                if (number < 0) continue;
                if (piece.Number != number)
                {
                    piece.Number = number;
                    var random = new System.Random(unchecked(1637 + number * 761));
                    float Next() => (float)random.NextDouble();
                    bool near = Next() < .24f;
                    piece.Duration = Mathf.Lerp(.63f, 1.55f, Next());
                    piece.Depth = near ? Mathf.Lerp(2.2f, 4.1f, Next()) : Mathf.Lerp(4.6f, 11.5f, Next());
                    piece.Height = Mathf.Lerp(.57f, .94f, Next());
                    piece.Size = near ? Mathf.Lerp(.13f, .42f, Next()) : Mathf.Lerp(.18f, .70f, Next());
                    piece.Spin = Mathf.Lerp(-760f, 760f, Next());
                    piece.Drift = Mathf.Lerp(-.08f, .08f, Next());
                    piece.Phase = Next() * Mathf.PI * 2f;
                    piece.Axis = new Vector3(Next() - .5f, Next() - .5f, Next() - .5f).normalized;
                }

                float progress = (time - number * Interval) / piece.Duration;
                bool visible = progress >= 0f && progress < 1f;
                if (piece.Object.activeSelf != visible) piece.Object.SetActive(visible);
                if (!visible) continue;
                float depth = piece.Depth * (1f - progress * .18f);
                float halfHeight = depth * halfFov;
                float x = Mathf.Lerp(-1.32f, 1.32f, progress);
                float y = piece.Height + piece.Drift * progress + .033f * Mathf.Sin(piece.Phase + progress * 7f);
                piece.Object.transform.localPosition = new Vector3(x * halfHeight * aspect, (y * 2f - 1f) * halfHeight, depth);
                piece.Object.transform.localScale = Vector3.one * piece.Size;
                piece.Object.transform.localRotation = Quaternion.AngleAxis(piece.Phase * Mathf.Rad2Deg +
                    progress * piece.Duration * piece.Spin, piece.Axis);
            }
        }

        static void AddVariants(string prefix, MeshFilter[] filters, List<(Mesh mesh, Material material)> output,
            List<UnityEngine.Object> resources)
        {
            foreach (var filter in filters.Where(f => f.name.StartsWith(prefix, StringComparison.Ordinal)).Take(2))
            {
                var source = filter.sharedMesh;
                var positions = source.vertices;
                var indices = source.triangles;
                var parents = Enumerable.Range(0, positions.Length).ToArray();
                var points = new Dictionary<Vector3Int, int>();
                int Root(int vertex)
                {
                    while (parents[vertex] != vertex)
                    {
                        parents[vertex] = parents[parents[vertex]];
                        vertex = parents[vertex];
                    }
                    return vertex;
                }

                for (int i = 0; i < positions.Length; i++)
                {
                    var key = Vector3Int.RoundToInt(positions[i] * 10000f);
                    if (points.TryGetValue(key, out int first)) parents[i] = first;
                    else points.Add(key, i);
                }
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int a = Root(indices[i]);
                    parents[Root(indices[i + 1])] = a;
                    parents[Root(indices[i + 2])] = a;
                }

                var islands = new Dictionary<int, List<int>>();
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int root = Root(indices[i]);
                    if (!islands.TryGetValue(root, out var island)) islands.Add(root, island = new List<int>());
                    island.Add(indices[i]);
                    island.Add(indices[i + 1]);
                    island.Add(indices[i + 2]);
                }

                var uv = source.uv;
                var normals = source.normals;
                var tangents = source.tangents;
                foreach (var island in islands.Values.OrderByDescending(part => part.Count).Take(3))
                {
                    var vertices = island.Distinct().ToArray();
                    var bounds = new Bounds(positions[vertices[0]], Vector3.zero);
                    foreach (int i in vertices) bounds.Encapsulate(positions[i]);
                    float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    if (longest < .01f) continue;
                    var remap = new Dictionary<int, int>();
                    for (int i = 0; i < vertices.Length; i++) remap.Add(vertices[i], i);
                    var mesh = new Mesh { name = "Wind fragment | " + filter.name };
                    mesh.vertices = vertices.Select(i => (positions[i] - bounds.center) / longest).ToArray();
                    mesh.uv = vertices.Select(i => uv[i]).ToArray();
                    mesh.normals = vertices.Select(i => normals[i]).ToArray();
                    mesh.tangents = vertices.Select(i => tangents[i]).ToArray();
                    mesh.SetTriangles(island.Select(i => remap[i]).ToArray(), 0);
                    output.Add((mesh, filter.GetComponent<MeshRenderer>().sharedMaterial));
                    resources.Add(mesh);
                }
            }
        }
    }
}
