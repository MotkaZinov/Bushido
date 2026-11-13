using UnityEngine;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        int bossSeals, bossFinalStage;
        float bossFinalTime, sealFlightTime, mapPaperExit = -1;
        bool bossInkBurst, finalCutReleased;
        Vector3 finalCutDirection;
        Transform flyingSeal, pendingSeal;
        Vector3 sealLaunchScale;
        Vector3 sealLaunchPosition;
        int landedBossSeals;
        Vector3 drawHiltPosition;
        float drawPullProgress;
        readonly Vector3[] bossPattern = { new Vector3(-1, .6f), new Vector3(0, -.6f), new Vector3(1, .6f), new Vector3(-1, .6f) };

        void ResetBossRitual()
        {
            ResetFinisherAudio();
            ResetDuel();
            BeginCursedMask();
            bossSeals = bossFinalStage = 0; bossFinalTime = 0; bossInkBurst = false;
            landedBossSeals = 0; spiritChains.Clear(); spiritChainTargets.Clear(); bossSealDrawing.Clear();
            pendingSeal = null; flyingSeal = null; mapPaperExit = -1; Shader.SetGlobalFloat("_BossFinalInk", 0);
        }
        bool BossGesture(Vector3 a, Vector3 b)
        {
            if (!IsBoss) return false;
            if (bossFinalStage == 1)
            {
                if (flyingSeal != null) return true;
                Vector2 pointer = view.WorldToScreenPoint(b);
                Vector2 pullAxis = new Vector2(-.707f, .707f);
                if (RitualModelKatana && drawSaya != null)
                {
                    Vector2 mouth = view.WorldToScreenPoint(drawSaya.position);
                    pullAxis = ((Vector2)view.WorldToScreenPoint(drawSaya.position + drawSaya.up) - mouth).normalized;
                }
                Vector2 drag = pointer - strokeStart;
                float pull = Vector2.Dot(drag, pullAxis);
                if (drawGripHeld) drawPullTarget = Mathf.Clamp01(drawGripStart + pull / (Screen.height * .18f));
                if (drawGripHeld && drawPullTarget >= .72f && Mathf.Abs(Vector2.Dot(drag, new Vector2(-pullAxis.y, pullAxis.x))) < Screen.height * .16f)
                    BeginBossInkFinish(new Vector3(-1, 1, 0));
                return true;
            }
            if (bossFinalStage > 1) return true;
            if (DuelActive) return true;
            return false;
        }
        void ClearPendingSeal()
        {
            if (pendingSeal != null) Destroy(pendingSeal.gameObject);
            pendingSeal = null;
        }
        Transform CreateSpiritTalisman()
        {
            var seal = new GameObject("Talismán espiritual " + (bossSeals + 1)).transform;
            seal.SetParent(world, false);
            art.Shape(seal, "Canto del papel", PrimitiveType.Cube, Vector3.zero, new Vector3(.80f, 1.28f, .028f), PrototypeArt.Paper);
            var paper = art.Shape(seal, "Ofuda ilustrado", PrimitiveType.Quad, Vector3.back * .024f, new Vector3(.86f, 1.36f, 1), Color.white);
            var material = art.PaperWeaponMaterial(Resources.Load<Texture2D>("UI/Seals/OfudaPaper"));
            material.SetTextureScale("_BaseMap", new Vector2(.465f, .95f));
            material.SetTextureOffset("_BaseMap", new Vector2(.267f, .025f));
            paper.GetComponent<Renderer>().sharedMaterial = material;
            var symbol = SpiritPattern(.38f);
            for (int i = 0; i < symbol.Length; i++) symbol[i] += Vector3.back * .05f;
            var brush = art.Line(seal, symbol, new Color(.065f, .075f, .06f), .047f);
            brush.numCapVertices = 6; brush.numCornerVertices = 6;
            brush.widthCurve = new AnimationCurve(new Keyframe(0, .25f), new Keyframe(.12f, .95f), new Keyframe(.32f, .6f), new Keyframe(.55f, 1), new Keyframe(.78f, .7f), new Keyframe(1, .12f));
            // A small artist's stamp sits apart from the drawable central symbol.
            art.Shape(seal, "Sello bermellón", PrimitiveType.Quad, new Vector3(.18f, -.48f, -.05f), new Vector3(.115f, .115f, 1), PrototypeArt.Red);
            art.Line(seal, new[] { new Vector3(.15f, -.45f, -.06f), new Vector3(.21f, -.45f, -.06f), new Vector3(.18f, -.5f, -.06f) }, PrototypeArt.Paper, .009f); return seal;
        }
        void ShowBossPattern()
        {
            if (counterGuide != null) Destroy(counterGuide.gameObject);
            ClearPendingSeal(); bossSealDrawing.Clear();
            pendingSeal = CreateSpiritTalisman();
            pendingSeal.position = view.ViewportToWorldPoint(new Vector3(.16f, .54f, 10));
            pendingSeal.localScale = Vector3.one * (view.orthographicSize * 2 * .44f / 1.3f);
        }
        void TraceBossPattern(Vector3 a, Vector3 b)
        {
            if (!MatchesSpiritSeal()) return;
            FighterCut(b - a); PlayKatanaCounter(b - a);
            LaunchSpiritSeal();
            bossSeals++;
            bossHealth = Mathf.Max(0, bossMaxHealth - bossSeals * bossMaxHealth / 3);
            CloseCounter();
            if (bossSeals >= 3)
            {
                theaterState = 2; theaterStrike = true;
                if (opponent != null) opponent.gameObject.SetActive(true);
            }
        }
        void LaunchSpiritSeal()
        {
            // The preview is the actual seal; recognition releases it, never before.
            flyingSeal = pendingSeal;
            pendingSeal = null;
            if (flyingSeal == null)
            {
                flyingSeal = CreateSpiritTalisman();
                flyingSeal.position = view.ViewportToWorldPoint(new Vector3(.16f, .54f, 10));
            }
            sealLaunchPosition = flyingSeal.position;
            sealLaunchScale = flyingSeal.localScale;
            sealFlightTime = 0;
        }
        void UpdateBossRitual(float delta)
        {
            UpdateBossActing(delta);
            UpdateCursedMask(delta);
            UpdateSpiritChains();
            if (flyingSeal != null)
            {
                sealFlightTime += delta;
                Vector3 target = BossSealTarget();
                float flight = Mathf.Clamp01(sealFlightTime / .65f);
                flyingSeal.position = Vector3.Lerp(sealLaunchPosition, target, Mathf.SmoothStep(0, 1, flight)) + Vector3.up * Mathf.Sin(flight * Mathf.PI) * 1.1f;
                flyingSeal.localScale = Vector3.Lerp(sealLaunchScale, Vector3.one * (cursedMask != null ? .45f : 1f), Mathf.SmoothStep(0, 1, flight));
                flyingSeal.rotation = Quaternion.Euler(Mathf.Sin(flight * Mathf.PI) * 12, Mathf.Sin(flight * Mathf.PI) * 18, Mathf.Sin(sealFlightTime * 14) * 8 * (1 - flight));
                if (sealFlightTime >= .65f)
                {
                    ReactBoss(BossReaction.Seal);
                    landedBossSeals = bossSeals;
                    GuardBurst(target, true);
                    if (cursedMask != null) flyingSeal.SetParent(cursedMask, true);
                    else if (opponent != null) flyingSeal.SetParent(opponent, true);
                    AddSpiritChains(flyingSeal);
                    flyingSeal = null;
                    if (landedBossSeals >= 3) bossFinalStage = 1;
                }
            }
            if (bossFinalStage == 1 && sheathedKatana != null)
            {
                sheathedKatana.gameObject.SetActive(false);
                UpdateRitualKatana(delta);
            }
            if (bossFinalStage != 2) return;
            bossFinalTime += delta;
            if (katanaSpirit != null) katanaSpirit.SampleRelease(bossFinalTime - .16f, CounterCenter + Vector3.back * 3.2f, finalCutDirection.normalized, drawSwordScale);
            if (!finalCutReleased && bossFinalTime >= .16f)
            {
                finalCutReleased = true; PlayKatanaCounter(finalCutDirection);
                PlayFinisherDrawSound();
                if (slash != null) slash.enabled = true;
                impactTime = Mathf.Max(impactTime, .08f); shakeTime = Mathf.Max(shakeTime, .12f);
            }
            if (bossFinalTime > .85f && !bossInkBurst)
            {
                bossInkBurst = true;
                PlaySpiritDissolveSound();
                if (opponent != null) opponent.gameObject.SetActive(false);
                for (int i = 0; i < 40; i++)
                {
                    float angle = i * 2.39996f;
                    var drop = art.Shape(world, "Tinta del yokai", PrimitiveType.Quad, CounterCenter + Vector3.back, new Vector3(.07f + (i % 3) * .035f, .19f + (i % 4) * .05f, .02f), PrototypeArt.Ink);
                    AddDebris(drop.transform, finalCutDirection.normalized * (2 + i % 5) + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (1 + i % 3), Vector3.forward * 90, 1.3f);
                }
            }
            if (bossFinalTime > 2.5f) { Shader.SetGlobalFloat("_BossFinalInk", 0); Finish(true); }
        }
        void BeginBossInkFinish(Vector3 direction)
        {
            if (katanaSpirit != null) katanaSpirit.HidePresentation();
            bossFinalStage = 2; bossFinalTime = 0; finalCutReleased = false; finalCutDirection = direction; ClearJutsuSeal();
            foreach (var renderer in world.GetComponentsInChildren<Renderer>())
                renderer.enabled = false;
            if (opponent != null)
            {
                opponent.gameObject.SetActive(true);
                foreach (var renderer in opponent.GetComponentsInChildren<Renderer>())
                {
                    renderer.enabled = true;
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetFloat("_SilhouetteInk", 1);
                    renderer.SetPropertyBlock(block);
                }
            }
            if (counterKatana != null)
                foreach (var renderer in counterKatana.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
            var white = art.Shape(world, "Papel blanco del remate", PrimitiveType.Quad, new Vector3(0, 4, 4), new Vector3(40, 24, 1), Color.white);
            white.GetComponent<Renderer>().sharedMaterial.SetFloat("_InkShading", 0);
            Shader.SetGlobalFloat("_BossFinalInk", 0);
            katanaCutTime = 0;
            if (counterKatana != null) counterKatana.gameObject.SetActive(false);
            slash = art.Line(world, new[] { CounterCenter - direction.normalized * 10 + Vector3.back * 3, CounterCenter + direction.normalized * 10 + Vector3.back * 3 }, PrototypeArt.Ink, .12f);
            slash.enabled = false;
        }
        void DrawBossRitualOverlay()
        {
            if (IsBoss && mode == Mode.Battle) DrawParryPrompt();
            if (bossFinalStage == 2 && mode != Mode.Map)
            {
                float t = Mathf.Clamp01((bossFinalTime - .2f) / .5f);
                Color previous = GUI.color; GUI.color = new Color(1, 1, 1, .38f);
                GUI.DrawTexture(new Rect(0, 720 * (1 - Mathf.SmoothStep(0, 1, t)), 1280, 720), Texture2D.whiteTexture);
                GUI.color = previous;
            }
            if (mapPaperExit >= 0)
            {
                float t = (Time.unscaledTime - mapPaperExit) / .6f;
                if (t >= 1) { mapPaperExit = -1; return; }
                Color previous = GUI.color; GUI.color = new Color(1, 1, 1, .5f);
                GUI.DrawTexture(new Rect(1280 * Mathf.SmoothStep(0, 1, t), 0, 1280, 720), Texture2D.whiteTexture); GUI.color = previous;
            }
        }

        void DrawBossSealIndicators()
        {
            for (int i = 0; i < 3; i++)
            {
                float x = 858 + i * 27;
                bool landed = i < landedBossSeals;
                Panel(new Rect(x, 17, 19, 34), landed ? PrototypeArt.Red : new Color(.2f, .22f, .18f, .3f));
                Panel(new Rect(x + 2, 19, 15, 30), landed ? PrototypeArt.Paper : new Color(.8f, .73f, .56f, .35f));
                if (!landed) continue;
                for (int mark = 0; mark < 4; mark++)
                    Panel(new Rect(x + 6 + (mark % 2) * 2, 23 + mark * 6, mark % 2 == 0 ? 8 : 4, 2), PrototypeArt.Ink);
            }
        }
    }
}








