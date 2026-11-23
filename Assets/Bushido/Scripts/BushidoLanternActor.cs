using System.Collections.Generic;
using UnityEngine;

namespace Bushido
{
    // One chōchin-obake (paper-lantern yokai) from the artist's rigged FBX, posed by code every frame.
    // The artist's "Scene" clip was sampled and ignored (see SOURCES.txt): it only holds the rest curl, which is the bind pose.
    //
    // The tongue (Cubo) is a flat ribbon 0.8 wide whose 22-bone chain curls in the model's Y-Z (sagittal) plane. So every bone is
    // turned ONLY about the model's X axis (the hinge the rest curl uses): the ribbon never bends sideways and never twists, and its
    // width never changes. To aim in screen space the whole lantern turns: yaw to put its sagittal plane in the screen plane
    // (profile), body tilt for a diagonal. Reach = uncurling plus an even lengthwise stretch (capped), plus the lantern swinging on its cord.
    public sealed class LanternActor
    {
        public enum Pose { Idle, Unlit, WindUp, Lash, Retract, Stunned, Spit, Calm }

        public const float BodyScale = 1.15f, HangDrop = 2.1f, BodyRadius = 1.15f;
        public const float LashStretch = 2.5f, WindStretch = 0.92f, CalmStretch = 0.75f, UnlitStretch = 0.9f, SpitStretch = 1.7f; // lengthwise only, never width
        public const float LashWhipTime = 0.12f, BlendTime = 0.07f;
        public const float ProfileYaw = 68f, IdleYaw = 40f, StunYaw = 20f;      // degrees away from facing the camera
        public const float ViewBias = 35f, TrackLimit = 25f;      // gaze: degrees from the camera before it is pulled toward it, and tracking range around that
        public const float SwingLash = 30f, SwingWind = 14f, SnapSpeed = 1800f;  // puppet facings: ~2-3 frames
        const int N = 22;

        public readonly bool white;
        public readonly Transform holder, model;
        public Pose pose; public float poseT, poseLen = 1f;
        public float pathAngle, spitAngle = -55f, reach, glowTarget, eyeTarget = 1f, wide = 1f;
        public Vector3 lookAt;
        public float Extension;                       // how far the tongue lies along its path (0..1), for hit tests
        public readonly Vector3[] Points = new Vector3[N + 1]; // tongue polyline: mouth .. tip (world), straight from the bones
        public float Glow { get; private set; }
        public float hurtKick, phase, charge;
        public float Yaw => yaw;

        readonly Transform[] bone = new Transform[N];
        readonly Quaternion[] bindRot = new Quaternion[N];
        readonly Vector3[] bindLocal = new Vector3[N];
        readonly float[] bindPhi = new float[N];     // segment direction in the sagittal plane at rest: 0 = forward (+Z), +90 = up
        readonly float[] phi = new float[N], lastPhi = new float[N];
        float stretch = 1f, lastStretch = 1f, blend = 1f, blendDur = BlendTime;
        float yaw, tilt, swing;
        readonly Transform endBone, tipBone, eye;
        readonly Quaternion tipOffset, eyeBindRot;
        readonly Vector3 eyeBindScale;
        readonly Renderer bodyRenderer, eyeRenderer, tongueRenderer;
        readonly List<Material> owned = new List<Material>();
        readonly List<(Material material, int role)> tints = new List<(Material, int)>(); // role: 0 body 1 dark 2 tongue 3 sclera 4 iris 5 pupil
        readonly LineRenderer lid, hangString, spiral;
        readonly LineRenderer rim;               // pulsing gold glow behind the tongue while it is charged
        readonly Transform socket, iris, pupil;   // ink socket behind the eyeball; big palette iris and ink pupil drawn on the visible front of it
        static Texture2D paperFace;
        readonly float coilDir;
        float blinkClock, nextBlink = 2.5f, blinkHold, hookY, sy1 = 1f;
        static readonly Color Dull = new Color(0.55f, 0.51f, 0.46f);

