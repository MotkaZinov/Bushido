using System.Collections.Generic;
using UnityEngine;
namespace Bushido
{
    // Parte la pantalla en polígonos convexos siguiendo cada corte y luego los deja caer.
    // Empieza como un único rectángulo; cada corte divide en dos mitades limpias los polígonos
    // que atraviesa (Sutherland-Hodgman contra los dos lados de la recta). Los puntos se guardan
    // en reposo y el UV sale de ellos, así la imagen nunca se estira al mover los fragmentos.
    public sealed class ScreenSlicer
    {
        public const float GlowTime = .35f, FallTime = 1.05f;

        public sealed class Shard
        {
            public readonly List<Vector2> points = new List<Vector2>();
            // Por arista (i -> i+1): instante en que se cortó, o -1 si es borde de pantalla.
            public readonly List<float> cutTime = new List<float>();
            public Vector2 offset, slide, velocity, push;
            public float angle, spin;

            public Vector2 Centroid()
            {
                Vector2 sum = Vector2.zero;
                foreach (var point in points) sum += point;
                return sum / points.Count;
            }

            public float Area()
            {
                float area = 0;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 a = points[i], b = points[(i + 1) % points.Count];
                    area += a.x * b.y - b.x * a.y;
                }
                return Mathf.Abs(area) * .5f;
            }
        }

        public readonly List<Shard> shards = new List<Shard>();
        public bool Released => released >= 0;
        public bool Finished => released >= 0 && clock - released >= FallTime;

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>(), glows = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();
        float halfWidth, halfHeight, clock, released = -1;

        public void Reset(float width, float height)
        {
            halfWidth = width; halfHeight = height; clock = 0; released = -1;
            shards.Clear();
            var screen = new Shard();
            screen.points.AddRange(new[] { new Vector2(-width, -height), new Vector2(width, -height), new Vector2(width, height), new Vector2(-width, height) });
            for (int i = 0; i < 4; i++) screen.cutTime.Add(-1);
            shards.Add(screen);
        }

        public float Area()
        {
            float area = 0;
            foreach (var shard in shards) area += shard.Area();
            return area;
        }

        // The blade crosses the whole screen, so the cut is the full line through a and b.
        // The two sides slide in opposite directions along the cut by `shear`.
        public void Cut(Vector2 a, Vector2 b, float shear)
        {
            Vector2 along = (b - a).normalized, normal = new Vector2(-along.y, along.x);
            int existing = shards.Count;
            for (int s = 0; s < existing; s++)
            {
                Shard shard = shards[s];
                Vector2 origin = a - shard.offset; // earlier cuts already slid this piece
                float min = float.MaxValue, max = float.MinValue;
                foreach (var point in shard.points)
                {
                    float d = Vector2.Dot(point - origin, normal);
                    min = Mathf.Min(min, d); max = Mathf.Max(max, d);
                }
                const float epsilon = 1e-4f;
                if (min > -epsilon || max < epsilon) continue;
                var front = new Shard(); var back = new Shard();
                int count = shard.points.Count;
                for (int i = 0; i < count; i++)
                {
                    Vector2 current = shard.points[i], next = shard.points[(i + 1) % count];
                    float dc = Vector2.Dot(current - origin, normal), dn = Vector2.Dot(next - origin, normal);
                    float edge = shard.cutTime[i];
                    if (dc >= 0) { front.points.Add(current); front.cutTime.Add(edge); }
                    if (dc <= 0) { back.points.Add(current); back.cutTime.Add(edge); }
                    if (dc * dn >= 0) continue;
                    Vector2 hit = current + (next - current) * (dc / (dc - dn));
                    // The side being left closes along the new cut; the other continues the old edge.
                    front.points.Add(hit); front.cutTime.Add(dc > 0 ? clock : edge);
                    back.points.Add(hit); back.cutTime.Add(dc > 0 ? edge : clock);
                }
                foreach (var half in new[] { front, back })
                {
                    float side = half == front ? 1 : -1;
                    half.offset = shard.offset;
                    half.slide = shard.slide + along * shear * side;
                    half.push = normal * side;
                }
                shards[s] = front;
                shards.Add(back);
            }
        }

