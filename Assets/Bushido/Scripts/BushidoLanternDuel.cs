using System.Collections.Generic;
using UnityEngine;

namespace Bushido
{
    // Node 3, "Los faroles gemelos" (Los faroles gemelos): after a 12 s frog opening, two paper-lantern yokai hang on the village cable.
    // Exactly one is lit and attacks (tongue lash, will-o'-wisps), then passes the flame along the cable. Read, then cut.
    public sealed partial class BushidoPrototype
    {
        // ---- Tuning: every number of the duel lives here ----
        const float LanternOpening = 12f;                 // s of frog waves before the duel
        const float LanternWindUp = 0.7f, LanternWindUp2 = 0.5f, LanternLash = 0.45f, LanternRetract = 0.3f; // 2 = survivor phase
        const float LanternParryAngle = 35f, LanternTongueReach = 0.3f, LanternParryExtension = 0.5f;
        const float LanternStun = 2.2f, LanternBlackout_ = 3f;
        const int LanternStunCuts = 2, LanternHealth = 4;
        const int LanternWisps = 4, LanternWisps2 = 5;
        const float LanternSpit = 0.35f, LanternSpitGap = 0.15f, LanternSpitEmit = 0.45f; // emit = fraction of the flick when the flame leaves
        const float LanternFlameRise = 3.5f, LanternFlameRadius = 0.7f;
        const float LanternRelay = 1.1f, LanternRelayRadius = 0.75f, LanternLightUp = 1.5f, LanternNextDelay = 0.8f;
        const float LanternFlicker = 1.2f, LanternEndBeat = 1.2f, LanternDusk = 1f, LanternBounceGap = 0.25f;
        const float LanternHookWhite = 0.2f, LanternHookRed = 0.62f; // the rank meter owns the right edge
        const int LanternFlameTag = -77;                    // Projectile.pair marks the duel's own flames
        static readonly float[] LanternPathsWhite = { -8f, -42f, -90f }, LanternPathsRed = { 188f, 222f, -90f }; // horizontal, diagonal, vertical
        // Awakening beat (the twins come out of two ordinary lanterns), rope reactions, light. Speeds in rope units per second.
        const float LanternAwaken = 1f, LanternTear = 0.4f, LanternSwell = 0.6f;       // beat length; when the paper tears; extra size at the end of the swell
        const float LanternTwitchLen = 0.5f, LanternTwitchDeg = 14f;                     // the foreshadow twitch
        static readonly float[] LanternTwitchAt = { 6f, 10f };                           // s into the opening: white twin, then red twin
        const float LanternTug = -4f, LanternJolt = 9f, LanternShake = -5f, LanternRelease = 10f, LanternAwakenDip = -9f; // rope kicks: lash wind-up, parry, body cut, purified, awakening
        const float LanternRelayPush = 14f, LanternRippleGap = 0.18f;                    // the flame presses the rope as it passes; a ring every gap
        const float LanternDarkBase = 0.6f, LanternDarkBlackout = 0.8f, LanternWarmth = 0.06f; // veil alpha; URP blends in linear space, so 0.6 reads as ~38% darker and 0.8 as ~55%
        const float LanternPoolTwin = 0.34f, LanternPoolSmall = 0.11f, LanternPoolFlame = 0.1f, LanternEmber = 0.045f, LanternBlackoutFade = 3f; // pool radii in screen heights
        const float LanternBrush = 0.2f, LanternBrushGrow = 0.28f, LanternBrushRim = 1.5f; // telegraph stroke width at rest, extra at full wind-up, paper rim factor

        enum LanternAct { None, LightUp, WindUp, Lash, Retract, Spit, Flicker, Relay, Stunned }
        enum LanternStep { Lash, Wisps, Relay, Flicker }

        bool LanternDuel => CampaignProgress.IsDuel(mission);
        int lanternStage;                                   // 0 opening (frog waves), 1 duel, 2 ending beat, 3 done
        readonly LanternActor[] twins = new LanternActor[2];  // 0 white, 1 red
        readonly int[] twinHealth = new int[2], twinStunCuts = new int[2], twinLastCut = new int[2];
        readonly bool[] twinPurified = new bool[2];
        readonly float[] twinBounce = new float[2];
        int litTwin = -1, lanternTurn, lanternPath, lashesLeft, spitsLeft, spitIndex, pendingLight = -1, blackoutNext;
        float lanternClock, lactT, stunLeft, blackoutLeft, relayT, endT, pendingLightT, duskNow, duskBlackout;
        LanternAct lact;
        bool lashResolved, spitEmitted;
        readonly Queue<LanternStep> lanternPlan = new Queue<LanternStep>();
        readonly HashSet<string> lanternHints = new HashSet<string>();
        Transform relayFlame; int relayTo;
        LineRenderer lanternPathLine, lanternPathRim;
        float awakenT = -1f, relayRipple;                   // seconds since the awakening began (-1: still disguised)
        bool awakenTorn, awakenLit;

