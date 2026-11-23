using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Bushido;
using UnityEditor;
using UnityEngine;

// Capturas del HUD y de los menús (incluida la capa IMGUI) para revisarlos a ojo.
//   Menú: Bushido/Revisión/Capturar HUD y menús
//   Sin -batchmode (OnGUI no se dibuja en lote): Unity -projectPath <proyecto> -executeMethod BushidoHudReview.Run
// Escribe PNG en QualityReview/. No guarda la partida: los resultados se fuerzan por reflexión sin llamar a Finish().
[InitializeOnLoad]
public static class BushidoHudReview
{
    const string Key = "Bushido.HudReview";
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../QualityReview"));
    static int ticks;

    static BushidoHudReview()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
    }

    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);

    [MenuItem("Bushido/Revisión/Capturar HUD y menús")]
    public static void Run()
    {
        SessionState.SetBool(Key, true); ticks = 0;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        if (EditorApplication.isPlaying) return;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CampaignMap.unity");
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || ++ticks < 90) return;
        var game = UnityEngine.Object.FindAnyObjectByType<BushidoPrototype>(); if (game == null) return;
        EditorApplication.update -= Tick;
        SessionState.SetBool(Key, false);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        game.StartCoroutine(Sequence(game));
    }

    static void Battle(BushidoPrototype game, int mission)
    {
        Set(game, "mission", mission); Call(game, "BeginBattle"); Set(game, "paused", false);
    }

    static IEnumerator Shot(string name, float wait)
    {
        yield return new WaitForSecondsRealtime(wait);
        yield return new WaitForEndOfFrame();
        Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG());
        Debug.Log("HUD review: " + name + " " + image.width + "x" + image.height);
        UnityEngine.Object.DestroyImmediate(image);
    }

    static void Result(BushidoPrototype game, bool won, string rank, float age)
    {
        var modeType = typeof(BushidoPrototype).GetNestedType("Mode", BindingFlags.NonPublic);
        Set(game, "hits", 21); Set(game, "misses", 3); Set(game, "wounds", 1); Set(game, "bestCombo", 9);
        Set(game, "won", won); Set(game, "unlockedNew", false);
        Set(game, "resultRank", Enum.Parse(typeof(Rank), rank));
        Set(game, "mode", Enum.Parse(modeType, "Result"));
        Set(game, "resultSince", Time.unscaledTime - age);
    }

    static IEnumerator Sequence(BushidoPrototype game)
    {
        Directory.CreateDirectory(Folder);
        Debug.Log("HUD review: pantalla " + Screen.width + "x" + Screen.height);
        // Sendero en curso, medidor a la mitad.
        Battle(game, 1);
        yield return new WaitForSecondsRealtime(2.2f);
        Set(game, "battleTime", 12f); Set(game, "hits", 14); Set(game, "misses", 2); Set(game, "judgmentCharge", .5f);
        Set(game, "stamina", 70f); Set(game, "lives", 2); Set(game, "jutsuTime", 2.5f);
        yield return Shot("hud-sendero", .9f);
        // Medidor lleno.
        Set(game, "judgmentCharge", 1f);
        yield return Shot("hud-juicio-lleno", 1.1f);
        // Puntería bajo el rango B, con una fuga reciente.
        Battle(game, 1);
        yield return new WaitForSecondsRealtime(.8f);
        Set(game, "battleTime", 20f); Set(game, "hits", 6); Set(game, "misses", 8); Set(game, "judgmentCharge", .25f); Set(game, "stamina", 30f);
        yield return new WaitForSecondsRealtime(.6f);
        Set(game, "misses", 9);
        yield return Shot("hud-bajo-rango", .1f);
        // Avisos: pista larga y sello corto.
        Battle(game, 1);
        yield return new WaitForSecondsRealtime(.6f);
        Call(game, "Tell", "Rana acorazada: dos tajos", 4f);
        yield return Shot("hud-aviso", .6f);
        Call(game, "Tell", "LAZO ROTO", 3f);
        yield return Shot("hud-aviso-sello", .35f);
        // Jefe.
        Battle(game, 4);
        yield return new WaitForSecondsRealtime(2.5f);
        Set(game, "bossHealth", 4); Set(game, "guard", 2);
        yield return new WaitForSecondsRealtime(.7f);
        Set(game, "bossHealth", 3);
        yield return Shot("hud-jefe", .15f);
        // Pausa.
        Battle(game, 1);
        yield return new WaitForSecondsRealtime(.6f);
        Call(game, "TogglePause");
        yield return Shot("pausa", .4f);
        Call(game, "TogglePause");
        // Resultados.
        Battle(game, 1);
        yield return new WaitForSecondsRealtime(.3f);
        Result(game, true, "A", .9f);
        yield return Shot("resultado-animando", .02f);
        Result(game, true, "A", 3.5f);
        yield return Shot("resultado-victoria", .1f);
        Battle(game, 1);
        yield return new WaitForSecondsRealtime(.3f);
        Result(game, false, "None", 3.5f);
        yield return Shot("resultado-derrota", .1f);
        Battle(game, 1);
        yield return new WaitForSecondsRealtime(.3f);
        Result(game, true, "C", 3.5f);
        yield return Shot("resultado-bajo-b", .1f);
        File.WriteAllText(Path.Combine(Folder, "hud-review.txt"), "ok " + DateTime.Now);
        EditorApplication.Exit(0);
    }
}
