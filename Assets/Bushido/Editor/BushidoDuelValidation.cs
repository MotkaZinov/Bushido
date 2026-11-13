using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Bushido;
using UnityEditor;
using UnityEngine;

// Integration checks use real gesture recognition, counter opening and seal flights.
// They intentionally do not assert exact animation frames or implementation constants.
[InitializeOnLoad]
public static class BushidoDuelValidation
{
    const string Key = "Bushido.DuelValidation";
    const float Step = 1f / 120f;
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static int wait;
    static readonly List<string> results = new List<string>();
    static BushidoDuelValidation()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
    }

    [MenuItem("Bushido/Revisión/Validar duelo de cuatro bloqueos %#F9")]
    public static void Run()
    {
        SessionState.SetBool(Key, true);
        wait = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
    }

    static object Get(object target, string name)
    {
        var type = target.GetType();
        var field = type.GetField(name, Flags);
        if (field != null) return field.GetValue(target);
        var property = type.GetProperty(name, Flags);
        if (property != null) return property.GetValue(target);
        throw new MissingMemberException(type.Name, name);
    }
    static void Set(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, Flags);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }
    static object Call(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, Flags);
        if (method == null) throw new MissingMethodException(target.GetType().Name, name);
        return method.Invoke(target, args);
    }
    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        results.Add("PASS: " + description);
    }
    static bool IsVulnerable(BushidoPrototype game) => (bool)Get(game, "vulnerable");
    static string Phase(BushidoPrototype game) => Get(game, "duelPhase").ToString();
    static void Advance(BushidoPrototype game)
    {
        Call(game, "UpdateBossDuel", Step);
        Set(game,"paused",false);Call(game,"ResolveDuelContact");Set(game,"paused",true);
        Set(game,"paused",false);
        Call(game,"ResolveDuelContact");
        Set(game,"paused",true);
        Call(game, "UpdateBossRitual", Step);
    }
    static void Until(BushidoPrototype game, Func<bool> condition, float timeout, string reason)
    {
        for (int i = 0; i < Mathf.CeilToInt(timeout / Step); i++)
        {
            if (condition()) return;
            Advance(game);
        }
        throw new TimeoutException(reason);
    }
    static void StartBattle(BushidoPrototype game)
    {
        Set(game, "mission", 2);
        Call(game, "BeginBattle");
        Set(game, "paused", true);
        Call(game, "BeginTheaterAttack");
    }

    static void Validate(BushidoPrototype game)
    {
        // The gesture adapter is kept here so the runtime retains no test hooks.
        StartBattle(game);
        for (int beat = 0; beat < 4; beat++)
        {
            Until(game, () => Phase(game) == "Telegraph", 6, "No apareció el golpe de la prueba de fallo");
            if (beat > 0) PrepareBlock(game);
            Until(game, () => Phase(game) == "Recovery", 4, "No se resolvió el impacto");
        }
        Until(game, () => Phase(game) == "RecoveryRound", 4, "Una ronda incompleta no se recuperó");
        Check(!IsVulnerable(game) && (int)Get(game, "bossSeals") == 0,
            "Tres de cuatro bloqueos no abren ni aplican un sello");
        Check((int)Get(game, "lives") == 2, "Un golpe omitido resta exactamente una cantimplora");

        StartBattle(game);
        for (int round = 0; round < 3; round++)
        {
            for (int beat = 0; beat < 4; beat++)
            {
                Until(game, () => Phase(game) == "Telegraph", 6,
                    "No se abrió la ventana de bloqueo " + (beat + 1));
                int before = (int)Get(game, "duelBlocks");
                PrepareBlock(game);
                Check((int)Get(game, "duelBlocks") == before && (bool)Get(game, "duelPrepared"),
                    "Gesto temprano prepara guardia; todavía no cuenta como impacto");
                Until(game, () => Phase(game) == "Recovery", 4, "No se resolvió el golpe preparado");
                Check((int)Get(game, "duelBlocks") == before + 1, "Ronda " + (round + 1) + ": gesto válido bloquea golpe " + (beat + 1));
                var gesture = new List<Vector3>((Vector3[])Call(game, "DuelPattern"));
                Call(game, "CompleteDuelGesture", gesture);
                Check((int)Get(game, "duelBlocks") == before + 1, "Gesto repetido no cuenta dos veces");
                if (beat < 3) Check(!IsVulnerable(game), "El sello espera los cuatro bloqueos");
            }
            Until(game, () => IsVulnerable(game), 4, "Cuatro bloqueos no abrieron el sello");
            var symbol = (Vector3[])Call(game, "SpiritPattern", 1f, round);
            var drawing = (List<Vector3>)Get(game, "bossSealDrawing");
            drawing.Clear();
            drawing.AddRange(symbol);
            Call(game, "TraceBossPattern", symbol[0], symbol[symbol.Length - 1]);
            Check((int)Get(game, "bossSeals") == round + 1, "Reconocimiento real lanza sello " + (round + 1));
            if(round==2)Check((int)Get(game,"bossFinalStage")==0,"El desenvaine espera el aterrizaje del tercer sello");
            Until(game, () => (int)Get(game, "landedBossSeals") == round + 1, 3,
                "El sello no aterrizó en la máscara");
            if (round < 2) Until(game, () => Phase(game) == "Charge", 8,
                "El jefe no preparó la ronda siguiente");
        }
        Check((int)Get(game, "bossFinalStage") == 1, "Tres sellos permiten el desenvaine final");
        ValidateTimingAndDefeat(game);
    }

    static void ValidateTimingAndDefeat(BushidoPrototype game)
    {
        StartBattle(game);
        Until(game, () => Phase(game) == "Telegraph", 6, "No empezó la prueba de precisión");
        Set(game, "duelGestureBeat", Get(game, "duelBeat"));
        Set(game,"strokeGestureEpoch",Get(game,"gestureEpoch"));
        Set(game,"strokeGestureEpoch",Get(game,"gestureEpoch"));
        var wrongDiagonal = new List<Vector3> { new Vector3(-.5f,.5f), new Vector3(.5f,-.5f) };
        Check(!(bool)Call(game, "CompleteDuelGesture", wrongDiagonal) && !(bool)Get(game, "duelPrepared"),
            "Diagonal opuesta al símbolo no prepara guardia");

        float clock = (float)Get(game, "duelClock");
        for (int i = 0; i < 30; i++) Call(game, "UpdateBossDuel", 0f);
        Check((float)Get(game, "duelClock") == clock && Phase(game) == "Telegraph",
            "Treinta actualizaciones con delta cero no adelantan el ataque");

        float strikeTime = (float)Get(game, "DuelStrikeDelay");
        Until(game, () => (float)Get(game, "duelClock") >= strikeTime - .1f, 4,
            "No se alcanzó la ventana de desvío perfecto");
        var correct = new List<Vector3>((Vector3[])Call(game, "DuelPattern"));
        Check((bool)Call(game, "CompleteDuelGesture", correct) && (bool)Get(game, "duelPerfect"),
            "Completar el símbolo en los últimos 100 ms prepara desvío perfecto");
        Until(game, () => Phase(game) == "Recovery", 2, "No impactó el desvío perfecto");
        Check((int)Get(game, "duelBlocks") == 1 && (int)Get(game, "lives") == 3,
            "El desvío perfecto cuenta un bloqueo y conserva las tres vidas");

        // Finish(false) persists campaign state. Preserve the exact pre-test value,
        // including a missing save key, so this death-path check cannot alter a save.
        string saveKey = (string)typeof(BushidoPrototype).GetField("SaveKey",
            BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        bool hadSave = PlayerPrefs.HasKey(saveKey);
        string savedValue = hadSave ? PlayerPrefs.GetString(saveKey) : null;
        try
        {
            StartBattle(game);
            for (int beat = 0; beat < 3; beat++)
            {
                Until(game, () => Phase(game) == "Telegraph", 6, "No apareció golpe de la prueba de derrota");
                Until(game, () => (int)Get(game, "lives") == 2 - beat, 4,
                    "Un golpe sin bloquear no descontó su vida");
            }
            Check(Get(game, "mode").ToString() != "Battle" && Phase(game) == "None",
                "El tercer fallo termina el combate y detiene la ráfaga");
            Check(!IsVulnerable(game) && (int)Get(game, "bossSeals") == 0,
                "La derrota no abre ni lanza un sello");
            for (int i = 0; i < 30; i++) Call(game, "UpdateBossDuel", Step);
            Check((int)Get(game, "lives") == 0 && !IsVulnerable(game),
                "Tras la derrota no aparecen golpes extra ni ventana de sello");
        }
        finally
        {
            game.StopAllCoroutines();
            if (hadSave) PlayerPrefs.SetString(saveKey, savedValue);
            else PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
        }
    }

    static void PrepareBlock(BushidoPrototype game)
    {
        var gesture = new List<Vector3>((Vector3[])Call(game, "DuelPattern"));
        // Simulate pointer-down ownership exactly as ReadSwipe does. A gesture begun
        // in the previous beat must not be accepted even if its shape is correct.
        Set(game, "duelGestureBeat", (int)Get(game, "duelBeat") - 1);
        Check(!(bool)Call(game, "CompleteDuelGesture", gesture), "No se reutiliza gesto del golpe anterior");
        Set(game, "duelGestureBeat", Get(game, "duelBeat"));
        Set(game,"strokeGestureEpoch",Get(game,"gestureEpoch"));
        Check(!(bool)Call(game, "CompleteDuelGesture", new List<Vector3> { Vector3.zero }),
            "Un toque sin trazo no prepara un bloqueo");
        // Drawing is deliberately translated and scaled: the player may trace anywhere.
        for (int i = 0; i < gesture.Count; i++) gesture[i] = gesture[i] * 1.4f + new Vector3(2, 1, 0);
        Check((bool)Call(game, "CompleteDuelGesture", gesture), "Reconoce símbolo trasladado y escalado");
        Check(!(bool)Call(game, "CompleteDuelGesture", gesture), "El mismo golpe solo admite una preparación");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || ++wait < 45) return;
        var game = UnityEngine.Object.FindAnyObjectByType<BushidoPrototype>();
        if (game == null || Get(game, "view") == null) return;
        EditorApplication.update -= Tick;
        results.Clear();
        bool success = false;
        try
        {
            Validate(game);
            success = true;
            Debug.Log("BUSHIDO_DUEL_VALIDATION_OK: " + results.Count + " comprobaciones");
        }
        catch (Exception e)
        {
            results.Add("FAIL: " + e);
            Debug.LogException(e);
        }
        finally
        {
            SessionState.SetBool(Key, false);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../QualityReview"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "duel-validation.txt"),
                DateTime.Now.ToString("s") + "\n" + string.Join("\n", results));
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
    }
}