        bool LanternPhase2 => twinPurified[0] || twinPurified[1];

        void LanternHint(string text) { if (lanternHints.Add(text)) Tell(text, 3.5f); }

        void BeginLanternDuel()
        {
            // The village lanterns share the cable: in the duel they leave room for the twins (the boss's positions come back otherwise).
            float[] spots = LanternDuel ? new[] { 0.06f, 0.31f, 0.72f, 0.79f } : new[] { 0.07f, 0.25f, 0.73f, 0.79f };
            for (int i = 0; i < 4; i++) LanternPositions[i] = spots[i];
            lanternStage = 0; litTwin = pendingLight = -1; lanternTurn = lanternPath = 0; lact = LanternAct.None; blackoutLeft = stunLeft = 0; endT = 0;
            duskNow = LanternDusk; duskBlackout = 0; awakenT = -1f; awakenTorn = awakenLit = false; relayRipple = 0f; relayFlame = null; lanternHints.Clear(); lanternPlan.Clear();
            if (!LanternDuel) return;
            for (int i = 0; i < 2; i++) { twinHealth[i] = LanternHealth; twinStunCuts[i] = 0; twinLastCut[i] = -1; twinPurified[i] = false; twinBounce[i] = 0; }
            BeginVillageLanterns();
            for (int i = 0; i < 2; i++)
            {
                twins[i] = new LanternActor(art, world, i == 0, CablePoint(i == 0 ? LanternHookWhite : LanternHookRed));
                twins[i].SetPose(LanternActor.Pose.Unlit, 1f, 0f); twins[i].eyeTarget = 0; twins[i].glowTarget = 0;
                twins[i].holder.gameObject.SetActive(false);   // until the awakening an ordinary paper lantern hangs here (village slot 4 + i)
            }
            // The telegraph is a tapered ink brush stroke over a paper-coloured one, so it reads on the dusk.
            lanternPathRim = LanternBrushLine(PrototypeArt.Paper); lanternPathLine = LanternBrushLine(PrototypeArt.Ink);
            LightVillageLanterns(0f);
            BeginDusk();
            ApplyDusk();
        }

        LineRenderer LanternBrushLine(Color color)
        {
            var line = art.Line(world, new Vector3[2], color, LanternBrush);
            var material = art.PaperWeaponMaterial(PrototypeArt.Brush(false)); material.SetFloat("_PaperAlphaCutoff", 0.3f); material.SetColor("_BaseColor", color);
            line.sharedMaterial = material; line.numCapVertices = 0; line.enabled = false;
            return line;
        }

        void DisposeLanternDuel()
        {
            for (int i = 0; i < 2; i++) if (twins[i] != null) { twins[i].Dispose(); twins[i] = null; }
        }

        // ---- per-frame ----
        void UpdateLanternDuel(float delta)
        {
            if (twins[0] == null) return;
            lanternClock += delta; maskAge += delta;
            if (lanternStage == 0 && battleTime >= LanternOpening) StartLanternDuel();
            else if (lanternStage == 1) RunLanternDuel(delta);
            else if (lanternStage == 2)
            {
                endT += delta;
                if (endT >= LanternEndBeat && bossFinalStage == 0) bossFinalStage = 1;
                if (bossFinalStage == 1) UpdateRitualKatana(delta);
                if (bossFinalStage == 2)
                {
                    UpdateLanternExecution(delta);
                    return;
                }
            }
            UpdateTwinDisguise(delta);
            UpdateLanternLights();
            UpdateVillageLanterns(true, delta);
            for (int i = 0; i < 2; i++)
            {
                twinBounce[i] = Mathf.Max(0, twinBounce[i] - delta);
                twins[i].MoveHook(RopePoint(hangU[4 + i]));
                twins[i].lookAt = fighter != null ? fighter.position : Vector3.zero;
                twins[i].Update(delta, lanternClock);
            }
            if (lact == LanternAct.Flicker && litTwin >= 0) twins[litTwin].glowTarget = 0.55f + 0.45f * (Mathf.Sin(lanternClock * 22f) > 0 ? 1f : 0f);
            UpdateLanternPathLine();
            UpdateLanternFlames(delta);
            ApplyDusk();
        }

        const float ExecutionDraw = .46f, TwinHitGap = .14f, SpiritMeeting = 2.25f, ExecutionEnd = 2.95f;
        readonly Vector3[] executionBodies = new Vector3[2], executionHooks = new Vector3[2];
        readonly Quaternion[] executionRotations = new Quaternion[2];
        readonly Transform[] releasedFlames = new Transform[2];
        float executionViewSize;
        LineRenderer executionStroke;

