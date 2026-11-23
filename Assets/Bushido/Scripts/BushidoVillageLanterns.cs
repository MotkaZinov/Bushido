using UnityEngine;
namespace Bushido
{
    // The village cable is a shimenawa: a twisted straw rope between two bamboo posts. It is a damped string of RopeNodes: every load
    // (a lantern, a twin, the cursed mask) sags it where it hangs, and kicks (a tug, a jolt, a cut) travel along it to the other hooks.
    public sealed partial class BushidoPrototype
    {
        // ---- Tuning: the sacred rope. Lengths in world units, u = 0..1 across the screen, speeds in world units per second ----
        const int RopeNodes = 33, ShideCount = 5, HangSlots = 6;      // hang slots 0-3 village lanterns, 4-5 the twins' disguise (duel only)
        const float RopeU0 = 0.012f, RopeU1 = 0.988f;                 // the knots sit on the posts, inside the HUD's margins
        const float RopeWidth = .18f; // 3x the old cable; edge = ink outline factor; twist = texture tiles per unit
        const float RopeStiff = 40f, RopeCouple = 650f, RopeDamp = 2.6f, RopeSubstep = 1f / 120f; // return to its sag; wave coupling (~25 nodes/s); damping
        const float RopeSagWidth = 0.15f, RopeLoadLantern = 0.1f, RopeLoadTwin = 0.5f, RopeLoadMask = 0.2f; // bell half-width in u; sag under each load
        const float RopeKickWidth = 0.06f, RopeLean = 30f, RopeBreeze = 0.015f;
        const float RopeGlowFade = 1.2f, RopeGlowWidth = 2.6f;        // the gold wash the relay flame leaves on the rope
        const float RopePostX = 0.0125f, RopePostTop = 0.885f, RopePostBottom = 0.3f, RopePostWidth = 0.3f; // viewport; clear of the gourds, the pause button and the tag
        const float ShideLength = 0.8f, ShideWidth = 0.6f, ShideTornLength = 0.5f, ShideTornSlant = 12f, ShideRepair = 0.8f; // torn ones are shorter and hang crooked
        const float LanternLitSpeed = 1.5f;
        static readonly float[] LanternPositions = { .07f, .25f, .73f, .79f };
        static readonly float[] LanternSize = { 1f, .82f, 1.12f, .92f, 1f, 1f };       // the twins' slots are an ordinary size so the disguise holds
        static readonly float[] LanternCord = { .7f, 1f, .6f, .85f, .9f, .9f };
        static readonly float[] ShideAt = { .13f, .4f, .46f, .55f, .69f };
        static readonly Color ShideGrey = Color.Lerp(PrototypeArt.Ink, PrototypeArt.Paper, .55f);
        static readonly Color LanternUnlitTint = new Color(.5f, .47f, .5f), LanternLitTint = new Color(1f, .93f, .7f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly HangingLanternModel[] hangingModels = new HangingLanternModel[HangSlots];
        readonly float[] lanternSwing = new float[HangSlots], lanternSwingSpeed = new float[HangSlots];
        float lanternMotionClock;
        Transform lanternRig;
        LineRenderer maskSuspension, ropeGlow;
        Mesh strawBraid;
        const int BraidSides = 15, BraidRings = (RopeNodes - 1) * 10 + 1;
        readonly Vector3[] braidVertices = new Vector3[BraidSides * BraidRings];
        readonly Vector2[] braidProfile = new Vector2[BraidSides * BraidRings];
        readonly Transform[] villageLanterns = new Transform[HangSlots], ropePosts = new Transform[2], ropeKnots = new Transform[2], shide = new Transform[ShideCount];
        readonly LineRenderer[] lanternStrings = new LineRenderer[HangSlots];
        readonly Material[] lanternMat = new Material[HangSlots], shideMat = new Material[ShideCount];
        readonly float[] ropeY = new float[RopeNodes], ropeV = new float[RopeNodes], ropeTarget = new float[RopeNodes];
        readonly float[] hangU = new float[HangSlots], hangLoad = new float[HangSlots], hangLen = new float[HangSlots], hangScale = new float[HangSlots], hangTwitch = new float[HangSlots];
        readonly float[] lanternLit = new float[HangSlots], lanternLitTo = new float[HangSlots], shideClean = new float[ShideCount];
        readonly Vector3[] ropePts = new Vector3[RopeNodes], glowPts = new Vector3[RopeNodes];
        float glowFrom, glowTo, glowAmt;

        Vector3 CablePoint(float u) => view.ViewportToWorldPoint(new Vector3(u, .815f + .045f * (2 * u - 1) * (2 * u - 1), 13));
        float RopeNodeU(int i) => Mathf.Lerp(RopeU0, RopeU1, i / (RopeNodes - 1f));
        Vector3 RopeNode(int i) => CablePoint(RopeNodeU(i)) + Vector3.up * ropeY[i];
        static float Along(float[] a, float u) { float f = Mathf.Clamp01((u - RopeU0) / (RopeU1 - RopeU0)) * (RopeNodes - 1), t = f - Mathf.Floor(f); int i = Mathf.Min((int)f, RopeNodes - 2); return Mathf.Lerp(a[i], a[i + 1], t); }
        // The rope's real position at u: every cord, the relay flame and the twins' hooks attach here.
        Vector3 RopePoint(float u) => CablePoint(u) + Vector3.up * Along(ropeY, u);

        // A pull on the rope at u (negative = down). It spreads to the neighbours through the string's coupling.
        void RopeKick(float u, float speed)
        {
            for (int i = 0; i < RopeNodes; i++)
            {
                float d = u - RopeNodeU(i);
                if (Mathf.Abs(d) < RopeKickWidth) ropeV[i] += speed * .5f * (1f + Mathf.Cos(Mathf.PI * d / RopeKickWidth));
            }
        }

        void RopeGlow(float from, float to) { glowFrom = from; glowTo = to; glowAmt = 1f; }

        void BeginVillageLanterns()
        {
            lanternRig = new GameObject("Guirnalda / capas de faroles de paja y papel").transform;
            lanternRig.SetParent(world, false);
            System.Array.Clear(ropeY, 0, RopeNodes); System.Array.Clear(ropeV, 0, RopeNodes); glowAmt = 0f;
            BuildStrawBraid();
            ropeGlow = art.Line(lanternRig, glowPts, PrototypeArt.Gold, 0f); ropeGlow.enabled = false;
            ropeGlow.widthCurve = new AnimationCurve(new Keyframe(0, .15f), new Keyframe(1, 1f));                                      // a comet: thin tail, bright head
            maskSuspension = art.Line(lanternRig, new Vector3[2], new Color(.22f, .16f, .09f), .03f);
            lanternMotionClock = 0f;
            int hang = LanternDuel ? HangSlots : 4;
            for (int i = 0; i < HangSlots; i++)
            {
                villageLanterns[i] = null; lanternMat[i] = null; lanternStrings[i] = null;
                hangU[i] = i < 4 ? LanternPositions[i] : i == 4 ? LanternHookWhite : LanternHookRed;
                hangLoad[i] = i < hang ? RopeLoadLantern : 0f; hangLen[i] = LanternCord[i]; hangScale[i] = LanternSize[i]; hangTwitch[i] = 0f;
                lanternLit[i] = lanternLitTo[i] = LanternDuel ? 0f : 1f;
                if (i >= hang) continue;
                hangingModels[i] = HangingLanternModel.Create(art, lanternRig, i);
                villageLanterns[i] = hangingModels[i].transform;
                lanternMat[i] = hangingModels[i].Paper;
                lanternSwing[i] = lanternSwingSpeed[i] = 0f;
                lanternStrings[i] = art.Line(lanternRig, new Vector3[2], new Color(.22f, .16f, .09f), .018f);
            }
            for (int i = 0; i < ShideCount; i++)
            {
                shide[i] = new GameObject("Shide").transform; shide[i].SetParent(lanternRig, false);
                var quad = art.Shape(shide[i], "Tira de papel en zigzag", PrimitiveType.Quad, new Vector3(0, -ShideLength / 2f, 0), new Vector3(ShideWidth, ShideLength, 1), Color.white);
                shideMat[i] = art.PaperWeaponMaterial(PrototypeArt.Shide); shideMat[i].SetFloat("_PaperAlphaCutoff", .5f);
                quad.GetComponent<Renderer>().sharedMaterial = shideMat[i];
                shideClean[i] = 0f;
            }
            for (int side = 0; side < 2; side++)
            {
                var post = new GameObject("Poste de bambú").transform;
                post.SetParent(lanternRig, false);
                ropePosts[side] = post;
                Color stalk = new Color(.40f, .48f, .23f);
                art.Shape(post, "Caña", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1f, .5f, 1f), stalk);
                for (int joint = 0; joint < 6; joint++)
                {
                    float y = -.46f + joint * .181f;
                    art.Shape(post, "Nudo del bambú", PrimitiveType.Cylinder, Vector3.up * y,
                        new Vector3(1.13f, .009f, 1.13f), Color.Lerp(stalk, PrototypeArt.InkSepia, .38f));
                }
                art.Shape(post, "Hueco del tallo", PrimitiveType.Cylinder, Vector3.up * .501f,
                    new Vector3(.73f, .002f, .73f), PrototypeArt.InkSepia);
                var knot = new GameObject("Amarre y borla").transform;
                knot.SetParent(lanternRig, false);
                ropeKnots[side] = knot;
                for (int wrap = -1; wrap <= 1; wrap++)
                {
                    var band = art.Shape(knot, "Vuelta de cáñamo", PrimitiveType.Cylinder,
                        Vector3.up * (wrap * .073f), new Vector3(RopePostWidth * 1.38f, .045f, RopePostWidth * 1.38f),
                        Color.Lerp(PrototypeArt.Gold, PrototypeArt.InkSepia, .25f));
                    band.transform.localRotation = Quaternion.Euler(4f, 0f, side == 0 ? 9f : -9f);
                }
                for (int s = -1; s <= 1; s++) art.Line(knot, new[] { new Vector3(0, -.12f, -.05f), new Vector3(s * .09f, -.62f + Mathf.Abs(s) * .08f, -.05f) }, PrototypeArt.Red, .06f);
                art.Line(knot, new[] { new Vector3(-.07f, -.26f, -.07f), new Vector3(.07f, -.26f, -.07f) }, PrototypeArt.Gold, .05f);
            }
            lanternRig.gameObject.SetActive(false);
        }

