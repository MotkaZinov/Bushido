using System.Collections.Generic;
using UnityEngine;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        readonly List<Transform> swayingCanopies = new List<Transform>();
        readonly List<Transform> windLeaves = new List<Transform>();
        readonly List<Vector3> windLeafOrigins = new List<Vector3>();
        Transform forestSky;
        static readonly Color ForestBark = new Color(0.24f, 0.19f, 0.15f);
        static readonly Color Vermilion = new Color(0.64f, 0.17f, 0.11f);
        static readonly Color[] AutumnColors = {
            new Color(0.65f, 0.31f, 0.10f), new Color(0.84f, 0.50f, 0.16f),
            new Color(0.49f, 0.34f, 0.14f), new Color(0.33f, 0.33f, 0.17f)
        };
        // X and Z place the trunk; Y is the crown height in the camera's composition.
        static readonly Vector3[] ForestFrameTrees = {
            new Vector3(-3.27f, 4.05f, -1.95f), new Vector3(3.25f, 4.35f, -0.75f),
            new Vector3(-3.85f, 5.25f, 2.4f), new Vector3(4.25f, 5.65f, 3.7f),
            new Vector3(-4.5f, 6.15f, 6.8f), new Vector3(4.55f, 6.1f, 7.9f),
            new Vector3(-4.1f, 7.6f, 11.6f), new Vector3(4.4f, 7.9f, 12.7f)
        };

        void ShowTitleForest()
        {
            NewWorld(true);
            mode = Mode.Title;
            view.orthographic = false;
            view.fieldOfView = 43f;
            view.backgroundColor = new Color(0.61f, 0.48f, 0.28f);
            var random = new System.Random(57);
            bool mobile = Application.isMobilePlatform;

            Vector3 sun = new Vector3(-0.9f, 12.8f, 48f);
            Shader.SetGlobalVector("_BushidoForestSun", new Vector4(sun.x, sun.y, sun.z, 1f));
            BuildForestSky(sun, mobile);
            BuildForestAtmosphere(sun, mobile);

            var bark = TexturedForestMaterial("JapaneseCedarBark", new Color(0.52f, 0.43f, 0.33f));
            var darkWood = TexturedForestMaterial("LacqueredCherryWood", new Color(0.26f, 0.23f, 0.18f));
            for (int i = 0; i < 4; i++)
            {
                float z = 0.8f + i * 3.9f;
                BuildTorii(z, StairHeightAt(z), darkWood);
            }

            var crownMeshes = new[] { BuildMapleMesh(random, 208), BuildMapleMesh(random, 176) };
            Mesh distantCrown = BuildMapleMesh(random, 124, true);
            Mesh mapleLeaf = BuildMapleMesh(random, 1);
            Material[] autumn = CreateAutumnMaterials();
            Mesh sedge = BuildSedgeMesh(random);
            var moss = art.Material(new Color(0.31f, 0.34f, 0.19f));
            var drySedge = art.Material(new Color(0.40f, 0.34f, 0.20f));
            var sedgeBatches = new[] { new List<CombineInstance>(), new List<CombineInstance>() };
            for (int i = 0; i < (mobile ? 102 : 140); i++)
            {
                bool near = i < 80;
                float z = Sample(random, -4.2f, near ? 5.5f : 17f);
                float x = Sample(random, 1.9f, near ? 4.7f : 6f) * (i % 2 == 0 ? -1f : 1f);
                float y = Mathf.Max(-0.075f, BankHeight(x, z)) - 0.015f;
                float scale = near ? Sample(random, 1.15f, 2.05f) : Sample(random, 0.65f, 1.55f);
                sedgeBatches[i % 4 == 0 ? 1 : 0].Add(new CombineInstance {
                    mesh = sedge,
                    transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, Sample(random, 0, 360), 0), Vector3.one * scale)
                });
            }
            PlaceCombinedFoliage("Hierba entre las piedras", sedgeBatches[0], moss);
            PlaceCombinedFoliage("Hierba seca entre las piedras", sedgeBatches[1], drySedge);

            var trunkFootprints = new List<Bounds>();
            var distantTrunks = new List<CombineInstance>();
            var distantCanopies = new[] { new List<CombineInstance>(), new List<CombineInstance>() };
            Mesh trunkMesh = null;
            int treeCount = mobile ? 42 : 50;
            for (int i = 0; i < treeCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                bool framed = i < ForestFrameTrees.Length;
                bool outer = !framed && i % 4 >= 2;
                float z = framed ? ForestFrameTrees[i].z : Sample(random, outer ? -1.0f : 4.5f, outer ? 27f : 34f);
                float x = framed ? ForestFrameTrees[i].x : side * (outer ? Sample(random, 7.2f, 13f) : Sample(random, 4.65f, 7.8f));
                float ground = BankHeight(x, z) - 0.13f;
                float height = framed ? ForestFrameTrees[i].y - ground : Sample(random, 4.8f, 8.9f);
                float trunkWidth = i < 2 ? Sample(random, 0.66f, 0.92f) : Sample(random, 0.34f, 0.70f);
                Vector3 trunkCenter = new Vector3(x, ground + height * 0.5f, z);
                Vector3 trunkScale = new Vector3(trunkWidth, height * 0.5f, trunkWidth);
                Quaternion trunkTilt = Quaternion.Euler(0, 0, Sample(random, -5f, 5f));
                if (framed)
                {
                    var trunk = ForestShape(world, "Tronco", PrimitiveType.Cylinder, trunkCenter, trunkScale, ForestBark, bark);
                    trunk.localRotation = trunkTilt;
                    trunkMesh = trunk.GetComponent<MeshFilter>().sharedMesh;
                }
                else
                {
                    distantTrunks.Add(new CombineInstance {
                        mesh = trunkMesh, transform = Matrix4x4.TRS(trunkCenter, trunkTilt, trunkScale)
                    });
                }
                trunkFootprints.Add(PlacedForestBounds(trunkMesh.bounds, trunkCenter, trunkTilt, trunkScale));

                Vector3 treetop = new Vector3(x, ground + height, z);
                if (!framed)
                {
                    distantCanopies[outer ? 1 : 0].Add(new CombineInstance {
                        mesh = distantCrown,
                        transform = Matrix4x4.TRS(treetop, Quaternion.Euler(0, Sample(random, 0, 360), 0),
                            new Vector3(outer ? 2.8f : 2.15f, 1.65f, 2.25f))
                    });
                    continue;
                }

                var canopy = new GameObject("Copa de arce").transform;
                canopy.SetParent(world, false);
                canopy.localPosition = treetop;
                swayingCanopies.Add(canopy);
                for (int crown = 0; crown < crownMeshes.Length; crown++)
                {
                    Vector3 offset = new Vector3(side * (crown == 0 ? -0.33f : 0.82f),
                        crown == 0 ? -0.08f : 0.42f, Sample(random, -0.55f, 0.8f));
                    Vector3 branchFoot = Vector3.down * 1.05f;
                    var branch = ForestShape(canopy, "Rama", PrimitiveType.Cylinder, (offset + branchFoot) * 0.5f,
                        new Vector3(0.13f, 0.5f * (offset - branchFoot).magnitude, 0.13f), ForestBark, bark);
                    branch.localRotation = Quaternion.FromToRotation(Vector3.up, offset - branchFoot);
                    var foliage = TitleMesh(canopy, "Copa de hojas de arce", crownMeshes[crown], autumn[(i + crown) % autumn.Length],
                        offset, new Vector3(Sample(random, 1.45f, 1.88f), Sample(random, 1.28f, 1.75f), Sample(random, 1.45f, 1.9f)));
                    foliage.localRotation = Quaternion.Euler(Sample(random, -15, 15), Sample(random, 0, 360), Sample(random, -18, 18));
                }

            }
            PlaceCombinedFoliage("Troncos entre las laderas", distantTrunks, bark);
            PlaceCombinedFoliage("Dosel interior", distantCanopies[0], autumn[2]);
            PlaceCombinedFoliage("Dosel exterior", distantCanopies[1], autumn[3]);
            BuildMountainPath(new System.Random(57), mobile, trunkFootprints);

            int fallingLeaves = mobile ? 36 : 64;
            for (int i = 0; i < fallingLeaves; i++)
            {
                float size = Sample(random, 0.08f, 0.20f);
                windLeafOrigins.Add(new Vector3(Sample(random, -6f, 6f), Sample(random, 1f, 7f), Sample(random, -2f, 14f)));
                windLeaves.Add(TitleMesh(world, "Hoja al viento", mapleLeaf, autumn[i % 2], windLeafOrigins[i], Vector3.one * size));
            }

            var pathLeafBatches = new[] { new List<CombineInstance>(), new List<CombineInstance>() };
            for (int i = 0; i < 130; i++)
            {
                float z = Sample(random, -3.9f, 11f);
                float x = (i % 4 == 0 ? Sample(random, 0f, 1.6f) : Sample(random, 0.97f, 1.62f)) * (i % 3 == 0 ? -1f : 1f);
                float size = Sample(random, 0.045f, 0.15f);
                pathLeafBatches[i % 2].Add(new CombineInstance {
                    mesh = mapleLeaf,
                    transform = Matrix4x4.TRS(new Vector3(x, StairHeightAt(z) + 0.036f, z),
                        Quaternion.Euler(90, Sample(random, 0, 360), 0), Vector3.one * size)
                });
            }
            PlaceCombinedFoliage("Hojas ocres del sendero", pathLeafBatches[0], autumn[0]);
            PlaceCombinedFoliage("Hojas doradas del sendero", pathLeafBatches[1], autumn[1]);

            BuildTitlePilgrim();
            UpdateTitleForest();
        }

        void BuildTorii(float z, float ground, Material darkWood)
        {
            foreach (int side in new[] { -1, 1 })
            {
                var pillar = ForestShape(world, "Pilar del torii", PrimitiveType.Cylinder,
                    new Vector3(side * 1.7f, ground + 1.85f, z), new Vector3(0.37f, 1.88f, 0.37f), Vermilion);
                pillar.localRotation = Quaternion.Euler(0, 0, side * 5f);
                ForestShape(world, "Base del torii", PrimitiveType.Cylinder, new Vector3(side * 1.84f, ground + 0.26f, z),
                    new Vector3(0.52f, 0.27f, 0.52f), PrototypeArt.Ink);
            }
            ForestShape(world, "Travesaño del torii", PrimitiveType.Cube, new Vector3(0, ground + 3.14f, z), new Vector3(4.2f, 0.22f, 0.36f), Vermilion);
            ForestShape(world, "Dintel rojo", PrimitiveType.Cube, new Vector3(0, ground + 3.76f, z), new Vector3(4.64f, 0.25f, 0.61f), Vermilion);
            ForestShape(world, "Remate oscuro", PrimitiveType.Cube, new Vector3(0, ground + 3.96f, z), new Vector3(4.88f, 0.19f, 0.78f), PrototypeArt.Ink, darkWood);
            ForestShape(world, "Placa central", PrimitiveType.Cube, new Vector3(0, ground + 3.44f, z), new Vector3(0.24f, 0.5f, 0.23f), ForestBark, darkWood);
        }

        Transform ForestShape(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Color color, Material surface = null)
        {
            GameObject item = art.Shape(parent, name, shape, position, scale, color);
            if (surface != null) item.GetComponent<Renderer>().sharedMaterial = surface;
            return item.transform;
        }

        static float Sample(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        void UpdateTitleForest()
        {
            float time = Time.unscaledTime;
            float gust = Mathf.Sin(time * 0.65f);
            UpdateTitlePilgrim(time, gust);
            view.transform.position = new Vector3(Mathf.Sin(time * 0.24f) * 0.34f, 1.72f, -6.3f + Mathf.Sin(time * 0.19f) * 0.22f);
            view.transform.LookAt(new Vector3(0, 3.2f, 12f));
            if (forestSky != null)
            {
                const float depth = 82f;
                float height = depth * 2f * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
                forestSky.SetPositionAndRotation(view.transform.position + view.transform.forward * depth, view.transform.rotation);
                forestSky.localScale = new Vector3(height * view.aspect, height, 1f);
            }
            for (int i = 0; i < swayingCanopies.Count; i++)
            {
                float bend = gust * 2.1f + Mathf.Sin(time * 1.6f + i * 0.7f) * 1.25f;
                swayingCanopies[i].localRotation = Quaternion.Euler(bend * 0.4f, 0, bend);
            }
            for (int i = 0; i < windLeaves.Count; i++)
            {
                Vector3 origin = windLeafOrigins[i];
                float x = Mathf.Repeat(origin.x + time * (0.85f + (i % 5) * 0.18f) + gust * 0.55f + 6f, 12f) - 6f;
                float y = Mathf.Repeat(origin.y - time * (0.54f + (i % 4) * 0.10f), 7.5f);
                windLeaves[i].localPosition = new Vector3(x, y, origin.z + Mathf.Sin(time * 1.4f + i) * 0.48f);
                windLeaves[i].localRotation = Quaternion.Euler(time * 72f + i * 14f, time * 44f, time * 96f + i * 21f);
            }
        }
    }
}
