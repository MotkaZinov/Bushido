using UnityEngine;
using System.Collections.Generic;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        readonly List<Vector3> bossSealDrawing = new List<Vector3>();
        sealed class SpiritChainStrip
        {
            public Transform root; public Mesh mesh; public Material material;
            public Vector3[] vertices = new Vector3[24 * 4];
            public float born;
            public readonly LineRenderer[] smoke = new LineRenderer[3];
        }
        readonly List<SpiritChainStrip> spiritChains = new List<SpiritChainStrip>();
        readonly List<Transform> spiritChainTargets = new List<Transform>();
        Vector3[] SpiritPattern(float scale = 1, int pattern = -1)
        {
            if (pattern < 0) pattern = bossSeals;
            if (pattern == 0) return SpiritSpiral(scale);
            Vector3[] points = pattern == 1 ? new[] {
                new Vector3(-.75f,.65f),new Vector3(-.25f,.59f),new Vector3(.3f,.6f),new Vector3(.8f,.65f),
                new Vector3(.05f,.15f),new Vector3(-.8f,-.4f),new Vector3(-.35f,-.6f),new Vector3(.2f,-.66f),new Vector3(.85f,-.66f)
            } : new[] {
                new Vector3(-.8f,-.75f),new Vector3(-.48f,-.6f),new Vector3(-.3f,-.3f),new Vector3(-.2f,.1f),
                new Vector3(-.22f,.42f),new Vector3(-.4f,.58f),new Vector3(-.58f,.45f),new Vector3(-.58f,.25f),
                new Vector3(-.35f,.12f),new Vector3(-.05f,.05f),new Vector3(.25f,.12f),new Vector3(.5f,.3f),
                new Vector3(.6f,.55f),new Vector3(.55f,.85f),new Vector3(.38f,.68f),new Vector3(.24f,.4f),
                new Vector3(.2f,.1f),new Vector3(.23f,-.25f),new Vector3(.4f,-.55f),new Vector3(.65f,-.78f),new Vector3(.85f,-.82f)
            };
            for (int i = 0; i < points.Length; i++) points[i] *= scale;
            return points;
        }
        Vector3[] SpiritSpiral(float scale = 1)
        {
            var p = new Vector3[49];
            for (int i = 0; i < p.Length; i++)
            { float t = i / 48f, angle = -t * Mathf.PI * 3.4f; float radius = .06f + t * .73f; p[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0) * scale; }
            return p;
        }
        Vector2[] NormalizeSeal(IList<Vector3> path)
        {
            if (path.Count < 2) return null;
            Vector2 min = path[0], max = path[0]; float length = 0;
            for (int i = 1; i < path.Count; i++) { min = Vector2.Min(min, path[i]); max = Vector2.Max(max, path[i]); length += Vector2.Distance(path[i - 1], path[i]); }
            float size = Mathf.Max(max.x - min.x, max.y - min.y);
            if (size < .25f || length < size * 1.5f) return null;
            var samples = new Vector2[48]; int segment = 1; float traveled = 0;
            for (int i = 0; i < 48; i++)
            {
                float distance = length * i / 47f;
                while (segment < path.Count - 1 && traveled + Vector2.Distance(path[segment - 1], path[segment]) < distance)
                { traveled += Vector2.Distance(path[segment - 1], path[segment]); segment++; }
                float segmentLength = Vector2.Distance(path[segment - 1], path[segment]);
                samples[i] = (Vector2.Lerp(path[segment - 1], path[segment], Mathf.Clamp01((distance - traveled) / Mathf.Max(segmentLength, .00001f))) - (min + max) * .5f) / size;
            }
            return samples;
        }
        bool MatchesSpiritSeal()
        {
            var drawing = NormalizeSeal(bossSealDrawing); var template = NormalizeSeal(SpiritPattern());
            if (drawing == null || template == null) return false;
            return GestureShapeError(drawing, template) < .25f;
        }
        void AddSpiritChains(Transform target)
        {
            for (int side = 0; side < 2; side++)
            {
                var go = new GameObject("Cadena espiritual de eslabones pintados"); go.transform.SetParent(world, false);
                var chain = new SpiritChainStrip { root = go.transform, mesh = new Mesh(), material = art.SpiritChainMaterial(), born = Time.time };
                var mistMaterial = art.SpiritSmokeMaterial();
                mistMaterial.SetColor("_Tint", new Color(.48f, .76f, .65f, .52f));
                for (int layer = 0; layer < chain.smoke.Length; layer++)
                {
                    var mist = art.Line(go.transform, new[] { Vector3.zero, Vector3.zero }, Color.white, .62f + layer * .16f);
                    mist.name = "Vaho espiritual de tinta " + layer;
                    mist.positionCount = 32; mist.sharedMaterial = mistMaterial;
                    mist.widthCurve = new AnimationCurve(new Keyframe(0, .1f), new Keyframe(.3f, .8f), new Keyframe(.65f, 1), new Keyframe(1, .1f));
                    chain.smoke[layer] = mist;
                }
                chain.mesh.name = "24 eslabones / una malla"; chain.mesh.MarkDynamic();
                var uv = new Vector2[96]; var tris = new int[144];
                for (int link = 0; link < 24; link++)
                {
                    // Two independent, interlocking link silhouettes in the original CC0 atlas.
                    Rect r = link % 2 == 0 ? new Rect(60f / 512, 1 - 324f / 1024, 136f / 512, 273f / 1024) : new Rect(103f / 512, 1 - 663f / 1024, 56f / 512, 270f / 1024);
                    int v = link * 4, t = link * 6;
                    uv[v] = new Vector2(r.xMin, r.yMin); uv[v + 1] = new Vector2(r.xMax, r.yMin);
                    uv[v + 2] = new Vector2(r.xMin, r.yMax); uv[v + 3] = new Vector2(r.xMax, r.yMax);
                    tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1; tris[t + 3] = v + 2; tris[t + 4] = v + 3; tris[t + 5] = v + 1;
                }
                chain.mesh.vertices = chain.vertices; chain.mesh.uv = uv; chain.mesh.triangles = tris;
                go.AddComponent<MeshFilter>().sharedMesh = chain.mesh; go.AddComponent<MeshRenderer>().sharedMaterial = chain.material;
                go.AddComponent<SliceMeshOwner>(); spiritChains.Add(chain); spiritChainTargets.Add(target);
            }
        }
        void UpdateSpiritChains()
        {
            for (int index = 0; index < spiritChains.Count; index++)
            {
                var chain = spiritChains[index]; var target = spiritChainTargets[index];
                if (chain.root == null || target == null) continue;
                chain.root.gameObject.SetActive(!bossInkBurst && target.gameObject.activeInHierarchy);
                float age = Time.time - chain.born, growth = Mathf.SmoothStep(0, 1, age / .42f);
                Vector3 start = view.ViewportToWorldPoint(new Vector3(index % 2 == 0 ? -.12f : 1.12f, .22f + (index / 2) * .19f, 15));
                Vector3 end = target.position;
                if (cursedMask != null)
                {
                    int pair = index / 2;
                    bool left = index % 2 == 0;
                    Vector2 origin = pair == 0 ? new Vector2(left ? -.04f : 1.04f, 1.04f)
                        : pair == 1 ? new Vector2(left ? -.04f : 1.04f, .78f)
                        : new Vector2(left ? .42f : .58f, 1.08f);
                    start = view.ViewportToWorldPoint(new Vector3(origin.x, origin.y, 13));
                    end = cursedMask.position + new Vector3(left ? -.17f : .17f, -.08f - pair * .1f, 0);
                }
                float chainDepth = cursedMask != null ? cursedMask.position.z - .35f : -.45f; end.z = chainDepth; start.z = chainDepth;
                end = Vector3.Lerp(start, end, growth);
                Vector3 direction = (end - start).normalized, side = Vector3.Cross(direction, Vector3.forward);
                float spacing = Vector3.Distance(start, end) / 24;
                float tension = Mathf.Exp(-Mathf.Max(0, age - .42f) * 5);
                for (int link = 0; link < 24; link++)
                {
                    float u = (link + .5f) / 24;
                    Vector3 p = Vector3.Lerp(start, end, u) + side * Mathf.Sin(u * Mathf.PI) * (Mathf.Sin(u * 15 - age * 24) * .22f * tension + .025f * Mathf.Sin(Time.time * 2 + u * 8));
                    float width = (link % 2 == 0 ? .12f : .05f) * growth;
                    Vector3 along = direction * spacing * .68f, across = side * width;
                    int v = link * 4; chain.vertices[v] = p - along - across; chain.vertices[v + 1] = p - along + across;
                    chain.vertices[v + 2] = p + along - across; chain.vertices[v + 3] = p + along + across;
                }
                for (int layer = 0; layer < chain.smoke.Length; layer++)
                    for (int k = 0; k < 32; k++)
                    {
                        float u = k / 31f;
                        Vector3 mist = Vector3.Lerp(start, end, u);
                        float phase = layer * 2.1f;
                        float wave = Mathf.Sin(u * 14 - Time.time * 2.4f + index + phase) * .19f + Mathf.Sin(u * 27 + Time.time * 1.3f + phase) * .065f;
                        mist += side * wave * Mathf.Sin(u * Mathf.PI) + Vector3.up * (.08f + layer * .12f + Mathf.Sin(u * 8 - Time.time + phase) * .09f);
                        mist.z -= .045f + layer * .015f; chain.smoke[layer].SetPosition(k, mist);
                        chain.smoke[layer].widthMultiplier = (.62f + layer * .16f) * growth;
                    }
                chain.mesh.vertices = chain.vertices; chain.mesh.RecalculateBounds();
                chain.material.SetFloat("_Pulse", Mathf.Clamp01(1 - Mathf.Abs(age - .42f) / .3f));
            }
        }
    }
}