        // Relights every cut edge, for the pause before the pieces fall.
        public void Flash()
        {
            foreach (var shard in shards)
                for (int i = 0; i < shard.cutTime.Count; i++)
                    if (shard.cutTime[i] >= 0) shard.cutTime[i] = clock;
        }

        public void Release(float impulse, int seed)
        {
            var random = new System.Random(seed);
            released = clock;
            foreach (var shard in shards)
            {
                float jitter = .6f + (float)random.NextDouble() * .8f;
                shard.velocity = shard.push * impulse * jitter + new Vector2((float)random.NextDouble() - .5f, (float)random.NextDouble() * .5f) * impulse * .35f;
                shard.spin = ((float)random.NextDouble() - .5f) * 110f;
            }
        }

        public void Step(float delta)
        {
            clock += delta;
            foreach (var shard in shards)
            {
                if (released < 0) { shard.offset = Vector2.Lerp(shard.offset, shard.slide, 1 - Mathf.Exp(-delta * 28)); continue; }
                shard.velocity += Vector2.down * halfHeight * 7f * delta;
                shard.offset += shard.velocity * delta;
                shard.angle += shard.spin * delta;
            }
        }

        public void Build(Mesh mesh, Color glowColor, float edgeWidth)
        {
            vertices.Clear(); uvs.Clear(); glows.Clear(); colors.Clear(); triangles.Clear();
            // Mathf.SmoothStep interpolates between its first two arguments, so normalise the fade window first.
            float alpha = released < 0 ? 1 : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.7f, 1, (clock - released) / FallTime));
            foreach (var shard in shards)
            {
                Vector2 centre = shard.Centroid();
                float sin = Mathf.Sin(shard.angle * Mathf.Deg2Rad), cos = Mathf.Cos(shard.angle * Mathf.Deg2Rad);
                int first = vertices.Count, count = shard.points.Count;
                for (int i = 0; i < count; i++)
                {
                    Vector2 rest = shard.points[i], local = rest - centre;
                    vertices.Add(centre + new Vector2(local.x * cos - local.y * sin, local.x * sin + local.y * cos) + shard.offset);
                    uvs.Add(new Vector2(rest.x / halfWidth * .5f + .5f, rest.y / halfHeight * .5f + .5f));
                    glows.Add(Vector2.zero);
                    colors.Add(new Color(1, 1, 1, alpha));
                }
                for (int i = 1; i < count - 1; i++) { triangles.Add(first); triangles.Add(first + i); triangles.Add(first + i + 1); }
            }
            // Glow only on freshly cut edges, drawn after the pieces so it sits on top of them.
            int pieceVertices = vertices.Count, start = 0;
            foreach (var shard in shards)
            {
                int count = shard.points.Count;
                for (int i = 0; i < count; i++)
                {
                    float glow = shard.cutTime[i] < 0 ? 0 : 1 - (clock - shard.cutTime[i]) / GlowTime;
                    if (glow <= 0) continue;
                    Vector3 a = vertices[start + i], b = vertices[start + (i + 1) % count];
                    Vector3 side = Vector3.Cross((b - a).normalized, Vector3.forward) * edgeWidth * .5f;
                    int first = vertices.Count;
                    vertices.Add(a - side); vertices.Add(a + side); vertices.Add(b + side); vertices.Add(b - side);
                    for (int k = 0; k < 4; k++)
                    {
                        uvs.Add(Vector2.zero); glows.Add(Vector2.right);
                        colors.Add(new Color(glowColor.r, glowColor.g, glowColor.b, glow * alpha));
                    }
                    triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                    triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
                }
                start += count;
            }
            Debug.Assert(start == pieceVertices);
            mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetUVs(1, glows); mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(halfWidth, halfHeight, 1) * 8);
        }
    }
}