        void BeginLanternExecution()
        {
            bossFinalStage = 2;
            bossFinalTime = 0f;
            finalCutReleased = false;
            drawGripHeld = false;
            katanaSpirit?.HidePresentation();
            drawSaya.gameObject.SetActive(false);
            counterKatana.gameObject.SetActive(false);
            executionViewSize = view.orthographicSize;
            for (int i = 0; i < twins.Length; i++)
            {
                executionBodies[i] = twins[i].Body;
                executionHooks[i] = twins[i].holder.position;
                executionRotations[i] = twins[i].holder.rotation;
                releasedFlames[i] = null;
            }
            finalCutDirection = (executionBodies[1] - executionBodies[0]).normalized;
            executionStroke = art.Line(world, new Vector3[2], PrototypeArt.Gold, .11f);
            executionStroke.useWorldSpace = true;
            executionStroke.enabled = false;
            executionStroke.widthCurve = AnimationCurve.EaseInOut(0, .18f, 1, 1);
            FighterCut(finalCutDirection);
        }

        void UpdateLanternExecution(float delta)
        {
            float previous = bossFinalTime;
            bossFinalTime += delta;
            float time = bossFinalTime;
            Vector3 centre = (executionBodies[0] + executionBodies[1]) * .5f;
            float framing = Mathf.SmoothStep(0, 1, time / ExecutionDraw)
                * (1f - Mathf.SmoothStep(0, 1, (time - SpiritMeeting) / (ExecutionEnd - SpiritMeeting)));
            Vector3 home = new Vector3(0, 4, -15);
            Vector3 focus = new Vector3(centre.x, centre.y, home.z);
            view.orthographicSize = executionViewSize * (1f - .16f * framing);
            view.transform.position = Vector3.Lerp(home, focus, .55f * framing);
            float strike = time - ExecutionDraw;
            katanaSpirit?.SampleRelease(strike, centre + Vector3.back, finalCutDirection, drawSwordScale);
            if (strike >= 0 && !finalCutReleased)
            {
                finalCutReleased = true;
                PlayFinisherDrawSound();
            }
            executionStroke.enabled = strike >= 0 && strike < .52f;
            if (executionStroke.enabled)
            {
                Vector3 start = executionBodies[0] - finalCutDirection * 1.3f + Vector3.back * 1.4f;
                Vector3 end = executionBodies[1] + finalCutDirection * 1.3f + Vector3.back * 1.4f;
                executionStroke.SetPosition(0, start);
                executionStroke.SetPosition(1, Vector3.Lerp(start, end, strike / TwinHitGap));
                executionStroke.widthMultiplier = .15f * (1f - Mathf.Clamp01((strike - TwinHitGap) / .38f));
            }
            for (int i = 0; i < twins.Length; i++)
            {
                float hitAt = ExecutionDraw + i * TwinHitGap;
                if (previous < hitAt && time >= hitAt) ReleaseLanternSpirit(i);
                if (time >= hitAt) AnimateFallingLantern(i, time - hitAt, centre);
            }
            if (previous < SpiritMeeting && time >= SpiritMeeting)
            {
                Vector3 meeting = centre + Vector3.up * .9f + Vector3.back;
                ShockRingBurst(meeting, Quaternion.identity, 1.65f, SpiritLight(.2f, 1.4f));
                PlaySpiritDissolveSound();
            }
            UpdateVillageLanterns(true, delta);
            if (time >= ExecutionEnd)
            {
                view.transform.position = home;
                view.orthographicSize = executionViewSize;
                bossFinalStage = 0;
                lanternStage = 3;
                Finish(true);
            }
        }

        void ReleaseLanternSpirit(int i)
        {
            Color tint = i == 0 ? PrototypeArt.Paper : PrototypeArt.Red;
            Vector3 body = executionBodies[i];
            twins[i].ReleaseCord();
            hangLoad[4 + i] = 0f;
            RopeKick(hangU[4 + i], LanternRelease);
            cutStrokeDir = finalCutDirection;
            CutBurst(body, true);
            SlashArcBurst(body + Vector3.back, finalCutDirection, 1.65f, SpiritLight(.3f, 1.4f));
            ShockRingBurst(body + Vector3.back, Quaternion.identity, 1.25f, tint);
            releasedFlames[i] = NewFlame(tint, PrototypeArt.Gold, .72f, false);
        }

