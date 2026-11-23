using System.Collections.Generic;
using UnityEngine;

namespace Bushido
{
    // Los jefes hablan en combate: un bocadillo de papel que sale de quien habla,
    // con su nombre en un sello rojo y el texto apareciendo letra a letra.
    public sealed partial class BushidoPrototype
    {
        // Who speaks: the umbrella, or one of the lantern twins (0 white, 1 red).
        const int VoiceUmbrella = -1;
        struct BossLine { public int speaker; public string text; public float seconds; }

        static readonly string[] UmbrellaIntro = {
            "¿Otro que viene a cerrarme? Suerte con eso.",
        };
        static readonly string[] UmbrellaAttack = {
            "¡A bailar bajo la lluvia!",
            "Con una pierna me sobra.",
            "¡Te voy a dejar empapado!",
            "¿Me sigues el paso?",
        };
        static readonly string[] UmbrellaTaunt = { "¡Ja! Eso fue solo una gota.", "Ni paraguas traes.", "¡Chof!" };
        static readonly string[] UmbrellaHurt = { "¡Oye, que se me tuercen las varillas!", "¡Mi papel!", "Eso dolió más que el granizo." };
        const string UmbrellaCornered = "Espera... ¿me vas a cerrar? Hace cien años que nadie me cerraba.";

        static readonly string[] LanternLit = {
            "Ahora brillo yo.", "¡Mi turno! Hermano, mira y aprende.",
            "¡Me toca a mí!", "Apártate, que esta llama es mía.",
        };
        static readonly string[] LanternTaunt = { "¡Chamuscado!", "¿Quema? Pues espera." };
        const string LanternParried = "¡Ay, mi lengua!";
        const string LanternBlackoutLine = "¿Quién apagó la luz?";

        readonly Queue<BossLine> bossVoiceQueue = new Queue<BossLine>();
        BossLine bossVoice;
        float bossVoiceAge = -1f, bossIntroIn = -1f;
        int bossVoiceTurn;
        GUIStyle bossVoiceText, bossVoiceName;

        // Queue a line; taunts that would pile up are dropped instead of delaying the fight's real cues.
        void BossSay(int speaker, string text, float seconds = 2.8f)
        {
            if (bossVoiceQueue.Count >= 2) return;
            bossVoiceQueue.Enqueue(new BossLine { speaker = speaker, text = text, seconds = seconds });
        }

        string BossPick(string[] lines) => lines[bossVoiceTurn++ % lines.Length];

        void ResetBossVoice()
        {
            bossVoiceQueue.Clear(); bossVoiceAge = -1f; bossVoiceTurn = 0;
            bossIntroIn = IsBoss ? 1.3f : -1f;
        }

        string BossVoiceName(int speaker) => speaker == VoiceUmbrella ? "Sombrilla" : speaker == 0 ? "Farol blanco" : "Farol rojo";

        // Screen point (1280x720) of the speaker's head; the umbrella speaks from behind the paper while hidden.
        Vector2 BossVoiceAnchor(int speaker)
        {
            Vector3 world = new Vector3(0, 6.3f, 0);
            if (speaker == VoiceUmbrella && opponent != null && opponent.gameObject.activeInHierarchy) world = opponent.position + Vector3.up * 3.1f;
            else if (speaker >= 0 && twins[speaker] != null) world = twins[speaker].Body + Vector3.up * .9f;
            Vector3 screen = view.WorldToScreenPoint(world);
            return new Vector2(screen.x * 1280f / Screen.width, 720f - screen.y * 720f / Screen.height);
        }

        void DrawBossVoice()
        {
            if (mode != Mode.Battle || (!IsBoss && !LanternDuel)) return;
            if (Event.current.type == EventType.Repaint && !paused)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
                if (bossIntroIn > 0 && (bossIntroIn -= dt) <= 0) BossSay(VoiceUmbrella, UmbrellaIntro[0], 3.2f);
                if (bossVoiceAge >= 0) bossVoiceAge += dt;
                if (bossVoiceAge > bossVoice.seconds) bossVoiceAge = -1f;
                if (bossVoiceAge < 0 && bossVoiceQueue.Count > 0) { bossVoice = bossVoiceQueue.Dequeue(); bossVoiceAge = 0; }
            }
            if (bossVoiceAge < 0) return;
            if (bossVoiceText == null)
            {
                bossVoiceText = new GUIStyle(textStyle) { fontSize = 19, wordWrap = true, alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Bold };
                bossVoiceText.normal.textColor = PrototypeArt.Ink;
                bossVoiceName = new GUIStyle(hudCap) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                bossVoiceName.normal.textColor = PrototypeArt.Paper;
            }
            float age = bossVoiceAge, life = bossVoice.seconds;
            // Pop in with a little overshoot, fade on the way out.
            float pop = Mathf.Clamp01(age / .18f);
            float scale = pop < 1f ? Mathf.Lerp(.6f, 1.06f, pop) : Mathf.Lerp(1.06f, 1f, Mathf.Clamp01((age - .18f) / .1f));
            float fade = Mathf.Clamp01((life - age) / .3f);
            int shown = Mathf.Min(bossVoice.text.Length, Mathf.FloorToInt(age * 38f));
            Vector2 head = BossVoiceAnchor(bossVoice.speaker);
            const float width = 300f;
            float height = Mathf.Max(52f, bossVoiceText.CalcHeight(new GUIContent(bossVoice.text), width - 36f) + 26f);
            // Bubble sits up and to the side of the speaker, kept on the page and clear of the top HUD.
            float side = bossVoice.speaker == 1 ? -1f : 1f;
            float x = Mathf.Clamp(head.x + side * 40f - (side < 0 ? width : 0), 24f, 1280f - width - 24f);
            float y = Mathf.Clamp(head.y - height - 40f, 128f, 560f - height);
            var box = new Rect(x, y, width, height);
            Matrix4x4 saved = GUI.matrix; Color tint = GUI.color;
            GUI.color = new Color(1, 1, 1, fade);
            Pivot(new Vector2(box.center.x, box.yMax), side * -1.5f, scale);
            // Tail: an ink brush stroke from the card toward the speaker's head.
            Vector2 root = new Vector2(Mathf.Clamp(head.x, box.x + 30, box.xMax - 30), box.yMax - 4);
            Vector2 tip = Vector2.MoveTowards(root, head, Mathf.Min(46f, Vector2.Distance(root, head) * .6f));
            Stroke(root, tip, 16f, PrototypeArt.Ink, true);
            Stroke(root + new Vector2(0, -3), tip, 8f, PrototypeArt.Paper, true);
            PaperCard(box);
            GUI.Label(new Rect(box.x + 18, box.y + 14, width - 36, height - 20), bossVoice.text.Substring(0, shown), bossVoiceText);
            // Name stamp on the card's top edge.
            string name = BossVoiceName(bossVoice.speaker);
            float stamp = bossVoiceName.CalcSize(new GUIContent(name)).x + 26f;
            var tag = new Rect(box.x + 14, box.y - 15, stamp, 26);
            Panel(new Rect(tag.x + 3, tag.y + 3, tag.width, tag.height), Ink(.3f));
            Panel(tag, bossVoice.speaker == 0 ? PrototypeArt.Ink : PrototypeArt.Red);
            GUI.Label(tag, name, bossVoiceName);
            GUI.matrix = saved; GUI.color = tint;
        }
    }
}
