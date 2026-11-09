using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraCabin.Galaxies.Zyl
{
    // Splits only the source edges near the point of contact. Adjacent triangles
    // share edge decisions, including along UV seams, so the wall stays continuous.
    static class FoolsContactMesh
    {
        const float Radius = 2.65f;
        const float FineRadius = 2.05f;

        static (Vector3, Vector3) Edge(Vector3 a, Vector3 b)
        {
            bool ordered = a.x < b.x || (a.x == b.x && (a.y < b.y || (a.y == b.y && a.z <= b.z)));
            return ordered ? (a, b) : (b, a);
        }

        public static Mesh Refine(MeshFilter filter, Vector3 contact)
        {
            var original = filter.sharedMesh;
            if (!original.isReadable) throw new InvalidOperationException("命中的胃壁网格未启用 Read/Write。");
            var vertices = new List<Vector3>(original.vertices);
            var normals = new List<Vector3>(original.normals);
            var tangents = new List<Vector4>(original.tangents);
            var uv = new List<Vector2>(original.uv);
            if (normals.Count != vertices.Count || tangents.Count != vertices.Count || uv.Count != vertices.Count)
                throw new InvalidOperationException("胃壁网格缺少位移所需的法线、切线或纹理坐标。");

            var matrix = filter.transform.localToWorldMatrix;
            var positions = vertices.ConvertAll(v => matrix.MultiplyPoint3x4(v));
            var submeshes = new List<int>[original.subMeshCount];
            for (int sub = 0; sub < submeshes.Length; sub++)
                submeshes[sub] = new List<int>(original.GetTriangles(sub));

            // Neighbouring chunks still use their original boundary edges.
            var boundary = new HashSet<(Vector3, Vector3)>();
            var seen = new HashSet<(Vector3, Vector3)>();
            void CountBoundary(int a, int b)
            {
                var edge = Edge(vertices[a], vertices[b]);
                if (seen.Add(edge)) boundary.Add(edge);
                else boundary.Remove(edge);
            }
            foreach (var indices in submeshes)
            for (int i = 0; i < indices.Count; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                CountBoundary(a, b); CountBoundary(b, c); CountBoundary(c, a);
            }

            for (int level = 0; level < 8; level++)
            {
                var splits = new HashSet<(Vector3, Vector3)>();
                void Mark(int a, int b, float edgeLimit)
                {
                    var edge = Edge(vertices[a], vertices[b]);
                    if (!boundary.Contains(edge) && (positions[a] - positions[b]).sqrMagnitude > edgeLimit * edgeLimit)
                        splits.Add(edge);
                }

                foreach (var indices in submeshes)
                for (int i = 0; i < indices.Count; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    var bounds = new Bounds(positions[a], Vector3.zero);
                    bounds.Encapsulate(positions[b]);
                    bounds.Encapsulate(positions[c]);
                    float squaredDistance = bounds.SqrDistance(contact);
                    if (squaredDistance > Radius * Radius) continue;
                    float edgeLimit = squaredDistance < FineRadius * FineRadius ? .16f : .30f;
                    Mark(a, b, edgeLimit); Mark(b, c, edgeLimit); Mark(c, a, edgeLimit);
                }
                if (splits.Count == 0) break;

                var midpoints = new Dictionary<(int, int), int>();
                int Mid(int a, int b)
                {
                    if (!splits.Contains(Edge(vertices[a], vertices[b]))) return -1;
                    var key = a < b ? (a, b) : (b, a);
                    if (midpoints.TryGetValue(key, out int index)) return index;
                    index = vertices.Count;
                    midpoints.Add(key, index);
                    vertices.Add((vertices[a] + vertices[b]) * .5f);
                    positions.Add((positions[a] + positions[b]) * .5f);
                    normals.Add(Vector3.Lerp(normals[a], normals[b], .5f).normalized);
                    var tangent = ((Vector3)(tangents[a] + tangents[b])).normalized;
                    tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, tangents[a].w));
                    uv.Add((uv[a] + uv[b]) * .5f);
                    return index;
                }

                for (int sub = 0; sub < submeshes.Length; sub++)
                {
                    var indices = submeshes[sub];
                    var next = new List<int>(indices.Count);
                    void Tri(int a, int b, int c) { next.Add(a); next.Add(b); next.Add(c); }
                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                        int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                        if (ab >= 0 && bc >= 0 && ca >= 0)
                        {
                            Tri(a, ab, ca); Tri(ab, b, bc); Tri(ca, bc, c); Tri(ab, bc, ca);
                        }
                        else if (ab >= 0 && bc >= 0) { Tri(a, ab, c); Tri(ab, b, bc); Tri(ab, bc, c); }
                        else if (bc >= 0 && ca >= 0) { Tri(b, bc, a); Tri(bc, c, ca); Tri(bc, ca, a); }
                        else if (ca >= 0 && ab >= 0) { Tri(c, ca, b); Tri(ca, a, ab); Tri(ca, ab, b); }
                        else if (ab >= 0) { Tri(a, ab, c); Tri(ab, b, c); }
                        else if (bc >= 0) { Tri(b, bc, a); Tri(bc, c, a); }
                        else if (ca >= 0) { Tri(c, ca, b); Tri(ca, a, b); }
                        else Tri(a, b, c);
                    }
                    submeshes[sub] = next;
                }
            }

            var mesh = new Mesh { name = original.name + " | refined Zyl15 contact",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : original.indexFormat };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i, false);
            var meshBounds = original.bounds;
            meshBounds.Expand(3f);
            mesh.bounds = meshBounds;
            return mesh;
        }
    }
}