        void AnimateFallingLantern(int i, float age, Vector3 centre)
        {
            var holder = twins[i].holder;
            float side = i == 0 ? -1f : 1f;
            if (holder.gameObject.activeSelf)
            {
                // Hold the silhouette for the hit, then let the empty paper shell tumble.
                float fall = Mathf.Max(0f, age - .12f);
                holder.position = executionHooks[i] + new Vector3(side * fall * 1.25f, .4f * fall - 3.6f * fall * fall, 0);
                holder.rotation = Quaternion.AngleAxis(side * fall * 82f, Vector3.forward) * executionRotations[i];
                holder.localScale = Vector3.one * (1f - Mathf.SmoothStep(0, 1, (fall - .55f) / .48f));
                if (fall >= 1.03f) holder.gameObject.SetActive(false);
            }
            Transform flame = releasedFlames[i];
            if (flame == null) return;
            Vector3 meeting = centre + Vector3.up * .9f + Vector3.back;
            float progress = Mathf.Clamp01(age / (SpiritMeeting - ExecutionDraw - i * TwinHitGap));
            float arc = Mathf.Sin(progress * Mathf.PI);
            flame.position = Vector3.Lerp(executionBodies[i] + Vector3.back, meeting, Mathf.SmoothStep(0, 1, progress))
                + new Vector3(side * .45f * arc, arc * (i == 0 ? 1.15f : .72f), 0);
            float fade = Mathf.SmoothStep(0, 1, (bossFinalTime - SpiritMeeting) / .36f);
            flame.localScale = Vector3.one * (1 - fade) * (1f + .08f * Mathf.Sin(age * 12f));
            if (fade >= 1f) flame.gameObject.SetActive(false);
        }

        void StartLanternDuel()
        {
            lanternStage = 1;
            pendingFrogs.Clear(); pendingBombDelay = -1f;   // the waves stop; frogs already in the air still fall
            LanternHint("Solo la linterna encendida ataca. La apagada rebota");
            BossSay(0, "¡Shh! Que no se despierte mi hermano.", 2.4f);
            BossSay(1, "Ya estoy despierto. Y tengo hambre.", 2.4f);
            LightTwin(0);
            awakenT = 0f;                                   // the awakening beat plays over the first light-up; the white twin's glow waits for its end
            twins[0].glowTarget = 0f;
        }

        void LightTwin(int i)
        {
            litTwin = i; lanternTurn++; lanternPlan.Clear();
            if (lanternTurn > 1) BossSay(i, BossPick(LanternLit), 2f);
            var t = twins[i];
            t.glowTarget = 1f; t.eyeTarget = 1f; t.SetPose(LanternActor.Pose.Idle, LanternLightUp, 0.05f);
            twins[1 - i].glowTarget = twinPurified[1 - i] ? 1f : 0f;
            bool lashFirst = lanternTurn % 2 == 1;           // the order of lash and wisps changes every turn
            var a = lashFirst ? LanternStep.Lash : LanternStep.Wisps; var b = lashFirst ? LanternStep.Wisps : LanternStep.Lash;
            lanternPlan.Enqueue(a); lanternPlan.Enqueue(b);
            lanternPlan.Enqueue(LanternPhase2 ? LanternStep.Flicker : LanternStep.Relay);
            lact = LanternAct.LightUp; lactT = 0;
        }

        void NextLanternStep()
        {
            if (lanternPlan.Count == 0) { LightTwin(litTwin); return; }   // survivor: a new cycle keeps the flame
            var step = lanternPlan.Dequeue();
            var t = twins[litTwin];
            switch (step)
            {
                case LanternStep.Lash: lashesLeft = LanternPhase2 ? 2 : 1; StartLash(); break;
                case LanternStep.Wisps: spitsLeft = LanternPhase2 ? LanternWisps2 : LanternWisps; spitIndex = 0; StartSpit(); break;
                case LanternStep.Relay: StartRelay(); break;
                case LanternStep.Flicker: t.SetPose(LanternActor.Pose.Idle, LanternFlicker, 0.05f); lact = LanternAct.Flicker; lactT = 0; break;
            }
        }

        float LanternPathAngle(int twin, int index) => (twin == 0 ? LanternPathsWhite : LanternPathsRed)[((index % 3) + 3) % 3];

        void StartLash()
        {
            var t = twins[litTwin];
            t.pathAngle = LanternPathAngle(litTwin, lanternPath++);
            RopeKick(hangU[4 + litTwin], LanternTug);       // the twin leans into the wind-up and pulls the rope
            float dur = LanternPhase2 ? LanternWindUp2 : LanternWindUp;
            t.SetPose(LanternActor.Pose.WindUp, dur);
            lact = LanternAct.WindUp; lactT = 0; lashResolved = false;
            LanternHint("Corta la lengua cuando brille");
        }

        void StartSpit()
        {
            twins[litTwin].SetPose(LanternActor.Pose.Spit, LanternSpit, 0.04f);
            twins[litTwin].spitAngle = litTwin == 0 ? -40f : -140f;
            lact = LanternAct.Spit; lactT = 0; spitEmitted = false;
            LanternHint("Llama roja: guarda. Llama blanca: corta.");
        }

        void StartRelay()
        {
            var t = twins[litTwin];
            relayTo = 1 - litTwin;
            t.glowTarget = 0f; t.eyeTarget = 0f; t.SetPose(LanternActor.Pose.Unlit, 1f, 0.05f);
            relayFlame = NewFlame(PrototypeArt.Gold, PrototypeArt.Paper, 0.95f, false);
            relayT = 0; lact = LanternAct.Relay; lactT = 0;
            LanternHint("Corta la llama en el cable: apagón");
        }

