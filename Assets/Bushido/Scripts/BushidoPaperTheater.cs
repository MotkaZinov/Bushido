using UnityEngine;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        Transform[] paperWings;
        Transform[] bambooLayers;
        Transform replacementPaper;
        Transform cloudLayer;
        float paperOpening, theaterTime;
        int theaterState; // 0 waves, 1 warning, 2 attack, 3 replacement
        bool theaterStrike;
        int duelBeat, duelBlocks;
        bool duelOpeningSeal;
        LineRenderer parryFixedRing, parrySwipeMark;

        void BeginPaperTheater()
        {
            paperOpening = theaterTime = 0;
            theaterState = 0;
            if (backdrop != null)
            {
                var backing = backdrop.GetComponent<Renderer>().sharedMaterial;
                backing.SetTexture("_BaseMap", Resources.Load<Texture2D>(IsBoss ? "Backgrounds/KamishibaiAldea" : "ThirdParty/PolyHaven/LacqueredCherryWood"));
                backing.SetColor("_BaseColor", Color.white);
                backing.SetTextureScale("_BaseMap", IsBoss ? Vector2.one : new Vector2(2, 1));
                backing.SetFloat("_PaperSurface", IsBoss ? 1 : 0);
                backing.SetFloat("_InkShading", 0);
            }
            replacementPaper = art.Backdrop(world, "Nueva hoja del teatro", "KamishibaiPradera");
            replacementPaper.localScale = new Vector3(24, 11, 1);
            replacementPaper.gameObject.SetActive(false);
            paperWings = new Transform[2];
            for (int side = 0; side < 2; side++)
                paperWings[side] = art.TornPaper(world, side == 0, new Vector3(0, 4.1f, 2f));
            bambooLayers = new Transform[2];
            string[] layers = { "KamishibaiRocas", "KamishibaiBambu", "KamishibaiNubes" };
            for (int i = 0; i < layers.Length; i++)
            {
                var layerRoot = new GameObject("Lámina recortada " + layers[i]).transform;
                layerRoot.SetParent(world, false);
                var shadow = art.Backdrop(layerRoot, "Sombra entre papeles", layers[i]);
                shadow.localPosition = new Vector3(0.08f, 4.02f, 1.85f - i * 0.3f);
                shadow.localScale = new Vector3(24, 11, 1);
                shadow.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", new Color(0.42f, 0.38f, 0.29f));
                var sheet = art.Backdrop(layerRoot, "Ilustración recortada", layers[i]);
                sheet.localPosition = new Vector3(0, 4.1f, 1.8f - i * 0.3f);
                sheet.localScale = new Vector3(24, 11, 1);
                if (i < 2) bambooLayers[i] = layerRoot;
                else cloudLayer = layerRoot;
            }
            if (opponent != null) opponent.gameObject.SetActive(false);
            jutsuFxDanger = 0; jutsuFxStruck = jutsuFxTorn = false; jutsuFxDangerWash = null;
            if (!IsBoss) return;
            // Red ink creeping in from the top and bottom edges: the boss attack tell.
            jutsuFxDangerWash = new LineRenderer[2];
            float halfWidth = view.orthographicSize * view.aspect * 1.1f;
            for (int side = 0; side < 2; side++)
            {
                var points = new Vector3[9];
                for (int i = 0; i < points.Length; i++)
                    points[i] = new Vector3((i / 8f * 2 - 1) * halfWidth,
                        4 + (side == 0 ? 1 : -1) * (view.orthographicSize + Mathf.Sin(i * 2.3f + side) * 0.12f), -1f);
                var wash = art.Line(world, points, Color.Lerp(PrototypeArt.Red, PrototypeArt.Ink, 0.35f), 0);
                wash.widthCurve = new AnimationCurve(new Keyframe(0, 0.7f), new Keyframe(0.2f, 1), new Keyframe(0.42f, 0.72f),
                    new Keyframe(0.63f, 0.95f), new Keyframe(0.85f, 0.7f), new Keyframe(1, 0.9f));
                wash.enabled = false;
                jutsuFxDangerWash[side] = wash;
            }
        }
        LineRenderer[] jutsuFxDangerWash;
        float jutsuFxDanger;
        bool jutsuFxStruck, jutsuFxTorn;
        const float JutsuFxTearHold = 0.45f;

        void BeginTheaterAttack()
        {
            theaterState = 1;
            theaterTime = 0;
            duelBeat = duelBlocks = 0;
            theaterStrike = false;
            pendingFrogs.Clear();
            pendingBombDelay = -1f;
            foreach (Projectile item in projectiles) Destroy(item.root.gameObject);
            projectiles.Clear();
            ClearJutsuSeal();
            StartDuelRound();
        }
        void UpdatePaperTheater(float delta)
        {
            if (paperWings == null || paperWings[0] == null) return;
            if(IsBoss)UpdateBossDuel(delta);
            if(mode!=Mode.Battle)return;
            // Anticipation: the closed sheet shakes harder and harder before it tears open.
            bool hold = DuelActive && duelPhase == DuelPhase.Charge && duelClock < JutsuFxTearHold && paperOpening <= 0;
            float target = vulnerable || (theaterState == 2 && !hold) || theaterState == 3 || paperOpening > 0 ? 1f : 0f;
            JutsuFxTheaterTells(delta);
            replacementPaper.gameObject.SetActive(theaterState == 3 && flyingSeal==null && bossReaction!=BossReaction.Seal);
            replacementPaper.localPosition = new Vector3(0, 4.1f + 11 * (1 - Mathf.SmoothStep(0, 1, theaterTime / 0.55f)), 1.99f);
            paperOpening = Mathf.MoveTowards(paperOpening, target, delta * 2.8f);
            for (int side = 0; side < 2; side++)
            {
                paperWings[side].gameObject.SetActive(paperOpening<.98f);
                float sign = side == 0 ? -1 : 1;
                float tremble = Mathf.Sin(theaterTime * 45) * 0.09f * (hold ? duelClock / JutsuFxTearHold : theaterState == 1 ? 0.4f : 0);
                float flutter = Mathf.Sin(theaterTime * 19 + side) * paperOpening * (1 - paperOpening) * 14;
                float ease = 1 - Mathf.Pow(1 - paperOpening, 3);
                paperWings[side].localPosition = new Vector3(sign * ease * 3.2f + tremble, 4.1f - paperOpening * paperOpening * 18f, 2 - ease * 0.2f);
                paperWings[side].localRotation = Quaternion.Euler(flutter, sign * ease * 35, sign * ease * 65 + flutter * 0.3f + tremble * 5f);
            }
            if (!jutsuFxTorn && paperOpening > 0)
            {
                jutsuFxTorn = true;
                JutsuFxShreds(new Vector3(0, 4.1f, -1f), 8, PrototypeArt.Paper, PrototypeArt.Ink, 4f, 0.8f);
            }
            if (opponent != null)
            {
                opponent.gameObject.SetActive(paperOpening > 0.08f && (vulnerable || theaterState == 2 || flyingSeal!=null || bossReaction==BossReaction.Seal));
            }
            for (int i = 0; i < bambooLayers.Length; i++)
                bambooLayers[i].localPosition = Vector3.right * Mathf.Sin(battleTime * 0.45f + i) * (0.035f + i * 0.025f) + Vector3.down * (paperOpening * paperOpening * 18f);
            cloudLayer.localPosition = Vector3.right * Mathf.Sin(battleTime * 0.08f) * 0.7f + Vector3.up * (paperOpening * 13f);
        }

        // Boss duel tells: the wash rises through the charge and each wind-up, calms once the
        // guard is drawn, and a hit that lands slams it shut with red and ink paper shreds.
        void JutsuFxTheaterTells(float delta)
        {
            if (jutsuFxDangerWash == null) return;
            bool struck = DuelActive && duelPhase == DuelPhase.Recovery;
            if (struck && !jutsuFxStruck && !duelPrepared)
            {
                jutsuFxDanger = 1.3f;
                JutsuFxShreds(CounterCenter + Vector3.back * 1.5f, 10, PrototypeArt.Red, PrototypeArt.Ink, 5f, 0.7f);
            }
            jutsuFxStruck = struck;
            float danger = 0;
            if (DuelActive && duelPhase == DuelPhase.Charge) danger = 0.55f * Mathf.Clamp01(duelClock / 0.3f);
            else if (DuelActive && duelPhase == DuelPhase.Telegraph)
            {
                float wind = Mathf.Clamp01(duelClock / DuelStrikeDelay);
                danger = (0.25f + 0.75f * wind * wind * wind) * (duelPrepared ? 0.4f : 1f);
            }
            jutsuFxDanger = Mathf.MoveTowards(jutsuFxDanger, danger, delta * (danger > jutsuFxDanger ? 4f : 2.5f));
            float width = jutsuFxDanger * 2.2f * (1 + Mathf.Sin(theaterTime * 18f) * 0.08f);
            foreach (LineRenderer wash in jutsuFxDangerWash)
            {
                wash.enabled = jutsuFxDanger > 0.01f;
                wash.widthMultiplier = width;
            }
        }
    }
}






