using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using Bushido;
using UnityEditor;
using UnityEngine;

// Capturas para revisar a ojo las animaciones del samurái y de la sombrilla, los diálogos y el tablero de misiones.
//   Menú: Bushido/Revisión/Animaciones y diálogos
//   Sin -batchmode (OnGUI no se dibuja en lote): Unity -projectPath <proyecto> -executeMethod BushidoAnimReview.Run
// Escribe anim-*.png y anim-review.txt en QualityReview/. No guarda la partida.
[InitializeOnLoad]
public static class BushidoAnimReview
{
    const string Key = "Bushido.AnimReview";
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../QualityReview"));
    static int ticks;
    static readonly StringBuilder report = new StringBuilder();

    static BushidoAnimReview() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static object Mode(string name) => System.Enum.Parse(typeof(BushidoPrototype).GetNestedType("Mode", BindingFlags.NonPublic), name);

    [MenuItem("Bushido/Revisión/Animaciones y diálogos")]
    public static void Run()
    {
        SessionState.SetBool(Key, true); ticks = 0; report.Clear();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        if (EditorApplication.isPlaying) return;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CampaignMap.unity");
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || ++ticks < 90) return;
        var game = Object.FindAnyObjectByType<BushidoPrototype>(); if (game == null) return;
        EditorApplication.update -= Tick;
        SessionState.SetBool(Key, false);
        game.StartCoroutine(Sequence(game));
    }

    static IEnumerator Shot(string name, float wait)
    {
        yield return new WaitForSecondsRealtime(wait);
        yield return new WaitForEndOfFrame();
        Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG());
        Object.DestroyImmediate(image);
    }

    static void Battle(BushidoPrototype game, int mission)
    {
        Set(game, "mission", mission); Call(game, "BeginBattle"); Set(game, "paused", false);
    }

    static void Dump(string title, Transform root)
    {
        report.AppendLine("== " + title);
        if (root == null) { report.AppendLine("(nulo)"); return; }
        foreach (var part in root.GetComponentsInChildren<Transform>(true))
        {
            string kind = part.GetComponent<SkinnedMeshRenderer>() != null ? " [skin]" : part.GetComponent<MeshRenderer>() != null ? " [mesh]" : "";
            var skinned = part.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null) kind += " huesos=" + string.Join(",", System.Array.ConvertAll(skinned.bones, x => x ? x.name : "-"));
            report.AppendLine(new string(' ', Depth(part, root) * 2) + part.name + kind);
        }
    }

    static int Depth(Transform t, Transform root) { int d = 0; while (t != root && t != null) { t = t.parent; d++; } return d; }

    static IEnumerator Sequence(BushidoPrototype game)
    {
        Directory.CreateDirectory(Folder);
        Time.timeScale = 1;
        Dump("Chancla (prefab)", Resources.Load<GameObject>("Models/chancla").transform);
        // Tablero: cada misión seleccionada y el cara a cara con su diálogo.
        Call(game, "ShowMap", false);
        Set(game, "shojiTime", -1f);
        var progress = Get(game, "progress");
        progress.GetType().GetField("unlockedThrough").SetValue(progress, 4);
        Call(game, "SelectTechniques", true);
        yield return Shot("anim-tecnicas", .4f);
        Call(game, "SelectTechniques", false);
        for (int i = 1; i <= 4; i++)
        {
            Set(game, "selected", i);
            yield return Shot("anim-tablero-" + i, .5f);
        }
        foreach (int boss in new[] { 3, 4 })
        {
            Set(game, "mission", boss); Set(game, "mode", Mode("FaceOff")); Set(game, "dialogueLine", 0);
            yield return Shot("anim-caracara-" + boss + "a", .6f);
            Set(game, "dialogueLine", 1);
            yield return Shot("anim-caracara-" + boss + "b", .4f);
        }
        // Samurái en combate: reposo y tajo, en tira de fotogramas.
        Battle(game, 1);
        yield return new WaitForSecondsRealtime(1f);
        Dump("Samurai en combate", (Transform)Get(game, "fighter"));
        Call(game, "SpawnWave");
        yield return new WaitForSecondsRealtime(.9f);
        var flying = (IList)Get(game, "projectiles");
        if (flying.Count > 0) Dump("Chancla", (Transform)flying[0].GetType().GetField("root").GetValue(flying[0]));
        for (int f = 0; f < 4; f++) yield return Shot("anim-samurai-reposo-" + f, .25f);
        // Cortes reales: kesa-giri (diagonal hacia abajo), kiri-age (hacia arriba), yoko-giri (lateral).
        string[] cuts = { "kesa", "kiriage", "yoko" };
        Vector2[] swipes = { new Vector2(-1, -1).normalized, new Vector2(-1, 1).normalized, Vector2.left };
        for (int c = 0; c < cuts.Length; c++)
        {
            yield return new WaitForSecondsRealtime(.4f);
            Call(game, "FighterCut", swipes[c], false);
            for (int f = 0; f < 7; f++) yield return Shot("anim-corte-" + cuts[c] + "-" + f, .055f);
        }
        // Chanclas en vuelo.
        Battle(game, 1);
        yield return new WaitForSecondsRealtime(.5f);
        Call(game, "SpawnWave");
        yield return new WaitForSecondsRealtime(.7f);
        var sandals = (IList)Get(game, "projectiles");
        foreach (var item in sandals)
        {
            var root = (Transform)item.GetType().GetField("root").GetValue(item);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                report.AppendLine("  raiz=" + root.position + " escalaRaiz=" + root.lossyScale + " mundo=" + filter.GetComponent<Renderer>().bounds + " escalaPieza=" + filter.transform.lossyScale + " vista=" + ((Camera)Get(game, "view")).WorldToViewportPoint(filter.GetComponent<Renderer>().bounds.center));
                report.AppendLine("chancla " + filter.name + " v=" + (filter.sharedMesh ? filter.sharedMesh.vertexCount : -1) + " bounds=" + (filter.sharedMesh ? filter.sharedMesh.bounds.size.ToString() : "-") + " on=" + filter.GetComponent<Renderer>().enabled);
            }
            break;
        }
        for (int f = 0; f < 8; f++) yield return Shot("anim-chanclas-" + f, .22f);
        // Sombrilla jefa: reposo y ataques, dejando correr el combate.
        Battle(game, 4);
        yield return new WaitForSecondsRealtime(1.2f);
        Dump("Sombrilla", (Transform)Get(game, "opponent"));
        var umbrella = (Transform)Get(game, "opponent");
        if (umbrella != null)
        {
            var cam = (Camera)Get(game, "view");
            report.AppendLine("activa=" + umbrella.gameObject.activeInHierarchy + " pos=" + umbrella.position + " escala=" + umbrella.lossyScale + " vista=" + cam.WorldToViewportPoint(umbrella.position));
            foreach (var r in umbrella.GetComponentsInChildren<Renderer>(true))
            {
                var skin = r as SkinnedMeshRenderer;
                if (skin != null)
                {
                    var used = new System.Collections.Generic.SortedSet<string>();
                    var mesh = skin.sharedMesh;
                    if (mesh != null && mesh.isReadable)
                        foreach (var w in mesh.boneWeights)
                        {
                            if (w.weight0 > .2f) used.Add(skin.bones[w.boneIndex0].name);
                            if (w.weight1 > .2f) used.Add(skin.bones[w.boneIndex1].name);
                        }
                    report.AppendLine("  skin " + r.name + " root=" + (skin.rootBone ? skin.rootBone.name : "-") + " legible=" + (mesh != null && mesh.isReadable) + " huesos=" + string.Join(",", System.Array.ConvertAll(skin.bones, x => x ? x.name : "-")) + " pesos=" + string.Join(",", used));
                }
                report.AppendLine("  " + r.name + " on=" + r.enabled + "/" + r.gameObject.activeInHierarchy + " bounds=" + r.bounds.center + " " + r.bounds.size + " mat=" + (r.sharedMaterial != null ? r.sharedMaterial.shader.name : "-"));
            }
        }
        Call(game, "BeginTheaterAttack");
        for (int f = 0; f < 16; f++) yield return Shot("anim-sombrilla-" + f, .3f);
        // Faroles: el despertar con sus diálogos.
        Battle(game, 3);
        yield return new WaitForSecondsRealtime(1f);
        Call(game, "StartLanternDuel");
        for (int f = 0; f < 6; f++) yield return Shot("anim-faroles-" + f, .5f);
        // Samurái: guardia y herida.
        Battle(game, 1);
        yield return new WaitForSecondsRealtime(.8f);
        Set(game, "guarding", true); Set(game, "guardHeld", true);
        yield return Shot("anim-samurai-guardia", .4f);
        Set(game, "guarding", false); Set(game, "guardHeld", false);
        Set(game, "hurtFlash", .32f); Set(game, "fighterReaction", .24f);
        yield return Shot("anim-samurai-herido", .08f);
        File.WriteAllText(Path.Combine(Folder, "anim-review.txt"), report.ToString());
        EditorApplication.Exit(0);
    }
}
