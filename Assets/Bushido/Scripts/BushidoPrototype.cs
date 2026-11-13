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
        enum ProjectileKind { Frog, Bomb, Heavy }

        sealed class Projectile
        {
            public Transform root;
            public ProjectileKind kind;
            public Vector3 velocity, spin;
            public float age;
            public float radius;
        }

        readonly string[] titles = { "El comienzo", "Sendero del bambú", "Drax", "Paso de la montaña", "Oni Samurái" };
        readonly Vector3[] nodes = {
            new Vector3(-5.8f, 0, -1.2f), new Vector3(-3.1f, 0, 1.2f),
            new Vector3(0, 0, -0.7f), new Vector3(3, 0, 1.3f), new Vector3(5.7f, 0, -0.6f)
        };
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<(float delay, float x, float speed, float drift)> pendingFrogs = new List<(float, float, float, float)>();
        int frogPattern;
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
        float faceTime;
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
            projectiles.Clear();
            pendingFrogs.Clear();
            pendingBombDelay = -1f;
            frogPattern = 0;
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
                    Transform figure = CreateBossFigure(i, nodes[i], i == 4 ? 2.1f : 1.85f);
                    figure.rotation = figure.GetComponent<PaperOgreSprite>() != null
                        ? view.transform.rotation : Quaternion.Euler(0, 180, 0);
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
            mode = Mode.FaceOff;
            faceTime = 0;
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
                opponent = CreateBossFigure(mission, new Vector3(0, 4.1f, 3), 3.15f);
                opponent.rotation = Quaternion.identity;
            }
            else opponent = null;
            BeginPaperTheater();
            ResetBossRitual();
            lives = 3;
            hits = misses = wounds = combo = bestCombo = guard = waveNumber = 0;
            battleTime = 0;
            comboTime = comboPulse = 0;
            waveTimer = 1.15f;
            bossMaxHealth = mission == 2 ? 6 : 12;
            bossHealth = bossMaxHealth;
            guardLimit = mission == 2 ? 5 : 7;
            vulnerable = false;
            slash = art.Line(world, new[] { Vector3.zero, Vector3.zero }, PrototypeArt.Paper, 0.055f);
            slash.enabled = false;
            BeginDefense();
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
            if (mode == Mode.Travel) UpdateTravel(delta);
            else if (mode == Mode.FaceOff)
            {
                faceTime += delta;
                if (faceTime >= 1.2f)
                {
                    mode = Mode.ScrollClosing;
                    scrollTime = 0;
                }
            }
            else if (mode == Mode.Battle)
            {
                UpdateCutEffects(delta);
                UpdateDefense(delta);
                UpdateJutsu(delta);
                float combatDelta = impactTime > 0 ? delta * 0.12f : delta;
                impactTime = Mathf.Max(0, impactTime - delta);
                UpdateBattle(combatDelta);
                if (mode == Mode.Battle) { ReadSwipe(delta); ResolveDuelContact(); }
                UpdateFighter(combatDelta);
                if (IsBoss) UpdateBossRitual(combatDelta);
            }
        }

        void UpdateBattle(float delta)
        {
            if (IsBoss && bossFinalStage > 0) return;
            battleTime += delta;
            UpdatePaperTheater(delta);
            if (mode != Mode.Battle) return;
            if (!IsBoss && battleTime >= 35)
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
                    SpawnProjectile(ProjectileKind.Bomb, pendingBombX, 10.9f);
                }
            }
            if (waveTimer <= 0 && (!IsBoss || theaterState == 0) && (IsBoss || battleTime < 30))
            {
                SpawnWave();
                float pressure = Mathf.Clamp01(battleTime / 30f);
                waveTimer = jutsuTime > 0 ? 1.12f : Mathf.Lerp(2.05f, SecondPhase ? 1.3f : 1.55f, pressure);
                waveTimer += waveNumber % 3 == 0 ? 0.24f : -0.10f;
            }
            float flightDelta = jutsuTime > 0 ? delta * 0.62f : delta;
            for (int i = pendingFrogs.Count - 1; i >= 0; i--)
            {
                var frog = pendingFrogs[i];
                if (frog.delay > LaunchTell && frog.delay - delta <= LaunchTell) LaunchRipple(frog.x, PrototypeArt.Paper);
                frog.delay -= delta;
                if (frog.delay > 0f) pendingFrogs[i] = frog;
                else
                {
                    SpawnProjectile(ProjectileKind.Frog, frog.x, frog.speed, frog.drift);
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
                    float pop = Mathf.Clamp01(item.age / 0.22f);
                    item.root.localScale = Vector3.one * (0.6f + 0.4f * pop + Mathf.Sin(pop * Mathf.PI) * 0.18f);
                }
                else if (item.kind == ProjectileKind.Bomb)
                    item.root.localScale = Vector3.one * (1f + Mathf.Sin(item.age * 16f) * 0.07f);
                if (item.kind == ProjectileKind.Heavy)
                    item.root.localScale = Vector3.one * (item.age >= 0.7f && item.age <= 1.8f ? 1.18f : 1);
                bool dangerous = item.kind == ProjectileKind.Heavy;
                if (item.root.position.y < (dangerous ? 0.65f : -1.8f) && item.velocity.y < 0)
                {
                    bool blocked = dangerous && TryGuard(item.root.position, 58f);
                    if (item.kind != ProjectileKind.Bomb && !blocked) misses++;
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
            int count = abundanceTime > 0 ? 6 : 4;
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
                pendingFrogs.Add((WaveLeadTime + delay, x, speed, drift));
            }
            if (jutsuTime <= 0 && (wave % 4 == 1 || SecondPhase))
            {
                pendingBombX = direction > 0 ? 5.2f : -5.2f;
                pendingBombDelay = WaveLeadTime + 0.95f;
            }
            if (wave % 4 == 3 && jutsuTime <= 0 && jutsuSeal == null) ShowJutsuSeal();
        }

        void SpawnProjectile(ProjectileKind kind, float x, float speed, float drift = 0f)
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
                spin = new Vector3(24f, 80f * turn, 38f * turn);
            }
            else if (kind == ProjectileKind.Bomb) spin = Vector3.forward * 30f;
            projectiles.Add(new Projectile { root = root, kind = kind, velocity = new Vector3(drift, speed, 0),
                spin = spin, radius = kind == ProjectileKind.Frog ? 1.1f : 0.6f });
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
                strokeWhooshPlayed = false;
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
                if (!vulnerable && !BossGesture(a, b) && !TraceJutsu(a, b)) SliceProjectiles(a, b);
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

        void SliceProjectiles(Vector3 a, Vector3 b)
        {
            // Resolve bombs first so a guard break cannot erase a bomb touched by the same slash.
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile item = projectiles[i];
                if (item.kind != ProjectileKind.Bomb || SegmentDistance(item.root.position, a, b) > item.radius) continue;
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
                    (item.age < 0.7f || item.age > 1.8f || Vector2.Dot(((Vector2)(b - a)).normalized, cutDirections[0]) < 0.7f)) continue;
                if (item.kind == ProjectileKind.Frog && !CutFrog(item, a, b)) continue;
                Vector3 position = item.root.position;
                int strength = item.kind == ProjectileKind.Heavy ? 3 : 1;
                RemoveProjectile(i);
                int previousStyle = StyleLevel;
                RewardCut(position);
                strokeHits++;
                FighterCut(b - a, StyleLevel > previousStyle);
                // Frogs sustain style; interrupting the yokai opens the sealing window.
            }
            if (IsBoss && guard >= guardLimit) OpenCounter();
        }

        void OpenCounter()
        {
            if (IsBoss && !duelOpeningSeal) return;
            gestureEpoch++;
            theaterState = 2;
            theaterTime = 0;
            theaterStrike = true;
            foreach (Projectile item in projectiles) Destroy(item.root.gameObject);
            projectiles.Clear();
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
            lives--;
            wounds++;
            combo = 0;
            comboTime = 0;
            hurtFlash = 0.32f;
            fighterReaction = 0.24f;
            shakeTime = 0.18f;
            if (lives <= 0) Finish(false);
        }

        void RemoveProjectile(int index)
        {
            Destroy(projectiles[index].root.gameObject);
            projectiles.RemoveAt(index);
        }

        void Finish(bool victory)
        {
            CancelActiveGesture();duelContactPending=false;
            mode = Mode.Transition;
            if (battleMusic != null) battleMusic.Stop();
            won = victory;
            resultRank = victory ? CampaignProgress.Grade(hits, misses, wounds) : Rank.None;
            int oldUnlocked = progress.unlockedThrough;
            progress.Record(mission, resultRank, victory);
            unlockedNew = progress.unlockedThrough > oldUnlocked;
            Save();
            validStroke = false;
            if (slash != null) slash.enabled = false;
            StartCoroutine(RevealMap());
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