        // Three lobes twist around the axis. Bake their cross-sections once and bend
        // the mesh through the same moving nodes that carry the hooks.
        void BuildStrawBraid()
        {
            var rope = new GameObject("Cuerda ceremonial de tres cabos");
            rope.transform.SetParent(lanternRig, false);
            strawBraid = new Mesh { name = rope.name };
            strawBraid.MarkDynamic();
            var indices = new int[(BraidRings - 1) * BraidSides * 6];
            int index = 0;
            for (int ring = 0; ring < BraidRings; ring++)
            {
                float twist = ring * (2f * Mathf.PI / 18f);
                for (int side = 0; side < BraidSides; side++)
                {
                    float angle = side * (2f * Mathf.PI / BraidSides);
                    float radius = RopeWidth * (.51f + .12f * Mathf.Cos(3f * angle));
                    int a = ring * BraidSides + side;
                    braidProfile[a] = new Vector2(Mathf.Cos(angle + twist), Mathf.Sin(angle + twist)) * radius;
                    if (ring == BraidRings - 1) continue;
                    int b = ring * BraidSides + (side + 1) % BraidSides;
                    indices[index++] = a; indices[index++] = b; indices[index++] = a + BraidSides;
                    indices[index++] = b; indices[index++] = b + BraidSides; indices[index++] = a + BraidSides;
                }
            }
            strawBraid.vertices = braidVertices;
            strawBraid.triangles = indices;
            rope.AddComponent<MeshFilter>().sharedMesh = strawBraid;
            rope.AddComponent<SliceMeshOwner>();
            rope.AddComponent<MeshRenderer>().sharedMaterial = art.Material(new Color(.74f, .53f, .28f));
        }

