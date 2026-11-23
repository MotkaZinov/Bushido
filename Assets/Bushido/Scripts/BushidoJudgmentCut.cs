using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Bushido
{
    // Corte del juicio: coreografía de la katana y su estela. La partición de la pantalla y la
    // caída de los fragmentos viven en ScreenSlicer; aquí solo se decide cuándo y por dónde se corta.
    //   Técnica (sello Destierro, en primera persona): desenvaine -> ráfaga de tajos que quedan abiertos en el
    //   espacio -> tajo final -> enfundado -> al cerrarse la guarda, la pantalla cae.
    //   Remate del jefe final: la katana del jugador da el tajo y la pantalla se parte por esa línea.
    public sealed partial class BushidoPrototype
    {
        const int JudgmentCuts = 6;
        const float JudgmentDraw = .42f, JudgmentCutTime = .12f, JudgmentFinalTime = .26f, JudgmentHitstop = .2f;
        const float JudgmentFinalStart = JudgmentDraw + JudgmentCuts * JudgmentCutTime + .1f;
        const int JudgmentFinalStrip = 100;
        // Noto: the katana slides back into its saya at screen centre, stops just short, waits, the guard
        // glints, and the last click of the guard is what lets the cut screen fall.
        const float JudgmentSheathIn = .45f, JudgmentSheathHold = 1f, JudgmentGlint = .4f, JudgmentSheathClose = .08f;
        const float JudgmentSheathStart = JudgmentFinalStart + JudgmentFinalTime + .12f;
        const float JudgmentSheathEnd = JudgmentSheathStart + JudgmentSheathIn + JudgmentSheathHold + JudgmentGlint + JudgmentSheathClose;
        // Paper weapon proportions (see CreatePaperWeapon): grip and tip along local Y.
        const float KatanaGrip = .9f, KatanaTip = -1.15f;

        struct TrailSample { public Vector2 blade, tip; public float time; public bool start; }

        readonly ScreenSlicer judgmentSlicer = new ScreenSlicer();
        readonly List<TrailSample> judgmentTrail = new List<TrailSample>();
        readonly List<Vector3> trailVertices = new List<Vector3>();
        readonly List<Vector2> trailUv = new List<Vector2>(), trailPositions = new List<Vector2>();
        readonly List<int> trailTriangles = new List<int>();
        readonly Vector2[] judgmentFrom = new Vector2[JudgmentCuts], judgmentTo = new Vector2[JudgmentCuts];
        // Each cut of the technique: the point the blade crosses and the direction it is travelling there.
        readonly Vector2[] judgmentThrough = new Vector2[JudgmentCuts], judgmentAlong = new Vector2[JudgmentCuts];
        readonly Renderer[] judgmentGhosts = new Renderer[3];
        MaterialPropertyBlock ghostBlock;
        GameObject judgmentLines;
        Material judgmentLineMaterial, judgmentGhostMaterial;
        Mesh judgmentLineMesh;
        int judgmentLinesShown;
        Transform judgmentRoot, judgmentKatana, judgmentSaya, trailBlade, trailTip;
        GameObject judgmentShards, judgmentActors, judgmentTrailObject;
        Renderer judgmentFlare;
        Material judgmentBackdrop, judgmentShatter, judgmentKatanaMaterial, judgmentSayaMaterial, judgmentTrailMaterial, judgmentFlareMaterial;
        Mesh judgmentShardMesh, judgmentTrailMesh, judgmentKatanaMesh, judgmentSayaMesh;
        RenderTexture judgmentFrame;
        float judgmentTime, judgmentCutClock, judgmentReleaseClock, judgmentTrailLife;
        int judgmentCutsDone, judgmentStrip;
        float judgmentSwing;
        bool judgmentFrozen, judgmentFinalDone, judgmentFinisher;
        Vector2 judgmentCentre, judgmentDirection;
        System.Action judgmentImpact;
        // Medidor del juicio: 0..1; cada rana cortada y cada desvío perfecto lo llenan.
        const float JudgmentSliceGain = 1f / 12f, JudgmentDeflectGain = 1f / 3f;
        float judgmentCharge;
        bool judgmentTap;
        void JudgmentGain(float amount) => judgmentCharge = Mathf.Min(1f, judgmentCharge + amount);

        float JudgmentHeight => view.orthographicSize;
        float JudgmentWidth => view.orthographicSize * view.aspect;

        // ---------- set-up ----------

        static Material JudgmentMaterial(string shader, int queue)
        {
            var source = Resources.Load<Shader>(shader);
            if (source == null) throw new System.InvalidOperationException("Falta el shader: " + shader);
            return new Material(source) { renderQueue = queue };
        }

        void JudgmentAssets()
        {
            // Guard on the last asset created: a hot reload can keep some fields and drop others.
            if (judgmentTrailMesh != null) return;
            judgmentBackdrop = JudgmentMaterial("BushidoJudgmentBackdrop", 4000);
            judgmentShatter = JudgmentMaterial("BushidoShatter", 4020);
            judgmentTrailMaterial = JudgmentMaterial("BushidoVfxEnergy", 4025);
            // The 3D katana, drawn over the frozen screen: it writes depth, so the saya hides the sheathed blade.
            judgmentKatanaMaterial = JudgmentMaterial("BushidoKatana", 4030);
            judgmentSayaMaterial = JudgmentMaterial("BushidoKatana", 4031);
            judgmentFlareMaterial = JudgmentMaterial("BushidoVfxEnergy", 4032);
            judgmentKatanaMaterial.SetFloat("_Energy", 1);
            judgmentKatanaMaterial.SetFloat("_Flat", .97f); judgmentSayaMaterial.SetFloat("_Flat", .97f);
            judgmentKatanaMaterial.SetTexture("_BaseMap", WeaponAtlas()); judgmentSayaMaterial.SetTexture("_BaseMap", WeaponAtlas());
            judgmentFlareMaterial.SetFloat("_Ink", 0); // the glint is spirit light, not a brush stroke
            judgmentTrailMaterial.SetFloat("_Reveal", 2);
            judgmentTrailMaterial.SetFloat("_Sharp", 1.3f);
            judgmentTrailMaterial.SetFloat("_Streak", .7f);
            judgmentTrailMaterial.SetFloat("_Radial", 1); // fades with the age stored in uv.x
            judgmentFlareMaterial.SetFloat("_Reveal", 2);
            judgmentFlareMaterial.SetFloat("_Sharp", 0);
            judgmentFlareMaterial.SetFloat("_Radial", 1);
            // Cuts in space: thin spirit light with a white core, not a brush stroke.
            judgmentLineMaterial = JudgmentMaterial("BushidoVfxEnergy", 4026);
            judgmentLineMaterial.SetFloat("_Ink", 0); judgmentLineMaterial.SetFloat("_Reveal", 2); judgmentLineMaterial.SetFloat("_Tail", -1);
            judgmentLineMaterial.SetFloat("_Sharp", 5); judgmentLineMaterial.SetFloat("_Streak", .3f);
            judgmentLineMaterial.SetColor("_Tint", SpiritLight(.3f, 1.6f));
            judgmentGhostMaterial = JudgmentMaterial("BushidoKatanaGhost", 4028); // under the katana itself
            judgmentGhostMaterial.SetFloat("_Flat", .97f);
            ghostBlock = new MaterialPropertyBlock();
            judgmentLineMesh = new Mesh { name = "Cortes en el espacio" }; judgmentLineMesh.MarkDynamic();
            judgmentKatanaMesh = LoadWeaponMesh("KatanaTinta");
            judgmentSayaMesh = LoadWeaponMesh("SayaTinta");
            judgmentShardMesh = new Mesh { name = "Pantalla cortada" };
            judgmentTrailMesh = new Mesh { name = "Estela del filo" };
            judgmentShardMesh.MarkDynamic(); judgmentTrailMesh.MarkDynamic();
        }

        Renderer JudgmentObject(Transform parent, string name, Mesh mesh, Material material)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // Dynamic and UV-driven meshes: never let stale bounds cull them.
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 200);
            return renderer;
        }

        // Spirit colour lifted toward white by lift (0-1); a is the HDR strength the energy shaders read from alpha.
        static Color SpiritLight(float lift, float a) { Color c = Color.Lerp(PrototypeArt.Spirit, Color.white, lift); c.a = a; return c; }

        void JudgmentBuild(System.Action impact, bool finisher)
        {
            if (judgmentRoot != null)
            {
                if (!judgmentFinisher) JudgmentFrogQueue(-1);
                Destroy(judgmentRoot.gameObject);
                if (!judgmentSlicer.Released) judgmentImpact?.Invoke();
            }
            JudgmentAssets();
            judgmentRoot = new GameObject("Corte del juicio").transform;
            judgmentRoot.SetParent(world, false);
            Destroy(judgmentRoot.gameObject, 9f); // safety net if the battle ends mid-effect
            judgmentKatanaMaterial.SetFloat("_Energy", 1);
            float h = JudgmentHeight, w = JudgmentWidth;
            var night = art.Shape(judgmentRoot, "Noche y eclipse", PrimitiveType.Quad, Vector3.zero, new Vector3(w * 2, h * 2, 1), Color.white);
            night.GetComponent<Renderer>().sharedMaterial = judgmentBackdrop;
            judgmentBackdrop.SetFloat("_Dim", 0); judgmentBackdrop.SetFloat("_Ring", 0); judgmentBackdrop.SetFloat("_Flash", 0);
            judgmentShards = JudgmentObject(judgmentRoot, "Fragmentos", judgmentShardMesh, judgmentShatter).gameObject;
            judgmentShards.SetActive(false);
            judgmentTrailMesh.Clear();
            judgmentTrailObject = JudgmentObject(judgmentRoot, "Estela del filo", judgmentTrailMesh, judgmentTrailMaterial).gameObject;
            judgmentLineMesh.Clear();
            judgmentLines = JudgmentObject(judgmentRoot, "Cortes en el espacio", judgmentLineMesh, judgmentLineMaterial).gameObject;
            judgmentActors = new GameObject("Katana");
            judgmentActors.transform.SetParent(judgmentRoot, false);
            for (int i = 0; i < judgmentGhosts.Length; i++)
            {
                judgmentGhosts[i] = JudgmentObject(judgmentActors.transform, "Estela fantasma", judgmentKatanaMesh, judgmentGhostMaterial);
                judgmentGhosts[i].enabled = false;
            }
            judgmentKatana = JudgmentObject(judgmentActors.transform, "Katana", judgmentKatanaMesh, judgmentKatanaMaterial).transform;
            judgmentSaya = JudgmentObject(judgmentActors.transform, "Funda", judgmentSayaMesh, judgmentSayaMaterial).transform;
            var flareMesh = Resources.Load<Mesh>("Models/Vfx/Flare");
            if (flareMesh == null) throw new System.InvalidOperationException("Falta la malla de VFX: Flare");
            judgmentFlare = JudgmentObject(judgmentActors.transform, "Destello de la guarda", flareMesh, judgmentFlareMaterial);
            judgmentFlare.enabled = false;
            // The finisher's trail is born from the weapon: two pivots on the blade, base and tip.
            Transform weapon = finisher && counterKatana != null ? counterKatana : judgmentKatana;
            trailBlade = new GameObject("Pivote base del filo").transform; trailBlade.SetParent(weapon, false); trailBlade.localPosition = Vector3.up * PaperSayaMouth;
            trailTip = new GameObject("Pivote punta").transform; trailTip.SetParent(weapon, false); trailTip.localPosition = Vector3.up * KatanaTip;
            if (finisher) { Destroy(trailBlade.gameObject, 6f); Destroy(trailTip.gameObject, 6f); }
            judgmentKatana.gameObject.SetActive(!finisher);
            judgmentSaya.gameObject.SetActive(!finisher);
            judgmentTrail.Clear();
            judgmentSlicer.Reset(w, h); // also clears the previous run's "released" state
            judgmentTime = 0; judgmentStrip = -1; judgmentSwing = 0;
            judgmentCutsDone = finisher ? JudgmentCuts : 0;
            judgmentFrozen = judgmentFinalDone = false;
            judgmentFinisher = finisher;
            judgmentImpact = impact;
            judgmentTrailLife = finisher ? .34f : .2f;
            // The finisher plays over white paper: a saturated band, or it would vanish.
            judgmentTrailMaterial.SetColor("_Tint", finisher ? SpiritLight(.2f, 1.8f) : SpiritLight(.5f, 1.6f));
            judgmentTrailMaterial.SetFloat("_White", finisher ? .3f : 1f);
            judgmentRoot.SetPositionAndRotation(view.transform.position + view.transform.forward, view.transform.rotation);
        }

        // Técnica: impact runs when the pieces fall, so the reward lands with the break.
        void BeginJudgmentCut(System.Action impact)
        {
            JudgmentBuild(impact, false);
            float h = JudgmentHeight, w = JudgmentWidth, reach = Mathf.Sqrt(w * w + h * h) * 1.35f;
            var random = new System.Random(waveNumber);
            for (int i = 0; i < JudgmentCuts; i++)
            {
                float angle = (i * 67.5f + (float)random.NextDouble() * 24f) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? 1 : -1);
                // Kept to the upper part of the screen: a tip that comes near the hands would fold the blade away.
                Vector2 through = new Vector2(((float)random.NextDouble() - .5f) * w * .7f, ((float)random.NextDouble() - .3f) * h * .7f);
                judgmentFrom[i] = through - direction * reach; judgmentTo[i] = through + direction * reach;
                judgmentThrough[i] = through; judgmentAlong[i] = direction;
            }
            judgmentCentre = Vector2.zero;
            judgmentDirection = new Vector2(1, -.5f).normalized;
            UpdateJudgmentCut(0);
        }

        // Remate del jefe final: called as the player's katana starts its cut.
        void BeginFinisherCut(Vector3 direction)
        {
            JudgmentBuild(null, true);
            judgmentCentre = judgmentRoot.InverseTransformPoint(CounterCenter);
            judgmentDirection = ((Vector2)direction).normalized;
        }

        // What the Destierro seal does when the screen breaks: every frog is cut, the boss loses posture.
        void JudgmentBanish()
        {
            if (mode != Mode.Battle) return;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                if (projectiles[i].kind != ProjectileKind.Frog) continue;
                RewardCut(projectiles[i].root.position); RemoveProjectile(i);
            }
            if (LanternDuel) LanternBlackout();
            if (IsBoss && !vulnerable) { DamagePosture(3); if (guard >= guardLimit) OpenCounter(); }
        }

        // Each slash line cuts the live frogs it crosses, as a swipe would (each line is its own stroke).
        void JudgmentSliceFrogs(Vector2 from, Vector2 to)
        {
            if (mode != Mode.Battle) return;
            strokeSerial++; strokeHits = 0;
            SliceProjectiles(judgmentRoot.TransformPoint(from), judgmentRoot.TransformPoint(to), true);
        }

        // The frozen screen is drawn over the scene, so frogs and their halves are lifted above it (and hidden from the
        // snapshot): the player sees the slash part them. queue -1 restores the shader default.
        void JudgmentFrogQueue(int queue)
        {
            art.Material(FrogInterior).renderQueue = queue;
            foreach (Projectile item in projectiles) if (item.kind == ProjectileKind.Frog) LiftRenderers(item.root, queue);
            foreach (CutEffect effect in cutEffects) if (effect.root != null && effect.root.name.StartsWith("Rana")) LiftRenderers(effect.root, queue);
        }

        static void LiftRenderers(Transform root, int queue)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials) if (material != null) material.renderQueue = queue;
        }

        // ---------- choreography ----------

        // Decisive swing: the grip travels along the cut line while the blade sweeps over it,
        // so the ribbon between the two blade pivots opens into a crescent.
        void JudgmentSwingPose(Transform weapon, float progress, float scale, bool world)
        {
            float eased = Mathf.SmoothStep(0, 1, progress), reach = JudgmentHeight * 1.15f;
            Vector2 grip = judgmentCentre + judgmentDirection * Mathf.Lerp(-reach, reach, eased);
            float tipAngle = Mathf.Atan2(judgmentDirection.y, judgmentDirection.x) * Mathf.Rad2Deg + Mathf.Lerp(125, -125, eased);
            JudgmentPose(weapon, grip, tipAngle, scale, world);
        }

        // Places a weapon by its grip, with the tip pointing at tipAngle (degrees, screen plane).
        // roll turns the blade around its own length (the trail pivots sit on that axis, so the ribbon is unaffected).
        void JudgmentPose(Transform weapon, Vector2 grip, float tipAngle, float scale, bool world, float roll = 0)
        {
            Quaternion turn = Quaternion.Euler(0, 0, tipAngle + 90) * Quaternion.Euler(0, roll, 0);
            Vector3 origin = (Vector3)grip - turn * Vector3.up * (KatanaGrip * scale);
            if (world) weapon.SetPositionAndRotation(judgmentRoot.TransformPoint(origin), judgmentRoot.rotation * turn);
            else { weapon.localPosition = origin; weapon.localRotation = turn; }
            weapon.localScale = Vector3.one * scale;
        }

        void JudgmentFlareAt(Vector3 localPoint, float power)
        {
            judgmentFlare.enabled = power > .01f;
            judgmentFlare.transform.localPosition = (Vector2)localPoint; // a tilted weapon reaches behind the camera; the glint stays on the plane
            judgmentFlare.transform.localRotation = Quaternion.Euler(0, 0, 18 + judgmentTime * 80);
            judgmentFlare.transform.localScale = Vector3.one * JudgmentHeight * (.5f + power * .9f);
            judgmentFlareMaterial.SetColor("_Tint", SpiritLight(.6f, power * 1.8f));
        }

        // Poses the technique's own katana, its saya and its afterimages at a given time.
        void JudgmentTechniquePose(float time)
        {
            float h = JudgmentHeight, w = JudgmentWidth;
            bool swinging = time >= JudgmentDraw && time < JudgmentSheathStart;
            judgmentSaya.gameObject.SetActive(!swinging);
            for (int k = 0; k < judgmentGhosts.Length; k++)
            {
                // Afterimages: the same blade a few hundredths of a second ago, each fainter than the last.
                float past = time - (k + 1) * .02f;
                bool shown = swinging && past >= JudgmentDraw;
                judgmentGhosts[k].enabled = shown;
                if (!shown) continue;
                ghostBlock.SetFloat("_Taper", JudgmentFirstPersonPose(judgmentGhosts[k].transform, past));
                ghostBlock.SetColor("_Tint", SpiritLight(0, .5f / (k + 1)));
                judgmentGhosts[k].SetPropertyBlock(ghostBlock);
            }
            if (time < JudgmentDraw)
            {
                // Iaido seen by the one who draws: the saya sits at the left hip and runs off the corner,
                // the grip points up into the scene, the blade slides out and the guard flares.
                float scale = h * 1.3f, pull = Mathf.SmoothStep(0, 1, (time - .1f) / .3f) * .5f;
                Quaternion turn = Quaternion.Euler(0, 0, -55) * Quaternion.Euler(-35, 0, 0);
                JudgmentTaper(.3f);
                judgmentSaya.localPosition = new Vector3(-w * .25f, -h * .7f) - turn * Vector3.up * (PaperSayaMouth * scale);
                judgmentSaya.localRotation = turn; judgmentSaya.localScale = Vector3.one * scale;
                SheathSlide(judgmentSaya, judgmentKatana, pull * 1.5f);
                float flare = Mathf.Exp(-Mathf.Pow((time - .22f) / .09f, 2));
                JudgmentFlareAt(judgmentKatana.localPosition + judgmentKatana.localRotation * Vector3.up * (PaperSayaMouth * scale), flare);
                return;
            }
            JudgmentFlareAt(Vector3.zero, 0);
            if (swinging) JudgmentTaper(JudgmentFirstPersonPose(judgmentKatana, time));
            else JudgmentSheathPose(time - JudgmentSheathStart);
        }

        void JudgmentTaper(float amount)
        {
            judgmentKatanaMaterial.SetFloat("_Taper", amount); judgmentSayaMaterial.SetFloat("_Taper", amount);
        }

        // The decisive cut's line, shortened to the part that crosses the screen.
        void JudgmentFinalLine(out Vector2 from, out Vector2 to)
        {
            float far = (JudgmentWidth + JudgmentHeight) * 2;
            from = judgmentCentre - judgmentDirection * far; to = judgmentCentre + judgmentDirection * far;
            ClipToScreen(ref from, ref to, JudgmentWidth * 1.1f, JudgmentHeight * 1.1f);
        }

        // Where the tip is, and how it is moving, between the draw and the sheathing.
        // The flurry is one unbroken stroke: a Hermite curve that crosses each cut at that cut's own angle, so the
        // blade is already travelling along a cut when it opens. Then the decisive cut: draw back, cross the screen.
        Vector2 JudgmentTip(float time, out Vector2 velocity)
        {
            JudgmentFinalLine(out Vector2 from, out Vector2 to);
            if (time >= JudgmentFinalStart)
            {
                float swing = Mathf.Clamp01((time - JudgmentFinalStart) / JudgmentFinalTime);
                float along = swing < .3f ? Mathf.Lerp(0, -.12f, Mathf.SmoothStep(0, 1, swing / .3f)) : Mathf.Lerp(-.12f, 1, Mathf.SmoothStep(0, 1, (swing - .3f) / .7f));
                velocity = (to - from) * (swing < .3f ? -1 : 1);
                return Vector2.LerpUnclamped(from, to, along);
            }
            // Knot -1 is the rest point after the draw, 0..5 the cuts, 6 the start of the decisive cut.
            int segment = Mathf.Min(Mathf.FloorToInt((time - JudgmentDraw) / JudgmentCutTime - .5f), JudgmentCuts - 1);
            JudgmentKnot(segment, from, out Vector2 p0, out Vector2 m0, out float t0);
            JudgmentKnot(segment + 1, from, out Vector2 p1, out Vector2 m1, out float t1);
            float u = Mathf.InverseLerp(t0, t1, time), u2 = u * u, u3 = u2 * u;
            velocity = (6 * u2 - 6 * u) * p0 + (3 * u2 - 4 * u + 1) * m0 + (6 * u - 6 * u2) * p1 + (3 * u2 - 2 * u) * m1;
            return (2 * u3 - 3 * u2 + 1) * p0 + (u3 - 2 * u2 + u) * m0 + (3 * u2 - 2 * u3) * p1 + (u3 - u2) * m1;
        }

        void JudgmentKnot(int index, Vector2 finalStart, out Vector2 point, out Vector2 tangent, out float time)
        {
            float sweep = JudgmentHeight * 2.4f; // how far the stroke carries past each knot
            if (index < 0) { point = new Vector2(-JudgmentWidth * .15f, -JudgmentHeight * .3f); tangent = new Vector2(.6f, .8f) * sweep; time = JudgmentDraw; }
            else if (index >= JudgmentCuts) { point = finalStart; tangent = -judgmentDirection * sweep * .5f; time = JudgmentFinalStart; }
            else { point = judgmentThrough[index]; tangent = judgmentAlong[index] * sweep; time = JudgmentDraw + (index + .5f) * JudgmentCutTime; }
        }

        // First person: the katana is held from below the screen and its tip follows JudgmentTip.
        // The camera is orthographic, so reach into the scene is faked: the blade tilts away until its projected
        // length matches the distance from hand to tip, and the shader tapers it by the amount returned here.
        float JudgmentFirstPersonPose(Transform weapon, float time)
        {
            float h = JudgmentHeight, w = JudgmentWidth, scale = h * 1.7f, length = (KatanaGrip - KatanaTip) * scale;
            Vector2 tip = JudgmentTip(time, out Vector2 velocity);
            // The hands are not a hinge: they follow the swing from below the screen.
            Vector2 hand = new Vector2(w * .22f + tip.x * .3f, -h * 1.25f + tip.y * .12f), reach = tip - hand;
            float pitch = Mathf.Acos(Mathf.Min(reach.magnitude / length, .995f)) * Mathf.Rad2Deg;
            float angle = Mathf.Atan2(reach.y, reach.x) * Mathf.Rad2Deg + 90;
            // The wrist turns the edge (local -x) into the direction of travel.
            Vector2 edge = Quaternion.Euler(0, 0, angle) * Vector3.left;
            float roll = velocity.sqrMagnitude > 1e-6f ? Mathf.Acos(Mathf.Clamp(Vector2.Dot(edge, velocity.normalized), -1, 1)) * Mathf.Rad2Deg : 0;
            Quaternion turn = Quaternion.Euler(0, 0, angle) * Quaternion.Euler(-pitch, 0, 0) * Quaternion.Euler(0, roll, 0);
            weapon.localPosition = (Vector3)hand - turn * Vector3.up * (KatanaGrip * scale);
            weapon.localRotation = turn; weapon.localScale = Vector3.one * scale;
            return Mathf.Sin(pitch * Mathf.Deg2Rad) * .6f;
        }

        // Shortens a segment to the part inside the rectangle of half-size (w, h) centred on the screen.
        static void ClipToScreen(ref Vector2 a, ref Vector2 b, float w, float h)
        {
            Vector2 d = b - a; float t0 = 0, t1 = 1;
            for (int axis = 0; axis < 2; axis++)
            {
                float step = d[axis], limit = axis == 0 ? w : h;
                if (Mathf.Abs(step) < 1e-5f) continue;
                float u0 = (-limit - a[axis]) / step, u1 = (limit - a[axis]) / step;
                t0 = Mathf.Max(t0, Mathf.Min(u0, u1)); t1 = Mathf.Min(t1, Mathf.Max(u0, u1));
            }
            Vector2 start = a + d * t0; b = a + d * t1; a = start;
        }

        void JudgmentSheathPose(float time)
        {
            float scale = JudgmentHeight * .9f;
            JudgmentTaper(0);
            float slide = Mathf.SmoothStep(0, 1, time / JudgmentSheathIn);
            float close = Mathf.Clamp01((time - JudgmentSheathIn - JudgmentSheathHold - JudgmentGlint) / JudgmentSheathClose);
            // 1.25 = tip just outside the mouth; .07 leaves a sliver of blade showing until the final click.
            float pull = Mathf.Lerp(Mathf.Lerp(1.25f, .07f, slide), 0, close);
            judgmentSaya.localPosition = Vector3.zero; judgmentSaya.localRotation = Quaternion.Euler(0, 0, 45); judgmentSaya.localScale = Vector3.one * scale;
            SheathSlide(judgmentSaya, judgmentKatana, pull * 1.5f);
            float glint = Mathf.Exp(-Mathf.Pow((time - JudgmentSheathIn - JudgmentSheathHold - JudgmentGlint * .5f) / (JudgmentGlint * .3f), 2));
            // The blade calms as it goes in, then flares once with the glint.
            judgmentKatanaMaterial.SetFloat("_Energy", 1 - slide + glint * 2);
            JudgmentFlareAt(judgmentKatana.localPosition + judgmentKatana.localRotation * Vector3.up * (PaperSayaMouth * scale), glint * .5f); // on the guard
        }

        // Cuts in space: each cut opens as a needle of light where the blade crossed, snaps to its full length and
        // stays until the guard clicks shut. Two fainter echoes per cut, slightly off-angle, fill the screen.
        void JudgmentLinesMesh()
        {
            float t = judgmentTime, h = JudgmentHeight;
            trailVertices.Clear(); trailUv.Clear(); trailPositions.Clear(); trailTriangles.Clear();
            if (!judgmentFinisher)
            {
                // When the screen falls every line flares once and is gone.
                float power = judgmentSlicer.Released ? Mathf.Clamp01(1 - (t - judgmentReleaseClock) / .18f) * 2.4f : 1;
                for (int i = 0; i < JudgmentCuts; i++)
                {
                    float age = t - JudgmentDraw - (i + .5f) * JudgmentCutTime;
                    Vector2 along = judgmentAlong[i], side = new Vector2(-along.y, along.x);
                    JudgmentLine(judgmentThrough[i], along, age, h * .05f * power, i);
                    JudgmentLine(judgmentThrough[i] + side * (h * .09f), Quaternion.Euler(0, 0, 5) * along, age - .03f, h * .028f * power, i + 7);
                    JudgmentLine(judgmentThrough[i] - side * (h * .13f), Quaternion.Euler(0, 0, -8) * along, age - .05f, h * .02f * power, i + 13);
                }
                JudgmentLine(judgmentCentre, judgmentDirection, t - JudgmentFinalStart - JudgmentFinalTime * .6f, h * .1f * power, 31);
            }
            judgmentLinesShown = trailVertices.Count / 4;
            judgmentLineMesh.Clear();
            judgmentLineMesh.SetVertices(trailVertices); judgmentLineMesh.SetUVs(0, trailUv); judgmentLineMesh.SetUVs(1, trailPositions);
            judgmentLineMesh.SetTriangles(trailTriangles, 0);
        }

        void JudgmentLine(Vector2 through, Vector2 along, float age, float width, int seed)
        {
            if (age < 0 || width <= 0) return;
            float far = (JudgmentWidth + JudgmentHeight) * 2, open = Mathf.Clamp01(age / .05f);
            Vector2 a = through - along * far, b = through + along * far;
            ClipToScreen(ref a, ref b, JudgmentWidth * 1.05f, JudgmentHeight * 1.05f);
            a = Vector2.Lerp(through, a, open); b = Vector2.Lerp(through, b, open);
            // Thick as it opens, a hair once it settles, never quite still.
            width *= (.35f + 1.6f * Mathf.Exp(-age * 16)) * (.85f + .15f * Mathf.Sin(judgmentTime * 31 + seed * 2.4f));
            Vector2 side = new Vector2(-along.y, along.x) * width;
            int v = trailVertices.Count;
            // A needle: pointed at both ends, full width where the blade crossed.
            trailVertices.Add(a); trailVertices.Add(through + side); trailVertices.Add(through - side); trailVertices.Add(b);
            trailPositions.Add(a); trailPositions.Add(through + side); trailPositions.Add(through - side); trailPositions.Add(b);
            trailUv.Add(new Vector2(0, .5f)); trailUv.Add(new Vector2(.5f, 1)); trailUv.Add(new Vector2(.5f, 0)); trailUv.Add(new Vector2(1, .5f));
            trailTriangles.Add(v); trailTriangles.Add(v + 1); trailTriangles.Add(v + 2);
            trailTriangles.Add(v + 3); trailTriangles.Add(v + 2); trailTriangles.Add(v + 1);
        }

        void JudgmentSample(float time, int strip)
        {
            if (strip >= 0 && trailBlade != null)
                judgmentTrail.Add(new TrailSample { blade = judgmentRoot.InverseTransformPoint(trailBlade.position), tip = judgmentRoot.InverseTransformPoint(trailTip.position), time = time, start = strip != judgmentStrip });
            judgmentStrip = strip;
        }

        void JudgmentTrailMesh()
        {
            judgmentTrail.RemoveAll(sample => judgmentTime - sample.time > judgmentTrailLife);
            trailVertices.Clear(); trailUv.Clear(); trailPositions.Clear(); trailTriangles.Clear();
            for (int i = 0; i < judgmentTrail.Count; i++)
            {
                TrailSample sample = judgmentTrail[i];
                float age = (judgmentTime - sample.time) / judgmentTrailLife;
                trailVertices.Add(sample.blade); trailVertices.Add(sample.tip);
                trailPositions.Add(sample.blade); trailPositions.Add(sample.tip);
                trailUv.Add(new Vector2(age, 0)); trailUv.Add(new Vector2(age, 1));
                if (i == 0 || sample.start) continue;
                int a = i * 2 - 2;
                trailTriangles.Add(a); trailTriangles.Add(a + 1); trailTriangles.Add(a + 2);
                trailTriangles.Add(a + 1); trailTriangles.Add(a + 3); trailTriangles.Add(a + 2);
            }
            judgmentTrailMesh.Clear();
            judgmentTrailMesh.SetVertices(trailVertices); judgmentTrailMesh.SetUVs(0, trailUv); judgmentTrailMesh.SetUVs(1, trailPositions);
            judgmentTrailMesh.SetTriangles(trailTriangles, 0);
        }

        // Freezes the scene, without weapon or trail, onto the single rectangle the cuts will divide.
        void JudgmentFreeze()
        {
            judgmentFrozen = true;
            // The camera's own size, not Screen: outside the game loop Screen can be another editor window.
            int width = view.pixelWidth, height = view.pixelHeight;
            if (judgmentFrame == null || judgmentFrame.width != width || judgmentFrame.height != height)
            {
                if (judgmentFrame != null) judgmentFrame.Release();
                judgmentFrame = new RenderTexture(width, height, 24);
            }
            bool weaponShown = judgmentFinisher && counterKatana != null && counterKatana.gameObject.activeSelf;
            bool inkLine = slash != null && slash.enabled;
            judgmentActors.SetActive(false); judgmentTrailObject.SetActive(false); judgmentLines.SetActive(false);
            if (weaponShown) counterKatana.gameObject.SetActive(false);
            if (inkLine) slash.enabled = false; // the cut must be an edge between pieces, never a line painted inside one
            // The technique's frogs stay live above the frozen screen (see JudgmentFrogQueue), so they are left out of the snapshot.
            var hidden = new List<GameObject>();
            if (!judgmentFinisher)
            {
                foreach (Projectile item in projectiles) if (item.kind == ProjectileKind.Frog) hidden.Add(item.root.gameObject);
                foreach (CutEffect effect in cutEffects) if (effect.root != null && effect.root.name.StartsWith("Rana")) hidden.Add(effect.root.gameObject);
                JudgmentFrogQueue(4040);
            }
            hidden.RemoveAll(item => !item.activeSelf);
            foreach (var item in hidden) item.SetActive(false);
            view.targetTexture = judgmentFrame;
            view.Render();
            view.targetTexture = null;
            foreach (var item in hidden) item.SetActive(true);
            judgmentActors.SetActive(true); judgmentTrailObject.SetActive(true); judgmentLines.SetActive(true);
            if (weaponShown) counterKatana.gameObject.SetActive(true);
            if (inkLine) slash.enabled = true;
            judgmentShatter.SetTexture("_MainTex", judgmentFrame);
            judgmentSlicer.Reset(JudgmentWidth, JudgmentHeight);
            judgmentShards.SetActive(true);
        }

        void JudgmentFinalCut()
        {
            judgmentFinalDone = true;
            judgmentCutClock = judgmentTime;
            judgmentSlicer.Cut(judgmentCentre - judgmentDirection, judgmentCentre + judgmentDirection, JudgmentHeight * .045f);
            if (!judgmentFinisher) { JudgmentFinalLine(out Vector2 from, out Vector2 to); JudgmentSliceFrogs(from, to); }
            judgmentSlicer.Flash();
            shakeTime = Mathf.Max(shakeTime, .22f);
        }

        void JudgmentRelease()
        {
            judgmentReleaseClock = judgmentTime;
            if (!judgmentFinisher) PlayGuardSound(true); // the click of the guard meeting the saya
            judgmentSlicer.Release(JudgmentHeight * 2.2f, waveNumber);
            ShockRingBurst(judgmentRoot.TransformPoint(judgmentCentre), judgmentRoot.rotation, JudgmentHeight * 1.2f, SpiritLight(.5f, 1.6f));
            judgmentActors.SetActive(false);
            judgmentBackdrop.SetFloat("_Dim", 0); judgmentBackdrop.SetFloat("_Ring", 0);
            shakeTime = Mathf.Max(shakeTime, .3f);
            var impact = judgmentImpact;
            judgmentImpact = null;
            impact?.Invoke();
        }

        void UpdateJudgmentCut(float delta)
        {
            // J (or a tap on the HUD meter) casts the technique once the meter is full; the Destierro seal casts it free (not during the boss's ritual finish).
            bool fire = judgmentTap || (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame);
            judgmentTap = false;
            if (fire && judgmentCharge >= 1f && judgmentRoot == null && bossFinalStage == 0) { judgmentCharge = 0; BeginJudgmentCut(JudgmentBanish); }
            if (judgmentRoot == null) return;
            judgmentRoot.SetPositionAndRotation(view.transform.position + view.transform.forward, view.transform.rotation);
            float h = JudgmentHeight;
            judgmentTime += delta;
            float t = judgmentTime;
            if (judgmentFinisher)
            {
                // The swing itself is posed in LateJudgmentCut, after the game has moved the weapon.
                if (!judgmentFrozen && katanaCutTime <= 0)
                {
                    JudgmentFreeze(); JudgmentFinalCut();
                    impactTime = Mathf.Max(impactTime, JudgmentHitstop * .7f);
                }
            }
            else
            {
                if (!judgmentSlicer.Released || !judgmentFrozen) impactTime = Mathf.Max(impactTime, .05f); // slow motion until the pieces fall
                judgmentKatanaMaterial.SetFloat("_Age", t * 3); // blade energy keeps flowing
                if (!judgmentFrozen)
                {
                    judgmentBackdrop.SetFloat("_Aspect", view.aspect);
                    judgmentBackdrop.SetFloat("_Dim", Mathf.SmoothStep(0, 1, t / .15f));
                    judgmentBackdrop.SetFloat("_Ring", Mathf.SmoothStep(0, 1, (t - .1f) / .25f));
                }
                JudgmentTechniquePose(t);
                if (!judgmentFrozen && t >= JudgmentDraw) JudgmentFreeze();
                while (judgmentFrozen && judgmentCutsDone < JudgmentCuts && t >= JudgmentDraw + (judgmentCutsDone + 1) * JudgmentCutTime)
                {
                    judgmentSlicer.Cut(judgmentFrom[judgmentCutsDone], judgmentTo[judgmentCutsDone], h * .014f);
                    JudgmentSliceFrogs(judgmentFrom[judgmentCutsDone], judgmentTo[judgmentCutsDone]);
                    judgmentCutsDone++;
                    shakeTime = Mathf.Max(shakeTime, .06f);
                }
                if (judgmentFrozen && !judgmentFinalDone && t >= JudgmentFinalStart + JudgmentFinalTime) JudgmentFinalCut();
            }
            // The technique holds the cut screen in place until the katana is fully sheathed.
            if (judgmentFinalDone && !judgmentSlicer.Released && t >= (judgmentFinisher ? judgmentCutClock + JudgmentHitstop : JudgmentSheathEnd)) JudgmentRelease();
            if (judgmentRoot == null) return; // the impact ended the battle
            if (judgmentSlicer.Released) judgmentBackdrop.SetFloat("_Flash", Mathf.Clamp01(1 - (t - judgmentReleaseClock) / .2f));
            if (judgmentFrozen)
            {
                judgmentSlicer.Step(delta);
                judgmentSlicer.Build(judgmentShardMesh, Color.Lerp(Color.white, PrototypeArt.Paper, .3f), h * .012f);
            }
            JudgmentTrailMesh();
            JudgmentLinesMesh();
            if (!judgmentFrozen || !judgmentSlicer.Finished) return;
            if (!judgmentFinisher) JudgmentFrogQueue(-1);
            Destroy(judgmentRoot.gameObject);
            judgmentRoot = null;
        }

        // Boss finisher: take the player's katana through the decisive swing and feed its pivots to the trail.
        void LateJudgmentCut()
        {
            if (judgmentRoot == null || !judgmentFinisher || judgmentFrozen || counterKatana == null || katanaCutTime <= 0) return;
            float progress = 1 - katanaCutTime / .52f;
            if (katanaBladeMaterial != null) { katanaBladeMaterial.SetFloat("_Energy", 1); katanaBladeMaterial.SetFloat("_Age", judgmentTime * 3); }
            // Sub-steps between frames, so the crescent stays round at any frame rate.
            for (int k = 1; k <= 6; k++)
            {
                JudgmentSwingPose(counterKatana, Mathf.Lerp(judgmentSwing, progress, k / 6f), JudgmentHeight * .78f, true);
                JudgmentSample(judgmentTime - Time.deltaTime * (1 - k / 6f), JudgmentFinalStrip);
            }
            judgmentSwing = progress;
            JudgmentFlareAt(judgmentRoot.InverseTransformPoint(counterKatana.TransformPoint(Vector3.up * PaperSayaMouth)), Mathf.Exp(-Mathf.Pow(progress / .22f, 2)));
            JudgmentTrailMesh();
        }

        void DisposeJudgmentCut()
        {
            if (judgmentFrame != null) judgmentFrame.Release();
            if (judgmentBackdrop == null) return;
            foreach (var item in new Object[] { judgmentBackdrop, judgmentShatter, judgmentKatanaMaterial, judgmentSayaMaterial, judgmentTrailMaterial, judgmentFlareMaterial,
                judgmentLineMaterial, judgmentGhostMaterial, judgmentLineMesh, judgmentShardMesh, judgmentTrailMesh, judgmentKatanaMesh, judgmentSayaMesh }) Destroy(item);
        }
    }
}
