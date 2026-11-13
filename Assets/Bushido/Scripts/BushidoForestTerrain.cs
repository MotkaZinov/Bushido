using System.Collections.Generic;
using UnityEngine;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        const float PathStart = -3.1f;
        const float StepRun = 0.77f;
        const float StepRise = 0.18f;
        const float PathHalfWidth = 1.67f;
        static readonly Vector2[] LanternSites = {
            new Vector2(-2.29f, -1.6f), new Vector2(2.46f, 1.9f), new Vector2(-2.38f, 6.9f)
        };
        static readonly Color LanternGranite = new Color(0.51f, 0.50f, 0.47f);

        static float StairHeight(int row) => Mathf.Clamp(row - 4, 0, 21) * StepRise;
        static float StairHeightAt(float z) => StairHeight(Mathf.RoundToInt((z - PathStart) / StepRun));

        static float BankHeight(float x, float z)
        {
            float distance = Mathf.Max(0f, Mathf.Abs(x) - PathHalfWidth);
            float rise = Mathf.Clamp((z - PathStart) / StepRun - 4f, 0f, 21f) * StepRise;
            bool left = x < 0f;
            float shoulder = left ? 0.95f + 0.31f * Mathf.Sin(z * 0.48f) : 2.2f + 0.44f * Mathf.Sin(z * 0.37f);
            float slope = Mathf.SmoothStep(0f, 1f, (distance - shoulder) / (left ? 1.55f : 2.6f));
            float crest = left ? 2.45f + 0.45f * Mathf.Sin(z * 0.38f + 0.7f) : 1.85f + 0.7f * Mathf.Sin(z * 0.28f - 1.1f);
            float ridges = Mathf.PerlinNoise(x * 0.58f + 17f, z * 0.53f + 24f) - 0.5f;
            float edge = Mathf.SmoothStep(0f, 1f, distance / 1.8f);
            float outerRidge = Mathf.SmoothStep(0f, 1f, (distance - 3.6f) / 3.8f);
            return rise - 0.13f + slope * crest + edge * ridges * 0.85f
                + outerRidge * (1.05f + 0.55f * Mathf.Sin(z * 0.31f + (left ? 0.8f : 2.9f)));
        }

        Material TexturedForestMaterial(string textureName, Color tint)
        {
            var material = new Material(art.Material(tint));
            titleAssets.Add(material);
            Texture2D texture = Resources.Load<Texture2D>("ThirdParty/PolyHaven/" + textureName);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
                material.SetFloat("_StoneMapping", 1f);
            }
            return material;
        }

        void BuildMountainPath(System.Random random, bool mobile, List<Bounds> rockClearances)
        {
            var soil = TexturedForestMaterial("ForestFloor", new Color(0.64f, 0.54f, 0.40f));
            var bank = TexturedForestMaterial("ForestFloor", new Color(0.54f, 0.52f, 0.43f));
            Texture2D cliffTexture = Resources.Load<Texture2D>("ThirdParty/PolyHaven/CliffRock");
            if (cliffTexture != null)
            {
                bank.SetTexture("_CliffMap", cliffTexture);
                bank.SetFloat("_TerrainBlend", 1f);
            }
            var stone = TexturedForestMaterial("StonePathway", new Color(0.74f, 0.69f, 0.59f));
            var exposedRock = TexturedForestMaterial("CliffRock", new Color(0.67f, 0.61f, 0.50f));
            var mossyRock = TexturedForestMaterial("MossyRock", new Color(0.62f, 0.67f, 0.45f));
            var granite = TexturedForestMaterial("CliffRock", LanternGranite);
            granite.SetFloat("_StoneMapping", 0f);
            granite.SetTextureScale("_BaseMap", Vector2.one * 0.38f);
            for (int i = 0; i < LanternSites.Length; i++)
            {
                Vector2 site = LanternSites[i];
                AddStoneLantern(site.x, site.y, 1.07f - i * 0.10f, granite);
                rockClearances.Add(new Bounds(new Vector3(site.x, 0f, site.y), new Vector3(1.24f, 0f, 1.24f)));
            }
            ForestShape(world, "Suelo del bosque", PrimitiveType.Cube, new Vector3(0, -0.35f, 17),
                new Vector3(38, 0.55f, 58), ForestBark).GetComponent<Renderer>().sharedMaterial = soil;

            BuildBankStrip(-18f, -PathHalfWidth + 0.15f, bank, true);
            BuildBankStrip(PathHalfWidth - 0.15f, 18f, bank, true);
            BuildBankStrip(-PathHalfWidth, PathHalfWidth, soil, false);

            var slabs = new[] { BuildStoneSlab(random), BuildStoneSlab(random), BuildStoneSlab(random) };
            for (int row = 0; row < 26; row++)
            {
                float z = PathStart + row * StepRun;
                float edge = Sample(random, -0.09f, 0.12f);
                float left = -PathHalfWidth + edge;
                int stones = row % 3 == 1 ? 3 : 2;
                for (int i = 0; i < stones; i++)
                {
                    float right = Mathf.Lerp(-PathHalfWidth, PathHalfWidth, (i + 1f) / stones) + edge;
                    if (i < stones - 1) right += Sample(random, -0.27f, 0.27f);
                    float width = right - left;
                    var slab = TitleMesh(world, "Losa de la escalera", slabs[(row + i) % slabs.Length], stone,
                        new Vector3((left + right) * 0.5f, StairHeight(row) + Sample(random, -0.025f, 0.029f), z + Sample(random, -0.034f, 0.034f)),
                        new Vector3(width + 0.035f, Sample(random, 0.85f, 1.18f), Sample(random, 0.79f, 0.86f)));
                    slab.localRotation = Quaternion.Euler(Sample(random, -1.3f, 1.3f), Sample(random, -4.5f, 4.5f), Sample(random, -1.4f, 1.4f));
                    left = right;
                }
            }

            GameObject rockSet = Resources.Load<GameObject>("ThirdParty/PolyHaven/RockMossSet");
            MeshFilter[] boulders = rockSet != null ? rockSet.GetComponentsInChildren<MeshFilter>(true) : System.Array.Empty<MeshFilter>();
            // Uneven groups break the banks' silhouettes and leave openings for the lanterns.
            Vector3[] groups = {
                new Vector3(-3.75f, 2.55f, -3.65f), new Vector3(-4.55f, 1.25f, -2.5f),
                new Vector3(-3.85f, 2.22f, 0.05f), new Vector3(-4.55f, 1.42f, 1.65f),
                new Vector3(3.55f, 2.42f, -2.85f), new Vector3(2.72f, 1.03f, -1.95f),
                new Vector3(4.75f, 2.8f, 0.45f), new Vector3(3.35f, 1.22f, 0.75f),
                new Vector3(4.35f, 2.25f, 5.15f), new Vector3(-3.65f, 2.4f, 4.75f)
            };
            for (int i = 0; i < groups.Length; i++)
            {
                Vector3 group = groups[i];
                MeshFilter[] models = mobile && group.y < 2.2f ? null : boulders;
                AddBankRock(group.x, group.z, group.y, Sample(random, 0f, 360f), i % 3 == 0 ? exposedRock : mossyRock,
                    models, slabs, i, rockClearances);
            }

            for (int i = 0; i < (mobile ? 24 : 36); i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = Sample(random, -3.9f, 24f);
                float x = side * Sample(random, 2.8f, 9.5f);
                float size = Sample(random, 0.55f, 1.7f);
                Material surface = i % 4 == 0 || i % 4 == 3 ? exposedRock : mossyRock;
                AddBankRock(x, z, size, Sample(random, 0f, 360f), surface, mobile ? null : boulders, slabs, i, rockClearances);
            }
        }

        static Bounds PlacedForestBounds(Bounds mesh, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Vector3 extents = Vector3.Scale(mesh.extents, scale);
            Vector3 x = rotation * new Vector3(extents.x, 0f, 0f);
            Vector3 y = rotation * new Vector3(0f, extents.y, 0f);
            Vector3 z = rotation * new Vector3(0f, 0f, extents.z);
            return new Bounds(position + rotation * Vector3.Scale(mesh.center, scale), new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2f);
        }

        void AddBankRock(float x, float z, float size, float angle, Material surface,
            MeshFilter[] boulders, Mesh[] slabs, int variant, List<Bounds> clearances)
        {
            var yaw = Quaternion.Euler(0f, angle, 0f);
            MeshFilter source = boulders != null && boulders.Length > 0 ? boulders[variant % boulders.Length] : null;
            Mesh scan = source != null ? source.sharedMesh : null;
            Mesh mesh = scan != null ? scan : slabs[variant % slabs.Length];
            Quaternion orientation = scan != null ? source.transform.rotation : Quaternion.Euler(variant % 2 == 0 ? 34f : -26f, 0f, 16f);
            Quaternion rotation = yaw * orientation;
            Bounds oriented = PlacedForestBounds(mesh.bounds, Vector3.zero, orientation, Vector3.one);
            float uniformScale = size / Mathf.Max(0.0001f, Mathf.Max(oriented.size.x, oriented.size.z));
            Vector3 scale = scan != null ? Vector3.one * uniformScale : new Vector3(size, size * 1.4f, size * 0.8f);
            Bounds rotated = PlacedForestBounds(mesh.bounds, Vector3.zero, rotation, scale);
            float side = Mathf.Sign(x);

            // Move along the bank, then outward, until the rock clears the path, trunks and lanterns.
            for (int attempt = 0; attempt < 9; attempt++)
            {
                float candidateX = x + side * (attempt / 3) * 0.85f;
                float candidateZ = z + ((attempt % 3 == 1) ? 1.0f : (attempt % 3 == 2) ? -1.0f : 0f);
                if (Mathf.Abs(candidateX) - rotated.extents.x < PathHalfWidth + 0.28f) continue;
                var footprint = new Bounds(new Vector3(candidateX, 0f, candidateZ), rotated.size + new Vector3(0.18f, 0f, 0.18f));
                if (clearances.Exists(other => Mathf.Abs(other.center.x - candidateX) < other.extents.x + footprint.extents.x
                    && Mathf.Abs(other.center.z - candidateZ) < other.extents.z + footprint.extents.z)) continue;

                var center = new Vector3(candidateX, BankHeight(candidateX, candidateZ) + rotated.extents.y * 0.67f, candidateZ);
                TitleMesh(world, scan != null ? "Peñasco de la ladera" : "Roca de la ladera", mesh, surface,
                    center - rotated.center, scale).localRotation = rotation;
                clearances.Add(footprint);
                return;
            }
        }

        void BuildBankStrip(float left, float right, Material material, bool hillside)
        {
            int columns = hillside ? 52 : 4;
            const int rows = 84;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int row = 0; row <= rows; row++)
            for (int col = 0; col <= columns; col++)
            {
                float z = Mathf.Lerp(-6f, 40f, row / (float)rows);
                float x = Mathf.Lerp(left, right, col / (float)columns);
                float y = hillside ? BankHeight(x, z) : BankHeight(0, z) - 0.18f;
                vertices.Add(new Vector3(x, y, z));
                if (row == 0 || col == 0) continue;
                int next = row * (columns + 1) + col;
                int above = next - columns - 1;
                triangles.Add(above - 1); triangles.Add(next - 1); triangles.Add(next);
                triangles.Add(above - 1); triangles.Add(next); triangles.Add(above);
            }
            TitleMesh(world, "Suelo de montaña", MakeTitleMesh("Ladera continua", vertices, triangles), material, Vector3.zero, Vector3.one);
        }

        Mesh BuildStoneSlab(System.Random random)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector2[] outline = {
                new Vector2(-0.5f, -0.31f), new Vector2(-0.36f, -0.5f), new Vector2(0.36f, -0.5f), new Vector2(0.5f, -0.34f),
                new Vector2(0.5f, 0.33f), new Vector2(0.36f, 0.5f), new Vector2(-0.36f, 0.5f), new Vector2(-0.5f, 0.31f)
            };
            for (int i = 0; i < outline.Length; i++)
                outline[i] += new Vector2(Sample(random, -0.065f, 0.065f), Sample(random, -0.065f, 0.065f));
            for (int ring = 0; ring < 3; ring++)
            {
                float size = ring == 1 ? 1f : 0.88f;
                float height = ring == 0 ? -0.31f : ring == 1 ? -0.07f : 0;
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 point = outline[i] * size;
                    vertices.Add(new Vector3(point.x, height, point.y));
                    if (ring == 0) continue;
                    int current = ring * outline.Length + i;
                    int next = ring * outline.Length + (i + 1) % outline.Length;
                    triangles.Add(current - outline.Length); triangles.Add(current); triangles.Add(next - outline.Length);
                    triangles.Add(next - outline.Length); triangles.Add(current); triangles.Add(next);
                }
            }
            int top = outline.Length * 2;
            for (int i = 1; i < outline.Length - 1; i++)
            {
                triangles.Add(top); triangles.Add(top + i + 1); triangles.Add(top + i);
                triangles.Add(0); triangles.Add(i); triangles.Add(i + 1);
            }
            return MakeTitleMesh("Piedra con cantos gastados", vertices, triangles);
        }

        void AddStoneLantern(float x, float z, float scale, Material stone)
        {
            var lantern = new GameObject("Farol de piedra").transform;
            lantern.SetParent(world, false);
            lantern.localPosition = new Vector3(x, BankHeight(x, z) - 0.025f, z);
            lantern.localScale = Vector3.one * scale;
            lantern.localRotation = Quaternion.Euler(0, x < 0 ? 11f : -22f, 0);
            ForestShape(lantern, "Base del farol", PrimitiveType.Cube, new Vector3(0, 0.085f, 0), new Vector3(0.66f, 0.17f, 0.65f), LanternGranite, stone);
            ForestShape(lantern, "Soporte", PrimitiveType.Cylinder, new Vector3(0, 0.39f, 0), new Vector3(0.17f, 0.27f, 0.17f), LanternGranite, stone);
            ForestShape(lantern, "Plataforma", PrimitiveType.Cube, new Vector3(0, 0.67f, 0), new Vector3(0.49f, 0.12f, 0.49f), LanternGranite, stone);
            Color amber = new Color(1.55f, 0.78f, 0.24f);
            ForestShape(lantern, "Luz de papel", PrimitiveType.Cube, new Vector3(0, 0.84f, 0), new Vector3(0.29f, 0.22f, 0.29f), amber);
            art.Material(amber).SetFloat("_ForestLight", 0f);
            art.Material(amber).SetFloat("_InkShading", 0f);
            for (int corner = 0; corner < 4; corner++)
            {
                float px = corner < 2 ? -0.18f : 0.18f;
                float pz = corner % 2 == 0 ? -0.18f : 0.18f;
                ForestShape(lantern, "Marco del farol", PrimitiveType.Cube, new Vector3(px, 0.84f, pz), new Vector3(0.075f, 0.3f, 0.075f), LanternGranite, stone);
            }
            ForestShape(lantern, "Alero", PrimitiveType.Cube, new Vector3(0, 1.01f, 0), new Vector3(0.68f, 0.11f, 0.64f), LanternGranite, stone);
            ForestShape(lantern, "Techo", PrimitiveType.Cube, new Vector3(0, 1.10f, 0), new Vector3(0.50f, 0.10f, 0.47f), LanternGranite, stone);
            ForestShape(lantern, "Remate", PrimitiveType.Sphere, new Vector3(0, 1.19f, 0), new Vector3(0.16f, 0.18f, 0.16f), LanternGranite, stone);
        }
    }
}
