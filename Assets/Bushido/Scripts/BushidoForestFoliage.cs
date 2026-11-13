using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        static readonly Vector2[] MapleOutline = {
            new Vector2(0, -0.62f), new Vector2(-0.29f, -0.35f), new Vector2(-0.57f, -0.34f),
            new Vector2(-0.37f, -0.08f), new Vector2(-0.72f, 0.19f), new Vector2(-0.29f, 0.16f),
            new Vector2(-0.25f, 0.52f), new Vector2(-0.10f, 0.33f), new Vector2(0, 0.76f),
            new Vector2(0.10f, 0.33f), new Vector2(0.25f, 0.52f), new Vector2(0.29f, 0.16f),
            new Vector2(0.72f, 0.19f), new Vector2(0.37f, -0.08f), new Vector2(0.57f, -0.34f),
            new Vector2(0.29f, -0.35f)
        };
        static readonly Vector2[] CanopyLeafOutline = {
            new Vector2(0, -0.62f), new Vector2(-0.55f, -0.25f), new Vector2(-0.72f, 0.22f),
            new Vector2(-0.29f, 0.15f), new Vector2(0, 0.76f), new Vector2(0.29f, 0.15f),
            new Vector2(0.72f, 0.22f), new Vector2(0.55f, -0.25f)
        };
        static readonly Vector2[] DistantLeafOutline = {
            new Vector2(0, -0.62f), new Vector2(-0.72f, 0.12f),
            new Vector2(0, 0.76f), new Vector2(0.72f, 0.12f)
        };

        readonly List<Object> titleAssets = new List<Object>();

        void ClearTitleAssets()
        {
            if (view != null && view.TryGetComponent(out UniversalAdditionalCameraData cameraData))
                cameraData.renderPostProcessing = false;
            foreach (Object asset in titleAssets) Destroy(asset);
            titleAssets.Clear();
            forestSky = null;
        }

        void BuildForestSky(Vector3 sun, bool mobile)
        {
            Shader shader = Resources.Load<Shader>("BushidoForestSky");
            if (shader == null || !shader.isSupported) return;
            var material = new Material(shader);
            material.SetVector("_SunPosition", new Vector4(sun.x, sun.y, sun.z, 1f));
            material.SetInteger("_CloudSamples", mobile ? 5 : 12);
            titleAssets.Add(material);
            forestSky = ForestShape(world, "Cielo con nubes volumétricas", PrimitiveType.Quad,
                Vector3.zero, Vector3.one, Color.white);
            forestSky.GetComponent<Renderer>().sharedMaterial = material;
        }

        void BuildForestAtmosphere(Vector3 sun, bool mobile)
        {
            view.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var volume = world.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 5f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            titleAssets.Add(profile);
            volume.sharedProfile = profile;
            var bloom = profile.Add<Bloom>(false);
            bloom.threshold.Override(1.08f);
            bloom.intensity.Override(0.46f);
            bloom.scatter.Override(0.64f);
            if (mobile)
            {
                bloom.downscale.Override(BloomDownscaleMode.Quarter);
                bloom.maxIterations.Override(4);
            }
            titleAssets.Add(bloom);
            var vignette = profile.Add<Vignette>(false);
            vignette.color.Override(new Color(0.09f, 0.065f, 0.035f));
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.57f);
            titleAssets.Add(vignette);

            Shader shader = Resources.Load<Shader>("BushidoForestSunlight");
            if (shader == null) return;
            var shafts = new Material(shader);
            shafts.SetFloat("_Strength", 0.32f);
            titleAssets.Add(shafts);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uv = new List<Vector2>();
            for (int i = 0; i < 7; i += mobile ? 2 : 1)
            {
                float lane = i - 3f;
                Vector3 start = Vector3.Lerp(new Vector3(0f, 1.72f, -6.3f), sun, 0.56f)
                    + new Vector3(lane * 0.26f, Mathf.Sin(i * 1.8f) * 0.42f, -i * 0.55f);
                Vector3 end = new Vector3(-0.25f + lane * 0.75f, 0.6f + i * 0.16f, 3.5f + i * 0.38f);
                float width = i % 2 == 0 ? 0.47f : 0.28f;
                int first = vertices.Count;
                vertices.Add(start - Vector3.right * 0.13f);
                vertices.Add(start + Vector3.right * 0.13f);
                vertices.Add(end + Vector3.right * width);
                vertices.Add(end - Vector3.right * width);
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            }
            var mesh = MakeTitleMesh("Rayos entre las ramas", vertices, triangles);
            mesh.SetUVs(0, uv);
            TitleMesh(world, "Rayos entre las ramas", mesh, shafts, Vector3.zero, Vector3.one);
        }

        Mesh BuildMapleMesh(System.Random random, int leafCount, bool distant = false)
        {
            Vector2[] silhouette = distant ? DistantLeafOutline : leafCount == 1 ? MapleOutline : CanopyLeafOutline;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int leaf = 0; leaf < leafCount; leaf++)
            {
                Vector3 center = Vector3.zero;
                Quaternion angle = Quaternion.identity;
                float size = 1f;
                if (leafCount > 1)
                {
                    float azimuth = Sample(random, 0f, 2f * Mathf.PI);
                    float radius = Mathf.Sqrt(Sample(random, 0f, 1f));
                    center = new Vector3(Mathf.Cos(azimuth) * radius, Sample(random, -0.31f, 0.31f), Mathf.Sin(azimuth) * radius);
                    angle = Quaternion.Euler(Sample(random, 0, 360), Sample(random, 0, 360), Sample(random, 0, 360));
                    size = distant ? Sample(random, 0.23f, 0.45f) : Sample(random, 0.14f, 0.36f);
                }
                int start = vertices.Count;
                vertices.Add(center + angle * Vector3.forward * size * 0.065f);
                uv.Add(new Vector2(0.49f, 0.41f));
                for (int i = 0; i < silhouette.Length; i++)
                {
                    Vector2 outline = silhouette[i];
                    vertices.Add(center + angle * (Vector3)outline * size);
                    // Sample the scanned blade's interior, using the 3D mesh for its silhouette.
                    uv.Add(new Vector2(0.49f + outline.x * 0.35f, 0.41f - outline.y * 0.25f));
                    triangles.Add(start);
                    triangles.Add(start + 1 + i);
                    triangles.Add(start + 1 + (i + 1) % silhouette.Length);
                }
            }
            var mesh = MakeTitleMesh("Hojas de arce", vertices, triangles);
            mesh.SetUVs(0, uv);
            return mesh;
        }

        Material[] CreateAutumnMaterials()
        {
            Texture2D scan = Resources.Load<Texture2D>("ThirdParty/OpenGameArt/MapleLeaf");
            var materials = new Material[AutumnColors.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var leaf = new Material(art.Material(AutumnColors[i]));
                if (scan != null)
                {
                    leaf.SetTexture("_BaseMap", scan);
                    leaf.SetFloat("_LeafSurface", 1f);
                }
                titleAssets.Add(leaf);
                materials[i] = leaf;
            }
            return materials;
        }

        Mesh BuildSedgeMesh(System.Random random)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int blade = 0; blade < 16; blade++)
            {
                float angle = Sample(random, 0f, 2f * Mathf.PI);
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 across = Vector3.Cross(outward, Vector3.up);
                Vector3 foot = outward * Sample(random, 0.01f, 0.11f);
                float height = Sample(random, 0.11f, 0.41f);
                float width = Sample(random, 0.015f, 0.028f);
                Vector3 mid = foot + Vector3.up * height * 0.62f + outward * height * 0.24f;
                int start = vertices.Count;
                vertices.Add(foot - across * width);
                vertices.Add(foot + across * width);
                vertices.Add(mid - across * width * 0.62f);
                vertices.Add(mid + across * width * 0.62f);
                vertices.Add(foot + Vector3.up * height * 0.83f + outward * height * 0.74f);
                foreach (int index in new[] { 0, 2, 1, 1, 2, 3, 2, 4, 3 }) triangles.Add(start + index);
            }
            return MakeTitleMesh("Mata de hierba fina", vertices, triangles);
        }

        Mesh MakeTitleMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            titleAssets.Add(mesh);
            return mesh;
        }

        Transform TitleMesh(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            return item.transform;
        }

        void PlaceCombinedFoliage(string name, List<CombineInstance> instances, Material material)
        {
            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(instances.ToArray());
            titleAssets.Add(mesh);
            TitleMesh(world, name, mesh, material, Vector3.zero, Vector3.one);
        }
    }
}