        void RunLanternDuel(float delta)
        {
            if (blackoutLeft > 0f)
            {
                blackoutLeft -= delta;
                if (blackoutLeft <= 0f) EndBlackout();
                return;
            }
            if (litTwin < 0)
            {
                if (pendingLight >= 0 && (pendingLightT -= delta) <= 0f) { int i = pendingLight; pendingLight = -1; LightTwin(i); }
                return;
            }
            lactT += delta;
            var t = twins[litTwin];
            switch (lact)
            {
                case LanternAct.LightUp: if (lactT >= LanternLightUp) NextLanternStep(); break;
                case LanternAct.WindUp:
                    if (lactT >= (LanternPhase2 ? LanternWindUp2 : LanternWindUp)) { t.SetPose(LanternActor.Pose.Lash, LanternLash, 0.02f); lact = LanternAct.Lash; lactT = 0; }
                    break;
                case LanternAct.Lash:
                    if (lactT >= LanternLash)
                    {
                        if (!lashResolved) { lashResolved = true; misses++; Hurt(); if (mode != Mode.Battle) return; }
                        lashesLeft--; t.SetPose(LanternActor.Pose.Retract, LanternRetract, 0.02f); lact = LanternAct.Retract; lactT = 0;
                    }
                    break;
                case LanternAct.Retract:
                    if (lactT >= LanternRetract) { if (lashesLeft > 0) StartLash(); else { t.SetPose(LanternActor.Pose.Idle, 1f); NextLanternStep(); } }
                    break;
                case LanternAct.Spit:
                    if (!spitEmitted && lactT >= LanternSpit * LanternSpitEmit) { spitEmitted = true; EmitFlame(t); }
                    if (lactT >= LanternSpit + LanternSpitGap)
                    {
                        if (--spitsLeft > 0) { spitIndex++; StartSpit(); } else { t.SetPose(LanternActor.Pose.Idle, 1f); NextLanternStep(); }
                    }
                    break;
                case LanternAct.Flicker: if (lactT >= LanternFlicker) { t.glowTarget = 1f; NextLanternStep(); } break;
                case LanternAct.Relay:
                    relayT += delta;
                    if (relayT >= LanternRelay) { Destroy(relayFlame.gameObject); relayFlame = null; LightTwin(relayTo); }
                    break;
                case LanternAct.Stunned:
                    stunLeft -= delta;
                    if (stunLeft <= 0f) { t.SetPose(LanternActor.Pose.Idle, 1f); t.glowTarget = 1f; NextLanternStep(); }
                    break;
            }
        }

        // ---- flames ----
        Transform NewFlame(Color body, Color rim, float size, bool danger)
        {
            var root = new GameObject(danger ? "Fuego fatuo rojo" : "Fuego fatuo").transform;
            root.SetParent(world, false);
            art.Shape(root, "Borde de tinta", PrimitiveType.Sphere, Vector3.forward * 0.03f, new Vector3(0.96f, 0.96f, 0.08f) * size, PrototypeArt.Ink);
            var tail = art.Shape(root, "Borde de tinta", PrimitiveType.Cube, new Vector3(0, 0.46f, 0.03f) * size, new Vector3(0.52f, 0.52f, 0.06f) * size, PrototypeArt.Ink);
            tail.transform.localRotation = Quaternion.Euler(0, 0, 45);
            art.Shape(root, "Llama", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.78f, 0.78f, 0.1f) * size, body);
            var inner = art.Shape(root, "Llama", PrimitiveType.Cube, new Vector3(0, 0.42f, 0f) * size, new Vector3(0.4f, 0.4f, 0.08f) * size, body);
            inner.transform.localRotation = Quaternion.Euler(0, 0, 45);
            art.Shape(root, "Corazón de la llama", PrimitiveType.Sphere, new Vector3(0, -0.05f, -0.08f) * size, new Vector3(0.36f, 0.36f, 0.05f) * size, rim);
            if (danger)
            {
                // The same cross the bomb wears: do not cut.
                art.Line(root, new[] { new Vector3(-0.2f, -0.25f, -0.12f), new Vector3(0.2f, 0.15f, -0.12f) }, PrototypeArt.Paper, 0.07f);
                art.Line(root, new[] { new Vector3(-0.2f, 0.15f, -0.12f), new Vector3(0.2f, -0.25f, -0.12f) }, PrototypeArt.Paper, 0.07f);
            }
            return root;
        }

        void EmitFlame(LanternActor from)
        {
            int count = LanternPhase2 ? LanternWisps2 : LanternWisps;
            bool red = LanternPhase2 ? (spitIndex + lanternTurn) % 2 == 1 : !from.white;
            Vector3 start = from.Tip; start.z = 0;
            float tx = -4.5f + (((spitIndex * 2 + lanternTurn) % count) + 0.5f) / count * 9f;
            float g = 5.2f, rise = LanternFlameRise;
            float fall = (rise + Mathf.Sqrt(rise * rise + 2f * g * (start.y + 1.8f))) / g;
            SpawnLanternFlame(start, new Vector3((tx - start.x) / fall, rise, 0), red);
            ShockRingBurst(start + Vector3.back, Quaternion.identity, 0.8f, red ? PrototypeArt.Red : PrototypeArt.Paper);
        }

