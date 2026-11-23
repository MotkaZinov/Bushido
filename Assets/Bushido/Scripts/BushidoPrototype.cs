using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bushido
{
    public sealed partial class BushidoPrototype : MonoBehaviour
    {
        const string SaveKey = "Bushido.Campaign.v1";
        enum Mode { Title, Map, Travel, FaceOff, Battle, Result, Transition, ScrollOpening, ScrollClosing }
        enum ProjectileKind { Frog, Bomb, Heavy, Wisp }
        enum FrogKind { Common, Armoured, Linked, Golden }

        sealed class Projectile
        {
            public Transform root;
            public ProjectileKind kind;
            public Vector3 velocity, spin;
            public float age;
            public float radius;
            // Special frogs (see FrogKind): armour props, spirit thread to the partner, per-frog material clones.
            public FrogKind frog;
            public int sandalStyle;
            public float size = 1f, orphan;
            public Transform armour;
            public Projectile partner;
            public LineRenderer thread;
            public int pair, tornStroke = -1, linkStroke = -1;
            public List<Material> tints;
            public SandalPuppet puppet;
        }

        readonly string[] titles = { "El comienzo", "Sendero del bambú", "Festival de las chanclas", "Los faroles gemelos", "La sombrilla de la lluvia" };
        readonly Vector3[] nodes = {
            new Vector3(-5.8f, 0, -1.2f), new Vector3(-3.1f, 0, 1.2f),
            new Vector3(0, 0, -0.7f), new Vector3(3, 0, 1.3f), new Vector3(5.7f, 0, -0.6f)
        };
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<(float delay, float x, float speed, float drift, FrogKind kind, int pair)> pendingFrogs = new List<(float, float, float, float, FrogKind, int)>();
        readonly HashSet<FrogKind> frogHints = new HashSet<FrogKind>();
        int frogPattern, linkSerial, strokeSerial, commonSandalCount;
        float pendingBombDelay = -1f;
        float pendingBombX;
        const float WaveLeadTime = 0.48f;
        CampaignProgress progress;
        PrototypeArt art;
        Transform world;
        Transform backdrop;
        bool frameBackdrop;
        Transform playerToken;
        Transform opponent;
        Camera view;
        LineRenderer slash;
        LineRenderer counterGuide;
        Mode mode;
        int selected = 1;
        int mission;
        int routeNode;
        int nextRouteNode;
        float travelTime;
        int dialogueLine;
        bool strokeHasSwing;
        bool paused;
        bool wasPressed;
        bool inputBlockedUntilRelease, gestureFromTouch;
        int gestureTouchId=-1,gestureEpoch,strokeGestureEpoch;
        bool validStroke;
        Vector2 lastPointer;
        Vector2 strokeStart;
        float waveTimer;
        int waveNumber;
        float battleTime;
        int lives;
        int hits;
        int misses;
        int wounds;
        int combo;
        int bestCombo;
        int bossHealth;
        int bossMaxHealth;
        int guard;
        int guardLimit;
        float counterTime;
        bool vulnerable;
        bool won;
        bool unlockedNew;
        Rank resultRank;
        string notice = "";
        float noticeTime;
        string saveWarning = "";
        readonly Vector2[] cutDirections = { new Vector2(1, 1).normalized, Vector2.right, new Vector2(1, -1).normalized };

        FestivalScore arcade = new FestivalScore();
        bool IsTrail => mission == 1 || mission == 2;
        float nextRushVolley;
        bool IsBoss => CampaignProgress.IsBoss(mission);
        bool SecondPhase => IsBoss && bossHealth <= bossMaxHealth / 2;
        Vector3 CounterCenter => new Vector3(0, IsBoss ? 3f : 4.1f, -0.5f);

        void Start()
        {
            progress = new CampaignProgress();
            try
            {
                if (PlayerPrefs.HasKey(SaveKey))
                    progress = JsonUtility.FromJson<CampaignProgress>(PlayerPrefs.GetString(SaveKey)) ?? new CampaignProgress();
            }
            catch (Exception exception)
            {
                saveWarning = "No se pudo leer la partida anterior. Se ha iniciado una nueva campaña.";
                Debug.LogWarning(exception.Message);
            }
            progress.Normalize();
            var cameraObject = new GameObject("Cámara de campaña");
            cameraObject.transform.SetParent(transform);
            view = cameraObject.AddComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.075f, 0.1f, 0.11f);
            view.orthographic = true;
            view.nearClipPlane = 0.1f;
            view.farClipPlane = 100;
            ShowTitleForest();
        }

        void Save()
        {
            try
            {
                PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(progress));
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                saveWarning = "El progreso está disponible en esta sesión, pero no pudo guardarse en el equipo.";
                Debug.LogWarning(exception.Message);
            }
        }

        void NewWorld(bool forest = false)
        {
            CancelActiveGesture();
            DisposeLanternDuel();
            if (battleMusic != null) battleMusic.Stop();
            if (world != null)
            {
                world.gameObject.SetActive(false);
                Destroy(world.gameObject);
            }
            art?.Dispose();
            ClearTitleAssets();
            backdrop = null;
            swayingCanopies.Clear();
            windLeaves.Clear();
            windLeafOrigins.Clear();
            art = new PrototypeArt(forest);
            world = new GameObject("Escenario Bushido").transform;
            world.SetParent(transform);
            ClearProjectiles();
            pendingFrogs.Clear();
            pendingBombDelay = -1f;
            frogPattern = commonSandalCount = 0;
            ResetJutsu();
            cutEffects.Clear();
            trailPoints.Clear();
            trailAges.Clear();
            impactTime = shakeTime = 0;
            slash = null;
            counterGuide = null;
            wasPressed = false;
            validStroke = false;
        }

        void ShowMap(bool animateScroll = true)
        {
            NewWorld();
            mode = animateScroll ? Mode.ScrollOpening : Mode.Map;
            scrollTime = 0;
            paused = false;
            notice = "";
            selected = Mathf.Clamp(selected, 1, progress.unlockedThrough);
            view.orthographic = true;
            view.backgroundColor = new Color(0.075f, 0.1f, 0.11f);
            view.transform.position = new Vector3(0, 13, -11);
            view.transform.LookAt(Vector3.zero);
            view.orthographicSize = Mathf.Max(6.5f, 9.5f / view.aspect);
            showTechniques = false;
            backdrop = art.Backdrop(world, "Mapa de campaña en tinta", "MapaTinta");

            frameBackdrop = true;
            PositionBackdrop();

            for (int i = 0; i < nodes.Length; i++)
            {
                bool available = progress.IsUnlocked(i);
                Color color = available ? PrototypeArt.Gold : new Color(0.33f, 0.35f, 0.32f);
                art.Shape(world, titles[i] + " · borde de tinta", PrimitiveType.Cylinder, nodes[i] + Vector3.up * 0.06f, new Vector3(1.55f, 0.05f, 1.55f), PrototypeArt.Ink);
                art.Shape(world, titles[i] + " · ficha de papel", PrimitiveType.Cylinder, nodes[i] + Vector3.up * 0.12f, new Vector3(1.38f, 0.04f, 1.38f), available ? PrototypeArt.Paper : new Color(.55f,.53f,.47f));
                if (CampaignProgress.IsBoss(i))
                {
                    Transform figure = CreateUmbrellaFigure(nodes[i], 2.1f);
                    figure.rotation = Quaternion.Euler(0, 180, 0);
                }
                else if (i > 0)
                {
                    var marker = new GameObject("Destino yokai").transform;
                    marker.SetParent(world, false);
                    marker.localPosition = nodes[i] + Vector3.up * 0.6f;
                    CreateFrog(marker, 1.1f, 0f);
                }
                if (i < nodes.Length - 1) art.Road(world, nodes[i], nodes[i + 1], progress.IsUnlocked(i + 1));
            }
            routeNode = progress.currentNode;
            playerToken = new GameObject("Ficha del samurái").transform;
            playerToken.SetParent(world, false);
            CreateSamuraiModel(playerToken, 1.55f);
            art.Shape(playerToken, "Base de la ficha", PrimitiveType.Cylinder,
                Vector3.up * 0.04f, new Vector3(0.85f, 0.04f, 0.85f), PrototypeArt.Jade);
            playerToken.position = TokenPosition(routeNode);
            playerToken.localRotation = Quaternion.Euler(0, 180, 0);
        }

        Vector3 TokenPosition(int node) => nodes[node] + new Vector3(0, 0, -0.85f);

        void LateUpdate()
        {
            if (backdrop != null) PositionBackdrop();
            LateJudgmentCut();
        }

        void PositionBackdrop()
        {
            float artworkAspect = frameBackdrop ? view.aspect : 16f / 9f;
            backdrop.SetPositionAndRotation(view.transform.position + view.transform.forward * 40f, view.transform.rotation);
            float height = view.orthographicSize * 2f * Mathf.Max(1f, view.aspect / artworkAspect);
            backdrop.localScale = new Vector3(height * artworkAspect, height, 1f);
        }

        void StartTravel()
        {
            if (mode != Mode.Map || paused || shojiTime >= 0f || !progress.IsUnlocked(selected) || selected == 0) return;
            mission = selected;
            mode = Mode.Travel;
            travelTime = 0;
            nextRouteNode = routeNode + Math.Sign(mission - routeNode);
            if (mission == routeNode) Arrive();
        }

        void UpdateTravel(float delta)
        {
            travelTime = Mathf.Min(1, travelTime + delta / 0.85f);
            Vector3 previous = playerToken.position;
            playerToken.position = PrototypeArt.PathPoint(TokenPosition(routeNode), TokenPosition(nextRouteNode), Mathf.SmoothStep(0, 1, travelTime));
            Vector3 heading = playerToken.position - previous;
            if (heading.sqrMagnitude > 0.0001f) playerToken.rotation = Quaternion.LookRotation(heading);
            if (travelTime < 1) return;
            routeNode = nextRouteNode;
            if (routeNode == mission) Arrive();
            else
            {
                nextRouteNode += Math.Sign(mission - routeNode);
                travelTime = 0;
            }
        }

        void Arrive()
        {
            progress.currentNode = mission;
            Save();
            mode = IsBoss || LanternDuel ? Mode.FaceOff : Mode.ScrollClosing;
            scrollTime = 0;
            dialogueLine = 0;
            playerToken.rotation = Quaternion.LookRotation(nodes[mission] - playerToken.position);
        }

        void BeginBattle()
        {
            NewWorld();
            mode = Mode.Battle;
            paused = false;
            view.orthographic = true;
            view.transform.position = new Vector3(0, 4, -15);
            view.transform.rotation = Quaternion.identity;
            view.orthographicSize = Mathf.Max(5.4f, 7.6f / view.aspect);
            view.backgroundColor = new Color(0.16f, 0.2f, 0.2f);
            backdrop = art.Backdrop(world, "Lámina del río", "KamishibaiRio");
            frameBackdrop = false;
            PositionBackdrop();
            if (IsBoss)
            {
                opponent = CreateUmbrellaFigure(new Vector3(0, 4.1f, 3), 3.15f);
                opponent.rotation = Quaternion.identity;
            }
            else opponent = null;
            BeginPaperTheater();
            ResetBossRitual();
            ResetBossVoice();
            lives = 3;
            hits = misses = wounds = combo = bestCombo = guard = waveNumber = 0;
            frogHints.Clear(); // each special type gets its one-line hint again in every battle
            battleTime = 0;
            arcade = new FestivalScore();
            nextRushVolley = 0;
            if (IsTrail) Tell("Corta al destello o reúne tres chanclas en un tajo", 3.2f);
            judgmentCharge = 0; judgmentTap = false;
            comboTime = comboPulse = 0;
            waveTimer = 1.15f;
            bossMaxHealth = 12;
            bossHealth = bossMaxHealth;
            guardLimit = 7;
            vulnerable = false;
            slash = art.Line(world, new[] { Vector3.zero, Vector3.zero }, PrototypeArt.Paper, 0.055f);
            slash.enabled = false;
            BeginDefense();
            BeginLanternDuel();
            PlayBattleMusic();
        }

        void Update()
        {
            if (view == null) return;
            if (UpdateShojiTransition()) return;
            if (mode == Mode.Title)
            {
                UpdateTitleForest();
                return;
            }
            if (mode == Mode.Transition) return;
            if (mode == Mode.ScrollOpening || mode == Mode.ScrollClosing)
            {
                UpdateScroll(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
                return;
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && mode != Mode.Map && mode != Mode.Result)
                TogglePause();
            if (paused) return;
            float delta = Mathf.Min(Time.deltaTime, 0.05f);
            noticeTime -= delta;
            if (mode == Mode.Result)
            {
                // Keep the actor alive beside the results scroll; combat and its timers stay finished.
                UpdateFighter(delta);
                return;
            }
            if (mode == Mode.Travel) UpdateTravel(delta);
            else if (mode == Mode.Battle)
            {
                UpdateCutEffects(delta);
                UpdateEnergyBursts(delta);
                UpdateDefense(delta);
                UpdateJutsu(delta);
                UpdateJudgmentCut(delta);
                float combatDelta = impactTime > 0 ? delta * 0.12f : delta;
                impactTime = Mathf.Max(0, impactTime - delta);
                UpdateBattle(combatDelta);
                if (mode == Mode.Battle) { ReadSwipe(delta); ResolveDuelContact(); }
                UpdateFighter(combatDelta);
                if (IsBoss) UpdateBossRitual(combatDelta);
                if (LanternDuel && mode == Mode.Battle) UpdateLanternDuel(combatDelta);
            }
        }

        void UpdateBattle(float delta)
        {
            if (bossFinalStage > 0) return;
            battleTime += delta;
            UpdatePaperTheater(delta);
            if (mode != Mode.Battle) return;
            if (!IsBoss && !LanternDuel && battleTime >= 35)
            {
                Finish(true);
                return;
            }
            if (vulnerable)
            {
                counterTime -= delta;
                if (counterTime <= 0) CloseCounter();
                return;
            }
            bool rush = IsTrail && arcade.RushLeft > 0;
            if (IsTrail) arcade.Tick(delta);
            if (rush && battleTime < 32f && (nextRushVolley -= delta) <= 0)
            {
                // Alternate banks. Stagger the second arrival just enough for one sweep.
                float bank = Mathf.Sin(battleTime * 3f) >= 0 ? -1f : 1f;
                for (int k = 0; k < 3; k++)
                    pendingFrogs.Add((WaveLeadTime + .09f * k, bank * (4.1f - k), 8f + .35f * k,
                        -bank * .9f, FrogKind.Common, 0));
                nextRushVolley = 1.35f;
            }
            waveTimer -= delta;
            if (projectiles.Count > 0 || pendingFrogs.Count > 0 || pendingBombDelay >= 0f)
                waveTimer = Mathf.Max(waveTimer, 0.38f);
            if (pendingBombDelay >= 0f)
            {
                if (pendingBombDelay > LaunchTell && pendingBombDelay - delta <= LaunchTell) LaunchRipple(pendingBombX, PrototypeArt.Red);
                pendingBombDelay -= delta;
                if (pendingBombDelay <= 0f)
                {
                    pendingBombDelay = -1f;
                    SpawnProjectile(ProjectileKind.Bomb, pendingBombX, mission == 2 ? 9f : 10.9f);
                }
            }
            if (!rush && waveTimer <= 0 && (!IsBoss || theaterState == 0) && (IsBoss || battleTime < 30) && (!LanternDuel || lanternStage == 0))
            {
                SpawnWave();
                float pressure = Mathf.Clamp01(battleTime / 30f);
                waveTimer = jutsuTime > 0 ? 1.12f : Mathf.Lerp(HardTrail ? HardTrailIntervalFrom : TrailIntervalFrom,
                    HardTrail ? HardTrailIntervalTo : SecondPhase ? 1.3f : TrailIntervalTo, pressure);
                waveTimer += waveNumber % 3 == 0 ? 0.24f : -0.10f;
            }
            float flightDelta = jutsuTime > 0 ? delta * 0.62f : delta;
            for (int i = pendingFrogs.Count - 1; i >= 0; i--)
            {
                var frog = pendingFrogs[i];
                if (frog.delay > LaunchTell && frog.delay - delta <= LaunchTell)
                {
                    LaunchRipple(frog.x, FrogRipple(frog.kind)); // the ripple colour names the type before it leaves the river
                    FrogHint(frog.kind);
                }
                frog.delay -= delta;
                if (frog.delay > 0f) pendingFrogs[i] = frog;
                else
                {
                    SpawnProjectile(ProjectileKind.Frog, frog.x, frog.speed, frog.drift, frog.kind, frog.pair);
                    pendingFrogs.RemoveAt(i);
                }
            }
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile item = projectiles[i];
                item.age += flightDelta;
                item.velocity += Vector3.down * 5.2f * flightDelta;
                item.root.position += item.velocity * flightDelta;
                item.root.Rotate(item.spin * flightDelta, Space.World);
                if (item.kind == ProjectileKind.Frog)
                {
                    // Leap out of the river with a small overshoot instead of appearing at full size.
                    if (IsTrail && item.velocity.y <= FestivalScore.ApexSpeed &&
                        item.velocity.y + 5.2f * flightDelta > FestivalScore.ApexSpeed)
                        ShockRingBurst(item.root.position + Vector3.back * .35f, Quaternion.identity, .9f, PrototypeArt.Gold);
                    float pop = Mathf.Clamp01(item.age / 0.22f);
                    item.root.localScale = Vector3.one * ((0.6f + 0.4f * pop + Mathf.Sin(pop * Mathf.PI) * 0.18f) * item.size);
                    // The sole stretches on launch, levels at the apex and tips on descent.
                    float tilt = Mathf.Clamp(item.velocity.y * 3f, -28f, 28f);
                    float roll = item.spin.z * item.age;
                    float flutter = Mathf.Sin(item.age * 7f);
                    if (item.frog == FrogKind.Armoured) roll = flutter * 9f;
                    else if (item.frog == FrogKind.Linked) roll = flutter * 22f;
                    else if (item.sandalStyle == 1) roll = 32f * flutter; // Jade rocks from heel to toe.
                    else if (item.sandalStyle == 2) roll *= 1.8f; // Vermilion cartwheels.
                    item.root.rotation = Quaternion.Euler(tilt, 12f * flutter, roll);
                    item.puppet?.Animate(item.age, item.velocity.y, item.orphan > 0f || item.frog == FrogKind.Linked ? .5f : 0f, view.transform.forward);
                    if (item.thread != null)
                    {
                        if (item.partner == null) { Destroy(item.thread.gameObject); item.thread = null; } // one end was cut or escaped: the thread snaps
                        else
                        {
                            item.thread.SetPosition(0, item.root.position + Vector3.back * 0.3f);
                            item.thread.SetPosition(1, item.partner.root.position + Vector3.back * 0.3f);
                        }
                    }
                    if (item.orphan > 0f && (item.orphan -= delta) <= 0f && item.partner == null) EnrageFrog(item);
                }
                else if (item.kind == ProjectileKind.Bomb)
                    item.root.localScale = Vector3.one * (1f + Mathf.Sin(item.age * 16f) * 0.07f);
                if (item.kind == ProjectileKind.Heavy)
                    item.root.localScale = Vector3.one * (item.age >= 0.7f && item.age <= 1.8f ? 1.18f : 1);
                bool dangerous = item.kind == ProjectileKind.Heavy;
                if (item.root.position.y < (dangerous ? 0.65f : -1.8f) && item.velocity.y < 0)
                {
                    bool blocked = dangerous && TryGuard(item.root.position, 58f);
                    // A golden frog is pure reward: letting it go is not a miss.
                    if (item.kind != ProjectileKind.Bomb && !blocked && item.frog != FrogKind.Golden) misses++;
                    RemoveProjectile(i);
                    if (dangerous && !blocked) Hurt();
                    if (blocked && guardHeldTime <= DeflectWindow)
                    {
                        DamagePosture(3);
                        if (guard >= guardLimit) { OpenCounter(); return; }
                    }
                    if (mode != Mode.Battle) return;
                }
            }
        }

        // Sendero tuning: node 1 is the base trail; node 3 (second trail) is harder. Boss nodes ignore these.
        const float TrailIntervalFrom = 2.05f, TrailIntervalTo = 1.55f, HardTrailIntervalFrom = 1.8f, HardTrailIntervalTo = 1.35f;
        const int TrailFrogs = 4, HardTrailFrogs = 5, TrailBombEvery = 4, HardTrailBombEvery = 3;
        bool HardTrail => mission == 2 || mission == 3;
        // Special frogs. Waves are 1-based; node 3 brings every type HardTrailEarlier waves sooner and allows two specials a wave.
        const int ArmouredFromWave = 4, GoldenFromWave = 5, LinkedFromWave = 7, BossLinkedFromWave = 3, HardTrailEarlier = 2;
        const int TrailSpecials = 1, HardTrailSpecials = 2;
        const float GoldenChance = 1f / 6f, GoldenSize = 0.7f, GoldenSpeed = 7.6f, GoldenDrift = 3.4f, GoldenJudgmentGain = 1f / 3f;
        const float ArmouredSize = 1.15f, ArmourJuggleRise = 2.6f;
        const float LinkedStrokeWindow = 0.3f, LinkedHopBoost = 7f, LinkedHopDrift = 1.5f, LinkedPairGain = 2f * JudgmentSliceGain;

        void SpawnWave()
        {
            int wave = waveNumber++;
            if (IsBoss && wave % 4 == 2 && jutsuTime <= 0)
            {
                BeginTheaterAttack();
                return;
            }
            float offset = Mathf.Sin(waveNumber * 2.1f) * 0.45f;
            int sequence = frogPattern++;
            // Opens with the plain volley, then mixes the rest so two waves never read alike.
            int pattern = PatternOrder[sequence % PatternOrder.Length];
            float direction = sequence % 2 == 0 ? 1f : -1f;
            int count = (HardTrail ? HardTrailFrogs : TrailFrogs) + (abundanceTime > 0 ? 2 : 0);
            bool splitBanks = mission == 2 && wave % HardTrailBombEvery == 1 && jutsuTime <= 0;
            int firstFrog = pendingFrogs.Count;
            for (int i = 0; i < count; i++)
            {
                float lane = i / (float)(count - 1) * 2f - 1f;
                float x = -0.5f + offset + lane * 4.15f;
                float speed = 9.25f;
                float drift = -lane * 0.4f;
                float delay = 0f;
                switch (pattern)
                {
                    case 1: // A sweep starts on one edge and crosses the arena in beats.
                        x = -0.5f + offset + lane * direction * 3.8f;
                        speed = 8.8f + i * 0.22f;
                        delay = i * 0.20f;
                        drift = direction * 0.48f;
                        break;
                    case 2: // Two pairs converge near the apex without sharing a hitbox.
                        x = -0.5f + offset + lane * 4.3f;
                        speed = 9.55f - Mathf.Abs(lane) * 0.35f;
                        drift = -lane * 1.45f;
                        delay = (i % 2) * 0.10f;
                        break;
                    case 3: // A tight fan opens into a single diagonal cut.
                        x = -0.5f + offset + lane * 1.95f;
                        speed += lane * direction * 0.70f;
                        drift = lane * 1.12f;
                        break;
                    case 4: // A short low volley followed by a high reply on the other side.
                        bool secondVolley = i >= count / 2;
                        x = -0.5f + offset + direction * (secondVolley ? 2.7f : -2.7f) + lane * 1.25f;
                        speed = secondVolley ? 9.75f : 8.55f;
                        delay = secondVolley ? 0.68f : 0f;
                        drift = direction * (secondVolley ? -0.30f : 0.62f);
                        break;
                    case 5: // Pairs leap from both banks and cross over the middle.
                        float bank = i % 2 == 0 ? -1f : 1f;
                        x = bank * 4.7f;
                        speed = 9.3f + (i / 2) * 0.3f;
                        drift = -bank * (1.9f + (i / 2) * 0.35f);
                        delay = (i / 2) * 0.34f;
                        break;
                    case 6: // A level row: one clean horizontal cut takes them all.
                        x = -0.5f + offset + lane * 3.1f;
                        speed = 9.4f;
                        drift = 0f;
                        break;
                    case 7: // A column that stretches as it rises: one vertical cut.
                        x = -0.5f + offset + direction * 2.4f + lane * 0.35f;
                        speed = 8.3f + i * 1.5f / (count - 1);
                        drift = -direction * 0.35f;
                        break;
                }
                if (splitBanks)
                {
                    // Two compact groups flank the bomb. Lift the blade and start a second stroke.
                    float bank = i < count / 2 ? -1f : 1f;
                    int slot = i < count / 2 ? i : i - count / 2;
                    x = bank * (2.25f + slot * .9f);
                    speed = 8.8f + slot * .22f;
                    drift = bank * .12f;
                    delay = bank == direction ? .16f : 0f;
                }
                pendingFrogs.Add((WaveLeadTime + delay, x, speed, drift, FrogKind.Common, 0));
            }
            AssignSpecials(firstFrog, count, wave);
            if (jutsuTime <= 0 && (wave % (HardTrail ? HardTrailBombEvery : TrailBombEvery) == 1 || SecondPhase))
            {
                // The warning ripple appears in the same lane as the bomb.
                pendingBombX = splitBanks ? 0f : direction * 5.2f;
                pendingBombDelay = WaveLeadTime + (splitBanks ? .08f : .95f);
            }
            if (wave % 4 == 3 && jutsuTime <= 0 && jutsuSeal == null) ShowJutsuSeal();
        }

        // Special frogs replace members of the wave just queued (pendingFrogs[first..first+count)); the 8 patterns are untouched.
        void AssignSpecials(int first, int count, int wave)
        {
            int number = wave + 1, early = HardTrail ? HardTrailEarlier : 0;
            int armoured = ArmouredFromWave - early, golden = GoldenFromWave - early, linked = (IsBoss ? BossLinkedFromWave : LinkedFromWave) - early;
            var kinds = new List<FrogKind>();
            // Priority order, cut to the per-wave cap. A type's first wave excludes the others, so each is taught alone before mixing.
            if (!IsBoss && number >= golden && number != armoured && number != linked && UnityEngine.Random.value < GoldenChance) kinds.Add(FrogKind.Golden);
            if (number >= linked && (number - linked) % 2 == 0) kinds.Add(FrogKind.Linked);
            if (!IsBoss && number >= armoured && number != linked) kinds.Add(FrogKind.Armoured);
            int cap = HardTrail ? HardTrailSpecials : TrailSpecials;
            if (kinds.Count > cap) kinds.RemoveRange(cap, kinds.Count - cap);
            var taken = new bool[count];
            foreach (FrogKind kind in kinds)
            {
                int need = kind == FrogKind.Linked ? 2 : 1;
                int start = kind == FrogKind.Armoured ? count / 2 : kind == FrogKind.Golden ? wave * 3 % count : wave % 2 == 0 ? 0 : count - 2;
                for (int tries = 0; tries < count; tries++)
                {
                    int i = (start + tries) % count;
                    if (i + need > count || taken[i] || (need == 2 && taken[i + 1])) continue;
                    var frog = pendingFrogs[first + i];
                    frog.kind = kind;
                    if (kind == FrogKind.Golden)
                    {
                        // Flatter and faster: it streaks low across the whole river instead of arcing up.
                        float side = wave % 2 == 0 ? -1f : 1f;
                        frog.x = side * 4.7f; frog.drift = -side * GoldenDrift; frog.speed = GoldenSpeed;
                    }
                    pendingFrogs[first + i] = frog;
                    taken[i] = true;
                    if (kind == FrogKind.Linked)
                    {
                        // Both ends leave together so the thread is visible from the first frame.
                        var mate = pendingFrogs[first + i + 1];
                        frog.pair = mate.pair = ++linkSerial;
                        mate.kind = kind; mate.delay = frog.delay; mate.speed = frog.speed;
                        pendingFrogs[first + i] = frog; pendingFrogs[first + i + 1] = mate;
                        taken[i + 1] = true;
                    }
                    break;
                }
            }
        }

        static Color FrogRipple(FrogKind kind) => kind == FrogKind.Armoured ? PrototypeArt.Red : kind == FrogKind.Golden ? PrototypeArt.Gold
            : kind == FrogKind.Linked ? JutsuInk : PrototypeArt.Paper;

        void FrogHint(FrogKind kind)
        {
            if (kind == FrogKind.Common || !frogHints.Add(kind)) return;
            Tell(kind == FrogKind.Armoured ? "Chancla acorazada: dos tajos" : kind == FrogKind.Linked ? "Chanclas enlazadas: córtalas de un solo tajo"
                : "Chancla dorada: restaura una vida y no cuenta si escapa", 3.5f);
        }

        void SpawnProjectile(ProjectileKind kind, float x, float speed, float drift = 0f, FrogKind frog = FrogKind.Common, int pair = 0)
        {
            var root = new GameObject(kind.ToString()).transform;
            root.SetParent(world);
            root.position = new Vector3(x, -1.1f, 0);
            if (kind == ProjectileKind.Frog)
            {
                CreateFrog(root, 1.55f);
            }
            else if (kind == ProjectileKind.Bomb)
            {
                // Flat cut-out with an ink border, like the rest of the paper theatre.
                art.Shape(root, "Borde de tinta", PrimitiveType.Sphere, Vector3.forward * 0.03f, new Vector3(0.96f, 0.96f, 0.08f), PrototypeArt.Ink);
                art.Shape(root, "Bomba", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.8f, 0.8f, 0.1f), PrototypeArt.Red);
                art.Shape(root, "Mecha", PrimitiveType.Cylinder, Vector3.up * 0.52f, new Vector3(0.1f, 0.2f, 0.05f), PrototypeArt.Ink);
                art.Shape(root, "Chispa", PrimitiveType.Sphere, Vector3.up * 0.76f, new Vector3(0.2f, 0.2f, 0.05f), PrototypeArt.Gold);
                art.Line(root, new[] { new Vector3(-0.2f, -0.2f, -0.1f), new Vector3(0.2f, 0.2f, -0.1f) }, PrototypeArt.Paper, 0.07f);
                art.Line(root, new[] { new Vector3(-0.2f, 0.2f, -0.1f), new Vector3(0.2f, -0.2f, -0.1f) }, PrototypeArt.Paper, 0.07f);
            }
            else
            {
                art.Shape(root, "Ataque pesado", PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.95f, PrototypeArt.Gold);
                art.Line(root, new[] { new Vector3(-0.35f, -0.35f, -0.5f), new Vector3(0.35f, 0.35f, -0.5f) }, PrototypeArt.Ink, 0.09f);
            }
            Vector3 spin = Vector3.zero;
            if (kind == ProjectileKind.Frog)
            {
                float turn = x < 0 ? 1f : -1f;
                // Specials only roll flat to the camera, so their paper props never turn edge-on.
                spin = frog == FrogKind.Common ? new Vector3(0, 0, 116f * turn) : new Vector3(0, 0, (frog == FrogKind.Golden ? 90f : 38f) * turn);
            }
            else if (kind == ProjectileKind.Bomb) spin = Vector3.forward * 30f;
            var spawned = new Projectile { root = root, kind = kind, velocity = new Vector3(drift, speed, 0),
                spin = spin, radius = kind == ProjectileKind.Frog ? 1.1f : 0.6f, frog = frog, pair = pair };
            spawned.puppet = root.GetComponentInChildren<SandalPuppet>();
            projectiles.Add(spawned);
            if (frog != FrogKind.Common) DecorateFrog(spawned);
            else if (kind == ProjectileKind.Frog)
            {
                spawned.sandalStyle = commonSandalCount++ % 3;
                if (spawned.sandalStyle != 0)
                {
                    spawned.tints = new List<Material>();
                    TintFrog(spawned, root.GetChild(0), spawned.sandalStyle == 1 ? PrototypeArt.Jade : PrototypeArt.Red, .48f);
                }
            }
            // River splash on launch; capped like the cut sparks so effects never pile up.
            int drops = Mathf.Min(3, Mathf.Max(0, 48 - cutEffects.Count));
            for (int i = 0; i < drops; i++)
            {
                float side = i - 1;
                var drop = art.Shape(world, "Salpicadura", PrimitiveType.Sphere, root.position + new Vector3(side * 0.25f, 0.1f, -0.4f),
                    new Vector3(0.09f, 0.22f, 0.03f), kind == ProjectileKind.Bomb ? PrototypeArt.Red : PrototypeArt.Paper);
                drop.transform.rotation = Quaternion.Euler(0, 0, -side * 32f);
                AddDebris(drop.transform, new Vector3(side * 1.7f + drift * 0.4f, 4.4f - Mathf.Abs(side) * 1.1f, 0), Vector3.zero, 0.42f);
            }
        }

        const float LaunchTell = 0.3f;
        static readonly int[] PatternOrder = { 0, 6, 1, 3, 5, 2, 7, 4 };

        // A paper ripple on the river edge marks where the next leap comes from.
        void LaunchRipple(float x, Color color)
        {
            if (cutEffects.Count >= 48) return;
            var ripple = art.Shape(world, "Onda del río", PrimitiveType.Sphere,
                new Vector3(x, 4.3f - view.orthographicSize, -0.4f), new Vector3(1.15f, 0.1f, 0.03f), color);
            AddDebris(ripple.transform, Vector3.up, Vector3.zero, LaunchTell);
        }

        void ReadSwipe(float delta)
        {
            bool pressed = false;
            Vector2 pointer = lastPointer;
            if (Touchscreen.current != null && (Touchscreen.current.primaryTouch.press.isPressed || Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
            {
                pressed = Touchscreen.current.primaryTouch.press.isPressed;
                pointer = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null)
            {
                pressed = Mouse.current.leftButton.isPressed;
                pointer = Mouse.current.position.ReadValue();
            }
            bool touchActive=Touchscreen.current!=null && Touchscreen.current.primaryTouch.press.isPressed;
            bool canceled=Touchscreen.current!=null && Touchscreen.current.primaryTouch.phase.ReadValue()==UnityEngine.InputSystem.TouchPhase.Canceled;
            if((wasPressed && !gestureFromTouch && touchActive) || canceled || (wasPressed && gestureFromTouch && (Touchscreen.current==null || (touchActive && Touchscreen.current.primaryTouch.touchId.ReadValue()!=gestureTouchId))))
            {CancelActiveGesture();return;}
            if(inputBlockedUntilRelease)
            {
                if(!AnyGesturePointerPressed())inputBlockedUntilRelease=false;
                lastPointer=pointer;return;
            }
            if (pressed && !wasPressed)
            {
                Vector2 ui=new Vector2(pointer.x*1280/Screen.width,(Screen.height-pointer.y)*720/Screen.height);
                if(new Rect(1140,30,112,52).Contains(ui)){CancelActiveGesture();return;}
                gestureFromTouch=touchActive;gestureTouchId=touchActive?Touchscreen.current.primaryTouch.touchId.ReadValue():-1;
                strokeGestureEpoch=gestureEpoch;
                strokeStart = lastPointer = pointer;
                if(bossFinalStage==1)BeginDrawGrip(pointer);
                duelDrawing.Clear();duelDrawing.Add(PointerWorld(pointer));
                duelGestureBeat=duelPhase==DuelPhase.Telegraph?duelBeat:-1;
                bossSealDrawing.Clear();
                bossSealDrawing.Add(PointerWorld(pointer));
                strokeHits = 0;
                strokeSerial++; // identifies this stroke for linked pairs and armour tears
                strokeWhooshPlayed = strokeHasSwing = false;
                validStroke = (DuelActive && pointer.y > Screen.height*.10f && pointer.y < Screen.height*.88f) || bossFinalStage == 1 || (pointer.y > Screen.height * 0.12f && pointer.y < Screen.height * 0.81f);
                ResetSealTrace();
                if (validStroke && !DuelActive && !vulnerable && bossFinalStage == 0) TraceJutsu(PointerWorld(pointer), PointerWorld(pointer));
                trailPoints.Clear();
                trailAges.Clear();
            }
            if (wasPressed && strokeGestureEpoch==gestureEpoch && validStroke && (pointer - lastPointer).sqrMagnitude > 4)
            {
                Vector3 a = PointerWorld(lastPointer);
                Vector3 b = PointerWorld(pointer);
                AddTrail(a, b);
                if(DuelActive && duelDrawing.Count<512)duelDrawing.Add(b);
                if (vulnerable && bossSealDrawing.Count < 512) bossSealDrawing.Add(b);
                if (!vulnerable && !BossGesture(a, b) && !TraceJutsu(a, b))
                {
                    SliceProjectiles(a, b);
                    if (!strokeHasSwing && !DuelActive && !guarding && mode == Mode.Battle &&
                        (pointer - strokeStart).sqrMagnitude > Screen.height * Screen.height * .0004f)
                    {
                        if (strokeHits == 0) FighterCut(b - PointerWorld(strokeStart));
                        strokeHasSwing = true;
                    }
                }
            }
            if (!pressed && wasPressed && validStroke && mode == Mode.Battle)
            {
                if(strokeWhooshPlayed && bossFinalStage==0)EndSwingSound();
                if(DuelActive && !vulnerable){duelDrawing.Add(PointerWorld(pointer));CompleteDuelGesture(duelDrawing);}
                else if (!vulnerable) BossGesture(PointerWorld(strokeStart), PointerWorld(pointer));
                if (vulnerable)
                {
                    bossSealDrawing.Add(PointerWorld(pointer));
                    TryCounter(PointerWorld(strokeStart), PointerWorld(pointer));
                }
                ResetSealTrace();
            }
            if(!pressed && wasPressed){validStroke=false;gestureFromTouch=false;}
            wasPressed = pressed;
            lastPointer = pointer;
        }

        Vector3 PointerWorld(Vector2 point) => view.ScreenToWorldPoint(new Vector3(point.x, point.y, 15));

        static float SegmentDistance(Vector3 point, Vector3 a, Vector3 b)
        {
            point.z = a.z = b.z = 0;
            Vector3 segment = b - a;
            float fraction = Vector3.Dot(point - a, segment) / Mathf.Max(0.00001f, segment.sqrMagnitude);
            return Vector3.Distance(point, a + segment * Mathf.Clamp01(fraction));
        }

        // judgment: a slash line of the Destierro technique. Same cut and reward as a swipe, but it never sets off bombs,
        // never lands heavy attacks and gives no judgment charge (the technique must not feed itself).
        void SliceProjectiles(Vector3 a, Vector3 b, bool judgment = false)
        {
            if (LanternDuel && !judgment) LanternCut(a, b);
            // Resolve bombs first so a guard break cannot erase a bomb touched by the same slash.
            for (int i = projectiles.Count - 1; i >= 0 && !judgment; i--)
            {
                Projectile item = projectiles[i];
                if (item.kind != ProjectileKind.Bomb || SegmentDistance(item.root.position, a, b) > item.radius) continue;
                if (IsTrail) arcade.HitBomb(strokeSerial);
                bool blocked = TryGuard(item.root.position, 38f);
                RemoveProjectile(i);
                if (!blocked) Hurt();
                if (mode != Mode.Battle) return;
            }
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile item = projectiles[i];
                if (item.kind == ProjectileKind.Bomb || SegmentDistance(item.root.position, a, b) > item.radius) continue;
                if (item.kind == ProjectileKind.Heavy &&
                    (judgment || item.age < 0.7f || item.age > 1.8f || Vector2.Dot(((Vector2)(b - a)).normalized, cutDirections[0]) < 0.7f)) continue;
                if (item.kind == ProjectileKind.Frog)
                {
                    if (item.tornStroke == strokeSerial) continue; // one stroke tears the armour, the next one purifies
                    if (item.armour != null) { TearArmour(item, a, b); continue; }
                    if (!CutFrog(item, a, b)) continue;
                }
                Vector3 position = item.root.position;
                int strength = item.kind == ProjectileKind.Heavy ? 3 : 1;
                // Cutting one end of a pair: the partner remembers the stroke (bonus if it falls to the same stroke, else it panics).
                bool pairBroken = item.linkStroke == strokeSerial;
                if (item.partner != null) { item.partner.linkStroke = strokeSerial; item.partner.orphan = LinkedStrokeWindow; }
                RemoveProjectile(i);
                int previousStyle = StyleLevel;
                RewardCut(position);
                if (item.kind == ProjectileKind.Frog)
                {
                    if (!judgment) JudgmentGain(item.frog == FrogKind.Golden ? GoldenJudgmentGain : JudgmentSliceGain);
                    if (item.frog == FrogKind.Golden) GoldenReward(position);
                    if (pairBroken)
                    {
                        if (!judgment) JudgmentGain(LinkedPairGain);
                        ShockRingBurst(position, Quaternion.identity, 1.6f, JutsuInk);
                        Tell("LAZO ROTO", 1.5f);
                    }
                }
                if (item.kind == ProjectileKind.Wisp && !judgment) JudgmentGain(JudgmentSliceGain);
                strokeHits++;
                // Clean stroke: every frog past the second in one stroke pays extra.
                if (item.kind == ProjectileKind.Frog && strokeHits >= 3)
                {
                    if (!judgment) JudgmentGain(JudgmentSliceGain);
                    Tell("TAJO LIMPIO ×" + strokeHits, 1.5f);
                }
                if (IsTrail && item.kind == ProjectileKind.Frog && !judgment)
                {
                    bool apex = Mathf.Abs(item.velocity.y) <= FestivalScore.ApexSpeed;
                    bool wasRushing = arcade.RushLeft > 0;
                    arcade.Cut(strokeSerial, battleTime, apex);
                    if (!wasRushing && arcade.RushLeft > 0)
                    {
                        nextRushVolley = 0;
                        Tell("FRENESÍ   ×2", 1.4f);
                    }
                    else if (apex && strokeHits < 3 && !pairBroken && item.frog != FrogKind.Golden)
                        Tell("CORTE EN LA CIMA", .8f);
                }
                FighterCut(b - a, StyleLevel > previousStyle);
                // Frogs sustain style; interrupting the yokai opens the sealing window.
            }
            if (!judgment && IsBoss && guard >= guardLimit) OpenCounter();
        }

        void OpenCounter()
        {
            if (IsBoss && !duelOpeningSeal) return;
            gestureEpoch++;
            theaterState = 2;
            theaterTime = 0;
            theaterStrike = true;
            ClearProjectiles();
            pendingFrogs.Clear();
            pendingBombDelay = -1f;
            ClearJutsuSeal();
            vulnerable = true;
            validStroke = false;
            counterTime = 10;
            ShowCounterGuide();
            PlayPostureSound(true);
        }

        void ShowCounterGuide() { ShowBossPattern(); }

        void TryCounter(Vector3 a, Vector3 b) { TraceBossPattern(a, b); }

        void CloseCounter()
        {
            gestureEpoch++;
            ClearPendingSeal();
            vulnerable = false;
            theaterState = DuelActive ? 2 : 3;
            theaterTime = 0;
            guard = 0;
            validStroke = false;
            if (counterGuide != null) Destroy(counterGuide.gameObject);
            waveTimer = 1.2f;
        }

        void Hurt()
        {
            if (IsBoss && DuelActive) BossSay(VoiceUmbrella, BossPick(UmbrellaTaunt), 2f);
            else if (LanternDuel && litTwin >= 0) BossSay(litTwin, BossPick(LanternTaunt), 2f);
            lives--;
            wounds++;
            combo = 0;
            comboTime = 0;
            hurtFlash = 0.32f;
            fighterReaction = 0.24f;
            shakeTime = 0.18f;
            if (lives <= 0) Finish(false);
        }

        void ClearProjectiles()
        {
            for (int i = projectiles.Count - 1; i >= 0; i--) RemoveProjectile(i);
        }

        void RemoveProjectile(int index)
        {
            Projectile item = projectiles[index];
            if (item.partner != null) item.partner.partner = null;
            // The halves of a cut frog still wear its tinted materials for a moment.
            if (item.tints != null) foreach (Material tint in item.tints) Destroy(tint, 2f);
            Destroy(item.root.gameObject);
            projectiles.RemoveAt(index);
        }

        void Finish(bool victory)
        {
            CancelActiveGesture();duelContactPending=false;
            mode = Mode.Result;
            if (battleMusic != null) battleMusic.Stop();
            if (IsTrail) progress.bestScores[mission] = Math.Max(progress.bestScores[mission], arcade.Points);
            if (mission == 2 && arcade.Points < FestivalScore.Target) victory = false;
            won = victory;
            companionMood = victory ? CompanionMood.Victory : CompanionMood.Disappointed;
            companionReactionTime = 1;
            companionReactionAge = 0;
            guarding = false;
            fighterSwing = 0;
            // The ink execution hides all stage renderers. Return the actor for the result bow.
            if (bossInkBurst && fighter != null)
                foreach (var renderer in fighter.GetComponentsInChildren<Renderer>()) renderer.enabled = true;
            resultRank = victory ? CampaignProgress.Grade(hits, misses, wounds) : Rank.None;
            if (mission == 2 && victory && resultRank < Rank.B) resultRank = Rank.B;
            int oldUnlocked = progress.unlockedThrough;
            progress.Record(mission, resultRank, victory);
            unlockedNew = progress.unlockedThrough > oldUnlocked;
            Save();
            validStroke = false;
            if (slash != null) slash.enabled = false;
        }

        void Tell(string message, float seconds) { notice = message; noticeTime = seconds; }

        bool AnyGesturePointerPressed()
        {
            if(Mouse.current!=null && Mouse.current.leftButton.isPressed)return true;
            if(Touchscreen.current!=null)foreach(var touch in Touchscreen.current.touches)if(touch.press.isPressed)return true;
            return false;
        }
        void CancelActiveGesture()
        {
            gestureEpoch++;validStroke=false;wasPressed=false;gestureFromTouch=false;gestureTouchId=-1;
            duelDrawing.Clear();bossSealDrawing.Clear();strokeHits=0;strokeWhooshPlayed=false;
            drawGripHeld=false;drawPullTarget=0;inputBlockedUntilRelease=true;
        }
        void TogglePause()
        {
            CancelActiveGesture();
            paused = !paused;
            if (finalDrawAudio != null) { if (paused) finalDrawAudio.Pause(); else finalDrawAudio.UnPause(); }
            if (spiritDissolveAudio != null) { if (paused) spiritDissolveAudio.Pause(); else spiritDissolveAudio.UnPause(); }
            if (battleMusic != null && mode == Mode.Battle)
            {
                if (paused) battleMusic.Pause();
                else battleMusic.UnPause();
            }
            validStroke = false;
            wasPressed = false;
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && !paused && (mode == Mode.Travel || mode == Mode.FaceOff || mode == Mode.Battle))
                TogglePause();
        }

        void OnDestroy()
        {
            DisposeSwingLayers();
            DisposeJudgmentCut();
            DisposeSamuraiTrail();
            DisposeEnergyBursts();
            Shader.SetGlobalFloat("_BossFinalInk", 0);
            art?.Dispose();
            ClearTitleAssets();
            if (cutSound != null) Destroy(cutSound);
            if (guardSound != null) Destroy(guardSound);
            if(parrySoundVariants!=null)foreach(var clip in parrySoundVariants)if(clip!=null)Destroy(clip);
            if (postureSound != null) Destroy(postureSound);
            if (battleFrame != null) Destroy(battleFrame);
        }
    }
}











