using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Revisión de los modelos de lámpara: vuelca jerarquía, huesos y clips, y guarda vistas frontal y lateral.
// Lote: -batchmode -executeMethod BushidoLampPreview.Run -quit
public static class BushidoLampPreview
{
    const string Dir = "Assets/Bushido/Resources/Models/Lamparas/";
    static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../../QualityReview"));

    [MenuItem("Bushido/Revisión/Modelos de lámpara")]
    public static void Run()
    {
        var log = new StringBuilder();
        foreach (string name in new[] { "lampara_blanca", "lampara_roja" })
        {
            string path = Dir + name + ".fbx";
            // Las texturas incrustadas en el FBX no se usan hasta extraerlas junto al modelo.
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && importer.ExtractTextures(Dir)) AssetDatabase.Refresh();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { log.AppendLine("FALTA " + path); continue; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var model = Object.Instantiate(prefab);
            log.AppendLine("== " + name);
            Dump(model.transform, 0, log);
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(r.bounds);
                log.Append("RENDER ").Append(r.name).Append(' ').Append(r.GetType().Name).Append(" tamaño ").Append(r.bounds.size);
                if (r is SkinnedMeshRenderer skin) log.Append(" huesos ").Append(skin.bones.Length).Append(" raíz ").Append(skin.rootBone ? skin.rootBone.name : "-");
                foreach (Material m in r.sharedMaterials)
                    log.Append(" | ").Append(m ? m.name + " [" + m.shader.name + "] tex=" + (m.mainTexture ? m.mainTexture.name : "ninguna") + " color=" + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "?") : "sin material");
                log.AppendLine();
            }
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) log.AppendLine("CLIP " + clip.name + " duración " + clip.length);
            log.AppendLine("LÍMITES centro " + bounds.center + " tamaño " + bounds.size);

            var light = new GameObject("Luz").AddComponent<Light>();
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(35, -30, 0);
            var camera = new GameObject("Cámara").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.90f, 0.82f, 0.65f);
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x, bounds.extents.z) * 1.15f;
            Shot(camera, bounds, Vector3.back, name + "-frente");
            Shot(camera, bounds, Vector3.right, name + "-lado");
        }
        Directory.CreateDirectory(Out);
        File.WriteAllText(Path.Combine(Out, "lamparas-modelo.txt"), log.ToString());
    }

    static void Dump(Transform t, int depth, StringBuilder log)
    {
        log.Append(' ', depth * 2).Append(t.name).Append("  pos ").Append(t.localPosition.ToString("F2")).AppendLine();
        foreach (Transform child in t) Dump(child, depth + 1, log);
    }

    static void Shot(Camera camera, Bounds bounds, Vector3 from, string file)
    {
        camera.transform.position = bounds.center + from * (bounds.size.magnitude * 2 + 1);
        camera.transform.LookAt(bounds.center);
        var target = new RenderTexture(640, 640, 24);
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Out, file + ".png"), image.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        Object.DestroyImmediate(target); Object.DestroyImmediate(image);
    }
}