        Projectile SpawnLanternFlame(Vector3 position, Vector3 velocity, bool red)
        {
            var root = NewFlame(red ? PrototypeArt.Red : new Color(1f, 0.97f, 0.86f), red ? PrototypeArt.Ink : PrototypeArt.Paper, 1f, red);
            root.position = position;
            var flame = new Projectile { root = root, kind = red ? ProjectileKind.Bomb : ProjectileKind.Wisp, velocity = velocity, spin = Vector3.zero,
                radius = LanternFlameRadius, pair = LanternFlameTag };
            projectiles.Add(flame);
            return flame;
        }

        void UpdateLanternFlames(float delta)
        {
            foreach (Projectile item in projectiles)
                if (item.pair == LanternFlameTag && item.kind == ProjectileKind.Wisp)
                    item.root.localScale = Vector3.one * (Mathf.Clamp01(item.age / 0.18f) * (1f + Mathf.Sin(item.age * 14f) * 0.06f));
            if (relayFlame != null)
            {
                float u0 = relayTo == 1 ? LanternHookWhite : LanternHookRed, u1 = relayTo == 1 ? LanternHookRed : LanternHookWhite;
                float s = Mathf.Clamp01(relayT / LanternRelay);
                float u = Mathf.Lerp(u0, u1, s);
                relayFlame.position = RopePoint(u) + new Vector3(0, -0.45f + Mathf.Sin(s * Mathf.PI * 6f) * 0.06f, -0.4f);   // it rides the real, sagging rope
                relayFlame.localScale = Vector3.one * (1f + Mathf.Sin(lanternClock * 18f) * 0.08f);
                RopeGlow(u0, u); RopeKick(u, -LanternRelayPush * delta);                                                       // glow behind it, a travelling dent
                if ((relayRipple -= delta) <= 0f) { relayRipple = LanternRippleGap; ShockRingBurst(RopePoint(u) + Vector3.back * 0.3f, Quaternion.identity, 0.6f, PrototypeArt.Gold); }
            }
        }

        void RemoveLanternFlames()
        {
            for (int i = projectiles.Count - 1; i >= 0; i--) if (projectiles[i].pair == LanternFlameTag) RemoveProjectile(i);
            if (relayFlame != null) { Destroy(relayFlame.gameObject); relayFlame = null; }
        }

        void UpdateLanternPathLine()
        {
            bool tell = lanternStage == 1 && litTwin >= 0 && lact == LanternAct.WindUp;
            lanternPathLine.enabled = lanternPathRim.enabled = tell;
            if (!tell) return;
            var t = twins[litTwin];
            float a = t.pathAngle * Mathf.Deg2Rad;
            Vector3 from = t.Points[0]; from.z = 0.2f;
            Vector3 to = from + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * t.reach;
            lanternPathLine.SetPosition(0, from); lanternPathLine.SetPosition(1, to);
            lanternPathLine.widthMultiplier = LanternBrush + LanternBrushGrow * t.P;
            from.z = to.z = 0.25f; lanternPathRim.SetPosition(0, from); lanternPathRim.SetPosition(1, to);
            lanternPathRim.widthMultiplier = lanternPathLine.widthMultiplier * LanternBrushRim;
        }

        // ---- the player's cuts ----
        bool LanternStunned(int i) => !twinPurified[i] && (blackoutLeft > 0f || (i == litTwin && lact == LanternAct.Stunned));

