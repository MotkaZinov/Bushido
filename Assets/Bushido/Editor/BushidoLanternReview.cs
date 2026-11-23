using System;
using System.Collections.Generic;
using System.IO;
using Bushido;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Capturas de las poses de los dos faroles sobre un fondo liso (sin HUD). Corre en Play Mode porque la malla con huesos
// solo se actualiza entre fotogramas reales.
//   Lote: Unity -batchmode -quit -projectPath <proyecto> -executeMethod BushidoLanternReview.Run   (sin -quit si falla: usa Exit)
// Escribe QualityReview/farol-<pose>.png.
[InitializeOnLoad]
public static class BushidoLanternReview
{
    const string Key = "Bushido.LanternReview";
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../QualityReview"));
    static Camera cam; static PrototypeArt art; static LanternActor white, red;
    static float clock, next; static int step; static string pendingShot;
    static readonly List<(float at, Action act)> script = new List<(float, Action)>();

    static BushidoLanternReview() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }

    static string stripName;                          // set: capture the eye strip instead of the poses
    static readonly List<Texture2D> tilesWhite = new List<Texture2D>(), tilesRed = new List<Texture2D>();

    // Tira de estados del ojo (farol blanco y rojo): QualityReview/farol-ojo-blanco.png y farol-ojo-rojo.png (sufijo "-antes" con -executeMethod RunEyeBefore).
    public static void RunEye() { SessionState.SetString(Key + ".strip", ""); Run(); }
    public static void RunEyeBefore() { SessionState.SetString(Key + ".strip", "-antes"); Run(); }

    [MenuItem("Bushido/Revisión/Poses de los faroles")]
    public static void Run()
    {
        if (new System.Diagnostics.StackTrace().GetFrame(1)?.GetMethod().Name != "RunEye" && new System.Diagnostics.StackTrace().GetFrame(1)?.GetMethod().Name != "RunEyeBefore") SessionState.EraseString(Key + ".strip");
        SessionState.SetBool(Key, true); step = 0; clock = 0;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        if (EditorApplication.isPlaying) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void Build()
    {
        cam = new GameObject("Cámara").AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 5.4f; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.Lerp(PrototypeArt.Paper, PrototypeArt.Ink, 0.35f);
        cam.transform.position = new Vector3(0, 4, -15); cam.nearClipPlane = 0.1f; cam.farClipPlane = 100;
        art = new PrototypeArt();
        var root = new GameObject("Raíz").transform;
        white = new LanternActor(art, root, true, new Vector3(-5.4f, 8.2f, -2f));
        red = new LanternActor(art, root, false, new Vector3(5.4f, 8.2f, -2f));
        white.lookAt = red.lookAt = new Vector3(7f, 0f, -3f);
        float t = 0;
        stripName = SessionState.GetString(Key + ".strip", "0001"); if (stripName == "0001") stripName = null;
        if (stripName != null) { BuildEyeStrip(); return; }
        void At(float dt, Action a) { t += dt; script.Add((t, a)); }
        void Shot(string n) => At(0.1f, () => pendingShot = n);
        At(0.1f, () => white.glowTarget = 1); At(1.2f, () => red.glowTarget = 1); At(1.2f, () => { }); Shot("reposo-encendido");
        // Comparison: the unposed imported model (left of centre) beside the idle pose, same camera, same scale and facing.
        At(0.1f, () =>
        {
            var lightGo = new GameObject("Luz"); lightGo.AddComponent<Light>().type = LightType.Directional; lightGo.transform.rotation = Quaternion.Euler(35, 150, 0); lightGo.name = "LuzComparacion";
            var raw = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Models/Lamparas/lampara_blanca"));
            raw.name = "ModeloCrudo";
            raw.transform.position = white.model.position + new Vector3(3.2f, 0, 0);
            raw.transform.rotation = white.model.rotation;
            raw.transform.localScale = Vector3.one * LanternActor.BodyScale;
            foreach (var r in raw.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) ms[i] = art.ModelMaterial(ms[i]);
                r.sharedMaterials = ms;
            }
        });
        At(0.2f, () => { }); Shot("comparacion-modelo-crudo");
        At(0.1f, () => { UnityEngine.Object.Destroy(GameObject.Find("ModeloCrudo")); UnityEngine.Object.Destroy(GameObject.Find("LuzComparacion")); });
        At(0.1f, () => { red.glowTarget = 0; red.eyeTarget = 0; red.SetPose(LanternActor.Pose.Unlit, 1); }); At(1.5f, () => { }); Shot("apagado-y-encendido");
        At(0.1f, () => { white.pathAngle = -45; red.pathAngle = -135; red.glowTarget = 1; red.eyeTarget = 1; white.SetPose(LanternActor.Pose.WindUp, 0.7f); red.SetPose(LanternActor.Pose.WindUp, 0.7f); });
        At(0.68f, () => { }); Shot("telegrafo");
        At(0.1f, () => { white.pathAngle = -8; red.pathAngle = -90; white.SetPose(LanternActor.Pose.Lash, 0.45f); red.SetPose(LanternActor.Pose.Lash, 0.45f); });
        At(0.02f, () => pendingShot = "latigazo-inicio");
        At(0.07f, () => pendingShot = "latigazo-medio");
        At(0.25f, () => pendingShot = "latigazo");
        At(0.1f, () => { });
        At(0.1f, () => { white.pathAngle = -42; red.pathAngle = 222; white.SetPose(LanternActor.Pose.WindUp, 0.2f); red.SetPose(LanternActor.Pose.WindUp, 0.2f); });
        At(0.3f, () => { white.SetPose(LanternActor.Pose.Lash, 0.45f); red.SetPose(LanternActor.Pose.Lash, 0.45f); }); At(0.3f, () => { }); Shot("latigazo-diagonal");
        At(0.1f, () => { white.SetPose(LanternActor.Pose.Stunned, 2.2f); red.SetPose(LanternActor.Pose.Stunned, 2.2f); white.hurtKick = 0.4f; }); At(0.8f, () => { }); Shot("aturdido");
        At(0.1f, () => { white.spitAngle = -40; red.spitAngle = -140; white.SetPose(LanternActor.Pose.Spit, 0.35f); red.SetPose(LanternActor.Pose.Spit, 0.35f); }); At(0.16f, () => { }); Shot("escupe");
        At(0.1f, () => { white.SetPose(LanternActor.Pose.Calm, 1f); red.SetPose(LanternActor.Pose.Calm, 1f); white.eyeTarget = red.eyeTarget = 0; }); At(1.3f, () => { }); Shot("purificado");
        At(0.2f, () => { Directory.CreateDirectory(Folder); SessionState.SetBool(Key, false); EditorApplication.update -= Tick; if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode(); });
    }

    static void Eye(string name, Action set, float wait = 0.5f)
    {
        float last = script.Count > 0 ? script[script.Count - 1].at : 0f;
        script.Add((last + 0.05f, set));
        script.Add((last + 0.05f + wait, () => pendingShot = "tile"));
    }

    static void BuildEyeStrip()
    {
        white.glowTarget = red.glowTarget = 1f;
        void Both(Action<LanternActor> a) { a(white); a(red); }
        script.Add((1.5f, () => { }));
        Eye("reposo", () => { });
        Eye("mira-derecha", () => Both(l => l.lookAt = new Vector3(14, 4, -3)));
        Eye("mira-izquierda", () => Both(l => l.lookAt = new Vector3(-14, 4, -3)));
        Eye("mira-arriba", () => Both(l => l.lookAt = new Vector3(0, 16, -3)));
        Eye("mira-abajo", () => Both(l => l.lookAt = new Vector3(0, -8, -3)));
        Eye("parpado", () => Both(l => { l.lookAt = new Vector3(7, 0, -3); l.eyeTarget = 0; }));
        Eye("golpe", () => Both(l => { l.eyeTarget = 1; l.hurtKick = 0.4f; }), 0.1f);
        Eye("aturdido", () => Both(l => l.SetPose(LanternActor.Pose.Stunned, 3f)), 0.8f);
        Eye("aviso", () => { white.pathAngle = -8; red.pathAngle = 188; Both(l => l.SetPose(LanternActor.Pose.WindUp, 0.7f)); }, 0.68f);
        Eye("latigazo", () => Both(l => l.SetPose(LanternActor.Pose.Lash, 0.45f)), 0.3f);
        Eye("escupe", () => { white.spitAngle = -40; red.spitAngle = -140; Both(l => l.SetPose(LanternActor.Pose.Spit, 0.35f)); }, 0.16f);
        Eye("calma", () => Both(l => { l.SetPose(LanternActor.Pose.Calm, 1f); l.eyeTarget = 0; }), 1f);
        Eye("apagado", () => Both(l => { l.SetPose(LanternActor.Pose.Unlit, 1f); l.glowTarget = 0; }), 1f);
        script.Add((script[script.Count - 1].at + 0.3f, () =>
        {
            Save(tilesWhite, "farol-ojo-blanco" + stripName + ".png"); Save(tilesRed, "farol-ojo-rojo" + stripName + ".png");
            SessionState.SetBool(Key, false); SessionState.EraseString(Key + ".strip"); EditorApplication.update -= Tick;
            if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
        }));
    }

    static void Save(List<Texture2D> tiles, string file)
    {
        const int w = 330, cols = 4;
        int rows = (tiles.Count + cols - 1) / cols;
        var sheet = new Texture2D(w * cols, w * rows, TextureFormat.RGB24, false);
        for (int i = 0; i < tiles.Count; i++) sheet.SetPixels((i % cols) * w, (rows - 1 - i / cols) * w, w, w, tiles[i].GetPixels());
        File.WriteAllBytes(Path.Combine(Folder, file), sheet.EncodeToPNG());
        tiles.Clear();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || ++step < 30) return;
        try
        {
            if (cam == null) { Debug.Log("LanternReview: build, strip=" + (SessionState.GetString(Key + ".strip", "none"))); Build(); next = 0; }
            if (pendingShot == "tile") { pendingShot = null; Tile(); }
            else if (pendingShot != null) { Render(Path.Combine(Folder, "farol-" + pendingShot + ".png")); pendingShot = null; }
            const float dt = 1f / 30f;
            clock += dt; white.Update(dt, clock); red.Update(dt, clock);
            while (script.Count > 0 && script[0].at <= clock) { var a = script[0].act; script.RemoveAt(0); a(); }
        }
        catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.update -= Tick; if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    static void Tile()
    {
        var target = new RenderTexture(1280, 720, 24);
        cam.targetTexture = target; cam.Render(); RenderTexture.active = target;
        foreach (var (list, cx) in new[] { (tilesWhite, 280), (tilesRed, 1000) })
        {
            var tile = new Texture2D(330, 330, TextureFormat.RGB24, false);
            tile.ReadPixels(new Rect(cx - 165, 720 - 230 - 165, 330, 330), 0, 0); tile.Apply();
            list.Add(tile);
        }
        RenderTexture.active = null; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(target);
    }

    static void Render(string path)
    {
        Directory.CreateDirectory(Folder);
        var target = new RenderTexture(1280, 720, 24);
        cam.targetTexture = target; cam.Render(); RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
    }
}
