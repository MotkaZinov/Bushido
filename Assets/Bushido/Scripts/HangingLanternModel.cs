using System.Collections.Generic;
using UnityEngine;

namespace Bushido
{
    // Geometry is generated once per lantern. Animation only moves the hanging parts.
    public sealed class HangingLanternModel : MonoBehaviour
    {
        public const float Top = .49f;
        const int Sides = 32;
        public Material Paper { get; private set; }
        readonly List<Mesh> meshes = new List<Mesh>();
        Transform tassel;
        Color pigment;
        float phase;

        public static HangingLanternModel Create(PrototypeArt art, Transform parent, int index)
        {
            var root = new GameObject("Farol 3D de papel y bambú");
            root.transform.SetParent(parent, false);
            var lamp = root.AddComponent<HangingLanternModel>();
            lamp.Build(art, index);
            return lamp;
        }

        static float Radius(float y) => .23f + .19f * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(y / .43f * Mathf.PI * .5f)), .65f);

        void Build(PrototypeArt art, int index)
        {
            phase = index * 1.73f;
            // The two hidden bosses use the same disguise as the surrounding lamps.
            pigment = index % 3 == 1 ? new Color(.93f, .74f, .43f) : new Color(.83f, .34f, .17f);
            Paper = art.PaperWeaponMaterial(Texture2D.whiteTexture);
            Paper.SetFloat("_InkShading", .65f);
            var profile = new List<Vector2>();
            profile.Add(new Vector2(0, -.43f));
            for (int row = 0; row <= 64; row++)
            {
                float y = Mathf.Lerp(-.43f, .43f, row / 64f);
                float pleat = .006f * Mathf.Cos(row * Mathf.PI * .5f);
                profile.Add(new Vector2(Radius(y) + pleat, y));
            }
            profile.Add(new Vector2(0, .43f));
            Surface("Papel plegado", profile, Paper);

            var ribs = new List<Vector2>();
            // Disconnected closed rings share one mesh and material.
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= 12; i++)
            {
                float y = Mathf.Lerp(-.41f, .41f, i / 12f);
                float r = Radius(y) + .009f;
                ribs.Clear();
                for (int j = 0; j <= 6; j++)
                {
                    float angle = j * Mathf.PI / 3f;
                    ribs.Add(new Vector2(r + Mathf.Cos(angle) * .009f, y + Mathf.Sin(angle) * .009f));
                }
                AppendLathe(ribs, vertices, triangles);
            }
            MeshObject("Aros de bambú", vertices, triangles, art.Material(new Color(.3f, .19f, .1f)));
            for (int sign = -1; sign <= 1; sign += 2)
                art.Shape(transform, "Tapa de madera", PrimitiveType.Cylinder,
                    new Vector3(0, sign * .455f, 0), new Vector3(.48f, .035f, .48f), PrototypeArt.InkSepia);
            tassel = new GameObject("Borla articulada").transform;
            tassel.SetParent(transform, false);
            tassel.localPosition = new Vector3(0, -.49f, 0);
            for (int i = -1; i <= 1; i++)
                art.Shape(tassel, "Hilo de seda", PrimitiveType.Cylinder,
                    new Vector3(i * .025f, -.105f, 0), new Vector3(.022f, .105f, .022f), PrototypeArt.Red);
            art.Shape(tassel, "Atadura dorada", PrimitiveType.Cylinder,
                new Vector3(0, -.025f, 0), new Vector3(.09f, .018f, .09f), PrototypeArt.Gold);
            Animate(0f, 0f, 0f);
        }

        void Surface(string label, List<Vector2> profile, Material material)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AppendLathe(profile, vertices, triangles);
            MeshObject(label, vertices, triangles, material);
        }

        static void AppendLathe(List<Vector2> profile, List<Vector3> vertices, List<int> triangles)
        {
            int start = vertices.Count;
            for (int row = 0; row < profile.Count; row++)
                for (int side = 0; side <= Sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / Sides;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * profile[row].x, profile[row].y,
                        Mathf.Sin(angle) * profile[row].x));
                    if (row == 0 || side == Sides) continue;
                    int a = start + (row - 1) * (Sides + 1) + side, b = a + Sides + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
        }

        void MeshObject(string label, List<Vector3> vertices, List<int> triangles, Material material)
        {
            var mesh = new Mesh { name = label };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            meshes.Add(mesh);
            var part = new GameObject(label);
            part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        public void Animate(float clock, float light, float angularSpeed)
        {
            float flicker = .97f + .02f * Mathf.Sin(clock * 7f + phase) + .01f * Mathf.Sin(clock * 13f + phase);
            Paper.SetColor("_BaseColor", Color.Lerp(pigment * .42f, pigment * flicker, light));
            tassel.localRotation = Quaternion.Euler(Mathf.Sin(clock * 2.1f + phase) * 7f, 0,
                Mathf.Clamp(-angularSpeed * .3f, -24f, 24f) + Mathf.Sin(clock * 2.8f + phase) * 5f);
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            // Materials belong to PrototypeArt and are released with the battle.
        }
    }
}
