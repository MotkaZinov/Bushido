using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Bushido;
using UnityEditor;
using UnityEngine;

// Validación de las ranas especiales y de los tajos del corte del juicio sobre ranas.
//   Menú: Bushido/Revisión/Validar ranas especiales
//   Lotes: Unity -batchmode -projectPath <proyecto> -executeMethod BushidoFrogValidation.Run
// Escribe QualityReview/frog-validation.txt (líneas PASS/FAIL) y QualityReview/ranas-especiales.png.
[InitializeOnLoad]
public static class BushidoFrogValidation
{
    const string Key = "Bushido.FrogValidation";
    static int ticks;
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
    static bool failed;
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../QualityReview"));

    static BushidoFrogValidation()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
    }

    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static void Check(bool condition, string message)
    {
        report.AppendLine((condition ? "PASS: " : "FAIL: ") + message);
        failed |= !condition;
    }

    [MenuItem("Bushido/Revisión/Validar ranas especiales")]
    public static void Run()
    {
        SessionState.SetBool(Key, true); ticks = 0; report.Clear(); failed = false;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        if (EditorApplication.isPlaying) return;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CampaignMap.unity");
        EditorApplication.EnterPlaymode();
    }

    static Type Nested(string name) => typeof(BushidoPrototype).GetNestedType(name, BindingFlags.NonPublic);
    static object Kind(string name) => Enum.Parse(Nested("FrogKind"), name);
    static IList Projectiles(BushidoPrototype game) => (IList)Get(game, "projectiles");

    // Starts a clean sendero battle with time frozen: the test drives every update itself.
    static void Fresh(BushidoPrototype game, int mission)
    {
        Set(game, "mission", mission); Call(game, "BeginBattle"); Set(game, "paused", false);
        Set(game, "waveTimer", 100f); Set(game, "strokeHits", 0);
    }

    static object Frog(BushidoPrototype game, string kind, Vector3 position, int pair = 0)
    {
        Call(game, "SpawnProjectile", Enum.Parse(Nested("ProjectileKind"), "Frog"), position.x, 9f, 0f, Kind(kind), pair);
        IList list = Projectiles(game);
        object item = list[list.Count - 1];
        Place(item, position);
        return item;
    }

    static void Place(object item, Vector3 position)
    {
        ((Transform)Get(item, "root")).position = position;
        Set(item, "velocity", Vector3.zero);
        Set(item, "age", 1f); // past the leap-out pop, so the frog is at full size
    }

    static void Slice(BushidoPrototype game, Vector3 a, Vector3 b, int stroke)
    {
        Set(game, "strokeSerial", stroke); Set(game, "strokeHits", 0);
        Call(game, "SliceProjectiles", a, b, false);
    }

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

    static int CountKind(BushidoPrototype game, string kind)
    {
        int count = 0;
        foreach (object frog in (IList)Get(game, "pendingFrogs"))
            if (frog.GetType().GetField("Item5").GetValue(frog).ToString() == kind) count++;
        return count;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || ++ticks < 60 || ticks % 15 != 0) return;
        var game = UnityEngine.Object.FindAnyObjectByType<BushidoPrototype>(); if (game == null) return;
        try
        {
            Time.timeScale = 0;
            if (Application.isBatchMode) ((Camera)Get(game, "view")).aspect = 16f / 9f;
            Vector3 left = new Vector3(-4, 3, 0), right = new Vector3(4, 3, 0);

            // 1. Rana acorazada: el primer tajo rompe la armadura y la lanza hacia arriba; el segundo la purifica.
            Fresh(game, 1);
            var actor = (Transform)Get(game, "fighter");
            Check(Get(game, "fighterRig") != null, "El samurái tiene su esqueleto de combate");
            Check(Array.Exists(actor.GetComponentsInChildren<SkinnedMeshRenderer>(), r => r.enabled),
                "La malla 3D está visible al entrar en combate");
            Check(Array.FindAll(actor.GetComponentsInChildren<Transform>(), part => (part.name == "Ojo.L" || part.name == "Ojo.R") && part.GetComponent<MeshRenderer>() != null).Length == 2,
                "Los dos ojos están montados en la cara");
            var tip = Array.Find(actor.GetComponentsInChildren<Transform>(), part => part.name.StartsWith("Katana"));
            var startHand = tip.position;
            Call(game, "FighterCut", Vector2.right, false);
            for (int sample = 0; sample < 8; sample++) Call(game, "UpdateFighter", .02f);
            Check((tip.position - startHand).sqrMagnitude > .001f && ((LineRenderer)Get(game, "companionTrail")).enabled,
                "Mover el brazo durante el tajo dibuja la estela del arma");
            object armoured = Frog(game, "Armoured", new Vector3(0, 3, 0));
            Check(Get(armoured, "armour") != null, "La rana acorazada nace con armadura de papel");
            Slice(game, left, right, 1);
            Check(Projectiles(game).Count == 1 && Get(armoured, "armour") == null && (int)Get(game, "hits") == 0, "Acorazada: el primer tajo rompe la armadura y la rana sigue viva sin contar acierto");
            Check(((Vector3)Get(armoured, "velocity")).y > 3f, "Acorazada: el primer tajo la lanza hacia arriba en un arco nuevo");
            Slice(game, left, right, 1);
            Check(Projectiles(game).Count == 1, "Acorazada: un segundo paso del mismo tajo no la purifica");
            Slice(game, left, right, 2);
            Check(Projectiles(game).Count == 0 && (int)Get(game, "hits") == 1, "Acorazada: el segundo tajo la purifica (un acierto)");

            // 2. Ranas enlazadas: un solo tajo las corta y da el lazo roto con carga doble.
            Fresh(game, 1);
            object a = Frog(game, "Linked", new Vector3(-1.2f, 3, 0), 7), b = Frog(game, "Linked", new Vector3(1.2f, 3, 0), 7);
            Check(Get(a, "thread") != null && Get(a, "partner") == b, "Enlazadas: un hilo une a las dos ranas");
            Set(game, "judgmentCharge", 0f); Set(game, "notice", "");
            Slice(game, left, right, 5);
            float charge = (float)Get(game, "judgmentCharge");
            Check(Projectiles(game).Count == 0 && (string)Get(game, "notice") == "LAZO ROTO" && Mathf.Abs(charge - 4f / 12f) < 0.001f,
                "Enlazadas: ambas en un tajo dan LAZO ROTO y carga doble (" + charge.ToString("0.000") + " = 4/12)");
            Fresh(game, 1);
            a = Frog(game, "Linked", new Vector3(-1.2f, 3, 0), 8); b = Frog(game, "Linked", new Vector3(5f, 3, 0), 8);
            Set(game, "judgmentCharge", 0f); Set(game, "notice", "");
            Slice(game, new Vector3(-3, 3, 0), new Vector3(0, 3, 0), 6);
            Check(Projectiles(game).Count == 1 && (string)Get(game, "notice") != "LAZO ROTO" && Get(b, "partner") == null, "Enlazadas: cortar solo una no da bonificación y el hilo se rompe");
            Place(b, new Vector3(5f, 3, 0));
            Set(game, "waveTimer", 100f);
            Call(game, "UpdateBattle", 0.35f);
            Check(((Vector3)Get(b, "velocity")).y > 3f, "Enlazadas: la superviviente salta más alto y más rápido");

            // 3. Rana dorada: si escapa no suma fallo; una común sí; cortarla da 1/3 de carga y una vida.
            Fresh(game, 1);
            object golden = Frog(game, "Golden", new Vector3(0, -1.7f, 0));
            Set(golden, "velocity", new Vector3(0, -3f, 0));
            int missesBefore = (int)Get(game, "misses");
            Call(game, "UpdateBattle", 0.1f);
            Check(Projectiles(game).Count == 0 && (int)Get(game, "misses") == missesBefore, "Dorada: al escapar no suma fallo");
            object common = Frog(game, "Common", new Vector3(0, -1.7f, 0));
            Set(common, "velocity", new Vector3(0, -3f, 0));
            Call(game, "UpdateBattle", 0.1f);
            Check((int)Get(game, "misses") == missesBefore + 1, "Control: una rana común que escapa sí suma fallo");
            Set(game, "lives", 2); Set(game, "judgmentCharge", 0f);
            Frog(game, "Golden", new Vector3(0, 3, 0));
            Slice(game, left, right, 9);
            Check((int)Get(game, "lives") == 3 && Mathf.Abs((float)Get(game, "judgmentCharge") - 1f / 3f) < 0.001f, "Dorada: cortarla restaura una vida y da 1/3 de carga");

            // 4. Tajo limpio: tres ranas en un tajo.
            Fresh(game, 1);
            Set(game, "judgmentCharge", 0f); Set(game, "notice", "");
            for (int i = 0; i < 3; i++) Frog(game, "Common", new Vector3(-2 + i * 2, 3, 0));
            Slice(game, left, right, 12);
            Check((string)Get(game, "notice") == "TAJO LIMPIO ×3" && Mathf.Abs((float)Get(game, "judgmentCharge") - 4f / 12f) < 0.001f, "Tajo limpio: tres ranas dan TAJO LIMPIO ×3 y 1/12 extra");

            // 5. Escalada: cada tipo se enseña solo antes de mezclarse.
            Fresh(game, 1);
            string trail = "";
            for (int wave = 1; wave <= 8; wave++)
            {
                ((IList)Get(game, "pendingFrogs")).Clear();
                Call(game, "SpawnWave");
                if (wave <= 3) Check(CountKind(game, "Common") == 4, "Sendero 1: la oleada " + wave + " es solo de ranas comunes");
                if (wave == 4) Check(CountKind(game, "Armoured") == 1 && CountKind(game, "Linked") == 0, "Sendero 1: la oleada 4 trae una acorazada y nada más");
                if (wave == 7) Check(CountKind(game, "Linked") == 2 && CountKind(game, "Armoured") == 0, "Sendero 1: la oleada 7 estrena las enlazadas solas");
                trail += CountKind(game, "Armoured") + "a" + CountKind(game, "Linked") + "l" + CountKind(game, "Golden") + "g ";
            }
            report.AppendLine("INFO: sendero 1, oleadas 1-8 (acorazadas/enlazadas/doradas): " + trail);
            Fresh(game, 3);
            for (int wave = 1; wave <= 2; wave++) { ((IList)Get(game, "pendingFrogs")).Clear(); Call(game, "SpawnWave"); }
            Check(CountKind(game, "Armoured") == 1, "Sendero 3: la acorazada llega en la oleada 2 (dos antes)");
            Fresh(game, 4);
            bool bossClean = true;
            for (int wave = 1; wave <= 12; wave++)
            {
                ((IList)Get(game, "pendingFrogs")).Clear();
                Call(game, "SpawnWave");
                bossClean &= CountKind(game, "Armoured") == 0 && CountKind(game, "Golden") == 0;
            }
            Check(bossClean, "Jefe: solo ranas comunes y enlazadas");

            // 6. Corte del juicio: el tajo que cruza una rana la corta antes del tajo final.
            Fresh(game, 1);
            Set(game, "judgmentCharge", 0f);
            Action banish = (Action)Delegate.CreateDelegate(typeof(Action), game, typeof(BushidoPrototype).GetMethod("JudgmentBanish", Flags));
            Call(game, "BeginJudgmentCut", new object[] { banish });
            var through = (Vector2[])Get(game, "judgmentThrough");
            Transform judgmentRoot = (Transform)Get(game, "judgmentRoot");
            Vector3 crossing = judgmentRoot.TransformPoint((Vector3)through[0]); crossing.z = 0;
            Frog(game, "Common", crossing);
            Frog(game, "Common", new Vector3(60, 60, 0));
            Call(game, "UpdateJudgmentCut", 0.45f);
            Check(Projectiles(game).Count == 2 && (int)Get(game, "judgmentCutsDone") == 0, "Juicio: antes del primer tajo las dos ranas siguen vivas");
            Call(game, "UpdateJudgmentCut", 0.1f);
            int halves = 0;
            foreach (object effect in (IList)Get(game, "cutEffects")) if (((Transform)Get(effect, "root")).name.StartsWith("Rana")) halves++;
            Check(Projectiles(game).Count == 1 && !(bool)Get(game, "judgmentFinalDone") && (int)Get(game, "hits") == 1 && halves >= 2,
                "Juicio: el tajo 1 corta la rana que cruza antes del tajo final (mitades: " + halves + ")");
            Check(Mathf.Abs((float)Get(game, "judgmentCharge")) < 0.0001f, "Juicio: los tajos no dan carga de juicio");
            Capture(game, "ranas-juicio-tajo");
            Call(game, "UpdateJudgmentCut", 4f);
            Check(Projectiles(game).Count == 0 && (int)Get(game, "hits") == 2, "Juicio: el tajo final sigue limpiando las ranas que ningún tajo cruzó");

            // 7. Captura: una oleada con todos los tipos.
            Fresh(game, 1);
            Frog(game, "Common", new Vector3(-5.5f, 1.5f, 0));
            Frog(game, "Armoured", new Vector3(-3f, 3f, 0));
            Frog(game, "Golden", new Vector3(-0.5f, 4.5f, 0));
            Frog(game, "Linked", new Vector3(2f, 3f, 0), 3);
            Frog(game, "Linked", new Vector3(4.6f, 2f, 0), 3);
            Call(game, "UpdateBattle", 0.0001f);
            Capture(game, "ranas-especiales");
            Finish();
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); failed = true; Debug.LogException(e); Finish(); }
    }

    static void Finish()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "frog-validation.txt"), report.ToString());
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Time.timeScale = 1;
        if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        else EditorApplication.ExitPlaymode();
    }
}
