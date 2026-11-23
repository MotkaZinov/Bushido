using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Bushido;
using UnityEditor;
using UnityEngine;

// Revisión del corte del juicio: lo dispara en una batalla, comprueba cada fase y guarda capturas.
//   Menú: Bushido/Revisión/Ver corte del juicio
//   Lotes: Unity -batchmode -projectPath <proyecto> -executeMethod BushidoJudgmentReview.Run
//   Editor abierto, sin tocarlo: crear el archivo Temp/judgment-review.request y recompilar.
[InitializeOnLoad]
public static class BushidoJudgmentReview
{
    const string Key = "Bushido.JudgmentReview";
    static int ticks, stage;
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../QualityReview"));
    static string Request => Path.Combine(Application.dataPath, "../Temp/judgment-review.request");

    static BushidoJudgmentReview()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        EditorApplication.delayCall += Run;
    }

    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); report.AppendLine("PASS: " + message); }

    [MenuItem("Bushido/Revisión/Ver corte del juicio")]
    public static void Run()
    {
        SessionState.SetBool(Key, true); ticks = stage = 0; report.Clear();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        if (EditorApplication.isPlaying) return;
        // A fresh checkout opens an empty scene, which has no game object to drive.
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CampaignMap.unity");
        EditorApplication.EnterPlaymode();
    }

    // Batch mode never presents a frame, so render the game camera straight to a file (no HUD).
    static void Capture(BushidoPrototype game, string name)
    {
        var camera = (Camera)Get(game, "view");
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target; camera.Render(); camera.targetTexture = null;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image); target.Release();
    }

    static void Advance(BushidoPrototype game, float seconds) => Call(game, "UpdateJudgmentCut", seconds);

    // The boss finisher is driven by the game's own katana swing: step all three together.
    static void AdvanceFinisher(BushidoPrototype game, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            Call(game, "UpdateKatanaProps", .05f);
            Call(game, "UpdateJudgmentCut", .05f);
            Call(game, "LateJudgmentCut");
        }
    }

    static void CheckArea(BushidoPrototype game, ScreenSlicer slicer, string message)
    {
        var camera = (Camera)Get(game, "view");
        float screen = 4 * camera.orthographicSize * camera.orthographicSize * camera.aspect;
        Check(Mathf.Abs(slicer.Area() - screen) < screen * 1e-3f, message);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || ++ticks < 60 || ticks % 15 != 0) return;
        var game = UnityEngine.Object.FindAnyObjectByType<BushidoPrototype>(); if (game == null) return;
        try
        {
            var slicer = (ScreenSlicer)Get(game, "judgmentSlicer");
            if (stage == 0)
            {
                Time.timeScale = 0;
                if (Application.isBatchMode) ((Camera)Get(game, "view")).aspect = 16f / 9f;
                Set(game, "mission", 1); Call(game, "BeginBattle"); Set(game, "paused", false);
                Call(game, "SpawnWave");
                for (int i = 0; i < 75; i++) Call(game, "UpdateBattle", 1f / 60);
                Call(game, "BeginJudgmentCut", new object[] { null });
                Advance(game, .3f);
                Check(!(bool)Get(game, "judgmentFrozen"), "El desenvaine ocurre antes de cualquier corte");
                Check(((ICollection)Get(game, "judgmentTrail")).Count == 0, "No hay estela mientras la katana sigue en la funda");
                Capture(game, "juicio-1-desenvaine");
            }
            if (stage == 1)
            {
                Advance(game, .27f); Capture(game, "juicio-2a-tajo-inicio"); // start, middle and end of the third cut
                Advance(game, .05f); Capture(game, "juicio-2b-tajo-medio");
                Advance(game, .04f); Capture(game, "juicio-2c-tajo-fin");
                Advance(game, .2f);
                Check((bool)Get(game, "judgmentFrozen") && slicer.shards.Count >= 4, "Cada tajo divide los polígonos que atraviesa");
                CheckArea(game, slicer, "Los fragmentos cubren exactamente la pantalla (sin huecos ni solapes)");
                Check((int)Get(game, "judgmentLinesShown") > 0, "Los cortes quedan abiertos en el espacio");
                Capture(game, "juicio-2-tajos");
            }
            if (stage == 2) { Advance(game, .53f); Capture(game, "juicio-3-tajo-final"); }
            if (stage == 3)
            {
                Advance(game, .2f);
                Check((bool)Get(game, "judgmentFinalDone") && !slicer.Released, "Tras el tajo final hay una pausa con desfase antes de la caída");
                CheckArea(game, slicer, "El tajo final también parte en mitades limpias");
                bool slid = false;
                foreach (var shard in slicer.shards) slid |= shard.slide.sqrMagnitude > 0;
                Check(slid, "Las mitades se deslizan a lo largo del corte");
                Capture(game, "juicio-4-desfase");
            }
            if (stage == 4)
            {
                // Noto: la katana vuelve a la funda en el centro, se detiene, destella la guarda y el cierre suelta la pantalla.
                Advance(game, .9f);
                Check(!slicer.Released && ((Transform)Get(game, "judgmentSaya")).gameObject.activeInHierarchy, "La pantalla sigue cortada mientras la katana se enfunda");
                Capture(game, "juicio-5-enfundado");
                Advance(game, .8f);
                Check(!slicer.Released, "La pantalla espera al destello de la guarda");
                Capture(game, "juicio-6-destello");
                Advance(game, .3f);
                Check(slicer.Released, "Los fragmentos caen al cerrarse la funda");
                Capture(game, "juicio-7-caida");
            }
            if (stage == 5)
            {
                Advance(game, 1f);
                Check((Transform)Get(game, "judgmentRoot") == null, "El efecto se retira y vuelve el juego");
            }
            if (stage == 6)
            {
                Set(game, "mission", 4); Call(game, "BeginBattle"); Set(game, "paused", false);
                Call(game, "BeginBossInkFinish", new Vector3(-1, 1, 0));
                Call(game, "UpdateBossRitual", .2f);
                Check((Transform)Get(game, "judgmentRoot") != null && (bool)Get(game, "judgmentFinisher"), "El remate del jefe final arranca con el tajo de la katana");
                AdvanceFinisher(game, 6);
                Check(((ICollection)Get(game, "judgmentTrail")).Count > 0 && !(bool)Get(game, "judgmentFrozen"), "La katana del jugador lidera la estela antes de que la pantalla se parta");
                Capture(game, "remate-1-estela");
            }
            if (stage == 7)
            {
                AdvanceFinisher(game, 5);
                Check((bool)Get(game, "judgmentFrozen") && slicer.shards.Count == 2, "El tajo parte la pantalla en dos mitades por su línea");
                Capture(game, "remate-2-desfase");
            }
            if (stage == 8)
            {
                Advance(game, .25f); Advance(game, .3f);
                Check(slicer.Released, "Las dos mitades caen y dejan ver la escena");
                Capture(game, "remate-3-caida");
            }
            if (stage == 9)
            {
                // Presentación de la katana 3D ante el jefe final: entra, se desenfunda un tramo y se carga.
                Advance(game, 2f);
                Set(game, "mission", 4); Call(game, "BeginBattle"); Set(game, "paused", false);
                Set(game, "bossFinalStage", 1); Set(game, "wasPressed", false);
                for (int i = 0; i < 44; i++) Call(game, "UpdateBossRitual", .05f);
                Capture(game, "katana-1-desenfunde");
                Set(game, "wasPressed", true); Set(game, "drawGripHeld", true); Set(game, "drawPullTarget", .6f);
                for (int i = 0; i < 8; i++) Call(game, "UpdateBossRitual", .05f);
                Capture(game, "katana-2-cargada");
                Finish(true); return;
            }
            stage++;
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); Finish(false); }
    }

    static void Finish(bool success)
    {
        File.WriteAllText(Path.Combine(Folder, "juicio-validation.txt"), report.ToString());
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Time.timeScale = 1;
        if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