        static float SegmentToSegment(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Vector2 r = b - a, s = d - c;
            float denom = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denom) > 1e-6f)
            {
                Vector2 ca = c - a;
                float t = (ca.x * s.y - ca.y * s.x) / denom, u = (ca.x * r.y - ca.y * r.x) / denom;
                if (t >= 0 && t <= 1 && u >= 0 && u <= 1) return 0f;
            }
            return Mathf.Min(Mathf.Min(SegmentDistance(a, c, d), SegmentDistance(b, c, d)), Mathf.Min(SegmentDistance(c, a, b), SegmentDistance(d, a, b)));
        }

        void LanternCut(Vector3 a, Vector3 b)
        {
            if (lanternStage != 1 || twins[0] == null) return;
            a.z = b.z = 0;
            if (relayFlame != null && SegmentDistance(relayFlame.position, a, b) <= LanternRelayRadius)
            {
                RewardCut(relayFlame.position);
                LanternBlackout();
                return;
            }
            // Tongue parry: cut across the extended tongue, not along it.
            if (litTwin >= 0 && lact == LanternAct.Lash && !lashResolved && twins[litTwin].Extension >= LanternParryExtension)
            {
                var pts = twins[litTwin].Points;
                Vector2 stroke = b - a;
                for (int j = 0; j < pts.Length - 1; j++)
                {
                    Vector2 p = pts[j], q = pts[j + 1];
                    if (SegmentToSegment(a, b, p, q) > LanternTongueReach) continue;
                    float angle = Vector2.Angle(stroke, q - p);
                    if (Mathf.Min(angle, 180f - angle) <= LanternParryAngle) continue;
                    Parry(pts[j]);
                    return;
                }
            }
            for (int i = 0; i < 2; i++)
            {
                if (twinPurified[i] || SegmentDistance(twins[i].Body, a, b) > LanternActor.BodyRadius) continue;
                if (LanternStunned(i) && twinStunCuts[i] < LanternStunCuts)
                {
                    if (twinLastCut[i] == strokeSerial) continue;   // one body cut per stroke
                    twinLastCut[i] = strokeSerial; twinStunCuts[i]++; twinHealth[i]--;
                    twins[i].hurtKick = 0.4f;
                    RopeKick(hangU[4 + i], LanternShake);
                    RewardCut(twins[i].Body);
                    FighterCut(b - a);
                    if (twinHealth[i] <= 0) PurifyTwin(i);
                }
                else if (twinBounce[i] <= 0f)
                {
                    twinBounce[i] = LanternBounceGap;            // paper bounces the blade: no reward, no penalty
                    twins[i].hurtKick = 0.15f;
                    SetupCutAudio(); PlayGuardSound(false);
                }
            }
        }

        void Parry(Vector3 contact)
        {
            lashResolved = true;
            var t = twins[litTwin];
            RopeKick(hangU[4 + litTwin], LanternJolt);      // the tongue is stopped dead: the rope jolts and passes it on
            hits++; combo++; bestCombo = Mathf.Max(bestCombo, combo); comboTime = 4.5f; comboPulse = 1;
            JudgmentGain(JudgmentDeflectGain);
            impactTime = 0.075f; shakeTime = 0.18f; fighterReaction = 0.3f;
            SetupCutAudio(); PlayGuardSound(true);
            CutBurst(contact, true);
            twinStunCuts[litTwin] = 0; stunLeft = LanternStun; lashesLeft = 0;
            BossSay(litTwin, LanternParried, 1.6f);
            t.SetPose(LanternActor.Pose.Stunned, LanternStun, 0.03f);
            t.hurtKick = 0.3f; t.glowTarget = 0.5f;
            lact = LanternAct.Stunned; lactT = 0;
        }

        void PurifyTwin(int i)
        {
            twinPurified[i] = true;
            BossSay(i, i == 0 ? "Hermano... ya no quema. Se está bien así." : "Bueno, vale. Pero la próxima vez gano yo.", 3f);
            hangLoad[4 + i] = RopeLoadLantern; RopeKick(hangU[4 + i], LanternRelease);   // the weight is gone: the rope springs up
            var t = twins[i];
            t.SetPose(LanternActor.Pose.Calm, 1f, 0.1f); t.glowTarget = 1f; t.eyeTarget = 0f;
            CutBurst(t.Body, true);
            if (twinPurified[0] && twinPurified[1])
            {
                RemoveLanternFlames();
                lanternStage = 2; endT = 0; litTwin = -1; lact = LanternAct.None; blackoutLeft = 0;
                LightVillageLanterns(1f);
                return;
            }
            int survivor = 1 - i;
            if (blackoutLeft > 0f) { blackoutNext = survivor; return; }
            // The survivor keeps the flame: it lights up after a short pause.
            RemoveLanternFlames();
            litTwin = -1; lact = LanternAct.None; lanternPlan.Clear();
            pendingLight = survivor; pendingLightT = LanternNextDelay;
        }

        // APAGÓN: the relay flame was cut (or the judgment fell): both twins go dark and stunned.
        void LanternBlackout()
        {
            if (lanternStage != 1 || blackoutLeft > 0f) return;
            int target = relayFlame != null ? relayTo : litTwin >= 0 ? 1 - litTwin : pendingLight >= 0 ? pendingLight : 0;
            RemoveLanternFlames();
            for (int i = 0; i < 2; i++)
            {
                twinStunCuts[i] = 0;
                if (twinPurified[i]) continue;
                twins[i].glowTarget = 0f; twins[i].eyeTarget = 1f; twins[i].SetPose(LanternActor.Pose.Stunned, LanternBlackout_, 0.03f);
            }
            blackoutNext = twinPurified[target] ? 1 - target : target;
            blackoutLeft = LanternBlackout_; litTwin = -1; pendingLight = -1; lact = LanternAct.None; lanternPlan.Clear();
            duskBlackout = 1f;
            Tell("APAGÓN", 2f);
            BossSay(twinPurified[1] ? 0 : 1, LanternBlackoutLine, 2f);
        }

        void EndBlackout()
        {
            for (int i = 0; i < 2; i++) if (!twinPurified[i]) { twins[i].SetPose(LanternActor.Pose.Unlit, 1f, 0.05f); twins[i].eyeTarget = 0f; }
            duskBlackout = 0f;
            LightTwin(blackoutNext);
        }

        // ---- the twins' disguise: two ordinary lanterns (village slots 4 and 5) until the awakening ----
        void UpdateTwinDisguise(float delta)
        {
            if (awakenT >= 0f) awakenT += delta;
            bool awake = awakenT >= LanternTear;
            for (int i = 0; i < 2; i++)
            {
                int slot = 4 + i;
                if (villageLanterns[slot] == null) continue;
                villageLanterns[slot].gameObject.SetActive(!awake); lanternStrings[slot].enabled = !awake; twins[i].holder.gameObject.SetActive(awake);
                float k = (battleTime - LanternTwitchAt[i]) / LanternTwitchLen;       // foreshadow: a shiver and a blink of light
                bool twitch = awakenT < 0f && k >= 0f && k < 1f;
                hangTwitch[slot] = twitch ? (1f - k) * Mathf.Sin(k * 40f) * LanternTwitchDeg : 0f;
                if (twitch) lanternLit[slot] = 0.7f * (1f - k);
                if (awakenT < 0f || awake) continue;
                float x = awakenT / LanternTear - 1f, back = 1f + 2.70158f * x * x * x + 1.70158f * x * x;   // ease-out-back: swells past its size, then settles
                hangScale[slot] = LanternSize[slot] * (1f + LanternSwell * back);
                hangLen[slot] = Mathf.Lerp(LanternCord[slot], LanternActor.HangDrop, back);                  // the cord pays out to where the yokai body will hang
                hangTwitch[slot] += Mathf.Sin(awakenT * 70f) * 7f;
            }
            if (awake && !awakenTorn)
            {
                awakenTorn = true;
                for (int i = 0; i < 2; i++)
                {
                    int slot = 4 + i; Vector3 at = villageLanterns[slot].position;
                    JutsuFxShreds(at + Vector3.back * 0.5f, 9, PrototypeArt.Paper, i == 0 ? PrototypeArt.Ink : PrototypeArt.Red, 4.5f, 0.8f);
                    ShockRingBurst(at + Vector3.back, Quaternion.identity, 1.4f, PrototypeArt.Paper);
                    hangLoad[slot] = RopeLoadTwin; RopeKick(hangU[slot], LanternAwakenDip);               // the new weight drops the rope
                    twins[i].eyeTarget = 1f; twins[i].SetPose(LanternActor.Pose.Spit, 0.5f, 0.03f);       // eye opens, tongue unrolls
                }
            }
            if (awakenT >= LanternAwaken && !awakenLit)
            {
                awakenLit = true;
                for (int i = 0; i < 2; i++)
                    if (twins[i].pose == LanternActor.Pose.Spit) { twins[i].SetPose(litTwin == i ? LanternActor.Pose.Idle : LanternActor.Pose.Unlit, 1f, 0.1f); if (litTwin != i) twins[i].eyeTarget = 0f; }
                if (litTwin >= 0 && twins[litTwin].glowTarget < 0.01f) twins[litTwin].glowTarget = 1f;     // the white twin lights up
            }
        }

        // The village lanterns catch fire one by one as the twins' combined health drops (a quarter of the 8 points each); all of them at victory.
        void UpdateLanternLights()
        {
            int lit = (2 * LanternHealth - twinHealth[0] - twinHealth[1]) * 4 / (2 * LanternHealth);
            for (int k = 0; k < 4; k++) lanternLitTo[k] = k < lit ? 1f : 0f;
        }

        float lightVillageNow = -1f;
        void LightVillageLanterns(float amount)
        {
            lightVillageNow = amount;
            for (int k = 0; k < 4; k++) lanternLitTo[k] = amount;
        }

        // ---- HUD: the twins' health as two brush bars in the hanging tag ----
        void DrawLanternTag()
        {
            Rect tag = new Rect(430, 10, 420, 74);
            Matrix4x4 m = GUI.matrix;
            HangingTag(tag);
            float barX = tag.x + 108, barW = tag.width - 108 - 22;
            for (int i = 0; i < 2; i++)
            {
                float y = tag.y + 12 + i * 26;
                Color ink = i == 0 ? PrototypeArt.Ink : PrototypeArt.Red;
                Caption(new Rect(tag.x + 34, y, 70, 22), i == 0 ? "Blanco" : "Rojo", ink);
                if (litTwin == i) Blot(new Vector2(tag.x + 24, y + 11), Vector2.one * 11, PrototypeArt.Gold);
                float health = twinHealth[i] / (float)LanternHealth;
                Bar(i == 0 ? BarLampWhite : BarLampRed, new Rect(barX, y + 4, barW, 13), health, ink);
            }
            GUI.matrix = m;
        }
        const int BarLampWhite = 7, BarLampRed = 8;
    }
}
