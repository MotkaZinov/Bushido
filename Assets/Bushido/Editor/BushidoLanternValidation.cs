using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Bushido;
using UnityEditor;
using UnityEngine;

// Validación del duelo "Los faroles gemelos" (nodo 3) y capturas con HUD.
//   Menú: Bushido/Revisión/Validar duelo de faroles
//   Lote: Unity -batchmode -projectPath <proyecto> -executeMethod BushidoLanternValidation.Run
//   Capturas (sin -batchmode, OnGUI no se dibuja en lote): Unity -projectPath <proyecto> -executeMethod BushidoLanternValidation.RunShots
// Escribe QualityReview/farol-validation.txt (PASS/FAIL) y QualityReview/duelo-*.png. Restaura la partida guardada.
[InitializeOnLoad]
public static class BushidoLanternValidation
{
    const string Key = "Bushido.LanternValidation", ShotsKey = "Bushido.LanternShots", SaveKey = "Bushido.Campaign.v1";
    static int ticks;
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
    static bool failed;
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../QualityReview"));

    static BushidoLanternValidation()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
        if (SessionState.GetBool(ShotsKey, false)) EditorApplication.update += ShotsTick;
    }

    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static bool Prop(object o, string n) => (bool)o.GetType().GetProperty(n, Flags).GetValue(o);
    static void Check(bool condition, string message) { report.AppendLine((condition ? "PASS: " : "FAIL: ") + message); failed |= !condition; }
    static int Int(object g, string n) => (int)Get(g, n);
    static float Flt(object g, string n) => (float)Get(g, n);
    static LanternActor Twin(object g, int i) => ((LanternActor[])Get(g, "twins"))[i];
    static string Act(object g) => Get(g, "lact").ToString();
    static void ClearPlan(object g) { object plan = Get(g, "lanternPlan"); plan.GetType().GetMethod("Clear").Invoke(plan, null); }
    static List<string> Plan(object g) { var l = new List<string>(); foreach (var x in (IEnumerable)Get(g, "lanternPlan")) l.Add(x.ToString()); return l; }

    [MenuItem("Bushido/Revisión/Validar duelo de faroles")]
    public static void Run()
    {
        SessionState.SetBool(Key, true); ticks = 0; report.Clear(); failed = false;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        if (EditorApplication.isPlaying) return;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CampaignMap.unity");
        EditorApplication.EnterPlaymode();
    }

    // ---- helpers ----
    static void Fresh(BushidoPrototype g, int lives = 3)
    {
        Set(g, "mission", 3); Call(g, "BeginBattle"); Set(g, "paused", false); Set(g, "lives", lives);
    }

    static void Sim(BushidoPrototype g, float seconds)
    {
        for (float t = 0; t < seconds - 1e-4f; t += 1f / 60f)
        {
            if (Get(g, "mode").ToString() != "Battle") return;
            Call(g, "UpdateBattle", 1f / 60f);
            if (Get(g, "mode").ToString() != "Battle") return;
            Call(g, "UpdateLanternDuel", 1f / 60f);
        }
    }

    static bool SimUntil(BushidoPrototype g, Func<bool> done, float max)
    {
        for (float t = 0; t < max; t += 1f / 60f) { if (done()) return true; Sim(g, 1f / 60f); }
        return done();
    }

    // Skips the frog opening and plays the 1 s awakening beat: twin 0 is lit, the plan is emptied so each check chooses what happens.
    static void Duel(BushidoPrototype g, int lives = 3)
    {
        Fresh(g, lives);
        Set(g, "battleTime", 12f); Sim(g, 1.05f);
        ClearPlan(g);
        Set(g, "lact", Enum.Parse(typeof(BushidoPrototype).GetNestedType("LanternAct", BindingFlags.NonPublic), "None"));
        Set(g, "lashesLeft", 1);
    }

    static void Cut(BushidoPrototype g, Vector3 a, Vector3 b)
    {
        Set(g, "strokeSerial", Int(g, "strokeSerial") + 1); Set(g, "strokeHits", 0);
        Call(g, "SliceProjectiles", a, b, false);
    }

    static void CutBody(BushidoPrototype g, int i)
    {
        Vector3 c = Twin(g, i).Body; c.z = 0;
        Cut(g, c + Vector3.left * 2f, c + Vector3.right * 2f);
    }

    static void Across(BushidoPrototype g, LanternActor t, bool along)
    {
        Vector3 p = t.Points[8], q = t.Points[12]; p.z = q.z = 0;
        Vector3 d = (q - p).normalized, n = new Vector3(-d.y, d.x, 0);
        Vector3 mid = (p + q) * 0.5f;
        if (along) Cut(g, p - d * 0.2f, q + d * 0.2f); else Cut(g, mid - n, mid + n);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || ++ticks < 60 || ticks % 15 != 0) return;
        var g = UnityEngine.Object.FindAnyObjectByType<BushidoPrototype>(); if (g == null) return;
        string saved = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
        try
        {
            Time.timeScale = 0;
            if (Application.isBatchMode) ((Camera)Get(g, "view")).aspect = 16f / 9f;
            EditorApplication.update -= Tick;

            // 1. Opening: waves for 12 s, then the duel; the 35 s auto-win never fires.
            Fresh(g, 99);
            Sim(g, 11.5f);
            int wavesAtEleven = Int(g, "waveNumber");
            var slots = (Transform[])Get(g, "villageLanterns");
            float ordinary = slots[0].localScale.x / ((float[])Get(g, "hangScale"))[0] * 1f;
            bool disguised = true;
            for (int i = 0; i < 2; i++)
            {
                var sprite = slots[4 + i]; var size = sprite.localScale.x / ordinary;
                disguised &= !Twin(g, i).holder.gameObject.activeSelf && sprite.gameObject.activeInHierarchy && size >= 0.8f && size <= 1.15f
                    && sprite.GetComponentInChildren<Renderer>().sharedMaterial.GetTexture("_BaseMap") == slots[0].GetComponentInChildren<Renderer>().sharedMaterial.GetTexture("_BaseMap");
            }
            bool volume = true;
            foreach (var slot in slots)
            {
                var mesh = slot.GetComponentInChildren<MeshFilter>().sharedMesh;
                volume &= mesh.bounds.size.z > .5f;
            }
            Check(volume, "Los seis faroles tienen volumen real, incluidos los disfraces de los gemelos");
            var beforePause = slots[0].position;
            Call(g, "UpdateVillageLanterns", true, 0f);
            Check(Vector3.Distance(beforePause, slots[0].position) < .0001f, "El balanceo no avanza con delta cero");
            Check(disguised, "Durante la apertura los gemelos son dos faroles de papel corrientes (mismo modelo y tamaño), sin revelar los yokai");
            Check(Int(g, "lanternStage") == 0 && wavesAtEleven > 0 && Int(g, "litTwin") == -1, "Los primeros 12 s son de ranas (oleadas " + wavesAtEleven + ") y ningún farol está encendido");
            Sim(g, 1.5f);
            Check(Int(g, "lanternStage") == 1 && Int(g, "litTwin") == 0, "A los 12 s empieza el duelo y se enciende el farol blanco");
            Check(((IList)Get(g, "pendingFrogs")).Count == 0, "Al empezar el duelo no quedan ranas por lanzar");
            int wavesAfter = Int(g, "waveNumber");
            Sim(g, 30f);
            Check(Int(g, "waveNumber") == wavesAfter, "Tras los 12 s no salen más oleadas de ranas");
            Check(Get(g, "mode").ToString() == "Battle" && Flt(g, "battleTime") > 36f, "El auto-triunfo de 35 s no se aplica al nodo 3 (tiempo " + Flt(g, "battleTime").ToString("0.0") + ")");
            int hints = ((HashSet<string>)Get(g, "lanternHints")).Count;
            Check(hints == 4, "Las cuatro pistas salen una vez cada una (" + hints + ")");
            Check(Int(g, "lanternTurn") >= 2, "El farol pasa la llama: turno " + Int(g, "lanternTurn"));

            // 1b. El despertar: el farol se hincha, el papel se rasga, el yokai 3D sustituye al sprite y la cuerda cede.
            Fresh(g, 99);
            Set(g, "battleTime", 12f); Sim(g, 0.25f);
            slots = (Transform[])Get(g, "villageLanterns");
            var hangScale = (float[])Get(g, "hangScale"); var hangLoad = (float[])Get(g, "hangLoad");
            Check(Flt(g, "awakenT") > 0f && slots[4].gameObject.activeSelf && !Twin(g, 0).holder.gameObject.activeSelf && hangScale[4] > 1.25f, "Despertar: el farol corriente se hincha antes de rasgarse (escala " + hangScale[4].ToString("0.00") + ")");
            float sagBefore = hangLoad[4];
            Sim(g, 0.3f);
            Check(!slots[4].gameObject.activeSelf && Twin(g, 0).holder.gameObject.activeSelf && Twin(g, 1).holder.gameObject.activeSelf && hangLoad[4] > sagBefore, "Despertar: el yokai 3D sustituye al sprite y la cuerda carga su peso");
            Sim(g, 0.7f);
            Check(Twin(g, 0).Glow > 0.3f && Int(g, "litTwin") == 0, "Despertar: al final del golpe el gemelo blanco se enciende");

            // 1c. La cuerda cede donde cuelga el gemelo cuando lanza la lengua.
            Duel(g);
            Sim(g, 3f);
            float hookU = ((float[])Get(g, "hangU"))[4];
            float y0 = ((Vector3)Call(g, "RopePoint", hookU)).y;
            Call(g, "StartLash"); Sim(g, 0.12f);
            float y1 = ((Vector3)Call(g, "RopePoint", hookU)).y;
            Check(Mathf.Abs(y1 - y0) > 0.02f, "El punto de la cuerda del gemelo se mueve al empezar el lengüetazo (" + (y1 - y0).ToString("0.000") + ")");

            // 1d. Faroles del pueblo: se encienden de uno en uno al bajar la vida de los gemelos.
            Duel(g, 99);
            var litTo = (float[])Get(g, "lanternLitTo");
            Check(litTo[0] + litTo[1] + litTo[2] + litTo[3] == 0f, "Al empezar el duelo ningún farol del pueblo está encendido");
            Call(g, "Parry", Twin(g, 0).Points[5]); CutBody(g, 0); CutBody(g, 0); Sim(g, 0.1f);
            Check(litTo[0] + litTo[1] + litTo[2] + litTo[3] == 1f, "Con 2 de los 8 puntos de vida perdidos se enciende un farol del pueblo");

            // 2. El farol apagado rebota: sin efecto ni castigo.
            Duel(g);
            int hits0 = Int(g, "hits"), lives0 = Int(g, "lives"), misses0;
            CutBody(g, 1);
            Check(Int(g, "hits") == hits0 && Int(g, "lives") == lives0 && ((int[])Get(g, "twinHealth"))[1] == 4, "Cortar el farol apagado no hace nada");
            CutBody(g, 0);
            Check(((int[])Get(g, "twinHealth"))[0] == 4 && Int(g, "hits") == hits0, "Un corte al farol encendido sin aturdir tampoco cuenta");

            // 3. Lengüetazo sin parar: hiere.
            Duel(g);
            Call(g, "StartLash");
            Check(SimUntil(g, () => Act(g) == "Lash", 2f), "El lengüetazo pasa por el aviso y llega a la lengua");
            lives0 = Int(g, "lives"); misses0 = Int(g, "misses");
            Sim(g, 0.5f);
            Check(Int(g, "lives") == lives0 - 1 && Int(g, "misses") == misses0 + 1, "Un lengüetazo sin parar hiere y cuenta un fallo");

            // 4. Corte transversal durante el lengüetazo: aturde. Corte a lo largo: no.
            Duel(g);
            Call(g, "StartLash"); SimUntil(g, () => Act(g) == "Lash", 2f); Sim(g, 0.15f);
            Across(g, Twin(g, 0), true);
            Check(Act(g) == "Lash", "Cortar a lo largo de la lengua no la para");
            hits0 = Int(g, "hits"); lives0 = Int(g, "lives"); float charge0 = Flt(g, "judgmentCharge");
            Across(g, Twin(g, 0), false);
            Check(Act(g) == "Stunned" && Mathf.Abs(Flt(g, "stunLeft") - 2.2f) < 0.01f && Int(g, "hits") == hits0 + 1, "Un corte que cruza la lengua la para y aturde 2,2 s");
            Check(Mathf.Abs(Flt(g, "judgmentCharge") - charge0 - 1f / 3f) < 0.001f, "El parry da la carga de desvío del juicio");
            Sim(g, 0.6f);
            Check(Int(g, "lives") == lives0, "Parado el lengüetazo no hiere");
            Check(Int(g, "litTwin") == 0 && Twin(g, 0).Glow > 0.3f && Twin(g, 0).pose == LanternActor.Pose.Stunned, "Aturdido por parry: sigue siendo el farol encendido, con brillo tenue (" + Twin(g, 0).Glow.ToString("0.00") + ")");

            // 5. Cortes al cuerpo: solo aturdido y máximo 2 por aturdimiento.
            hits0 = Int(g, "hits");
            CutBody(g, 0); CutBody(g, 0); CutBody(g, 0);
            Check(((int[])Get(g, "twinHealth"))[0] == 2 && Int(g, "hits") == hits0 + 2, "Aturdido admite 2 cortes al cuerpo y el tercero no cuenta");

            // 6. Llamas: blanca se corta (acierto), roja sin guardia hiere, blanca escapada es fallo.
            Duel(g);
            hits0 = Int(g, "hits"); lives0 = Int(g, "lives"); charge0 = Flt(g, "judgmentCharge");
            Call(g, "SpawnLanternFlame", new Vector3(0, 3, 0), Vector3.zero, false);
            Cut(g, new Vector3(-2, 3, 0), new Vector3(2, 3, 0));
            Check(Int(g, "hits") == hits0 + 1 && Int(g, "lives") == lives0 && Mathf.Abs(Flt(g, "judgmentCharge") - charge0 - 1f / 12f) < 0.001f, "Llama blanca cortada: acierto, sin daño y carga de tajo");
            Call(g, "SpawnLanternFlame", new Vector3(0, 3, 0), Vector3.zero, true);
            Cut(g, new Vector3(-2, 3, 0), new Vector3(2, 3, 0));
            Check(Int(g, "lives") == lives0 - 1, "Llama roja cortada sin guardia hiere");
            misses0 = Int(g, "misses");
            Call(g, "SpawnLanternFlame", new Vector3(0, -1.7f, 0), new Vector3(0, -3, 0), false);
            Sim(g, 0.1f);
            Check(Int(g, "misses") == misses0 + 1, "Llama blanca que escapa cuenta un fallo");
            misses0 = Int(g, "misses");
            Call(g, "SpawnLanternFlame", new Vector3(0, -1.7f, 0), new Vector3(0, -3, 0), true);
            Sim(g, 0.1f);
            Check(Int(g, "misses") == misses0, "Llama roja que escapa no cuenta fallo");

            // 7. Relevo: cortar la llama del cable apaga a los dos (2 cortes cada uno); sin cortar, el gemelo se enciende.
            Duel(g);
            Call(g, "StartRelay"); Sim(g, 0.4f);
            Transform flame = (Transform)Get(g, "relayFlame");
            Check(flame != null, "Al pasar la llama aparece un fuego en el cable");
            Vector3 fp = flame.position; fp.z = 0;
            Cut(g, fp + Vector3.left, fp + Vector3.right);
            Check(Flt(g, "blackoutLeft") > 2.9f && (bool)Call(g, "LanternStunned", 0) && (bool)Call(g, "LanternStunned", 1) && Get(g, "relayFlame") == null, "Cortar la llama del cable causa APAGÓN: los dos aturdidos 3 s");
            for (int i = 0; i < 2; i++) { CutBody(g, i); CutBody(g, i); CutBody(g, i); }
            var health = (int[])Get(g, "twinHealth");
            Check(health[0] == 2 && health[1] == 2, "En el apagón cada gemelo admite 2 cortes (vida " + health[0] + "/" + health[1] + ")");
            Sim(g, 3.1f);
            Check(Flt(g, "blackoutLeft") <= 0 && Int(g, "litTwin") == 1, "Pasado el apagón se enciende el gemelo que recibía la llama");
            Duel(g);
            Call(g, "StartRelay"); Sim(g, 1.2f);
            Check(Int(g, "litTwin") == 1 && Get(g, "relayFlame") == null && Flt(g, "blackoutLeft") <= 0, "Sin cortar la llama, el gemelo rojo se enciende");

            // 8. Corte del juicio durante el duelo: apagón.
            Duel(g);
            Call(g, "JudgmentBanish");
            Check(Flt(g, "blackoutLeft") > 2.9f, "El corte del juicio durante el duelo causa APAGÓN");

            // 9. Cuatro cortes purifican; fase 2; victoria y nodo 3 pasado con cualquier rango.
            Duel(g, 99);
            for (int round = 0; round < 2; round++) { Call(g, "Parry", Twin(g, 0).Points[5]); CutBody(g, 0); CutBody(g, 0); }
            var purified = (bool[])Get(g, "twinPurified");
            Check(purified[0] && ((int[])Get(g, "twinHealth"))[0] == 0, "4 cortes al cuerpo (2 por aturdimiento) purifican al farol");
            Sim(g, 0.1f);
            litTo = (float[])Get(g, "lanternLitTo");
            Check(litTo[0] + litTo[1] + litTo[2] + litTo[3] == 2f, "Con medio duelo (4 de 8 puntos) hay dos faroles del pueblo encendidos");
            Check(Prop(g, "LanternPhase2"), "Al caer uno empieza la fase 2");
            Sim(g, 1.0f);
            Check(Int(g, "litTwin") == 1, "El superviviente conserva la llama");
            var plan = Plan(g);
            Check(plan.Count == 3 && plan[2] == "Flicker", "Fase 2: el ciclo acaba en parpadeo de 1,2 s (" + string.Join(",", plan) + ")");
            bool sawWind = false, sawSpit = false;
            SimUntil(g, () =>
            {
                if (Act(g) == "WindUp" && !sawWind) { sawWind = true; Check(Mathf.Abs(Twin(g, 1).poseLen - 0.5f) < 0.001f && Int(g, "lashesLeft") == 2, "Fase 2: aviso de 0,5 s y dos lengüetazos"); }
                if (Act(g) == "Spit" && !sawSpit) { sawSpit = true; Check(Int(g, "spitsLeft") == 5, "Fase 2: salvas de 5 llamas"); }
                return sawWind && sawSpit;
            }, 20f);
            Check(sawWind && sawSpit, "Fase 2: el superviviente alterna lengüetazos y llamas");
            Set(g, "hits", 0); Set(g, "misses", 50); Set(g, "wounds", 3);
            var progress = Get(g, "progress"); progress.GetType().GetField("unlockedThrough").SetValue(progress, 3);
            for (int round = 0; round < 2; round++) { Call(g, "Parry", Twin(g, 1).Points[5]); CutBody(g, 1); CutBody(g, 1); }
            Check(Int(g, "lanternStage") == 2 && Get(g, "mode").ToString() == "Battle", "Los dos purificados: espera antes de presentar la katana");
            litTo = (float[])Get(g, "lanternLitTo");
            Check(litTo[0] + litTo[1] + litTo[2] + litTo[3] == 4f, "En la victoria los cuatro faroles del pueblo están encendidos");
            Sim(g, 4f);
            Check(Int(g, "bossFinalStage") == 1 && Get(g, "mode").ToString() == "Battle", "La victoria espera al desenvaine");
            Camera camera = (Camera)Get(g, "view");
            Vector2 grip = camera.WorldToScreenPoint((Vector3)Get(g, "drawHiltPosition"));
            Transform saya = (Transform)Get(g, "drawSaya");
            Vector2 axis = ((Vector2)camera.WorldToScreenPoint(saya.position + saya.up) - (Vector2)camera.WorldToScreenPoint(saya.position)).normalized;
            Vector3 end = camera.ScreenToWorldPoint(new Vector3(grip.x + axis.x * Screen.height * .23f, grip.y + axis.y * Screen.height * .23f, 8f));
            Set(g, "strokeStart", grip);
            Call(g, "BeginDrawGrip", grip + Vector2.down * Screen.height * .4f);
            Call(g, "BossGesture", saya.position, end);
            Check(Int(g, "bossFinalStage") == 1, "Un gesto fuera del mango no ejecuta a los faroles");
            Call(g, "BeginDrawGrip", grip);
            Call(g, "BossGesture", saya.position, end);
            float wideShot = camera.orthographicSize;
            Sim(g, .52f);
            var spirits = (Transform[])Get(g, "releasedFlames");
            Check(spirits[0] != null && spirits[1] == null && camera.orthographicSize < wideShot, "El encuadre se acerca y el tajo alcanza primero al farol blanco");
            Sim(g, .3f);
            Check(spirits[1] != null && Twin(g, 1).holder.gameObject.activeSelf, "La llama roja se libera mientras cae su farol");
            Sim(g, 1.15f);
            Check(!Twin(g, 0).holder.gameObject.activeSelf && !Twin(g, 1).holder.gameObject.activeSelf, "Ambos cuerpos caen antes de la reunión de las llamas");
            Sim(g, 1.3f);
            Check(Mathf.Abs(camera.orthographicSize - wideShot) < .001f, "La cámara recupera su encuadre al terminar");
            Check(Get(g, "mode").ToString() == "Result" && (bool)Get(g, "won"), "Ambos purificados: victoria");
            Check(((bool[])progress.GetType().GetField("completed").GetValue(progress))[3] && (int)progress.GetType().GetField("unlockedThrough").GetValue(progress) == 4 && Get(g, "resultRank").ToString() == "C",
                "El nodo 3 se registra como pasado aunque el rango sea C");
            var actor = Twin(g, 0);
            actor.SetPose(LanternActor.Pose.Spit, .35f);
            actor.Update(.35f, 1f);
            bool finite = true;
            foreach (var point in actor.Points)
                finite &= !float.IsNaN(point.sqrMagnitude) && !float.IsInfinity(point.sqrMagnitude);
            Check(finite, "La lengua conserva posiciones válidas al terminar de escupir");
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); failed = true; Debug.LogException(e); }
        if (saved != null) PlayerPrefs.SetString(SaveKey, saved); else PlayerPrefs.DeleteKey(SaveKey);
        Finish();
    }

    static void Finish()
    {
        Directory.CreateDirectory(Folder);
        int pass = 0, fail = 0;
        foreach (string line in report.ToString().Split('\n')) { if (line.StartsWith("PASS")) pass++; else if (line.StartsWith("FAIL")) fail++; }
        report.AppendLine("RESUMEN: " + pass + " PASS, " + fail + " FAIL");
        File.WriteAllText(Path.Combine(Folder, "farol-validation.txt"), report.ToString());
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Time.timeScale = 1;
        if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0); else EditorApplication.ExitPlaymode();
    }

    // ---- Capturas con HUD ----
    [MenuItem("Bushido/Revisión/Capturar duelo de faroles")]
    public static void RunShots()
    {
        SessionState.SetBool(ShotsKey, true); ticks = 0;
        EditorApplication.update -= ShotsTick; EditorApplication.update += ShotsTick;
        if (EditorApplication.isPlaying) return;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CampaignMap.unity");
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        EditorApplication.EnterPlaymode();
    }

    static void ShotsTick()
    {
        if (!EditorApplication.isPlaying || ++ticks < 90) return;
        var g = UnityEngine.Object.FindAnyObjectByType<BushidoPrototype>(); if (g == null) return;
        EditorApplication.update -= ShotsTick; SessionState.SetBool(ShotsKey, false);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        g.StartCoroutine(ShotSequence(g));
    }

    static IEnumerator Frames(float seconds)
    {
        float t = 0;
        while (t < seconds) { yield return null; t += Mathf.Min(Time.deltaTime, 0.05f); }
    }

    static IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG());
        var gm = UnityEngine.Object.FindAnyObjectByType<BushidoPrototype>();
        try { Debug.Log("Duelo: " + name + " litTwin=" + Get(gm, "litTwin") + " act=" + Get(gm, "lact") + " blackout=" + Get(gm, "blackoutLeft") + " glow0=" + Twin(gm, 0).Glow + " glow1=" + Twin(gm, 1).Glow + " pose0=" + Twin(gm, 0).pose); }
        catch (Exception) { Debug.Log("Captura: " + name); }
        UnityEngine.Object.DestroyImmediate(image);
    }

    static void Purify(BushidoPrototype g, int i)
    {
        Set(g, "litTwin", i);
        for (int r = 0; r < 2; r++) { Call(g, "Parry", Twin(g, i).Points[5]); CutBody(g, i); CutBody(g, i); }
    }

    static IEnumerator ShotSequence(BushidoPrototype g)
    {
        string saved = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
        Directory.CreateDirectory(Folder);
        Set(g, "mission", 3); Call(g, "BeginBattle"); Set(g, "paused", false); Set(g, "lives", 99);
        yield return Frames(1.5f);
        Set(g, "battleTime", 5.6f);                      // the white twin's foreshadow twitch is at 6 s: the opening, twins still ordinary lanterns
        yield return Frames(0.75f);
        yield return Shot("duelo-inicio");
        Set(g, "waveTimer", 100f);                        // no new waves; frogs already in the air finish their arcs
        for (float w = 0; w < 5f && ((IList)Get(g, "projectiles")).Count > 0; w += Time.deltaTime) yield return null;
        Set(g, "battleTime", 11.92f);
        yield return Frames(0.34f);                      // the swell of the awakening
        yield return Shot("duelo-despertar");
        yield return Frames(0.25f);                      // the paper has torn, the yokai is out
        yield return Shot("duelo-despertar-rasgado");
        yield return Frames(1.6f);                       // white lit and its first hint
        ClearPlan(g);
        Set(g, "lanternPath", 1); Call(g, "StartLash");
        yield return Frames(0.62f);
        yield return Shot("duelo-telegrafo");
        yield return Frames(0.2f);
        yield return Shot("duelo-latigazo");
        Call(g, "Parry", Twin(g, 0).Points[8]);
        yield return Frames(0.9f);
        yield return Shot("duelo-aturdido");
        Call(g, "LightTwin", 0); ClearPlan(g);
        yield return Frames(1.6f);
        Call(g, "StartRelay");
        yield return Frames(0.5f);
        yield return Shot("duelo-relevo");
        yield return Frames(0.8f);
        Call(g, "LightTwin", 0); ClearPlan(g);
        yield return Frames(1.6f);
        Call(g, "StartSpit"); Set(g, "spitsLeft", 4); Set(g, "spitIndex", 0);
        Call(g, "SpawnLanternFlame", new Vector3(-1.5f, 4.6f, 0), new Vector3(1.0f, 0.3f, 0), true);
        Call(g, "SpawnLanternFlame", new Vector3(2.8f, 3.8f, 0), new Vector3(-0.8f, 0.3f, 0), true);
        yield return Frames(1.3f);
        yield return Shot("duelo-fuegos");
        Call(g, "LanternBlackout");
        yield return Frames(1.0f);
        yield return Shot("duelo-apagon");
        yield return Frames(2.3f);
        Purify(g, 0);
        yield return Frames(3.0f);
        Set(g, "lives", 99);
        yield return Shot("duelo-fase2");
        Purify(g, 1);
        yield return Frames(1.0f);
        yield return Shot("duelo-purificado");
        Set(g, "mission", 4); Call(g, "BeginBattle"); Set(g, "paused", false); Set(g, "lives", 99);   // a boss fight: the same rope holds the cursed mask
        yield return Frames(1.0f);
        Set(g, "theaterState", 2); Set(g, "theaterTime", 0.3f); Set(g, "paperOpening", 1f);   // the sheet is open: the mask hangs from the rope
        yield return Frames(0.6f);
        yield return Shot("jefe-cuerda");
        if (saved != null) PlayerPrefs.SetString(SaveKey, saved); else PlayerPrefs.DeleteKey(SaveKey);
        if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
    }
}