        public LanternActor(PrototypeArt art, Transform parent, bool white, Vector3 hook)
        {
            this.white = white; coilDir = white ? 1f : -1f; phase = white ? 0f : 2.2f;
            holder = new GameObject(white ? "Farol blanco" : "Farol rojo").transform;
            holder.SetParent(parent, false); holder.position = hook; hookY = hook.y;
            var prefab = Resources.Load<GameObject>("Models/Lamparas/" + (white ? "lampara_blanca" : "lampara_roja"));
            if (prefab == null) throw new System.InvalidOperationException("Falta el modelo de lámpara en Resources/Models/Lamparas.");
            model = Object.Instantiate(prefab, holder, false).transform;
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity; model.localScale = Vector3.one;
            // Bind pose, read at scale 1 with no rotation: the model faces +Z, its sagittal plane is Y-Z.
            Transform t = model.Find("Esqueleto/Hueso");
            var pos = new Vector3[N + 1];
            for (int i = 0; i < N; i++) { bone[i] = t; pos[i] = t.position; bindRot[i] = t.rotation; bindLocal[i] = t.localPosition; t = t.childCount > 0 ? t.GetChild(0) : null; }
            endBone = t; pos[N] = t.position;
            for (int i = 0; i < N; i++) { Vector3 d = pos[i + 1] - pos[i]; bindPhi[i] = Mathf.Atan2(d.y, d.z) * Mathf.Rad2Deg; }
            tipBone = model.Find("Esqueleto/Hueso.022");
            tipOffset = Quaternion.Inverse(bone[N - 1].rotation) * tipBone.rotation;
            eye = model.Find("Esqueleto.001/Bone");
            eyeBindRot = eye.rotation; eyeBindScale = eye.localScale;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()) { skin.updateWhenOffscreen = true; if (skin.name == "Cubo") tongueRenderer = skin; else eyeRenderer = skin; }
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>()) bodyRenderer = r;
            Dress(art, bodyRenderer, tongueRenderer, eyeRenderer);
            float length = 0f;
            for (int i = 0; i < N; i++) length += (pos[i + 1] - pos[i]).magnitude;
            reach = length * BodyScale * LashStretch;
            model.localScale = Vector3.one * BodyScale;
            model.localPosition = new Vector3(0, -HangDrop, 0);
            yaw = 180f - (white ? 1f : -1f) * IdleYaw;
            spiral = art.Line(model, new Vector3[40], PrototypeArt.Ink, 0.05f); spiral.enabled = false;
            hangString = art.Line(holder, new[] { Vector3.zero, new Vector3(0, -(HangDrop - 1.2f), 0) }, PrototypeArt.Ink, 0.05f);
            lid = art.Line(holder, new Vector3[9], PrototypeArt.Ink, 0.14f); lid.enabled = false; lid.useWorldSpace = true; lid.numCapVertices = 6;
            spiral.transform.SetParent(holder, false); spiral.useWorldSpace = true; spiral.widthMultiplier = 0.07f;
            rim = art.Line(holder, new Vector3[N + 1], PrototypeArt.Gold, 0.4f); rim.useWorldSpace = true; rim.enabled = false; rim.numCapVertices = 6;
            socket = art.Shape(model, "Cuenca de tinta", PrimitiveType.Sphere, new Vector3(0, 0.354f, 0.5f), new Vector3(0.82f, 0.82f, 0.5f), PrototypeArt.Ink).transform;
            iris = art.Shape(holder, "Iris", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.4f, 0.4f, 0.03f), white ? PrototypeArt.Red : PrototypeArt.Gold).transform;
            pupil = art.Shape(holder, "Pupila", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.19f, 0.19f, 0.03f), PrototypeArt.Ink).transform;
            phi.CopyTo(lastPhi, 0);
            SetPose(Pose.Idle, 1f, 0f);
            Update(0f, 0f);
        }

        void Dress(PrototypeArt art, Renderer body, Renderer tongue, Renderer eyeR)
        {
            var cuerpo = Resources.Load<Texture2D>("Models/Lamparas/Cuerpo_Final");
            foreach (var r in new[] { body, tongue, eyeR })
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    string n = shared[i] != null ? shared[i].name : "";
                    var m = new Material(art.ModelMaterial(shared[i]));
                    int role = r == tongue ? 2 : r == body ? (n.StartsWith("Cuerpo") ? 0 : 1) : n.Contains("OJO") ? 3 : n.EndsWith("002") ? 4 : 5;
                    m.SetFloat("_PaperSurface", role == 0 ? 1f : 0f);
                    if (role == 0 && !white && cuerpo != null) m.SetTexture("_BaseMap", cuerpo); // the red lantern wears the artist's ribs
                    else if (role == 0) m.SetTexture("_BaseMap", PaperFace());                  // the white one's texture never arrived: the red one's ink strokes over paper
                    owned.Add(m); tints.Add((m, role)); shared[i] = m;
                }
                r.sharedMaterials = shared;
            }
        }

        // Cuerpo_Final's brightness over white: the ribs and the ink grin of the red lantern, with the red taken out, so the twins read as siblings.
        static Texture2D PaperFace()
        {
            if (paperFace != null) return paperFace;
            var src = Resources.Load<Texture2D>("Models/Lamparas/Cuerpo_Final");
            if (src == null) return null;
            const int n = 1024;
            var rt = RenderTexture.GetTemporary(n, n, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Cuerpo en papel", hideFlags = HideFlags.HideAndDontSave };
            tex.ReadPixels(new Rect(0, 0, n, n), 0, 0);
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float f = Mathf.Clamp01(Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b)) / 176f);
                byte v = (byte)(255f * Mathf.Lerp(0.18f * f, 1f, Mathf.SmoothStep(0f, 1f, f)));
                px[i] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            return paperFace = tex;
        }

        public void Dispose() { foreach (var m in owned) if (m != null) Object.Destroy(m); owned.Clear(); }

        public void SetPose(Pose next, float duration, float blendTime = BlendTime)
        {
            phi.CopyTo(lastPhi, 0); lastStretch = stretch; blend = 0f; blendDur = Mathf.Max(0.001f, blendTime);
            pose = next; poseT = 0f; poseLen = Mathf.Max(0.001f, duration);
        }

        public float P => Mathf.Clamp01(poseT / poseLen);
        public Vector3 Tip => Points[N];
        public void MoveHook(Vector3 p) { holder.position = p; hookY = p.y; }   // the rope sags and dances: the twin hangs from where it really is
        public Vector3 Body => model.position;
        public void ReleaseCord()
        {
            hangString.enabled = false;
            // The closed lid is drawn in world coordinates while the puppet hangs.
            lid.enabled = false;
        }

        // Which side the lantern faces for the current path (+1 right, -1 left); a vertical drop faces the centre.
        float Side { get { float c = Mathf.Cos(pathAngle * Mathf.Deg2Rad); return Mathf.Abs(c) < 0.2f ? (white ? 1f : -1f) : Mathf.Sign(c); } }
        bool Vertical => Mathf.Abs(pathAngle + 90f) < 10f;

        // Fills phi[] (sagittal angle of each segment, degrees) and returns the lengthwise stretch.
        float Shape(float t)
        {
            float p = P;
            Extension = 0f;
            switch (pose)
            {
                case Pose.Idle:   // the artist's rest curl with a small travelling wave on the hinge
                    for (int i = 0; i < N; i++) phi[i] = bindPhi[i] + Wave(i, t, 9f);
                    return 1f;
                case Pose.Unlit:
                    for (int i = 0; i < N; i++) phi[i] = bindPhi[i] + Wave(i, t * 0.5f, 4f);
                    return UnlitStretch;
                case Pose.Calm:
                    for (int i = 0; i < N; i++) phi[i] = bindPhi[i] + Wave(i, t * 0.7f, 3f);
                    return CalmStretch;
                case Pose.WindUp:  // gathers back: the curl a little tighter and the ribbon shorter, trembling near the release
                {
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p * 1.5f)), shake = p > 0.5f ? 1.5f + 2f * p : 0f;
                    for (int i = 0; i < N; i++) phi[i] = bindPhi[i] * (1f + 0.12f * e) + shake * Mathf.Sin(t * 47f + i * 1.7f);
                    return Mathf.Lerp(1f, WindStretch, e);
                }
                case Pose.Lash:
                case Pose.Retract:
                {
                    bool outward = pose == Pose.Lash;
                    float ext = outward ? 1f - Mathf.Pow(1f - Mathf.Clamp01(poseT / LashWhipTime), 3f) : 1f - Mathf.SmoothStep(0f, 1f, p);
                    Extension = ext;
                    float straight = Vertical ? -90f : 0f;
                    for (int i = 0; i < N; i++)
                    {
                        float u = i / (N - 1f), w = Mathf.Clamp01(ext * 1.6f - u * 0.6f);   // the uncurl runs root to tip
                        float whip = outward ? 30f * Mathf.Sin(u * 6f - poseT * 42f) * (1f - w) + 5f * Mathf.Sin(u * 8f - poseT * 30f) * Mathf.Exp(-poseT * 7f) * w : 0f;
                        float sCurve = outward ? w * 7f * Mathf.Sin(u * 5.5f - poseT * 14f) : 0f, tipCurl = 28f * Mathf.SmoothStep(0.7f, 1f, u) * w * ext;  // a slight S travelling root to tip, a small curl at the tip
                        phi[i] = Mathf.Lerp(bindPhi[i] * (1f + 0.12f * (1f - w)), straight, w) + whip + sCurve + tipCurl;
                    }
                    return Mathf.Lerp(WindStretch, LashStretch * (outward ? 1f + 0.04f * Mathf.Sin(poseT * 40f) * Mathf.Exp(-poseT * 8f) : 1f), ext);
                }
                case Pose.Stunned:  // hanging limp in a loose curl, a slow pendulum
                    for (int i = 0; i < N; i++)
                    {
                        float u = i / (N - 1f);   // the artist's rest curl, drooping a little, swinging slowly
                        phi[i] = bindPhi[i] - 14f * u + 16f * u * Mathf.Sin(t * 1.6f - u * 1.5f);
                    }
                    return 1f;
                case Pose.Spit:     // flick: uncurl part-way along the spit direction and back
                {
                    // Sin(pi) can round below zero; clamp before the fractional power.
                    float ext = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(p * Mathf.PI)), 0.7f);
                    for (int i = 0; i < N; i++)
                    {
                        float u = i / (N - 1f), w = Mathf.Clamp01(ext * 1.8f - u * 0.5f);
                        phi[i] = Mathf.Lerp(bindPhi[i] + Wave(i, t, 6f), 0f, w) + 12f * Mathf.Sin(u * 8f - poseT * 35f) * (1f - ext * 0.6f);
                    }
                    return Mathf.Lerp(1f, SpitStretch, ext);
                }
            }
            return 1f;
        }

        float Wave(int i, float t, float amp) { float u = i / (N - 1f); return amp * Mathf.Pow(u, 1.2f) * Mathf.Sin(t * 2.1f + phase - u * 5f); }

        public void Update(float dt, float clock)
        {
            charge = pose == Pose.WindUp ? Mathf.Clamp01(P * 1.3f) : pose == Pose.Lash ? 1f : Mathf.MoveTowards(charge, 0f, dt * 6f);
            poseT += dt; hurtKick = Mathf.Max(0f, hurtKick - dt);
            Glow = Mathf.MoveTowards(Glow, glowTarget, dt * 6f);
            float targetStretch = Shape(clock);
            if (blend < 1f)
            {
                blend = Mathf.Min(1f, blend + dt / blendDur);
                float k = Mathf.SmoothStep(0f, 1f, blend);
                for (int i = 0; i < N; i++) phi[i] = Mathf.Lerp(lastPhi[i], phi[i], k);
                targetStretch = Mathf.Lerp(lastStretch, targetStretch, k);
            }
            stretch = targetStretch;
            Body_(dt, clock);
            Tongue();
            Eye(dt, clock);
            Paint(clock);
        }

        // Body: facing (snaps like a puppet), pendulum swing on the cord, tilt, squash and stretch.
        void Body_(float dt, float clock)
        {
            float p = P, t = poseT, sx = 1f, sy = 1f, bob = Mathf.Sin(clock * 2.3f + phase) * 0.04f;
            float towardCentre = white ? 1f : -1f, side = Side;
            float sway = pose == Pose.Unlit || pose == Pose.Calm ? 2f * Mathf.Sin(clock * 0.9f + phase) : 3.5f * Mathf.Sin(clock * 1.5f + phase);
            float yawT = 180f - towardCentre * IdleYaw, tiltT = 0f, swingT = 0f;
            switch (pose)
            {
                case Pose.WindUp:
                {
                    float e = Mathf.SmoothStep(0f, 1f, p);
                    yawT = 180f - side * ProfileYaw; swingT = -side * SwingWind * e;
                    sy = 1f + 0.09f * e; sx = 1f - 0.05f * e; bob += 0.1f * e; sway = 0f;
                    break;
                }
                case Pose.Lash:
                case Pose.Retract:
                {
                    yawT = 180f - side * ProfileYaw; swingT = side * SwingLash * Extension;   // the lantern lunges on its cord
                    if (pose == Pose.Lash) { float e = Mathf.Clamp01(1f - t / 0.3f); sy = 1f - 0.07f * e; sx = 1f + 0.06f * e; }
                    sway = 0f;
                    break;
                }
                case Pose.Stunned:
                    yawT = 180f - towardCentre * IdleYaw; swingT = coilDir * 12f + 9f * Mathf.Sin(t * 5.5f) * Mathf.Exp(-t * 0.5f);
                    sy = 0.91f; sx = 1.06f; bob = -0.18f + Mathf.Sin(t * 9f) * 0.02f; sway = 0f;
                    break;
                case Pose.Spit:
                {
                    float s = white ? 1f : -1f, e = Mathf.Sin(p * Mathf.PI);
                    yawT = 180f - s * ProfileYaw; sy = 1f + 0.07f * e; sx = 1f - 0.04f * e; sway = 0f;
                    break;
                }
            }
            if (pose != Pose.Stunned && pose != Pose.WindUp && pose != Pose.Lash) sy += 0.015f * Mathf.Sin(clock * 3f + phase); // lazy breathing
            if (hurtKick > 0f)
            {
                float k = Mathf.Sin(hurtKick * 38f) * 0.24f * (hurtKick / 0.4f);
                sx *= 1f + k; sy *= 1f - k;
            }
            // Keep attack reach exact; let passive poses settle with weight instead of snapping.
            bool striking = pose == Pose.WindUp || pose == Pose.Lash || pose == Pose.Retract || pose == Pose.Spit;
            float settle = 1f - Mathf.Exp(-dt * 9f);
            swing = striking ? swingT + sway : Mathf.Lerp(swing, swingT + sway, settle);
            // Tilt makes the tongue's straight direction match the screen path (diagonals); the swing is compensated so only the lash direction matters.
            if (pose == Pose.WindUp || pose == Pose.Lash || pose == Pose.Retract)
                tiltT = Vertical ? -swing : (side > 0f ? pathAngle : pathAngle - 180f) - swing;
            else if (pose == Pose.Spit)
                tiltT = ((white ? 1f : -1f) > 0f ? spitAngle : spitAngle - 180f) - swing;
            float snap = (striking ? SnapSpeed : 240f) * dt;
            yaw = Mathf.MoveTowardsAngle(yaw, yawT, snap);
            tilt = Mathf.MoveTowardsAngle(tilt, tiltT, snap);
            sy1 = sy;
            holder.rotation = Quaternion.Euler(0, 0, swing);
            holder.position = new Vector3(holder.position.x, hookY + bob, holder.position.z);
            model.localRotation = Quaternion.Euler(0, 0, tilt) * Quaternion.Euler(0, yaw, 0);
            model.localScale = new Vector3(sx, sy, 1f) * BodyScale;
            model.localPosition = new Vector3(0, -HangDrop, 0);
            hangString.SetPosition(1, new Vector3(0, -(HangDrop - 1.2f * sy), 0));
        }

        // Every bone turns only about the model's X axis, by the difference between its rest direction and the wanted one.
        void Tongue()
        {
            Quaternion root = model.rotation;
            for (int i = 0; i < N; i++)
            {
                bone[i].rotation = root * Quaternion.AngleAxis(bindPhi[i] - phi[i], Vector3.right) * bindRot[i];
                if (i > 0) bone[i].localPosition = bindLocal[i] * stretch;   // even lengthwise stretch
            }
            endBone.localPosition = endBone.localPosition.normalized * (bindLocal[N - 1].magnitude * stretch);
            tipBone.position = endBone.position;
            tipBone.rotation = bone[N - 1].rotation * tipOffset;
            for (int i = 0; i < N; i++) Points[i] = bone[i].position;
            Points[N] = endBone.position;
            rim.enabled = charge > 0.05f;
            if (rim.enabled)
            {
                for (int i = 0; i <= N; i++) rim.SetPosition(i, Points[i] + new Vector3(0, 0, 0.12f));
                rim.widthMultiplier = 0.42f * charge * (0.85f + 0.15f * Mathf.Sin(Time.time * 30f));
            }
        }

        // The eye is always drawn on the part of the eyeball that faces the camera: the gaze is biased toward the camera when the body is
        // in profile or three-quarter view, and tracking only moves it a little around that. A closed eye is a thick ink lid line.
        void Eye(float dt, float clock)
        {
            blinkClock += dt;
            bool blink = false;
            if (pose == Pose.Idle || pose == Pose.WindUp)
            {
                if (blinkClock > nextBlink) { blinkClock = 0f; nextBlink = Random.Range(2.5f, 5f); blink = true; blinkHold = 0.12f; }
            }
            blinkHold -= dt;
            blink |= blinkHold > 0f;
            bool open = eyeTarget > 0.5f && !blink;
            bool dizzy = open && pose == Pose.Stunned;
            Vector3 centre = eye.position, fwd = model.rotation * Vector3.forward;
            float off = Vector3.Angle(fwd, Vector3.back);
            Vector3 baseDir = off > ViewBias ? Vector3.Slerp(fwd, Vector3.back, (off - ViewBias) / off).normalized : fwd;
            float size = (pose == Pose.WindUp ? 1f + 0.3f * Mathf.SmoothStep(0, 1, P) : pose == Pose.Lash ? 1.25f : 1f) * wide;
            float radius = 0.325f * BodyScale * size;
            eyeRenderer.enabled = open; socket.gameObject.SetActive(open);
            iris.gameObject.SetActive(open && !dizzy); pupil.gameObject.SetActive(open && !dizzy);
            lid.enabled = !open; spiral.enabled = dizzy;
            if (!open)
            {
                bool happy = pose == Pose.Calm;
                var arc = new Vector3[9];
                Vector3 mid = centre + baseDir * (radius + 0.05f);
                for (int i = 0; i < arc.Length; i++) { float x = i / 8f * 2f - 1f; arc[i] = mid + Vector3.right * (x * 0.36f) + Vector3.up * ((1f - x * x) * (happy ? 0.11f : -0.09f)); }
                lid.SetPositions(arc);
                return;
            }
            Vector3 want = (lookAt - centre).normalized;
            if (Vector3.Angle(baseDir, want) > TrackLimit) want = Vector3.Slerp(baseDir, want, TrackLimit / Vector3.Angle(baseDir, want));
            if (dizzy)
            {
                var pts = new Vector3[40];
                Vector3 mid = centre + baseDir * (radius + 0.06f);
                for (int i = 0; i < pts.Length; i++)
                {
                    float f = i / 39f, a = f * Mathf.PI * 5f + clock * 12f;
                    pts[i] = mid + Vector3.right * (Mathf.Cos(a) * f * 0.34f) + Vector3.up * (Mathf.Sin(a) * f * 0.34f);
                }
                spiral.SetPositions(pts);
                want = baseDir;
            }
            Quaternion root = model.rotation;
            eye.rotation = root * Quaternion.FromToRotation(Vector3.forward, Quaternion.Inverse(root) * want) * eyeBindRot;
            eye.localScale = eyeBindScale * size;
            iris.SetPositionAndRotation(centre + want * (radius + 0.015f), Quaternion.LookRotation(want));
            pupil.SetPositionAndRotation(centre + want * (radius + 0.03f), Quaternion.LookRotation(want));
            iris.localScale = new Vector3(0.4f, 0.4f, 0.03f) * (size / 1f);
            pupil.localScale = new Vector3(0.19f, 0.19f, 0.03f) * size;
        }

        void Paint(float clock)
        {
            float g = Glow;   // the light itself is the duel's dusk veil, not a sticker on the twin
            Color body = white ? Color.Lerp(Dull, new Color(1f, 0.95f, 0.78f), g) : Color.Lerp(new Color(0.5f, 0.42f, 0.4f), Color.white, g);
            Color tongue = white ? Color.Lerp(new Color(0.62f, 0.5f, 0.45f), new Color(0.85f, 0.52f, 0.45f), g) : Color.Lerp(new Color(0.4f, 0.2f, 0.17f), PrototypeArt.Red, g);
            tongue = Color.Lerp(tongue, new Color(1f, 0.82f, 0.4f), charge * (0.16f + 0.08f * Mathf.Sin(clock * 30f)));
            foreach (var (m, role) in tints)
            {
                switch (role)
                {
                    case 0: m.SetColor("_BaseColor", body); break;
                    case 1: m.SetColor("_BaseColor", PrototypeArt.Ink); break;
                    case 2: m.SetColor("_BaseColor", tongue); break;
                    case 3: m.SetColor("_BaseColor", Color.Lerp(Dull, new Color(1f, 0.98f, 0.92f), 0.5f + 0.5f * g)); break;
                    case 4: m.SetColor("_BaseColor", white ? PrototypeArt.Red : PrototypeArt.Gold); break;
                    default: m.SetColor("_BaseColor", PrototypeArt.Ink); break;
                }
            }
        }
    }
}
