using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        sealed class CutEffect
        {
            public Transform root;
            public Vector3 velocity, spin, scale;
            public float age, lifetime;
            // Optional easing, set by the cut burst; defaults keep every other caller's debris as it was.
            public float gravity = 7, drag, punch, width;
            public LineRenderer line;
        }
        // Brush stroke: thin entry, full belly, dry tail. Shared so no curve is allocated per frame.
        static readonly AnimationCurve cutBrushCurve = new AnimationCurve(new Keyframe(0, 0.12f), new Keyframe(0.3f, 1), new Keyframe(1, 0.18f));
        static readonly AnimationCurve cutTrailCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.78f, 1), new Keyframe(1, 0.4f));
        static readonly Vector3[] cutStrokePoints = { Vector3.left, Vector3.right };
        static readonly Vector3[] cutEnsoPoints = CutEnso();
        readonly Vector3[] cutTrailBuffer = new Vector3[48];
        Vector3 cutStrokeDir;
        readonly List<CutEffect> cutEffects = new List<CutEffect>();
        readonly List<Vector3> trailPoints = new List<Vector3>();
        readonly List<float> trailAges = new List<float>();
        GameObject sandalPrefab;
        AudioSource cutAudio;
        AudioClip cutSound;
        float comboTime, comboPulse, impactTime, shakeTime;
        float scrollTime;
        const float ScrollOpenDuration = 1.22f;
        const float ScrollCloseDuration = 0.82f;
        Texture2D battleFrame;
        GUIStyle styleLetter;
        static readonly int[] styleThresholds = { 0, 3, 6, 10, 15, 22, 30 };
        static readonly string[] styleLetters = { "D", "C", "B", "A", "S", "SS", "SSS" };
        static readonly string[] styleNames = { "APRENDIZ", "ESPADACHÍN", "RONIN", "SAMURÁI", "MAESTRO DE LA ESPADA", "SHOGUN", "LEYENDA DEL BUSHIDO" };
        static readonly Color[] styleColors = { new Color(0.65f, 0.8f, 0.8f), new Color(0.3f, 0.9f, 0.65f), new Color(0.25f, 0.7f, 1), new Color(0.7f, 0.4f, 1), new Color(1, 0.68f, 0.18f), new Color(1, 0.35f, 0.18f), new Color(1, 0.15f, 0.4f) };
        int StyleLevel
        {
            get
            {
                for (int i = styleThresholds.Length - 1; i > 0; i--) if (combo >= styleThresholds[i]) return i;
                return 0;
            }
        }

        Transform CreateFrog(Transform parent, float size, float yaw = 180f)
        {
            if (sandalPrefab == null) sandalPrefab = Resources.Load<GameObject>("Models/chancla");
            if (sandalPrefab == null) throw new System.InvalidOperationException("Falta el modelo chancla en Resources/Models.");
            Transform model = Instantiate(sandalPrefab, parent, false).transform;
            model.localRotation = Quaternion.Euler(-12, yaw, -10);
            // Each sandal owns the meshes it shows: its puppet re-bakes the pose into them every frame,
            // so the slicer always cuts the pose that is on screen.
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var pieces = new MeshFilter[skins.Length];
            for (int i = 0; i < skins.Length; i++)
            {
                var skin = skins[i];
                var mesh = new Mesh { name = skin.name + " / pose" };
                mesh.MarkDynamic();
                skin.BakeMesh(mesh);
                var piece = new GameObject(skin.name + " / corte");
                piece.transform.SetParent(skin.transform, false);
                pieces[i] = piece.AddComponent<MeshFilter>();
                pieces[i].sharedMesh = mesh;
                piece.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                piece.AddComponent<SliceMeshOwner>();
                // Keep skinning alive for BakeMesh, but only the baked piece is drawn.
                skin.forceRenderingOff = true;
            }
            model.gameObject.AddComponent<SandalPuppet>().Bind(skins, pieces);
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) throw new System.InvalidOperationException("El modelo de chancla no contiene mallas renderizables.");
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = art.ModelMaterial(materials[i]);
                }
                renderer.sharedMaterials = materials;
            }
            float scale = size / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.001f);
            Vector3 offset = parent.InverseTransformPoint(bounds.center) - model.localPosition;
            model.localScale *= scale;
            model.localPosition = -offset * scale;
            return model;
        }

        void SetupCutAudio()
        {
            if (cutAudio != null) return;
            cutAudio = gameObject.AddComponent<AudioSource>();
            cutAudio.playOnAwake = false;
            cutAudio.spatialBlend = 0;
            if (FindAnyObjectByType<AudioListener>() == null) view.gameObject.AddComponent<AudioListener>();
            const int rate = 22050;
            var samples = new float[rate / 6];
            var random = new System.Random(27);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Exp(-t * 34) * Mathf.Min(1, t * 600);
                samples[i] = ((float)random.NextDouble() * 0.8f - 0.4f + Mathf.Sin(t * 1200 - t * t * 2100) * 0.5f) * envelope;
            }
            cutSound = AudioClip.Create("Acero y corte", samples.Length, 1, rate, false);
            cutSound.SetData(samples, 0);
        }

        static readonly Color FrogInterior = new Color(0.95f, 0.69f, 0.25f);

        // Special frogs read in flight by tint, size and a paper prop, all in the ink palette. Props and tints use
        // per-frog material clones so lifting a frog's render queue (judgment cut) never touches shared materials.
        void DecorateFrog(Projectile item)
        {
            item.tints = new List<Material>();
            Transform model = item.root.GetChild(0); // CreateFrog's model
            if (item.frog == FrogKind.Linked)
            {
                TintFrog(item, model, JutsuInk, 0.5f);
                LinkFrogs(item);
            }
            else if (item.frog == FrogKind.Golden)
            {
                item.size = GoldenSize;
                TintFrog(item, model, PrototypeArt.Gold, 0.85f);
                var halo = new Vector3[21];
                for (int i = 0; i < halo.Length; i++) halo[i] = new Vector3(Mathf.Cos(i * Mathf.PI * 2 / 20), Mathf.Sin(i * Mathf.PI * 2 / 20), -0.3f) * 0.95f;
                FrogLine(item, item.root, "Rana • aura", halo, PrototypeArt.Gold, 0.07f);
            }
            else
            {
                // Armour: a paper breastplate and helmet with ink borders and red cords.
                item.size = ArmouredSize;
                item.armour = new GameObject("Armadura de papel").transform;
                item.armour.SetParent(item.root, false);
                FramePlate(item, new Vector3(0, -0.05f, -0.5f), new Vector3(0.9f, 0.7f, 0.05f));
                FramePlate(item, new Vector3(0, 0.62f, -0.5f), new Vector3(0.76f, 0.26f, 0.05f));
                FrogLine(item, item.armour, "Rana • cordón", new[] { new Vector3(-0.45f, 0.15f, -0.6f), new Vector3(0.45f, 0.15f, -0.6f) }, PrototypeArt.Red, 0.09f);
                FrogLine(item, item.armour, "Rana • cordón", new[] { new Vector3(0, 0.45f, -0.6f), new Vector3(0, -0.4f, -0.6f) }, PrototypeArt.Red, 0.07f);
            }
            item.radius *= item.size;
        }

        Material FrogProp(Projectile item, Color color)
        {
            var material = new Material(art.Material(color)) { renderQueue = -1 };
            item.tints.Add(material);
            return material;
        }

        void FramePlate(Projectile item, Vector3 position, Vector3 scale)
        {
            art.Shape(item.armour, "Rana • placa", PrimitiveType.Cube, position, scale * 1.1f, PrototypeArt.Ink)
                .GetComponent<Renderer>().sharedMaterial = FrogProp(item, PrototypeArt.Ink);
            art.Shape(item.armour, "Rana • placa", PrimitiveType.Cube, position + Vector3.back * 0.04f, scale, PrototypeArt.Paper)
                .GetComponent<Renderer>().sharedMaterial = FrogProp(item, PrototypeArt.Paper);
        }

        LineRenderer FrogLine(Projectile item, Transform parent, string name, Vector3[] points, Color color, float width)
        {
            var line = art.Line(parent, points, color, width);
            line.name = name;
            line.sharedMaterial = FrogProp(item, color);
            return line;
        }

        void TintFrog(Projectile item, Transform model, Color tint, float amount)
        {
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var copy = new Material(materials[i]) { renderQueue = -1 };
                    copy.SetColor("_BaseColor", Color.Lerp(copy.GetColor("_BaseColor"), tint, amount));
                    item.tints.Add(copy);
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }

        // A spirit thread joins the two ends once both have leapt; the first end carries the line.
        void LinkFrogs(Projectile item)
        {
            foreach (Projectile other in projectiles)
            {
                if (other == item || other.pair != item.pair || other.partner != null) continue;
                other.partner = item; item.partner = other;
                other.thread = FrogLine(other, other.root, "Rana • hilo", new Vector3[2], JutsuInk, 0.09f);
                other.thread.useWorldSpace = true;
                return;
            }
        }

        // First cut of an armoured frog: the plates tear off as paper and the frog is knocked back up into a short arc.
        void TearArmour(Projectile item, Vector3 a, Vector3 b)
        {
            item.tornStroke = strokeSerial;
            Vector3 position = item.root.position;
            var plates = new List<Transform>();
            foreach (Transform plate in item.armour) plates.Add(plate);
            foreach (Transform plate in plates)
            {
                plate.SetParent(world, true);
                if (cutEffects.Count >= 48) { Destroy(plate.gameObject); continue; }
                Vector3 away = (plate.position - position).normalized;
                AddDebris(plate, away * 5f + Vector3.up * 3f, Vector3.forward * Random.Range(-360f, 360f), 0.55f).drag = 2f;
            }
            Destroy(item.armour.gameObject);
            item.armour = null;
            // Knock-back: rises ArmourJuggleRise above the cut point (v = sqrt(2 g h), g = 5.2 as in flight) and drifts the other way.
            item.velocity = new Vector3(-item.velocity.x * 0.6f, Mathf.Sqrt(2f * 5.2f * ArmourJuggleRise), 0);
            if (!strokeWhooshPlayed) { BeginSwingSound(); strokeWhooshPlayed = true; }
            SetupCutAudio();
            SliceImpactSound();
            cutStrokeDir = ((Vector2)(b - a)).normalized;
            CutBurst(position, false);
            shakeTime = Mathf.Max(shakeTime, 0.1f);
            impactTime = Mathf.Max(impactTime, 0.03f);
            FighterCut(b - a);
        }

        void GoldenReward(Vector3 position)
        {
            ShockRingBurst(position, Quaternion.identity, 1.8f, PrototypeArt.Gold);
            if (lives < 3) { lives++; Tell("Chancla dorada: una vida recuperada", 2.5f); }
            else Tell("Chancla dorada", 1.5f);
        }

        // The survivor of a half-cut pair bolts: a spirit burst, then a faster and higher hop.
        void EnrageFrog(Projectile item)
        {
            ShockRingBurst(item.root.position + Vector3.back * 0.5f, Quaternion.identity, 1.5f, JutsuInk);
            item.velocity = new Vector3(item.velocity.x * LinkedHopDrift, Mathf.Max(item.velocity.y, 0f) + LinkedHopBoost, 0);
        }

        bool CutFrog(Projectile item, Vector3 a, Vector3 b)
        {
            if (!FrogSlicer.Slice(item.root, a, b, art.Material(FrogInterior), out var first, out var second)) return false;
            Vector3 normal = Vector3.Cross(b - a, Vector3.forward).normalized;
            // Halves pop slightly as they part, then settle.
            AddDebris(first.transform, item.velocity * 0.35f + normal * 3.2f + Vector3.back, new Vector3(30, 40, -100), 1.4f).punch = 0.16f;
            AddDebris(second.transform, item.velocity * 0.35f - normal * 3.2f + Vector3.forward, new Vector3(-40, -30, 110), 1.4f).punch = 0.16f;
            cutStrokeDir = ((Vector2)(b - a)).normalized;
            return true;
        }

        CutEffect AddDebris(Transform root, Vector3 velocity, Vector3 spin, float lifetime)
        {
            var effect = new CutEffect { root = root, velocity = velocity, spin = spin, scale = root.localScale, lifetime = lifetime };
            cutEffects.Add(effect);
            return effect;
        }

        static Vector3[] CutEnso()
        {
            // Open brush circle (ensō): the gap and the tapered width make it read as one stroke.
            var points = new Vector3[25];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i / 24f * Mathf.PI * 1.86f;
                points[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            }
            return points;
        }

        // A sumi stroke that sweeps out fast, lingers and thins; unit points are sized by the root scale.
        void CutStroke(Vector3 position, Vector3[] points, Color color, float width, float lifetime, float size, float angle)
        {
            if (cutEffects.Count >= 48) return;
            var line = art.Line(world, points, color, width);
            line.name = "Trazo sumi";
            line.widthCurve = cutBrushCurve;
            line.numCornerVertices = 2;
            line.transform.position = position;
            line.transform.rotation = Quaternion.Euler(0, 0, angle);
            line.transform.localScale = Vector3.one * size;
            var effect = AddDebris(line.transform, Vector3.zero, Vector3.zero, lifetime);
            effect.gravity = 0; effect.line = line; effect.width = width;
            line.transform.localScale *= 0.3f; // matches the update's starting reach, so the spawn frame does not flash full size
        }

        // Ink stroke along the cut, torn paper and ink drops thrown off both sides; ensō on big moments.
        void CutBurst(Vector3 position, bool big)
        {
            Vector3 dir = cutStrokeDir;
            cutStrokeDir = Vector3.zero;
            bool directed = dir != Vector3.zero; // only frog cuts know their direction; the rest burst radially
            float cutAngle = Mathf.Atan2(dir.y, dir.x);
            Color rank = styleColors[StyleLevel];
            position += Vector3.back * 0.6f;
            if (directed) CutStroke(position, cutStrokePoints, PrototypeArt.Ink, big ? 0.17f : 0.12f, 0.3f, 1.7f, cutAngle * Mathf.Rad2Deg);
            if (big)
            {
                float size = 1.4f + StyleLevel * 0.12f, turn = Random.Range(0f, 360f);
                CutStroke(position, cutEnsoPoints, PrototypeArt.Ink, 0.15f, 0.44f, size, turn);
                CutStroke(position + Vector3.back * 0.02f, cutEnsoPoints, rank, 0.07f, 0.36f, size * 0.84f, turn + 150f);
            }
            int count = Mathf.Min(big ? 11 : 5, Mathf.Max(0, 48 - cutEffects.Count));
            for (int i = 0; i < count; i++)
            {
                float angle = directed ? cutAngle + (i % 2 == 0 ? 0.5f : -0.5f) * Mathf.PI + Random.Range(-0.6f, 0.6f)
                    : (i + Random.value * 0.6f) * Mathf.PI * 2 / count;
                Vector3 fly = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                bool ink = i % 3 == 2;
                // Flattened in z so both read as cut-outs: a blot of ink, a shred of washi.
                var piece = art.Shape(world, ink ? "Gota de tinta" : "Jirón de papel", ink ? PrimitiveType.Sphere : PrimitiveType.Cube, position + fly * 0.2f,
                    ink ? new Vector3(0.1f, 0.22f, 0.02f) : new Vector3(Random.Range(0.07f, 0.13f), Random.Range(0.16f, 0.3f), 0.01f) * (big ? 1.3f : 1f),
                    ink ? PrototypeArt.Ink : i % 5 == 0 ? rank : PrototypeArt.Paper);
                piece.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90f);
                AddDebris(piece.transform, fly * (Random.Range(5f, 8f) * (big ? 1.25f : 1f)),
                    ink ? Vector3.zero : Vector3.forward * Random.Range(-420f, 420f), Random.Range(0.36f, 0.5f)).drag = 4.5f;
            }
        }

        void RewardCut(Vector3 position)
        {
            if (!strokeWhooshPlayed)
            {
                BeginSwingSound();
                strokeWhooshPlayed = true;
            }
            int styleBefore = StyleLevel;
            hits++;
            combo++;
            bestCombo = Mathf.Max(bestCombo, combo);
            comboTime = 4.5f;
            comboPulse = 1;
            impactTime = 0.045f;
            // Combo milestones and rank ups get the bigger payoff (camera only; hit-stop is unchanged).
            bool big = combo % 5 == 0 || StyleLevel > styleBefore;
            shakeTime = big ? 0.2f : 0.14f;
            SetupCutAudio();
            SliceImpactSound();
            CutBurst(position, big);
        }

        void UpdateCutEffects(float delta)
        {
            comboPulse = Mathf.Max(0, comboPulse - delta * 4);
            if (!vulnerable && mode == Mode.Battle)
            {
                comboTime = Mathf.Max(0, comboTime - delta);
                if (comboTime <= 0) combo = 0;
            }
            shakeTime = Mathf.Max(0, shakeTime - delta);
            if(battleMusic!=null)battleMusic.volume=Mathf.MoveTowards(battleMusic.volume,katanaParryTime>0?.16f:.32f,delta*1.8f);
            view.transform.position = new Vector3(0, 4, -15) + (Vector3)Random.insideUnitCircle * shakeTime * 0.45f;
            for (int i = cutEffects.Count - 1; i >= 0; i--)
            {
                CutEffect effect = cutEffects[i];
                effect.age += delta;
                effect.velocity += Vector3.down * delta * effect.gravity;
                if (effect.drag > 0) effect.velocity *= Mathf.Exp(-effect.drag * delta);
                effect.root.position += effect.velocity * delta;
                effect.root.Rotate(effect.spin * delta, Space.World);
                if (effect.line != null)
                {
                    // Stroke: ease-out reach, then the ink thins away.
                    float u = Mathf.Min(1, effect.age / effect.lifetime), rest = 1 - u;
                    effect.root.localScale = effect.scale * (1 - 0.7f * rest * rest * rest);
                    effect.line.widthMultiplier = effect.width * (1 - u * u);
                }
                else effect.root.localScale = effect.scale * (Mathf.Clamp01((effect.lifetime - effect.age) / 0.3f)
                    * (1 + effect.punch * Mathf.Clamp01(1 - effect.age / 0.12f)));
                if (effect.age < effect.lifetime) continue;
                Destroy(effect.root.gameObject);
                cutEffects.RemoveAt(i);
            }
            for (int i = trailAges.Count - 1; i >= 0; i--)
            {
                trailAges[i] += delta;
                if (trailAges[i] < 0.2f) continue;
                trailAges.RemoveAt(i); trailPoints.RemoveAt(i);
            }
            RefreshTrail();
        }

        void AddTrail(Vector3 a, Vector3 b)
        {
            if (trailPoints.Count == 0) { trailPoints.Add(a + Vector3.back * 2); trailAges.Add(0); }
            trailPoints.Add(b + Vector3.back * 2); trailAges.Add(0);
            if (trailPoints.Count > 48) { trailPoints.RemoveAt(0); trailAges.RemoveAt(0); }
            RefreshTrail();
        }

        void RefreshTrail()
        {
            if(bossFinalStage==2){if(trailAura!=null)trailAura.enabled=false;return;}
            if (slash == null) return;
            int count = Mathf.Min(trailPoints.Count, cutTrailBuffer.Length);
            trailPoints.CopyTo(0, cutTrailBuffer, 0, count);
            slash.positionCount = count;
            slash.SetPositions(cutTrailBuffer);
            slash.enabled = count > 1;
            // Thicker with rank, and a brief swell on every cut.
            slash.widthMultiplier = 0.10f + StyleLevel * 0.02f + comboPulse * 0.05f;
            slash.sharedMaterial = art.Material(Color.Lerp(styleColors[StyleLevel],Color.white,.42f));
            slash.widthCurve = cutTrailCurve;
            RefreshSlashAura(cutTrailBuffer, count);
        }

        void DrawStyleMeter()
        {
            if (mode != Mode.Battle && !(mode == Mode.Transition && battleFrame == null)) return;
            DrawPaperCompanion();
        }

        IEnumerator RevealMap()
        {
            // Capture the last frame after the battle HUD has drawn.
            yield return new WaitForEndOfFrame();
            if (IsBoss && won && bossFinalStage == 2)
            {
                ShowMap();
                mode = Mode.Map;
                mapPaperExit = Time.unscaledTime;
                yield break;
            }
            battleFrame = ScreenCapture.CaptureScreenshotAsTexture();
            ShowMap();
            Tell((won ? "Encuentro completado · " + resultRank : "Encuentro terminado") + " · Mejor combo " + bestCombo, 5);
        }

        void UpdateScroll(float delta)
        {
            scrollTime += delta;
            if (mode == Mode.ScrollClosing)
            {
                if (scrollTime >= ScrollCloseDuration) BeginBattle();
                return;
            }
            if (scrollTime < ScrollOpenDuration) return;
            if (battleFrame != null)
            {
                Destroy(battleFrame);
                battleFrame = null;
            }
            mode = Mode.Map;
        }

        void DrawScrollTransition()
        {
            if (Event.current.type != EventType.Repaint) return;
            const float width = 1280f;
            bool closing = mode == Mode.ScrollClosing;
            float duration = closing ? ScrollCloseDuration : ScrollOpenDuration;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(scrollTime / duration));
            float open = closing ? 1f - t : t;
            float left = Mathf.Lerp(640, -75, open);
            float right = width - left;
            CoverMap(new Rect(0, 0, Mathf.Max(0, left), 720));
            CoverMap(new Rect(right, 0, Mathf.Max(0, width - right), 720));
            DrawCurvedScrollEdge(left, false, open);
            DrawCurvedScrollEdge(right, true, open);
        }

        void CoverMap(Rect rect)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            if (battleFrame == null)
                Panel(rect, new Color(0.075f, 0.10f, 0.11f));
            else
                GUI.DrawTextureWithTexCoords(rect, battleFrame,
                    new Rect(rect.x / 1280f, 1f - rect.yMax / 720f, rect.width / 1280f, rect.height / 720f), false);
        }
    }
}




