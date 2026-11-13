using System.Collections.Generic;
using UnityEngine;

namespace Bushido
{
    // Replace these generated miniatures with production prefabs when the models are ready.
    public sealed class PrototypeArt
    {
        public static readonly Color Ink = new Color(0.13f, 0.17f, 0.17f);
        public static readonly Color Red = new Color(0.66f, 0.17f, 0.12f);
        public static readonly Color Gold = new Color(0.82f, 0.62f, 0.28f);
        public static readonly Color Paper = new Color(0.9f, 0.82f, 0.65f);
        public static readonly Color Jade = new Color(0.26f, 0.47f, 0.36f);
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly Dictionary<Material, Material> modelMaterials = new Dictionary<Material, Material>();
        readonly List<Object> ownedAssets = new List<Object>();
        readonly Shader shader;
        readonly bool forest;
        Mesh paperLeafMesh;

        public void PaperSurface(Renderer renderer)
        {
            // Each paper colour uses the art instance's shared material cache.
            renderer.sharedMaterial.SetFloat("_PaperSurface", 1f);
            renderer.sharedMaterial.SetFloat("_InkShading", 0f);
        }

        public Transform PaperLeaf(Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            if (paperLeafMesh == null)
            {
                paperLeafMesh = new Mesh { name = "Hoja lanceolada de papel" };
                paperLeafMesh.vertices = new[] { new Vector3(-0.5f, 0, 0), new Vector3(-0.12f, 0.48f, 0), new Vector3(0.5f, 0, 0), new Vector3(-0.08f, -0.4f, 0) };
                paperLeafMesh.uv = new[] { new Vector2(0, 0.5f), new Vector2(0.38f, 1), new Vector2(1, 0.5f), new Vector2(0.42f, 0) };
                paperLeafMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                paperLeafMesh.RecalculateNormals(); paperLeafMesh.RecalculateBounds();
                ownedAssets.Add(paperLeafMesh);
            }
            var leaf = Shape(parent, "Hoja de bambú recortada", PrimitiveType.Quad, position, scale, color);
            leaf.GetComponent<MeshFilter>().sharedMesh = paperLeafMesh;
            PaperSurface(leaf.GetComponent<Renderer>());
            return leaf.transform;
        }

        public PrototypeArt(bool forest = false)
        {
            this.forest = forest;
            shader = Resources.Load<Shader>("BushidoInk");
            if (shader == null) throw new System.InvalidOperationException("Falta Resources/BushidoInk.shader");
        }

        public Material Material(Color color)
        {
            if (!materials.TryGetValue(color, out Material material))
            {
                material = new Material(shader);
                material.SetColor("_BaseColor", color);
                if (forest) material.SetFloat("_ForestLight", 1f);
                materials.Add(color, material);
                ownedAssets.Add(material);
            }
            return material;
        }

