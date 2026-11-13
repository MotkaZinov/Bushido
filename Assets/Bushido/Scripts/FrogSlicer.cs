using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bushido
{
    // Clips actual triangles in world space. Each disconnected cut contour gets its own cap.
    public static class FrogSlicer
    {
        struct Vertex
        {
            public Vector3 position, normal;
            public Vector2 uv;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex {
                position = Vector3.Lerp(a.position, b.position, t),
                normal = Vector3.Lerp(a.normal, b.normal, t).normalized,
                uv = Vector2.Lerp(a.uv, b.uv, t)
            };
        }

        sealed class Surface
        {
            readonly List<Vector3> positions = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int>[] triangles;
            public Surface(int count)
            {
                triangles = new List<int>[count];
                for (int i = 0; i < count; i++) triangles[i] = new List<int>();
            }
            public void Triangle(Vertex a, Vertex b, Vertex c, int material)
            {
                foreach (Vertex vertex in new[] { a, b, c })
                {
                    triangles[material].Add(positions.Count);
                    positions.Add(vertex.position);
                    normals.Add(vertex.normal);
                    uvs.Add(vertex.uv);
                }
            }
            public Mesh Build(Vector3 origin)
            {
                if (positions.Count == 0) return null;
                for (int i = 0; i < positions.Count; i++) positions[i] -= origin;
                var mesh = new Mesh { name = "Fragmento de yokai", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        public static bool Slice(Transform target, Vector3 a, Vector3 b, Material interior,
            out GameObject positive, out GameObject negative)
        {
            positive = negative = null;
            Vector3 normal = Vector3.Cross(b - a, Vector3.forward).normalized;
            if (normal.sqrMagnitude < 0.5f) return false;
            var plane = new Plane(normal, a);
            var filters = target.GetComponentsInChildren<MeshFilter>();
            bool hit = false;
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var vertices = mesh.vertices;
                var indices = mesh.triangles;
                for (int i = 0; i < indices.Length && !hit; i += 3)
                    hit = Intersects(a, b, filter.transform.TransformPoint(vertices[indices[i]]),
                        filter.transform.TransformPoint(vertices[indices[i + 1]]), filter.transform.TransformPoint(vertices[indices[i + 2]]));
            }
            if (!hit) return false;
            positive = new GameObject("Rana • fragmento +");
            negative = new GameObject("Rana • fragmento −");
            positive.transform.position = negative.transform.position = target.position;
            positive.transform.SetParent(target.parent, true);
            negative.transform.SetParent(target.parent, true);
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || !mesh.isReadable) continue;
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var uvs = mesh.uv;
                var sides = new[] { new Surface(mesh.subMeshCount + 1), new Surface(mesh.subMeshCount + 1) };
                var edges = new List<Vector3[]>();
                Matrix4x4 normalMatrix = filter.transform.localToWorldMatrix.inverse.transpose;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var indices = mesh.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var triangle = new Vertex[3];
                        for (int j = 0; j < 3; j++)
                        {
                            int index = indices[i + j];
                            triangle[j] = new Vertex { position = filter.transform.TransformPoint(vertices[index]),
                                normal = normals.Length == vertices.Length ? normalMatrix.MultiplyVector(normals[index]).normalized : Vector3.up,
                                uv = uvs.Length == vertices.Length ? uvs[index] : Vector2.zero };
                        }
                        var intersections = new List<Vector3>(2);
                        for (int j = 0; j < 3; j++)
                        {
                            Vertex start = triangle[j], end = triangle[(j + 1) % 3];
                            float d0 = plane.GetDistanceToPoint(start.position), d1 = plane.GetDistanceToPoint(end.position);
                            if ((d0 >= 0) != (d1 >= 0)) intersections.Add(Vector3.Lerp(start.position, end.position, d0 / (d0 - d1)));
                        }
                        if (intersections.Count == 2 && (intersections[0] - intersections[1]).sqrMagnitude > 0.00000001f)
                            edges.Add(intersections.ToArray());
                        for (int side = 0; side < 2; side++)
                        {
                            var polygon = new List<Vertex>(4);
                            float sign = side == 0 ? 1 : -1;
                            for (int j = 0; j < 3; j++)
                            {
                                Vertex start = triangle[j], end = triangle[(j + 1) % 3];
                                float d0 = plane.GetDistanceToPoint(start.position) * sign, d1 = plane.GetDistanceToPoint(end.position) * sign;
                                if (d0 >= 0) polygon.Add(start);
                                if ((d0 >= 0) != (d1 >= 0)) polygon.Add(Vertex.Lerp(start, end, d0 / (d0 - d1)));
                            }
                            for (int j = 1; j + 1 < polygon.Count; j++) sides[side].Triangle(polygon[0], polygon[j], polygon[j + 1], sub);
                        }
                    }
                }
                Cap(edges, normal, sides, mesh.subMeshCount);
                var materials = new Material[mesh.subMeshCount + 1];
                for (int i = 0; i < mesh.subMeshCount; i++) materials[i] = renderer.sharedMaterials[Mathf.Min(i, renderer.sharedMaterials.Length - 1)];
                materials[mesh.subMeshCount] = interior;
                for (int side = 0; side < 2; side++)
                {
                    Mesh fragment = sides[side].Build(target.position);
                    if (fragment == null) continue;
                    var part = new GameObject(filter.name);
                    part.transform.SetParent(side == 0 ? positive.transform : negative.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = fragment;
                    part.AddComponent<MeshRenderer>().sharedMaterials = materials;
                    part.AddComponent<SliceMeshOwner>();
                }
            }
            return true;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float x = Cross(b - a, p - a), y = Cross(c - b, p - b), z = Cross(a - c, p - c);
            return (x >= -0.00001f && y >= -0.00001f && z >= -0.00001f) || (x <= 0.00001f && y <= 0.00001f && z <= 0.00001f);
        }
        static bool Intersects(Vector2 a, Vector2 b, Vector2 x, Vector2 y, Vector2 z)
        {
            if (Mathf.Abs(Cross(y - x, z - x)) < 0.000001f) return false;
            if (Inside(a, x, y, z) || Inside(b, x, y, z)) return true;
            var points = new[] { x, y, z };
            for (int i = 0; i < 3; i++)
            {
                Vector2 edge = points[(i + 1) % 3] - points[i];
                float divisor = Cross(b - a, edge);
                if (Mathf.Abs(divisor) < 0.000001f) continue;
                float t = Cross(points[i] - a, edge) / divisor;
                float u = Cross(points[i] - a, b - a) / divisor;
                if (t >= 0 && t <= 1 && u >= 0 && u <= 1) return true;
            }
            return false;
        }

        static void Cap(List<Vector3[]> edges, Vector3 normal, Surface[] sides, int material)
        {
            Vector3 axis = Vector3.Cross(normal, Vector3.forward).normalized;
            Vector3 up = Vector3.Cross(normal, axis);
            while (edges.Count > 0)
            {
                var loop = new List<Vector3> { edges[0][0], edges[0][1] };
                edges.RemoveAt(0);
                bool closed = false;
                while (edges.Count > 0)
                {
                    int match = edges.FindIndex(e => (e[0] - loop[loop.Count - 1]).sqrMagnitude < 0.0000001f || (e[1] - loop[loop.Count - 1]).sqrMagnitude < 0.0000001f);
                    if (match < 0) break;
                    var edge = edges[match];
                    Vector3 next = (edge[0] - loop[loop.Count - 1]).sqrMagnitude < 0.0000001f ? edge[1] : edge[0];
                    edges.RemoveAt(match);
                    if ((next - loop[0]).sqrMagnitude < 0.0000001f) { closed = true; break; }
                    loop.Add(next);
                }
                if (!closed || loop.Count < 3) continue;
                for (int i = loop.Count - 1; i >= 0 && loop.Count > 3; i--)
                {
                    Vector3 previous = loop[(i + loop.Count - 1) % loop.Count];
                    Vector3 next = loop[(i + 1) % loop.Count];
                    if (Vector3.Cross((loop[i] - previous).normalized, (next - loop[i]).normalized).sqrMagnitude < 0.000001f)
                        loop.RemoveAt(i);
                }
                var points = loop.ConvertAll(p => new Vector2(Vector3.Dot(p, axis), Vector3.Dot(p, up)));
                float area = 0;
                for (int i = 0; i < points.Count; i++) area += Cross(points[i], points[(i + 1) % points.Count]);
                if (area < 0) { points.Reverse(); loop.Reverse(); }
                // Ear clipping preserves concave outlines instead of filling them with a centroid fan.
                while (loop.Count >= 3)
                {
                    bool found = false;
                    for (int i = 0; i < loop.Count; i++)
                    {
                        int prev = (i + loop.Count - 1) % loop.Count, next = (i + 1) % loop.Count;
                        if (Cross(points[i] - points[prev], points[next] - points[i]) <= 0.00000001f) continue;
                        bool blocked = false;
                        for (int j = 0; j < loop.Count; j++)
                            if (j != prev && j != i && j != next && Inside(points[j], points[prev], points[i], points[next])) { blocked = true; break; }
                        if (blocked) continue;
                        var a = new Vertex { position = loop[prev], normal = normal, uv = points[prev] };
                        var b = new Vertex { position = loop[i], normal = normal, uv = points[i] };
                        var c = new Vertex { position = loop[next], normal = normal, uv = points[next] };
                        sides[1].Triangle(a, b, c, material);
                        a.normal = b.normal = c.normal = -normal;
                        sides[0].Triangle(c, b, a, material);
                        loop.RemoveAt(i); points.RemoveAt(i); found = true; break;
                    }
                    if (!found) break;
                }
            }
        }
    }

    public sealed class SliceMeshOwner : MonoBehaviour
    {
        void OnDestroy() { Destroy(GetComponent<MeshFilter>().sharedMesh); }
    }
}