        void UpdateStrawBraid()
        {
            for (int ring = 0; ring < BraidRings; ring++)
            {
                float position = ring * (RopeNodes - 1f) / (BraidRings - 1);
                int i = Mathf.Min((int)position, RopeNodes - 2);
                Vector3 tangent = (ropePts[i + 1] - ropePts[i]).normalized;
                Vector3 up = Vector3.Cross(Vector3.forward, tangent).normalized;
                Vector3 centre = Vector3.Lerp(ropePts[i], ropePts[i + 1], position - i);
                for (int side = 0; side < BraidSides; side++)
                {
                    int vertex = ring * BraidSides + side;
                    Vector2 offset = braidProfile[vertex];
                    braidVertices[vertex] = centre + up * offset.x + Vector3.forward * offset.y;
                }
            }
            strawBraid.vertices = braidVertices;
            strawBraid.RecalculateNormals();
            strawBraid.RecalculateBounds();
        }

        // A string of nodes pulled to the sag its loads cause; kicks ring along it and fade.
        void StepRope(float dt)
        {
            bool maskOn = cursedMask != null && cursedMask.gameObject.activeSelf;
            for (int i = 0; i < RopeNodes; i++)
            {
                float u = RopeNodeU(i), sag = 0f;
                for (int k = 0; k < HangSlots; k++) sag += hangLoad[k] * Bell(u - hangU[k]);
                if (maskOn) sag += RopeLoadMask * Bell(u - .5f);
                ropeTarget[i] = -sag + (i > 0 && i < RopeNodes - 1 ? RopeBreeze * Mathf.Sin(maskAge * 1.3f + u * 7f) : 0f);
            }
            if (dt <= 0f) return;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / RopeSubstep), 1, 6); float h = Mathf.Min(dt, .05f) / steps;
            for (int s = 0; s < steps; s++)
            {
                for (int i = 1; i < RopeNodes - 1; i++)
                    ropeV[i] += (RopeStiff * (ropeTarget[i] - ropeY[i]) + RopeCouple * (ropeY[i - 1] + ropeY[i + 1] - 2f * ropeY[i]) - RopeDamp * ropeV[i]) * h;
                for (int i = 1; i < RopeNodes - 1; i++) ropeY[i] += ropeV[i] * h;   // the knots on the posts never move
            }
        }
        static float Bell(float d) => Mathf.Abs(d) < RopeSagWidth ? .5f * (1f + Mathf.Cos(Mathf.PI * d / RopeSagWidth)) : 0f;

        void UpdateVillageLanterns(bool visible, float delta = -1f)
        {
            if (lanternRig == null) return;
            lanternRig.gameObject.SetActive(visible);
            if (!visible) return;
            float dt = delta < 0f ? Time.deltaTime : delta;
            dt = Mathf.Clamp(dt, 0f, .05f);
            lanternMotionClock += dt;
            StepRope(dt);
            Vector3 fwd = view.transform.forward;
            for (int i = 0; i < RopeNodes; i++)
            {
                ropePts[i] = lanternRig.InverseTransformPoint(RopeNode(i));
            }
            UpdateStrawBraid();
            glowAmt = Mathf.MoveTowards(glowAmt, 0f, dt * RopeGlowFade);
            ropeGlow.enabled = glowAmt > .01f;
            if (ropeGlow.enabled)
            {
                for (int j = 0; j < RopeNodes; j++) glowPts[j] = RopePoint(Mathf.Lerp(glowFrom, glowTo, j / (RopeNodes - 1f))) + fwd * .02f;
                ropeGlow.SetPositions(glowPts); ropeGlow.widthMultiplier = RopeWidth * RopeGlowWidth * glowAmt;
            }
            for (int side = 0; side < 2; side++)
            {
                Vector3 c = view.ViewportToWorldPoint(new Vector3(side == 0 ? RopePostX : 1f - RopePostX, (RopePostTop + RopePostBottom) / 2f, 13.2f));
                ropePosts[side].position = c; ropePosts[side].localScale = new Vector3(RopePostWidth, (RopePostTop - RopePostBottom) * 2f * view.orthographicSize, RopePostWidth);
                ropeKnots[side].position = new Vector3(c.x, RopeNode(side == 0 ? 0 : RopeNodes - 1).y, c.z);
                ropeKnots[side].rotation = Quaternion.Euler(0, 0, Mathf.Sin(maskAge * 1.7f + side * 2f) * 4f);   // the tassel sways
            }
            float cleanTarget = LanternDuel ? ((twinPurified[0] ? .5f : 0f) + (twinPurified[1] ? .5f : 0f)) : -1f;   // boss fights: neutral shide
            for (int i = 0; i < ShideCount; i++)
            {
                float u = ShideAt[i], want = cleanTarget < 0f ? .5f : Mathf.Clamp01(cleanTarget * ShideCount - i);
                shideClean[i] = Mathf.MoveTowards(shideClean[i], want, dt * ShideRepair);
                float torn = cleanTarget < 0f ? 0f : 1f - shideClean[i];
                float flutter = Mathf.Sin(maskAge * 2.4f + i * 1.9f) * 7f + Along(ropeV, u) * 4f;
                shide[i].position = RopePoint(u) - fwd * .03f;
                shide[i].rotation = Quaternion.Euler(0, 0, flutter * (1f + torn * .8f) + (i % 2 == 0 ? 1f : -1f) * torn * ShideTornSlant);
                shide[i].localScale = new Vector3(1f, Mathf.Lerp(1f, ShideTornLength / ShideLength, torn), 1f);
                shideMat[i].SetColor(BaseColorId, Color.Lerp(ShideGrey, PrototypeArt.Paper, shideClean[i]));
            }
            for (int k = 0; k < HangSlots; k++)
            {
                if (villageLanterns[k] == null) continue;
                lanternLit[k] = Mathf.MoveTowards(lanternLit[k], lanternLitTo[k], dt * LanternLitSpeed);

                float u = hangU[k];
                Vector3 hook = RopePoint(u), along = RopePoint(u + .01f) - RopePoint(u - .01f);
                // A damped pendulum follows the rope; each cord length gives a different rhythm.
                float length = hangLen[k], scale = hangScale[k];
                float slope = along.y / Mathf.Max(.001f, Mathf.Abs(along.x));
                float target = Mathf.Sin(lanternMotionClock * 1.15f + k * .8f) * 3f
                    + Mathf.Clamp(slope * RopeLean, -18f, 18f) + hangTwitch[k];
                int steps = Mathf.Max(1, Mathf.CeilToInt(dt / RopeSubstep));
                float h = dt / steps;
                for (int step = 0; step < steps; step++)
                {
                    float acceleration = (target - lanternSwing[k]) * (24f / Mathf.Max(.3f, length))
                        - lanternSwingSpeed[k] * 3.4f + Along(ropeV, u) * 12f;
                    lanternSwingSpeed[k] += acceleration * h;
                    lanternSwing[k] += lanternSwingSpeed[k] * h;
                }
                float swing = lanternSwing[k];
                float depthSway = Mathf.Sin(lanternMotionClock * .85f + k * 1.7f) * 4f;
                Quaternion rotation = Quaternion.Euler(depthSway, Mathf.Sin(lanternMotionClock * .6f + k) * 9f, swing);
                Vector3 up = rotation * Vector3.up;
                // Rotate around the hook, keeping the top cap attached to the cord.
                Vector3 center = hook - up * length;
                villageLanterns[k].SetPositionAndRotation(center, rotation);
                villageLanterns[k].localScale = Vector3.one * scale;
                hangingModels[k].Animate(lanternMotionClock, lanternLit[k], lanternSwingSpeed[k]);
                lanternStrings[k].SetPosition(0, hook);
                lanternStrings[k].SetPosition(1, center + up * HangingLanternModel.Top * scale);
            }
            if (cursedMask == null) return;
            maskSuspension.SetPosition(0, RopePoint(.5f));
            maskSuspension.SetPosition(1, cursedMask.position + cursedMask.up * .65f);
        }
    }
}