        public Material ModelMaterial(Material source)
        {
            if (source == null) return Material(Jade);
            if (modelMaterials.TryGetValue(source, out Material existing)) return existing;
            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                : source.HasProperty("baseColorFactor") ? source.GetColor("baseColorFactor")
                : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_CelShading", 1f);
            material.SetFloat("_CharacterInk", 1f);
            material.SetFloat("_InkContour", 0.96f);
            if (forest) material.SetFloat("_ForestLight", 1f);
            Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap")
                : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            string textureProperty = source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null ? "_BaseMap"
                : source.HasProperty("baseColorTexture") && source.GetTexture("baseColorTexture") != null ? "baseColorTexture"
                : source.HasProperty("_MainTex") ? "_MainTex" : null;
            if (textureProperty != null)
            {
                texture = source.GetTexture(textureProperty);
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", source.GetTextureScale(textureProperty));
                material.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureProperty));
            }
            ownedAssets.Add(material);
            modelMaterials.Add(source, material);
            return material;
        }

        public GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = Material(color);
            Object.Destroy(shape.GetComponent<Collider>());
            return shape;
        }

        public Transform Samurai(Transform parent, Vector3 position, Color armor, float scale = 1)
        {
            Transform root = new GameObject("Miniatura samurái").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localScale = Vector3.one * scale;
            Shape(root, "Peana", PrimitiveType.Cylinder, new Vector3(0, 0.1f, 0), new Vector3(1, 0.1f, 1), Ink);
            Shape(root, "Borde de bronce", PrimitiveType.Cylinder, new Vector3(0, 0.21f, 0), new Vector3(0.91f, 0.025f, 0.91f), Gold);
            Shape(root, "Pierna izquierda", PrimitiveType.Cube, new Vector3(-0.17f, 0.5f, 0), new Vector3(0.22f, 0.6f, 0.25f), Ink);
            Shape(root, "Pierna derecha", PrimitiveType.Cube, new Vector3(0.17f, 0.5f, 0), new Vector3(0.22f, 0.6f, 0.25f), Ink);
            Shape(root, "Coraza", PrimitiveType.Cube, new Vector3(0, 1, 0), new Vector3(0.65f, 0.65f, 0.4f), armor);
            Shape(root, "Faja", PrimitiveType.Cube, new Vector3(0, 0.85f, -0.02f), new Vector3(0.68f, 0.1f, 0.44f), Gold);
            Shape(root, "Rostro", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0), Vector3.one * 0.43f, Paper);
            Shape(root, "Máscara", PrimitiveType.Cube, new Vector3(0, 1.48f, 0.19f), new Vector3(0.32f, 0.16f, 0.12f), Ink);
            Cone(root, new Vector3(0, 1.78f, 0), 0.49f, 0.27f, Ink);
            Shape(root, "Hombro izquierdo", PrimitiveType.Cube, new Vector3(-0.43f, 1.16f, 0), new Vector3(0.28f, 0.2f, 0.48f), armor);
            Shape(root, "Hombro derecho", PrimitiveType.Cube, new Vector3(0.43f, 1.16f, 0), new Vector3(0.28f, 0.2f, 0.48f), armor);
            GameObject sword = Shape(root, "Katana", PrimitiveType.Cube, new Vector3(0.57f, 1.13f, 0.2f), new Vector3(0.055f, 1.1f, 0.09f), Paper);
            sword.transform.localRotation = Quaternion.Euler(0, 0, -28);
            return root;
        }

        void Cone(Transform parent, Vector3 position, float radius, float height, Color color)
        {
            const int sides = 16;
            var vertices = new Vector3[sides * 3];
            var triangles = new int[vertices.Length];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                float b = (i + 1) * Mathf.PI * 2 / sides;
                vertices[i * 3] = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                vertices[i * 3 + 1] = Vector3.up * height;
                vertices[i * 3 + 2] = new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius);
                triangles[i * 3] = i * 3;
                triangles[i * 3 + 1] = i * 3 + 1;
                triangles[i * 3 + 2] = i * 3 + 2;
            }
            Mesh mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            ownedAssets.Add(mesh);
            var hat = new GameObject("Sombrero");
            hat.transform.SetParent(parent, false);
            hat.transform.localPosition = position;
            hat.AddComponent<MeshFilter>().sharedMesh = mesh;
            hat.AddComponent<MeshRenderer>().sharedMaterial = Material(color);
        }

        public LineRenderer Line(Transform parent, Vector3[] points, Color color, float width)
        {
            var line = new GameObject("Pincelada").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.sharedMaterial = Material(color);
            return line;
        }

        public static Vector3 PathPoint(Vector3 a, Vector3 b, float t)
        {
            Vector3 side = Vector3.Cross((b - a).normalized, Vector3.up);
            return Vector3.Lerp(a, b, t) + side * Mathf.Sin(t * Mathf.PI) * 0.4f;
        }

        Texture2D roadGrain;
        readonly Dictionary<Color, Material> roadMaterials = new Dictionary<Color, Material>();
        Material RoadMaterial(Color ink)
        {
            if (roadMaterials.TryGetValue(ink, out Material cached)) return cached;
            if (roadGrain == null)
            {
                const int size = 64;
                roadGrain = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Grano de tinta seca" };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Fine horizontal fibres; deterministic and shared by all road segments.
                    float fibre = Mathf.Sin(y * 2.7f + Mathf.Sin(x * .19f) * 1.8f) * .08f;
                    float grain = Mathf.PerlinNoise(x * .16f, y * .41f) * .16f;
                    byte c = (byte)(Mathf.Clamp01(.74f + fibre + grain) * 255);
                    pixels[y * size + x] = new Color32(c,c,c,255);
                }
                roadGrain.SetPixels32(pixels); roadGrain.Apply(true, true); ownedAssets.Add(roadGrain);
            }
            var material = new Material(shader);
            material.SetColor("_BaseColor", ink);
            material.SetTexture("_BaseMap", roadGrain);
            ownedAssets.Add(material); roadMaterials.Add(ink, material);
            return material;
        }
        public void Road(Transform parent, Vector3 a, Vector3 b, bool unlocked)
        {
            Color ink = unlocked ? new Color(.27f,.13f,.10f) : new Color(.43f,.39f,.32f);
            Vector3[] points = new Vector3[25];
            for (int i=0;i<points.Length;i++) points[i]=PathPoint(a,b,i/24f)+Vector3.up*.06f;
            var stroke=Line(parent,points,ink,.16f);
            stroke.sharedMaterial=RoadMaterial(ink);
            stroke.textureMode=LineTextureMode.Tile;
            stroke.widthCurve=new AnimationCurve(new Keyframe(0,.18f),new Keyframe(.12f,.85f),
                new Keyframe(.38f,.55f),new Keyframe(.61f,1f),new Keyframe(.83f,.48f),new Keyframe(1,.12f));
            Vector3 side=Vector3.Cross((b-a).normalized,Vector3.up);
            Vector3[] dry=new Vector3[points.Length];
            for(int i=0;i<dry.Length;i++) dry[i]=points[i]+side*(.055f+.015f*Mathf.Sin(i*.9f))+Vector3.up*.01f;
            var edge=Line(parent,dry,unlocked?new Color(.45f,.27f,.20f):new Color(.52f,.48f,.4f),.028f);
            edge.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.22f,1),new Keyframe(.7f,.6f),new Keyframe(1,0));
        }
        public Material SpiritSmokeMaterial()
        {
            var material=new Material(Resources.Load<Shader>("BushidoSpiritSmoke"));
            ownedAssets.Add(material);return material;
        }
        public Material KatanaMaterial()
        {
            var material=new Material(Resources.Load<Shader>("BushidoKatana"));
            material.SetTexture("_BaseMap",Resources.Load<Texture2D>("Models/Weapons/katana/katana material_Base_Color"));
            ownedAssets.Add(material);return material;
        }
        public Material KatanaAuraMaterial()
        {
            var material=new Material(Resources.Load<Shader>("BushidoKatanaAura"));
            ownedAssets.Add(material);return material;
        }
        public Material SpiritChainMaterial()
        {
            var material=new Material(Resources.Load<Shader>("BushidoSpiritChain"));
            material.SetTexture("_BaseMap",Resources.Load<Texture2D>("ThirdParty/SpiritChain/chain/chain"));
            ownedAssets.Add(material);return material;
        }
        public Material PaperWeaponMaterial(Texture2D texture)
        {
            var material=new Material(Material(Color.white));material.SetTexture("_BaseMap",texture);
            material.SetFloat("_InkShading",0);material.SetFloat("_PaperSurface",1);material.SetFloat("_PaperAlphaCutoff",.9f);
            ownedAssets.Add(material);return material;
        }
        public Transform Backdrop(Transform parent, string name, string textureName)
        {
            var quad = Shape(parent, name, PrimitiveType.Quad, Vector3.zero, Vector3.one, Color.white);
            var material = new Material(Material(Color.white));
            material.SetTexture("_BaseMap", Resources.Load<Texture2D>("Backgrounds/" + textureName));
            material.SetFloat("_InkShading", 0f);
            material.SetFloat("_PaperSurface", 1f);
            quad.GetComponent<Renderer>().sharedMaterial = material;
            ownedAssets.Add(material);
            return quad.transform;
        }
        public Transform TornPaper(Transform parent, bool left, Vector3 position)
        {
            const int rows = 12;
            var vertices = new Vector3[(rows + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[rows * 6];
            for (int row = 0; row <= rows; row++)
            {
                float y = -5.5f + row * 11f / rows;
                float seam = Mathf.Sin(row * 2.3f) * 0.22f;
                float a = left ? -12 : seam - 0.015f;
                float b = left ? seam + 0.015f : 12;
                vertices[row * 2] = new Vector3(a, y, 0);
                vertices[row * 2 + 1] = new Vector3(b, y, 0);
                uv[row * 2] = new Vector2((a + 12) / 24f, row / (float)rows);
                uv[row * 2 + 1] = new Vector2((b + 12) / 24f, row / (float)rows);
                if (row == rows) continue;
                int v = row * 2, t = row * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Papel rasgado", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            ownedAssets.Add(mesh);
            Transform panel = Backdrop(parent, "Panel de papel", "KamishibaiPradera");
            panel.GetComponent<MeshFilter>().sharedMesh = mesh;
            panel.localPosition = position;
            return panel;
        }

        public void Dispose()
        {
            foreach (Object asset in ownedAssets) Object.Destroy(asset);
            ownedAssets.Clear();
            materials.Clear();
            modelMaterials.Clear();
        }
    }
}






